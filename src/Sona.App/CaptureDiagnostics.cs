using Sona.App.Windows;

namespace Sona.App;

internal sealed record CaptureObservation(
    string Attempt, DateTimeOffset ObservedAt, string Status, int? ControlTypeId,
    int? ProviderProcessId, int? WindowProcessId, long ForegroundWindow,
    long FocusEpochBefore, long FocusEpochAfter, bool Stable,
    bool? FieldInForegroundWindow, string Apartment, string? RuntimeIdentityHash, string InitialStatus, int CaptureAttempts, bool? ReadOnly)
{
    public static CaptureObservation From(string attempt, TargetCaptureResult result) => new(
        attempt, DateTimeOffset.UtcNow, result.Status.ToString(), result.ControlTypeId,
        result.ProviderProcessId, result.WindowProcessId, result.ForegroundWindow,
        result.FocusEpochBefore, result.FocusEpochAfter, result.Stable,
        result.FieldInForegroundWindow, Thread.CurrentThread.GetApartmentState().ToString(),
        result.RuntimeIdentityHash ?? (result.Target == null ? null : TargetCaptureResult.HashRuntimeId(result.Target.RuntimeId)), result.InitialStatus.ToString(), result.CaptureAttempts, result.ReadOnly);
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
        var result = await TargetCaptureReadiness.CaptureAsync(ReadDestinationCapture, mayContinue,
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
