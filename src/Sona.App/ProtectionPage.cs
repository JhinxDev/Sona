using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Sona.Core;
using Sona.App.Windows;
namespace Sona.App;

public partial class MainWindow
{
    private ProtectedCredentials? credentialVault;
    private ProtectionService? protection;
    private ProtectionHotkey? protectionHotkey;
    private Task protectionReleaseOperation = Task.CompletedTask;
    private readonly DispatcherTimer protectionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private void InitializeProtection()
    {
        ProtectionEnabled.IsChecked = preferences.ProtectionEnabled; SilenceIncoming.IsChecked = preferences.SilenceIncoming;
        WatchedApps.Text = string.Join("\n", preferences.WatchedApps); ProtectionDelay.Text = preferences.ProtectionReleaseDelayMs.ToString();
        ProtectionShortcutInput.Text = preferences.ProtectionShortcut;
        credentialVault = new(Path.Combine(Path.GetDirectoryName(preferencesPath)!, "discord-credentials.dpapi"));
        protection = new(credentialVault); protection.Start();
        UpdateProtectionHotkey();
        _ = ConfigureProtectionAsync();
        protectionTimer.Tick += (_, _) => HushStatus.Text = protection?.Status ?? "Off"; protectionTimer.Start();
    }
    private void UpdateProtectionHotkey()
    {
        protectionHotkey?.Dispose(); protectionHotkey = null;
        if (!preferences.ProtectionEnabled || arguments.Contains("--test-ui")) return;
        protectionHotkey = new(new System.Windows.Interop.WindowInteropHelper(this).Handle, preferences.ProtectionShortcut, () => _ = ToggleProtectionPauseAsync());
        if (!protectionHotkey.Registered) ProtectionResult.Text = "The pause shortcut is in use. Pause remains available here; choose a different shortcut or restart after retiring Hush.";
    }
    private async Task ConfigureProtectionAsync()
    {
        if (protection == null) return;
        try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7)); await protection.ConfigureAsync(preferences, deadline.Token); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { ProtectionResult.Text = "Protection settings could not be applied. Reconnect Discord and retry."; }
    }
    private async Task ReleaseOwnProtectionAsync()
    {
        if (protection == null) return;
        try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7)); await protection.EndOwnRecordingAsync(deadline.Token); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or System.Security.SecurityException) { ProtectionResult.Text = "Discord restoration needs retry. Pause protection or reconnect."; }
    }
    private async void SaveProtection_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || protection == null) return;
        if (!int.TryParse(ProtectionDelay.Text, out var delay) || delay is < 0 or > 5000) { ProtectionResult.Text = "Release delay must be between 0 and 5000 milliseconds."; return; }
        var apps = WatchedApps.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (apps.Length > 50 || apps.Any(a => a.Length > 160 || a.Any(char.IsControl) || a.Contains('\\') || a.Contains('/'))) { ProtectionResult.Text = "Use at most 50 executable names or package IDs, without paths."; return; }
        if (!ProtectionShortcut.TryParse(ProtectionShortcutInput.Text, out _, out _)) { ProtectionResult.Text = "Use Ctrl, Alt or Win with a letter, digit, Space or F1-F24, optionally Shift."; return; }
        var candidate = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        candidate.ProtectionEnabled = ProtectionEnabled.IsChecked == true; candidate.SilenceIncoming = SilenceIncoming.IsChecked == true; candidate.ProtectionReleaseDelayMs = delay; candidate.WatchedApps = apps.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        candidate.ProtectionShortcut = ProtectionShortcutInput.Text.Trim(); preferencesApplying = true;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7)); await protection.ConfigureAsync(candidate, deadline.Token); candidate.Save(preferencesPath);
            preferences.ProtectionEnabled = candidate.ProtectionEnabled; preferences.SilenceIncoming = candidate.SilenceIncoming; preferences.ProtectionReleaseDelayMs = candidate.ProtectionReleaseDelayMs; preferences.WatchedApps = candidate.WatchedApps;
            preferences.ProtectionShortcut = candidate.ProtectionShortcut; UpdateProtectionHotkey();
            ProtectionResult.Text = "Protection settings saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException) { await ConfigureProtectionAsync(); ProtectionResult.Text = "Protection settings could not be saved. Previous preferences were retained."; }
        finally { preferencesApplying = false; }
    }
    private void ImportHush_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || credentialVault == null) return;
        try
        {
            credentialVault.ImportHush();
            var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hush", "settings.json");
            if (File.Exists(legacy) && new FileInfo(legacy).Length <= 65536)
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(legacy));
                if (doc.RootElement.TryGetProperty("watched_apps", out var apps) && apps.ValueKind == JsonValueKind.Array) WatchedApps.Text = string.Join("\n", apps.EnumerateArray().Take(50).Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()));
                if (doc.RootElement.TryGetProperty("focus_deafen", out var deafen) && deafen.ValueKind is JsonValueKind.True or JsonValueKind.False) SilenceIncoming.IsChecked = deafen.GetBoolean();
                if (doc.RootElement.TryGetProperty("mute_release_delay_ms", out var delay) && delay.TryGetInt32(out var ms)) ProtectionDelay.Text = Math.Clamp(ms, 0, 5000).ToString();
                if (doc.RootElement.TryGetProperty("hotkey_pause", out var shortcut) && shortcut.ValueKind == JsonValueKind.String) ProtectionShortcutInput.Text = shortcut.GetString();
                if (doc.RootElement.TryGetProperty("autostart_enabled", out var startup) && startup.ValueKind is JsonValueKind.True or JsonValueKind.False) StartWithWindows.IsChecked = startup.GetBoolean();
                if (doc.RootElement.TryGetProperty("close_to_tray", out var trayPreference) && trayPreference.ValueKind is JsonValueKind.True or JsonValueKind.False) CloseToTray.IsChecked = trayPreference.GetBoolean();
            }
            ProtectionResult.Text = "Hush credentials protected with Windows encryption. Review settings, then save. Pause and quit Hush before connecting Discord.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { ProtectionResult.Text = "Import could not complete. Existing Sona and Hush setup was preserved."; }
    }
    private async void ConnectDiscord_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || protection == null) return;
        try { ProtectionResult.Text = "Connecting Discord; approve its prompt if shown."; using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75)); await protection.ConnectAsync(true, deadline.Token); ProtectionResult.Text = protection.Status; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException or JsonException) { ProtectionResult.Text = "Discord connection failed. Check setup and pause/quit standalone Hush first."; }
    }
    private async void PauseProtection_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || protection == null || !(preferences.ProtectionEnabled || protection.Paused || protection.OwnsChanges)) return;
        await ToggleProtectionPauseAsync();
        RefreshSettingsActions();
    }
    private async Task ToggleProtectionPauseAsync()
    {
        if (protection == null) return;
        try { using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7)); await protection.PauseAsync(!protection.Paused, deadline.Token); ProtectionResult.Text = protection.Status; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { ProtectionResult.Text = "Pause or restoration needs retry. Reconnect Discord."; }
    }
    private void SaveDiscordSetup_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || credentialVault == null || protection?.Connected == true) { ProtectionResult.Text = "Disconnect by quitting Sona before replacing existing Discord setup."; return; }
        var id = DiscordClientId.Text.Trim(); var secret = DiscordClientSecret.Password;
        if (id.Length is < 5 or > 50 || !id.All(char.IsAsciiDigit) || secret.Length is < 8 or > 256) { ProtectionResult.Text = "Enter a Discord application ID and secret from your own developer application."; return; }
        try { credentialVault.Save(new DiscordCredentials { ClientId = id, ClientSecret = secret }); DiscordClientId.Clear(); DiscordClientSecret.Clear(); ProtectionResult.Text = "Discord setup saved with Windows encryption. Connect Discord when ready."; }
        catch (IOException) { ProtectionResult.Text = "Windows could not save the protected setup."; }
    }
}
