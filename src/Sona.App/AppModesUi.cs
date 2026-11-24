using System.Windows;
using System.Windows.Controls;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    private readonly List<AppMode> pendingAppModes = [];
    private readonly List<string> recentObservedApps = [];

    private sealed record AppModeRow(AppMode Mode)
    {
        public string App => Mode.App;
        public string Summary => AppMode.StyleLabel(Mode.Style) + " style, " + AppMode.ToneLabel(Mode.Tone) + " tone";
        public string RemoveLabel => "Remove mode for " + Mode.App;
    }

    private void InitializeAppModes()
    {
        pendingAppModes.Clear();
        pendingAppModes.AddRange(preferences.AppModes);
        RefreshAppModes();
        RefreshRecentApps();
    }

    private void ObserveRecentApp(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || !AppModes.ValidName(trimmed)) return;
        if (trimmed is "Sona" or "No focused text field" or "No active app" or "Unknown app" or "Captured app" or "Checking destination") return;
        if (recentObservedApps.Count > 0 && recentObservedApps[0].Equals(trimmed, StringComparison.OrdinalIgnoreCase)) return;
        recentObservedApps.RemoveAll(existing => existing.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        recentObservedApps.Insert(0, trimmed);
        if (recentObservedApps.Count > 8) recentObservedApps.RemoveRange(8, recentObservedApps.Count - 8);
        if (Pages?.SelectedIndex == 2 || RecentAppsList?.ItemsSource == null) RefreshRecentApps();
    }

    private void RefreshRecentApps()
    {
        if (RecentAppsPanel == null || RecentAppsList == null) return;
        RecentAppsPanel.Visibility = recentObservedApps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RecentAppsList.ItemsSource = recentObservedApps.Count == 0 ? null : recentObservedApps.ToArray();
    }

    private void UseRecentApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string app }) return;
        AppModeApp.Text = app;
        AppModeApp.Focus();
        AppModeApp.CaretIndex = app.Length;
    }

    private void RefreshAppModes()
    {
        if (AppModeList == null) return;
        AppModeList.ItemsSource = pendingAppModes.Select(mode => new AppModeRow(mode)).ToList();
        AppModeEmpty.Visibility = pendingAppModes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddAppModeButton.IsEnabled = preferencesLoad.CanSave && pendingAppModes.Count < AppModes.Maximum;
    }

    private void ResetAppModes()
    {
        pendingAppModes.Clear();
        pendingAppModes.AddRange(preferences.AppModes);
        RefreshAppModes();
    }

    private void AddAppMode_Click(object sender, RoutedEventArgs e)
    {
        if (!preferencesLoad.CanSave) { AppModeResult.Text = preferencesLoad.Message; return; }
        if (processing || recorder != null || preparing) { AppModeResult.Text = "Finish or cancel the current dictation before changing modes."; return; }
        var app = (AppModeApp.Text ?? "").Trim();
        if (!AppModes.ValidName(app)) { AppModeResult.Text = "Enter an app name up to 160 characters, for example Codex or Discord."; return; }
        if (pendingAppModes.Count >= AppModes.Maximum) { AppModeResult.Text = "Sona supports up to " + AppModes.Maximum + " per-app rules."; return; }
        if (pendingAppModes.Any(mode => mode.App.Equals(app, StringComparison.OrdinalIgnoreCase)))
        { AppModeResult.Text = "A rule for " + app + " already exists. Remove it first, or enter a different app name."; return; }
        var style = (string)((ComboBoxItem)AppModeStyle.SelectedItem).Content;
        var tone = (string)((ComboBoxItem)AppModeTone.SelectedItem).Content switch
        { "Casual" => "casual", "Lowercase" => "very casual", _ => "formal" };
        pendingAppModes.Add(new AppMode(app, style, tone));
        AppModeApp.Clear();
        AppModeResult.Text = "Rule added for " + app + ". Save preferences to keep it.";
        RefreshAppModes();
        RefreshSettingsActions();
    }

    private void RemoveAppMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AppMode mode }) return;
        if (processing || recorder != null || preparing) { AppModeResult.Text = "Finish or cancel the current dictation before changing modes."; return; }
        pendingAppModes.RemoveAll(existing => existing.App.Equals(mode.App, StringComparison.OrdinalIgnoreCase));
        AppModeResult.Text = "Rule removed. Save preferences to keep this change.";
        RefreshAppModes();
        RefreshSettingsActions();
    }
}
