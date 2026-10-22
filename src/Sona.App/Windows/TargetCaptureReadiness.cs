namespace Sona.App.Windows;

// Only the initial intentional recording gesture may wait for a direct UIA field.
// No descendant guessing, destination rebasing or output retries happen here.
internal static class TargetCaptureReadiness
{
    internal static async Task<TargetCaptureResult> CaptureAsync(
        Func<TargetCaptureResult> capture, Func<bool> mayContinue,
        Func<CancellationToken, Task>? wait, CancellationToken cancellation)
    {
        void CheckAdmission()
        {
            cancellation.ThrowIfCancellationRequested();
            if (!mayContinue()) throw new OperationCanceledException(cancellation);
        }
        CheckAdmission();
        var first = capture();
        CheckAdmission();
        if (!EligiblePane(first)) return first;
        var current = first;
        for (var attempt = 2; attempt <= 4; attempt++)
        {
            CheckAdmission();
            if (wait == null) await Task.Delay(40, cancellation);
            else await wait(cancellation);
            CheckAdmission();
            current = capture();
            CheckAdmission();
            current = current with { InitialStatus = first.Status, CaptureAttempts = attempt };
            if (!SameScope(first, current)) return RejectChanged(current);
            if (current.Target != null && current.Status == TargetCaptureStatus.Ready)
                return CoherentTarget(current) ? current : RejectChanged(current);
            if (!EligiblePane(current)) return current;
            if (current.ProviderProcessId != first.ProviderProcessId || current.RuntimeIdentityHash != first.RuntimeIdentityHash)
                return RejectChanged(current);
        }
        return current;
    }

    private static bool EligiblePane(TargetCaptureResult result) =>
        result.Target == null && result.Status == TargetCaptureStatus.UnsupportedControl &&
        result.ControlTypeId == 50033 && result.ReadOnly != true && result.Stable &&
        result.ForegroundWindow != 0 && result.WindowProcessId is > 0 && result.ProviderProcessId is > 0 &&
        result.FocusEpochBefore >= 0 && result.FocusEpochBefore == result.FocusEpochAfter &&
        result.FieldInForegroundWindow == true && !string.IsNullOrEmpty(result.RuntimeIdentityHash);

    private static bool SameScope(TargetCaptureResult first, TargetCaptureResult current) =>
        current.Stable && current.FieldInForegroundWindow == true &&
        current.ForegroundWindow == first.ForegroundWindow && current.WindowProcessId == first.WindowProcessId &&
        current.FocusEpochBefore == first.FocusEpochBefore && current.FocusEpochAfter == first.FocusEpochBefore;

    private static bool CoherentTarget(TargetCaptureResult result) => result.Target is { } target &&
        target.Window.ToInt64() == result.ForegroundWindow && target.OwnerProcessId == result.WindowProcessId &&
        target.ProcessId == result.ProviderProcessId && target.FocusEpoch == result.FocusEpochAfter &&
        result.RuntimeIdentityHash == TargetCaptureResult.HashRuntimeId(target.RuntimeId) && result.ReadOnly != true;

    private static TargetCaptureResult RejectChanged(TargetCaptureResult result) =>
        result with { Target = null, Status = TargetCaptureStatus.FocusChanged, Stable = false };
}