using System.Text.Json;
using System.Windows;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    /// <summary>First-run welcome, local-model setup guidance and draft word count.</summary>
    private void InitializeOnboarding()
    {
        RefreshOnboarding();
        UpdateDraftStats();
    }

    private void RefreshOnboarding()
    {
        if (WelcomeCard == null || SetupCallout == null) return;
        var modelReady = transcriber.AvailableFor(preferences);
        var state = Onboarding.Evaluate(preferencesLoad.Status, preferences.OnboardingDismissed, modelReady);
        WelcomeCard.Visibility = state.ShowWelcome ? Visibility.Visible : Visibility.Collapsed;
        SetupCallout.Visibility = !state.ShowWelcome && state.ShowSetup ? Visibility.Visible : Visibility.Collapsed;
        WelcomeModelStatus.Text = modelReady
            ? "A local speech model is ready on this PC."
            : "No local speech model is ready yet. Choose one below to start dictating.";
        WelcomeModelsButton.Content = modelReady ? "Review speech models" : "Choose a speech model";
        SetupCalloutDetail.Text = transcriber.Available
            ? "The selected model is not ready yet. Open Dictation Models to verify it or choose another."
            : "The local speech runtime is missing. Install or build Sona with its runtime, then choose a model.";
        if (!arguments.Contains("--test-ui"))
        {
            RecordButton.IsEnabled = preferencesLoad.CanSave && modelReady;
            RecordButton.ToolTip = modelReady ? "Start or stop an intentional recording." : "Choose a local speech model before recording.";
        }
    }

    private void OpenModels_Click(object sender, RoutedEventArgs e) => Pages.SelectedItem = ModelsTabItem;

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => Pages.SelectedItem = SettingsTabItem;

    private void DismissWelcome_Click(object sender, RoutedEventArgs e)
    {
        if (!preferencesLoad.CanSave) { StatusDetail.Text = preferencesLoad.Message; return; }
        try
        {
            // Persist only committed values, never unsaved form edits from another page.
            var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
            next.OnboardingDismissed = true;
            next.Save(preferencesPath);
            preferences.OnboardingDismissed = true;
            RefreshOnboarding();
            StatusDetail.Text = "Welcome dismissed. Use Settings and Dictation Models whenever you want to change setup.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { StatusDetail.Text = "Welcome dismissal could not be saved. " + ex.Message; }
    }

    private void UpdateDraftStats()
    {
        if (DraftStats == null) return;
        DraftStats.Text = TextStats.WordLabel(Draft?.Text);
    }
}
