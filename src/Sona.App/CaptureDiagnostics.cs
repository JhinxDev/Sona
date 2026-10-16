using Sona.App.Windows;

namespace Sona.App;

internal sealed record CaptureObservation(
    string Attempt, DateTimeOffset ObservedAt, string Status, int? ControlTypeId,
    int? ProviderProcessId, int? WindowProcessId, long ForegroundWindow,
    long FocusEpochBefore, long FocusEpochAfter, bool Stable,
    bool? FieldInForegroundWindow, string Apartment, string? RuntimeIdentityHash)
{
    public static CaptureObservation From(string attempt, TargetCaptureResult result) => new(
        attempt, DateTimeOffset.UtcNow, result.Status.ToString(), result.ControlTypeId,
        result.ProviderProcessId, result.WindowProcessId, result.ForegroundWindow,
        result.FocusEpochBefore, result.FocusEpochAfter, result.Stable,
        result.FieldInForegroundWindow, Thread.CurrentThread.GetApartmentState().ToString(),
        result.RuntimeIdentityHash ?? (result.Target == null ? null : TargetCaptureResult.HashRuntimeId(result.Target.RuntimeId)));
}

public partial class MainWindow
{
    private CaptureObservation? lastDestinationCapture;
    private TargetCaptureResult CaptureDestination(string attempt)
    {
        var result = insertion.CaptureWithStatus();
        lastDestinationCapture = CaptureObservation.From(attempt, result);
        return result;
    }
}
