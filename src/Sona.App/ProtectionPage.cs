using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Sona.Core;
using Sona.App.Windows;
namespace Sona.App;

public partial class MainWindow
{
    private ProtectedCredentials? credentialVault;
    private bool discordConnectionBusy;
    private bool refreshingProtectionToggles;
    private ProtectionService? protection;
    private ProtectionHotkey? protectionHotkey;
    private Task protectionReleaseOperation = Task.CompletedTask;
    private readonly DispatcherTimer protectionTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private void InitializeProtection()
    {
        RefreshProtectionFields();
        credentialVault = new(Path.Combine(Path.GetDirectoryName(preferencesPath)!, "discord-credentials.dpapi"));
        protection = new(credentialVault); protection.Start();
        UpdateProtectionHotkey();
        _ = ConfigureProtectionAsync();
        protectionTimer.Tick += (_, _) => RefreshDiscordConnection(); protectionTimer.Start();
        RefreshDiscordConnection();
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
        candidate.ProtectionShortcut = ProtectionShortcutInput.Text.Trim();
        await SaveProtectionPreferencesAsync(candidate);
    }
    private async void ProtectionToggleChanged(object sender, RoutedEventArgs e)
    {
        if (refreshingProtectionToggles || protection == null || preferencesApplying || discordConnectionBusy) return;
        var master = ReferenceEquals(sender, ProtectionEnabled);
        var requested = master ? ProtectionEnabled.IsChecked == true : SilenceIncoming.IsChecked == true;
        if (requested == (master ? preferences.ProtectionEnabled : preferences.SilenceIncoming)) return;
        if (SettingsBusy)
        {
            RefreshProtectionToggles(); ProtectionToggleResult.Text = "Finish the current operation before changing protection."; return;
        }
        // Switches commit only their own choice, leaving advanced/general edits pending.
        var candidate = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        if (master) candidate.ProtectionEnabled = requested; else candidate.SilenceIncoming = requested;
        await SaveProtectionPreferencesAsync(candidate);
    }
    private async Task SaveProtectionPreferencesAsync(Preferences candidate)
    {
        if (protection == null) return;
        var wasPaused = protection.Paused;
        preferencesApplying = true; ProtectionToggleResult.Text = "Applying and saving"; RefreshSettingsActions();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            await protection.ConfigureAsync(candidate, deadline.Token); candidate.Save(preferencesPath);
            preferences.ProtectionEnabled = candidate.ProtectionEnabled; preferences.SilenceIncoming = candidate.SilenceIncoming;
            preferences.ProtectionReleaseDelayMs = candidate.ProtectionReleaseDelayMs; preferences.WatchedApps = candidate.WatchedApps;
            preferences.ProtectionShortcut = candidate.ProtectionShortcut; UpdateProtectionHotkey();
            ProtectionToggleResult.Text = ProtectionPolicySummary;
            ProtectionResult.Text = "Protection settings saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            var restored = true;
            try
            {
                using var rollback = new CancellationTokenSource(TimeSpan.FromSeconds(7));
                await protection.RestoreConfigurationAsync(preferences, wasPaused, rollback.Token);
            }
            catch (Exception rollbackError) when (rollbackError is IOException or UnauthorizedAccessException or OperationCanceledException or JsonException or InvalidOperationException) { restored = false; }
            RefreshProtectionToggles();
            ProtectionToggleResult.Text = restored ? "Change not saved. Previous choice restored; try again." : "Change not saved. Discord restoration needs retry.";
            ProtectionResult.Text = ProtectionToggleResult.Text;
        }
        finally { preferencesApplying = false; RefreshSettingsActions(); RefreshDiscordConnection(); }
    }
    private string ProtectionPolicySummary => !preferences.ProtectionEnabled ? "Saved. Discord protection is off."
        : preferences.SilenceIncoming ? "Saved. Microphone and incoming audio are silenced while dictating."
        : "Saved. Microphone only; incoming audio stays on.";
    private void RefreshProtectionToggles()
    {
        refreshingProtectionToggles = true;
        try { ProtectionEnabled.IsChecked = preferences.ProtectionEnabled; SilenceIncoming.IsChecked = preferences.SilenceIncoming; }
        finally { refreshingProtectionToggles = false; }
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
                ApplyHushSettings(doc.RootElement);
            }
            ProtectionResult.Text = "Hush credentials protected with Windows encryption. Review settings, then save. Pause and quit Hush before connecting Discord.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { ProtectionResult.Text = "Import could not complete. Existing Sona and Hush setup was preserved."; }
    }
    private void ApplyHushSettings(JsonElement settings)
    {
        var previousGuard = refreshingProtectionToggles; refreshingProtectionToggles = true;
        try
        {
            if (settings.TryGetProperty("watched_apps", out var apps) && apps.ValueKind == JsonValueKind.Array) WatchedApps.Text = string.Join("\n", apps.EnumerateArray().Take(50).Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()));
            if (settings.TryGetProperty("focus_deafen", out var deafen) && deafen.ValueKind is JsonValueKind.True or JsonValueKind.False) SilenceIncoming.IsChecked = deafen.GetBoolean();
            if (settings.TryGetProperty("mute_release_delay_ms", out var delay) && delay.TryGetInt32(out var ms)) ProtectionDelay.Text = Math.Clamp(ms, 0, 5000).ToString();
            if (settings.TryGetProperty("hotkey_pause", out var shortcut) && shortcut.ValueKind == JsonValueKind.String) ProtectionShortcutInput.Text = shortcut.GetString();
            if (settings.TryGetProperty("autostart_enabled", out var startup) && startup.ValueKind is JsonValueKind.True or JsonValueKind.False) StartWithWindows.IsChecked = startup.GetBoolean();
            if (settings.TryGetProperty("close_to_tray", out var trayPreference) && trayPreference.ValueKind is JsonValueKind.True or JsonValueKind.False) CloseToTray.IsChecked = trayPreference.GetBoolean();
        }
        finally { refreshingProtectionToggles = previousGuard; }
        ProtectionToggleResult.Text = "Imported choices are not saved. Review them, then select Save protection.";
        RefreshSettingsActions();
    }
    private async void ConnectDiscord_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || discordConnectionBusy || protection == null || protection.Connected) return;
        discordConnectionBusy = true; preferencesApplying = true; RefreshSettingsActions();
        try
        {
            ProtectionResult.Text = "Approve the connection in Discord if asked.";
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75));
            await protection.ConnectAsync(true, deadline.Token);
            ProtectionResult.Text = PersistDiscordConnectionChoice(false) ? "Discord connected." : "Connected for now. Could not save this choice; reconnect may be needed after restarting Sona.";
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException or JsonException)
        { ProtectionResult.Text = "Could not connect. Open Discord, check your setup, and try again."; }
        finally { discordConnectionBusy = false; preferencesApplying = false; RefreshSettingsActions(); RefreshDiscordConnection(); }
    }
    private async void DisconnectDiscord_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || discordConnectionBusy || protection == null || !protection.Connected && !protection.OwnsChanges && !protection.ConnectionSuppressed) return;
        discordConnectionBusy = true; preferencesApplying = true; RefreshSettingsActions();
        try
        {
            ProtectionResult.Text = "Restoring Discord before disconnecting...";
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            await protection.DisconnectAsync(deadline.Token);
            ProtectionResult.Text = PersistDiscordConnectionChoice(true) ? "Disconnected. Your saved setup is kept." : "Disconnected for now. Could not save this choice; restarting Sona may reconnect. Retry Disconnect.";
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException)
        { ProtectionResult.Text = "Restoration could not finish. The connection is kept; retry Disconnect."; }
        finally { discordConnectionBusy = false; preferencesApplying = false; RefreshSettingsActions(); RefreshDiscordConnection(); }
    }
    private bool PersistDiscordConnectionChoice(bool disconnected)
    {
        var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        next.DiscordDisconnected = disconnected;
        try { next.Save(preferencesPath); preferences.DiscordDisconnected = disconnected; return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    private void RefreshProtectionFields()
    {
        RefreshProtectionToggles(); ProtectionToggleResult.Text = ProtectionPolicySummary;
        WatchedApps.Text = string.Join("\n", preferences.WatchedApps); ProtectionDelay.Text = preferences.ProtectionReleaseDelayMs.ToString();
        ProtectionShortcutInput.Text = preferences.ProtectionShortcut;
    }
    internal void AttachProtectionForTests(ProtectionService service)
    {
        if (!arguments.Contains("--test-ui")) throw new InvalidOperationException("Fake protection requires isolated UI mode.");
        protection = service; RefreshProtectionFields(); RefreshDiscordConnection();
    }
    private void RefreshDiscordConnection()
    {
        if (ConnectDiscordButton == null || DiscordConnectionBadge == null) return;
        var connected = protection?.Connected == true;
        HushStatus.Text = connected ? "Connected" : "Not connected";
        DiscordConnectionDetail.Text = discordConnectionBusy ? "Updating the Discord connection..." : connected ? protection!.Status + (preferences.ProtectionEnabled ? preferences.SilenceIncoming ? ". Microphone and incoming audio." : ". Microphone only." : "") : protection?.ConnectionSuppressed == true ? "Disconnected; call protection is inactive. Your saved setup is kept." : "Connect Discord to use call protection. Your saved setup is kept.";
        ConnectDiscordButton.Content = discordConnectionBusy && !connected ? "Connecting..." : "Connect Discord";
        ConnectDiscordButton.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        ConnectDiscordButton.IsEnabled = !SettingsBusy && !discordConnectionBusy && protection != null;
        DisconnectDiscordButton.Visibility = connected || protection?.OwnsChanges == true || protection?.ConnectionSuppressed == true && !preferences.DiscordDisconnected ? Visibility.Visible : Visibility.Collapsed;
        DisconnectDiscordButton.IsEnabled = !SettingsBusy && !discordConnectionBusy;
        DisconnectDiscordButton.Content = protection?.ConnectionSuppressed == true && !preferences.DiscordDisconnected ? "Retry disconnect" : "Disconnect";
        PauseProtectionButton.IsEnabled = !SettingsBusy && !discordConnectionBusy && connected && (preferences.ProtectionEnabled || protection!.Paused || protection.OwnsChanges);
        SaveDiscordSetupButton.IsEnabled = !SettingsBusy && !discordConnectionBusy && !connected;
        if (connected && !SystemParameters.HighContrast)
        {
            DiscordConnectionBadge.Background = new SolidColorBrush(Color.FromRgb(0x20,0x65,0x42));
            DiscordConnectionBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x66,0xC2,0x91));
            HushStatus.Foreground = DiscordConnectionDetail.Foreground = Brushes.White;
        }
        else
        {
            DiscordConnectionBadge.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "Inset");
            DiscordConnectionBadge.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "Line");
            HushStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Ink");
            DiscordConnectionDetail.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "Muted");
        }
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
        if (SettingsBusy || credentialVault == null || protection?.Connected == true) { ProtectionResult.Text = "Disconnect Discord before replacing its saved setup."; return; }
        var id = DiscordClientId.Text.Trim(); var secret = DiscordClientSecret.Password;
        if (id.Length is < 5 or > 50 || !id.All(char.IsAsciiDigit) || secret.Length is < 8 or > 256) { ProtectionResult.Text = "Enter a Discord application ID and secret from your own developer application."; return; }
        try { credentialVault.Save(new DiscordCredentials { ClientId = id, ClientSecret = secret }); DiscordClientId.Clear(); DiscordClientSecret.Clear(); ProtectionResult.Text = "Discord setup saved with Windows encryption. Connect Discord when ready."; }
        catch (IOException) { ProtectionResult.Text = "Windows could not save the protected setup."; }
    }
}
