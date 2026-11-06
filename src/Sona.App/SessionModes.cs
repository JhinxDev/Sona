using System.Text.Json;
using Sona.App.Windows;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    private AppMode? sessionMode;
    private Preferences? sessionDeliveryPreferences;

    private void ResetSessionMode()
    {
        sessionMode = null;
        sessionDeliveryPreferences = null;
    }

    /// <summary>
    /// Resolve the first matching per-app rule for the captured destination only.
    /// The rule affects the draft style and local cleanup tone for this one session;
    /// insertion, focus, submission and privacy guards are unchanged.
    /// </summary>
    private void ResolveSessionMode(DraftTarget? target)
    {
        ResetSessionMode();
        if (target == null || preferences.AppModes.Count == 0) return;
        var match = AppModes.Resolve(preferences.AppModes, Insertion.AppIdentities(target));
        if (match == null) return;
        var delivery = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        delivery.Style = match.Style;
        delivery.CleanupTone = match.Tone;
        sessionMode = match;
        sessionDeliveryPreferences = delivery;
    }

    private void DescribeSessionMode()
    {
        if (sessionMode == null || string.IsNullOrEmpty(StatusDetail.Text)) return;
        StatusDetail.Text = "Applied " + AppMode.StyleLabel(sessionMode.Style) + " style and " + AppMode.ToneLabel(sessionMode.Tone) + " tone for " + sessionMode.App + ". " + StatusDetail.Text;
    }
}
