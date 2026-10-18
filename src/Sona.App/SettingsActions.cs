using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    private readonly DispatcherTimer settingsActionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool restoringIdleChoice;
    private bool HasUnsavedGeneralSettings(Preferences? candidate = null) => JsonSerializer.Serialize(candidate ?? ReadGeneralPreferences()) != JsonSerializer.Serialize(preferences);
    private bool HasUnsavedProtectionSettings =>
        (ProtectionEnabled.IsChecked == true) != preferences.ProtectionEnabled ||
        (SilenceIncoming.IsChecked == true) != preferences.SilenceIncoming ||
        !WatchedApps.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).SequenceEqual(preferences.WatchedApps, StringComparer.Ordinal) ||
        !int.TryParse(ProtectionDelay.Text, out var delay) || delay != preferences.ProtectionReleaseDelayMs ||
        ProtectionShortcutInput.Text.Trim() != preferences.ProtectionShortcut;
    private bool HasUnsavedSettings => HasUnsavedGeneralSettings() || HasUnsavedProtectionSettings;

    // General preferences are deliberately separate from protected credentials
    // and call-protection configuration, which retain their explicit save actions.
    private Preferences ReadGeneralPreferences()
    {
        var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        next.Language = (string)((ComboBoxItem)SpeechLanguage.SelectedItem).Tag;
        next.Engine = TranscriptionEngine.SelectedIndex == 1 ? "FastCpu" : "Auto";
        next.Style = (string)((ComboBoxItem)DraftStyle.SelectedItem).Content;
        next.Device = Device.SelectedItem is DeviceOption option ? option.Id : -1;
        next.SoundEffects = SoundEffects.IsChecked == true; next.PasteCompletedDraft = PasteCompletedDraft.IsChecked == true; next.EnterToSend = EnterToSend.IsChecked == true;
        next.LiveTranscription = LiveTranscription.IsChecked == true; next.AutoInsert = AutoInsert.IsChecked == true; next.CopyToClipboard = CopyToClipboard.IsChecked == true;
        next.CheckForUpdates = CheckUpdates.IsChecked == true; next.CloseToTray = CloseToTray.IsChecked == true;
        next.Startup = StartWithWindows.IsChecked == true; next.KeepHistory = KeepHistory.IsChecked == true;
        next.ShortcutMode = ShortcutMode.SelectedIndex == 1 ? "Toggle" : "Hold";
        next.Theme = ThemeChoice.SelectedItem is ThemeDefinition theme ? theme.Id : "charcoal";
        next.RecordingDisplay = PanelDisplay.IsChecked == true ? "Panel" : "Capsule";
        next.ModelIdleMinutes = preferences.ModelIdleMinutes;
        ReadCleanupPreferences(next);
        return next;
    }

    private void InitializeSettingsActions()
    {
        RefreshProtectionFields();
        foreach (var choice in new[] { SpeechLanguage, TranscriptionEngine, DraftStyle, Device, ShortcutMode, CleanupToneChoice, CleanupEngineChoice, CleanupIdleChoice, ThemeChoice })
            choice.SelectionChanged += (_, _) => RefreshSettingsActions();
        foreach (var choice in new[] { SoundEffects, PasteCompletedDraft, EnterToSend, LiveTranscription, AutoInsert, CopyToClipboard, CheckUpdates, CloseToTray, StartWithWindows, KeepHistory, CleanupEnabled })
        { choice.Checked += (_, _) => RefreshSettingsActions(); choice.Unchecked += (_, _) => RefreshSettingsActions(); }
        PanelDisplay.Checked += (_, _) => RefreshSettingsActions(); CapsuleDisplay.Checked += (_, _) => RefreshSettingsActions();
        ProtectionEnabled.Checked += ProtectionToggleChanged; ProtectionEnabled.Unchecked += ProtectionToggleChanged;
        SilenceIncoming.Checked += ProtectionToggleChanged; SilenceIncoming.Unchecked += ProtectionToggleChanged;
        ModelIdleChoice.SelectionChanged += ModelIdleChanged;
        settingsActionTimer.Tick += (_, _) => RefreshSettingsActions();
        Loaded += (_, _) => settingsActionTimer.Start(); Closed += (_, _) => settingsActionTimer.Stop();
        RefreshSettingsActions();
    }

    private void RefreshSettingsActions()
    {
        if (SavePreferencesButton == null || CleanupIdleChoice.SelectedItem == null || SpeechLanguage.SelectedItem == null) return;
        var next = ReadGeneralPreferences();
        PreferencesSaveBar.Visibility = HasUnsavedGeneralSettings(next) ? Visibility.Visible : Visibility.Collapsed;

        SavePreferencesButton.IsEnabled = !SettingsBusy;
        RefreshHistoryActions();
        // Programmatic initialization is guarded; deliberate switch changes save directly.
        ProtectionEnabled.IsEnabled = !SettingsBusy && !discordConnectionBusy;
        SilenceIncoming.IsEnabled = ProtectionEnabled.IsChecked == true && !SettingsBusy && !discordConnectionBusy;
        SaveProtectionButton.IsEnabled = !SettingsBusy && !discordConnectionBusy;
        SilenceIncoming.ToolTip = ProtectionEnabled.IsChecked == true ? "Also silence incoming audio when Discord protection is active." : "Enable Discord protection to use this option. Your choice is retained.";
        StartupReason.Visibility = StartWithWindows.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        StartupReason.Text = arguments.Contains("--test-ui") ? "Disabled in isolated preview." : "Unavailable for this build.";
        StartWithWindows.ToolTip = StartWithWindows.IsEnabled ? "Start Sona automatically when you sign in." : StartupReason.Text + " Windows startup is unchanged.";
        var selected = Transcriber.SelectedModel(preferences);
        var resident = transcriber.LoadedModel != null || transcriber.ResidentCleanupPending;
        UnloadModelButton.Visibility = resident ? Visibility.Visible : Visibility.Collapsed;
        UnloadModelButton.IsEnabled = !SettingsBusy;
        // A different selected model can still be loaded while another is resident.
        LoadModelButton.Visibility = transcriber.LoadedModel != selected && !transcriber.ResidentCleanupPending ? Visibility.Visible : Visibility.Collapsed;
        LoadModelButton.IsEnabled = !SettingsBusy && transcriber.AvailableFor(preferences);
        CancelModelDownloadButton.Visibility = downloadingModel != null || modelStore.CleanupPending || ParakeetAssets.CleanupPending ? Visibility.Visible : Visibility.Collapsed;
        CancelModelDownloadButton.Content = downloadingModel != null ? "Cancel download" : "Retry partial-file cleanup";
        CancelModelDownloadButton.IsEnabled = downloadingModel != null || !SettingsBusy;

        var setupNeeded = !cleanupModel.AvailableFor(next);
        var cleanupResident = cleanupModel.LoadedBackend != null || cleanupModel.CleanupPending;
        InstallCleanupButton.Visibility = setupNeeded && !cleanupInstalling ? Visibility.Visible : Visibility.Collapsed;
        InstallCleanupButton.IsEnabled = !SettingsBusy;
        var selectedBackend = CleanupAssets.ResolveBackend(preferences.CleanupEngine);
        LoadCleanupButton.Visibility = !cleanupModel.CleanupPending && cleanupModel.LoadedBackend != selectedBackend ? Visibility.Visible : Visibility.Collapsed;
        LoadCleanupButton.IsEnabled = !SettingsBusy && preferences.CleanupEnabled && preferences.Style != "Verbatim" && cleanupModel.AvailableFor(preferences);
        LoadCleanupButton.ToolTip = preferences.CleanupEnabled ? "Load the saved cleanup backend." : "Enable cleanup and save preferences first.";
        UnloadCleanupButton.Visibility = cleanupResident ? Visibility.Visible : Visibility.Collapsed;
        UnloadCleanupButton.IsEnabled = !SettingsBusy;
        CancelCleanupSetupButton.Visibility = cleanupInstalling || CleanupAssets.CleanupPending ? Visibility.Visible : Visibility.Collapsed;
        CancelCleanupSetupButton.Content = cleanupInstalling ? "Cancel setup" : "Retry setup file cleanup";
        CancelCleanupSetupButton.IsEnabled = cleanupInstalling || !SettingsBusy;
        PauseProtectionButton.IsEnabled = !SettingsBusy && protection != null && (preferences.ProtectionEnabled || protection.Paused || protection.OwnsChanges);
        PauseProtectionButton.Content = protection?.Paused == true ? "Resume protection" : "Pause protection";
        RefreshDiscordConnection();
    }

    private void ModelIdleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (restoringIdleChoice || ModelIdleChoice.SelectedItem is not ComboBoxItem item || !int.TryParse((string)item.Tag, out var minutes) || minutes == preferences.ModelIdleMinutes) return;
        if (SettingsBusy) { RestoreModelIdle(); ModelResult.Text = "Finish the current operation before changing memory preference."; return; }
        var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        next.ModelIdleMinutes = minutes;
        try { next.Save(preferencesPath); preferences.ModelIdleMinutes = minutes; ModelResult.Text = "Memory preference saved. Applies at the next model use."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { RestoreModelIdle(); ModelResult.Text = "Memory preference could not be saved. Previous choice retained."; }
        RefreshSettingsActions();
    }
    private void RestoreModelIdle()
    {
        restoringIdleChoice = true;
        try { SetIdleChoice(preferences.ModelIdleMinutes); }
        finally { restoringIdleChoice = false; }
    }
}
