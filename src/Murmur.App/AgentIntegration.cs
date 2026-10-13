using System.Text.Json;
using System.Windows.Controls;
using Murmur.Core;
using Murmur.App.Audio;
using Murmur.App.Windows;
namespace Murmur.App;

public partial class MainWindow
{
    private AgentServer? agentServer;
    private Task<AgentReply> HandleAgentAsync(AgentRequest request, CancellationToken cancellation) => Dispatcher.InvokeAsync(() => HandleAgentOnUiAsync(request, cancellation), System.Windows.Threading.DispatcherPriority.Normal, cancellation).Task.Unwrap();
    private async Task<AgentReply> HandleAgentOnUiAsync(AgentRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (shutdownPending) return new(false, Error: "Sona is shutting down.");
        switch (request.Method)
        {
            case "status": return new(true, new { version = AppVersion.Number, state = recorder != null ? "recording" : processing || preparing || preferencesApplying || cleanupInstalling ? "busy" : "idle", model = Transcriber.SelectedModel(preferences), loadedModel = transcriber.LoadedModel, residentProcessId = transcriber.ResidentProcessId, residentCleanupPending = transcriber.ResidentCleanupPending, downloadingModel, downloadPercent, downloadOutcome, apiVersion = 1, cleanupBackend = cleanupModel.LoadedBackend, cleanupProcessId = cleanupModel.ProcessId, cleanupInstalling, cleanupPercent, cleanupOutcome, microphoneControl = false });
            case "settings.get": return new(true, AgentSettings.View(preferences));
            case "settings.patch":
                if (SettingsBusy) return new(false, Error: "Finish the current dictation before changing settings.");
                var next = AgentSettings.Apply(preferences, request.Parameters);
                if (next.Device != -1 && !Recorder.Devices().Any(d => d.Id == next.Device)) return new(false, Error: "Requested microphone is not available.");
                if (!transcriber.AvailableFor(next)) return new(false, Error: "Install a compatible model and engine first.");
                cancellation.ThrowIfCancellationRequested();
                preferencesApplying = true;
                try
                {
                    if (protection != null) await protection.ConfigureAsync(next, cancellation);
                    cancellation.ThrowIfCancellationRequested(); next.Save(preferencesPath);
                }
                catch { await ConfigureProtectionAsync(); throw; }
                finally { preferencesApplying = false; }
                preferences.Language = next.Language; preferences.Engine = next.Engine; preferences.Model = next.Model; preferences.ShortcutMode = next.ShortcutMode;
                preferences.Style = next.Style; preferences.CleanupEnabled = next.CleanupEnabled; preferences.CleanupTone = next.CleanupTone; preferences.CleanupEngine = next.CleanupEngine; preferences.CleanupIdleMinutes = next.CleanupIdleMinutes; RefreshCleanupChoices(); preferences.Device = next.Device; preferences.AutoInsert = next.AutoInsert; preferences.CopyToClipboard = next.CopyToClipboard; preferences.CloseToTray = next.CloseToTray;
                preferences.Theme = next.Theme; preferences.RecordingDisplay = next.RecordingDisplay; preferences.ModelIdleMinutes = next.ModelIdleMinutes;
                ThemeManager.Apply(preferences.Theme); ThemeChoice.SelectedItem = ThemeCatalog.Resolve(preferences.Theme); overlay.SetDisplay(preferences.RecordingDisplay); PanelDisplay.IsChecked = preferences.RecordingDisplay == "Panel"; CapsuleDisplay.IsChecked = !PanelDisplay.IsChecked;
                SetIdleChoice(preferences.ModelIdleMinutes);
                preferences.ProtectionEnabled = next.ProtectionEnabled; preferences.SilenceIncoming = next.SilenceIncoming; preferences.ProtectionReleaseDelayMs = next.ProtectionReleaseDelayMs; preferences.WatchedApps = next.WatchedApps;
                preferences.ProtectionShortcut = next.ProtectionShortcut; ProtectionShortcutInput.Text = preferences.ProtectionShortcut; UpdateProtectionHotkey();
                ProtectionEnabled.IsChecked = preferences.ProtectionEnabled; SilenceIncoming.IsChecked = preferences.SilenceIncoming; WatchedApps.Text = string.Join("\n", preferences.WatchedApps); ProtectionDelay.Text = preferences.ProtectionReleaseDelayMs.ToString();
                SpeechLanguage.SelectedIndex = preferences.Language == "en" ? 1 : preferences.Language == "fr" ? 2 : 0;
                TranscriptionEngine.SelectedIndex = preferences.Engine == "FastCpu" ? 1 : 0;
                ShortcutMode.SelectedIndex = preferences.ShortcutMode == "Toggle" ? 1 : 0;
                DraftStyle.SelectedIndex = preferences.Style == "Verbatim" ? 1 : preferences.Style == "Notes" ? 2 : 0;
                AutoInsert.IsChecked = preferences.AutoInsert; CopyToClipboard.IsChecked = preferences.CopyToClipboard; CloseToTray.IsChecked = preferences.CloseToTray;
                Volatile.Write(ref shortcutToggleMode, preferences.ShortcutMode == "Toggle" ? 1 : 0); hotkeys?.ResetBaseline(); RefreshDevices(); RefreshModelCards();
                modelPreparation = WarmAllSelectedAsync();
                return new(true, AgentSettings.View(preferences));
            case "cleanup.status": return new(true, new { enabled = preferences.CleanupEnabled, available = cleanupModel.AvailableFor(preferences), backend = cleanupModel.LoadedBackend, modelVramMiB = cleanupModel.ModelVramMiB, processId = cleanupModel.ProcessId, cleanupPending = cleanupModel.CleanupPending, setupCleanupPending = CleanupAssets.CleanupPending, installing = cleanupInstalling, percent = cleanupPercent, outcome = cleanupOutcome });
            case "cleanup.install":
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                cleanupInstallOperation = InstallCleanupAsync(preferences.CleanupEngine); return new(true, new { accepted = true });
            case "cleanup.cancel":
                if (cleanupInstalling) { cleanupInstallCancellation?.Cancel(); return new(true, new { cancellationRequested = true }); }
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                preferencesApplying = true;
                try { await CleanupAssets.RetryCleanupAsync(); return new(true, new { cancellationRequested = false, setupCleanupPending = CleanupAssets.CleanupPending }); }
                finally { preferencesApplying = false; }
            case "cleanup.load":
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                cleanupPreparation = WarmCleanupAsync(); return new(true, new { preparation = "started" });
            case "cleanup.unload":
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                preferencesApplying = true;
                try { await cleanupModel.UnloadAsync(cancellation); RefreshCleanupStatus(); return new(true, new { backend = cleanupModel.LoadedBackend }); }
                finally { preferencesApplying = false; }
            case "models.unload":
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                preferencesApplying = true;
                try { await transcriber.UnloadAsync(cancellation); RefreshResidentStatus(); RefreshModelCards(); return new(true, new { loadedModel = transcriber.LoadedModel }); }
                finally { preferencesApplying = false; }
            case "models.load":
                if (SettingsBusy) return new(false, Error: "Finish the current operation first.");
                modelPreparation = WarmAllSelectedAsync(); return new(true, new { preparation = "started" });
            case "models.list": return new(true, ModelCatalog.All.Select(m => new { m.Id, m.Name, m.Bytes, m.ParametersMillions, m.Description, m.Measurements, installed = modelStore.Present(m.Id), selected = Transcriber.SelectedModel(preferences) == m.Id, requiresCuda = ModelCatalog.NeedsCuda(m.Id) }).ToArray());
            case "models.info":
                var model = ModelCatalog.Get(ModelId(request.Parameters)); return new(true, new { model, installed = modelStore.Present(model.Id), license = model.License, revision = model.Revision });
            case "models.install":
                var installId = ModelId(request.Parameters); ModelCatalog.Get(installId);
                if (downloadingModel != null) return new(false, Error: "Another model download is running.");
                cancellation.ThrowIfCancellationRequested(); modelDownloadOperation = DownloadModelAsync(installId); return new(true, new { accepted = true, model = installId });
            case "models.verify":
                var verifyId = ModelId(request.Parameters); return new(true, new { model = verifyId, verified = await modelStore.VerifyAsync(verifyId, cancellation) });
            case "models.cancel": modelDownloadCancellation?.Cancel(); return new(true, new { requested = true });
            case "protection.status": return new(true, new { enabled = preferences.ProtectionEnabled, connected = protection?.Connected ?? false, paused = protection?.Paused ?? false, ownsChanges = protection?.OwnsChanges ?? false, shortcutRegistered = protectionHotkey?.Registered ?? false, status = protection?.Status ?? "Off" });
            case "protection.pause":
            case "protection.resume":
                if (protection == null) return new(false, Error: "Protection is unavailable in UI test mode.");
                await protection.PauseAsync(request.Method == "protection.pause", cancellation); return new(true, new { paused = protection.Paused });
            default: return new(false, Error: "Unknown operation.");
        }
    }
    private static string ModelId(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String) throw new ArgumentException("A model ID is required.");
        return id.GetString()!;
    }
}
