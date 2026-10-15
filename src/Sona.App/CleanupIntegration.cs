using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;
namespace Sona.App;

public partial class MainWindow
{
    internal Func<string, CancellationToken, Task<string>>? CleanupForTests { get; set; }
    private readonly CleanupModel cleanupModel = new();
    private Task cleanupPreparation = Task.CompletedTask;
    private Task cleanupInstallOperation = Task.CompletedTask;
    private CancellationTokenSource? cleanupInstallCancellation;
    private bool cleanupInstalling;
    private int cleanupPercent;
    private string cleanupOutcome = "idle";
    private sealed record PolishedText(string Text, bool Applied);

    private static void Choose(ComboBox choice, string tag)
    {
        var item = choice.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag);
        if (item == null) { item = new ComboBoxItem { Content = tag + " minutes", Tag = tag }; choice.Items.Add(item); }
        choice.SelectedItem = item;
    }
    private void RefreshCleanupChoices()
    {
        CleanupEnabled.IsChecked = preferences.CleanupEnabled;
        Choose(CleanupToneChoice, preferences.CleanupTone); Choose(CleanupEngineChoice, preferences.CleanupEngine);
        Choose(CleanupIdleChoice, preferences.CleanupIdleMinutes.ToString());
    }
    private void ReadCleanupPreferences(Preferences next)
    {
        next.CleanupEnabled = CleanupEnabled.IsChecked == true;
        next.CleanupTone = (string)((ComboBoxItem)CleanupToneChoice.SelectedItem).Tag;
        next.CleanupEngine = (string)((ComboBoxItem)CleanupEngineChoice.SelectedItem).Tag;
        next.CleanupIdleMinutes = int.Parse((string)((ComboBoxItem)CleanupIdleChoice.SelectedItem).Tag);
    }
    private void InitializeCleanup()
    {
        RefreshCleanupChoices(); RefreshCleanupStatus();
        modelIdleTimer.Tick += async (_, _) =>
        {
            if (shutdownPending || SettingsBusy) return;
            try { await cleanupModel.ExpireAsync(modelLifetime.Token); RefreshCleanupStatus(); }
            catch (OperationCanceledException) { }
            catch (IOException) { CleanupMemory.Text = "Cleanup memory is still releasing. Try Unload cleanup model."; }
        };
        Closed += (_, _) => cleanupInstallCancellation?.Dispose();
    }
    private void RefreshCleanupStatus()
    {
        CleanupMemory.Text = cleanupModel.CleanupPending ? "Cleanup memory is still releasing. Try Unload cleanup model."
            : cleanupModel.LoadedBackend == "GPU" ? $"Loaded on GPU / VRAM · NVIDIA CUDA · {cleanupModel.ModelVramMiB:0} MiB model weights"
            : cleanupModel.LoadedBackend == "CPU" ? "Loaded on CPU / system RAM"
            : cleanupModel.AvailableFor(preferences) ? "Installed · not loaded in memory"
            : "Set up the selected backend to download the local model and runtime.";
    }
    private async Task WarmAllSelectedAsync()
    {
        await WarmSelectedAsync();
        cleanupPreparation = WarmCleanupAsync(); await cleanupPreparation;
    }
    private async Task WarmCleanupAsync()
    {
        if (arguments.Contains("--test-ui") || SettingsBusy || shutdownPending) return;
        preferencesApplying = true;
        try
        {
            if (preferences.CleanupEnabled && !cleanupModel.AvailableFor(preferences)) { await cleanupModel.UnloadAsync(modelLifetime.Token); CleanupResult.Text = "Set up the selected backend first. Dictation still works with basic cleanup."; return; }
            CleanupMemory.Text = preferences.CleanupEnabled ? "Preparing cleanup model…" : "Releasing cleanup memory…";
            if (preferences.Style == "Verbatim") await cleanupModel.UnloadAsync(modelLifetime.Token);
            else await cleanupModel.ReconcileAsync(preferences, modelLifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or TimeoutException or System.ComponentModel.Win32Exception) { CleanupResult.Text = "Cleanup model could not load. Choose CPU / RAM or check the installed GPU backend."; }
        finally { preferencesApplying = false; if (!shutdownPending) RefreshCleanupStatus(); }
    }
    private async Task<PolishedText> PolishAsync(string original, CancellationToken token, string? appName = null)
    {
        token.ThrowIfCancellationRequested();
        var fallback = TextPipeline.Apply(original, preferences);
        var snippet = preferences.Snippets.Any(s => s.Trigger.Trim().Equals(original.Trim().TrimEnd('.', '!', '?'), StringComparison.OrdinalIgnoreCase));
        if (!preferences.CleanupEnabled || preferences.Style == "Verbatim" || snippet) return new(fallback, false);
        if (arguments.Contains("--test-ui") && CleanupForTests != null) return new(await CleanupForTests(original, token), true);
        if (!cleanupModel.AvailableFor(preferences)) { CleanupResult.Text = "Local cleanup is not ready. Basic cleanup was used."; return new(fallback, false); }
        Status.Text = "Polishing your draft"; StatusDetail.Text = "Cleaning up locally. Esc still cancels."; overlay.Update("Polishing", "Esc cancels");
        try
        {
            var result = await CleanupProcessor.RunAsync(original, preferences, appName ?? Insertion.AppName(target), (prompt, ct) => cleanupModel.RunAsync(prompt, preferences, ct), token);
            CleanupResult.Text = result.Outcome switch
            {
                "polished" => "Text polished locally. Use original restores the transcript.",
                "preserved" => "Original wording kept: cleanup could not safely preserve your terms or the text exceeded its limits.",
                "unavailable" => "Local cleanup was unavailable. Basic cleanup was used; the original transcript is retained.",
                _ => "Local model cleanup skipped for this mode."
            };
            return new(result.Text, result.Applied);
        }
        finally { RefreshCleanupStatus(); }
    }
    private async void InstallCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { CleanupResult.Text = "Finish the current operation first."; return; }
        cleanupInstallOperation = InstallCleanupAsync((string)((ComboBoxItem)CleanupEngineChoice.SelectedItem).Tag); await cleanupInstallOperation;
    }
    private async Task InstallCleanupAsync(string engine)
    {
        cleanupInstalling = true; cleanupPercent = 0; cleanupOutcome = "installing";
        cleanupInstallCancellation?.Dispose(); cleanupInstallCancellation = new(); InstallCleanupButton.IsEnabled = false;
        var progress = new Progress<int>(p => { cleanupPercent = p; CleanupResult.Text = $"Setting up local cleanup: {p}%"; });
        try { await CleanupAssets.RetryCleanupAsync(); await CleanupAssets.InstallAsync(engine, progress, cleanupInstallCancellation.Token); cleanupPercent = 100; cleanupOutcome = "installed"; CleanupResult.Text = "Setup verified. Enable cleanup and Save preferences to use it."; }
        catch (OperationCanceledException) { cleanupOutcome = "cancelled"; CleanupResult.Text = "Setup cancelled. Existing installed assets were preserved."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException)
        { cleanupOutcome = "failed"; CleanupResult.Text = "Setup could not finish. Existing files were preserved. Check connection and disk space."; }
        finally { cleanupInstalling = false; InstallCleanupButton.IsEnabled = true; RefreshCleanupStatus(); }
    }
    private async void CancelCleanupSetup_Click(object sender, RoutedEventArgs e)
    {
        if (cleanupInstalling) { cleanupInstallCancellation?.Cancel(); return; }
        if (SettingsBusy) return;
        preferencesApplying = true;
        try { await CleanupAssets.RetryCleanupAsync(); CleanupResult.Text = "Setup cleanup completed."; }
        catch (IOException) { CleanupResult.Text = "A setup file is still locked. Try again shortly."; }
        finally { preferencesApplying = false; }
    }
    private async void LoadCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) return;
        if (!preferences.CleanupEnabled) { CleanupResult.Text = "Enable cleanup and Save preferences first."; return; }
        cleanupPreparation = WarmCleanupAsync(); await cleanupPreparation;
    }
    private async void UnloadCleanup_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) return;
        preferencesApplying = true;
        try { await cleanupModel.UnloadAsync(modelLifetime.Token); CleanupResult.Text = "Cleanup model unloaded. The next dictation will load it again."; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { CleanupResult.Text = "Cleanup memory release needs attention. Try again shortly."; }
        finally { preferencesApplying = false; RefreshCleanupStatus(); }
    }
}
