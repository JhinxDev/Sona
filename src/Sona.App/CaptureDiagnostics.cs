using Sona.App.Windows;

namespace Sona.App;

internal sealed record CaptureObservation(
    string Attempt, DateTimeOffset ObservedAt, string Status, int? ControlTypeId,
    int? ProviderProcessId, int? WindowProcessId, long ForegroundWindow,
    long FocusEpochBefore, long FocusEpochAfter, bool Stable,
    bool? FieldInForegroundWindow, string Apartment, string? RuntimeIdentityHash, string InitialStatus, int CaptureAttempts, bool? ReadOnly, bool WindowStable)
{
    public static CaptureObservation From(string attempt, TargetCaptureResult result) => new(
        attempt, DateTimeOffset.UtcNow, result.Status.ToString(), result.ControlTypeId,
        result.ProviderProcessId, result.WindowProcessId, result.ForegroundWindow,
        result.FocusEpochBefore, result.FocusEpochAfter, result.Stable,
        result.FieldInForegroundWindow, Thread.CurrentThread.GetApartmentState().ToString(),
        result.RuntimeIdentityHash ?? (result.Target == null ? null : TargetCaptureResult.HashRuntimeId(result.Target.RuntimeId)), result.InitialStatus.ToString(), result.CaptureAttempts, result.ReadOnly, result.WindowStable);
}

public partial class MainWindow
{
    private CaptureObservation? lastDestinationCapture;
    internal Func<TargetCaptureResult>? TargetCaptureForTests { get; set; }
    internal Func<CancellationToken, Task>? CaptureDelayForTests { get; set; }
    private TargetCaptureResult ReadDestinationCapture() =>
        arguments.Contains("--test-ui") && TargetCaptureForTests != null ? TargetCaptureForTests() : insertion.CaptureWithStatus();
    private async Task<TargetCaptureResult> CaptureRecordingDestinationAsync(Func<bool> mayContinue, CancellationToken token)
    {
        var origin = recordingWindow;
        TargetCaptureResult CaptureInRecordingWindow()
        {
            var captured = ReadDestinationCapture();
            if (origin != null && (!insertion.IsRecordingWindowForeground(origin) ||
                captured.Target is { } field && (field.Window != origin.Window || field.OwnerProcessId != origin.OwnerProcessId) ||
                captured.ForegroundWindow != 0 && (captured.ForegroundWindow != origin.Window.ToInt64() || captured.WindowProcessId != origin.OwnerProcessId)))
                return captured with { Target = null, Status = TargetCaptureStatus.FocusChanged, Stable = false, WindowStable = false };
            return captured;
        }
        var result = await TargetCaptureReadiness.CaptureAsync(CaptureInRecordingWindow, mayContinue,
            arguments.Contains("--test-ui") ? CaptureDelayForTests : null, token);
        token.ThrowIfCancellationRequested();
        if (!mayContinue()) throw new OperationCanceledException(token);
        lastDestinationCapture = CaptureObservation.From("recording", result);
        return result;
    }
    private TargetCaptureResult CaptureDestination(string attempt)
    {
        var result = ReadDestinationCapture();
        lastDestinationCapture = CaptureObservation.From(attempt, result);
        return result;
    }
}
