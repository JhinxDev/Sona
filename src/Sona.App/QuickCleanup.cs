using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
namespace Sona.App;

public partial class MainWindow
{
    private void RefreshQuickCleanup() => overlay.SetQuickCleanup(preferences.CleanupEnabled ? preferences.CleanupMode : "Off", preferencesLoad.CanSave && !shutdownPending && !processing && !preferencesApplying && !cleanupInstalling);
    private void CycleQuickCleanup()
    {
        if (!preferencesLoad.CanSave || shutdownPending || processing || preferencesApplying || cleanupInstalling) return;
        var mode = !preferences.CleanupEnabled ? "Auto" : preferences.CleanupMode == "Auto" ? "Always" : "Off";
        try
        {
            var next = JsonSerializer.Deserialize<Sona.Core.Preferences>(JsonSerializer.Serialize(preferences))!;
            next.CleanupEnabled = mode != "Off"; if (mode != "Off") next.CleanupMode = mode;
            next.Save(preferencesPath);
            preferences.CleanupEnabled = next.CleanupEnabled; preferences.CleanupMode = next.CleanupMode;
            CleanupEnabled.IsChecked = next.CleanupEnabled; Choose(CleanupModeChoice, next.CleanupMode);
            CleanupResult.Text = mode == "Off" ? "Model cleanup turned off from the recording pill." : mode + " cleanup selected from the recording pill. This recording uses the new choice.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { CleanupResult.Text = "Cleanup choice could not be saved. The previous choice is still active."; }
        finally { RefreshQuickCleanup(); }
    }
}
