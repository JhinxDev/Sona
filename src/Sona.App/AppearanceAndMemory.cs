using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Sona.Core;
namespace Sona.App;
public partial class MainWindow
{
    private bool appearanceInitialized;
    private readonly DispatcherTimer modelIdleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource modelLifetime = new();
    private Task modelPreparation = Task.CompletedTask;
    private string? displayedResident;
    private void RefreshResidentStatusIfReady() { if (ResidentStatus != null) RefreshResidentStatus(); }
    private void SetIdleChoice(int minutes)
    {
        var item = ModelIdleChoice.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == minutes.ToString());
        if (item == null) { item = new ComboBoxItem { Content = minutes + " minutes", Tag = minutes.ToString() }; ModelIdleChoice.Items.Add(item); }
        ModelIdleChoice.SelectedItem = item;
    }
    private void InitializeAppearance()
    {
        var themes = CollectionViewSource.GetDefaultView(ThemeCatalog.All);
        themes.GroupDescriptions.Add(new PropertyGroupDescription("Group")); ThemeChoice.ItemsSource = themes;
        ThemeChoice.SelectedItem = ThemeCatalog.Resolve(preferences.Theme);
        PanelDisplay.IsChecked = preferences.RecordingDisplay == "Panel"; CapsuleDisplay.IsChecked = !PanelDisplay.IsChecked;
        SetIdleChoice(preferences.ModelIdleMinutes);
        ThemeManager.Apply(preferences.Theme); ThemeManager.ApplyTitleBar(this);
        overlay.SetDisplay(preferences.RecordingDisplay); overlay.UpdateModel(ModelCatalog.Get(Audio.Transcriber.SelectedModel(preferences)).Name);
        appearanceInitialized = true;
        modelIdleTimer.Tick += async (_, _) => { if (shutdownPending || SettingsBusy) return; try { await transcriber.ExpireAsync(modelLifetime.Token); if (displayedResident != transcriber.LoadedModel) { displayedResident = transcriber.LoadedModel; RefreshModelCards(); } RefreshResidentStatus(); } catch (OperationCanceledException) { } catch (Exception ex) when (ex is IOException or TimeoutException) { ResidentStatus.Text = "Memory release needs attention. Try Unload now."; } };
        Closed += (_, _) => { modelIdleTimer.Stop(); modelLifetime.Cancel(); modelLifetime.Dispose(); };
        RefreshResidentStatus();
    }
    private void RefreshResidentStatus()
    {
        ResidentStatus.Text = transcriber.ResidentCleanupPending ? "Model memory is still releasing. Use Unload now to retry." : transcriber.LoadedModel is { } id ? ModelCatalog.Get(id).Name + " is loaded in memory." : "No model loaded. Your downloaded models remain on disk.";
        var selected = Audio.Transcriber.SelectedModel(preferences);
        if (selected != transcriber.LoadedModel) ResidentStatus.Text += " " + ModelCatalog.Get(selected).Name + " is selected for the next dictation.";
    }
    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!appearanceInitialized || ThemeChoice.SelectedItem is not ThemeDefinition selected || selected.Id == preferences.Theme) return;
        if (SettingsBusy) { RestoreSavedThemeChoice(); SettingsResult.Text = "Finish the current operation before changing appearance."; return; }
        var next = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!;
        next.Theme = selected.Id;
        try
        {
            // Commit just the theme from active preferences. Other unsaved
            // form choices remain untouched until Save preferences is used.
            next.Save(preferencesPath);
            preferences.Theme = next.Theme;
            ThemeManager.Apply(preferences.Theme);
            SettingsResult.Text = "Appearance saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RestoreSavedThemeChoice();
            SettingsResult.Text = "Appearance could not be saved. Your previous theme is retained.";
        }
    }
    private void RestoreSavedThemeChoice()
    {
        appearanceInitialized = false;
        try { ThemeChoice.SelectedItem = ThemeCatalog.Resolve(preferences.Theme); }
        finally { appearanceInitialized = true; }
    }
    private void DisplayChanged(object sender, RoutedEventArgs e)
    {
        if (appearanceInitialized && !SettingsBusy && sender is RadioButton choice) overlay.SetDisplay((string)choice.Tag);
    }
    private async Task WarmSelectedAsync()
    {
        if (arguments.Contains("--test-ui") || SettingsBusy || shutdownPending) return;
        preferencesApplying = true; ResidentStatus.Text = "Loading the selected model into memory…";
        try { await transcriber.ReconcileAsync(preferences, modelLifetime.Token); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException or System.ComponentModel.Win32Exception) { if (!shutdownPending) ModelResult.Text = "The model could not stay loaded. " + ex.Message; }
        finally { preferencesApplying = false; if (!shutdownPending) { RefreshResidentStatus(); RefreshModelCards(); } }
    }
    private async void LoadModel_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { ModelResult.Text = "Finish the current operation first."; return; }
        modelPreparation = WarmSelectedAsync(); await modelPreparation;
    }
    private async void UnloadModel_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { ModelResult.Text = "Finish the current operation first."; return; }
        preferencesApplying = true;
        try { await transcriber.UnloadAsync(modelLifetime.Token); ModelResult.Text = "Model unloaded. The next dictation will load it again."; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { ModelResult.Text = "Memory release could not be confirmed."; }
        finally { preferencesApplying = false; RefreshResidentStatus(); RefreshModelCards(); }
    }
}
