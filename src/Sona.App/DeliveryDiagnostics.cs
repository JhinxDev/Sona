using Sona.Core;

namespace Sona.App;

// One in-memory outcome, no transcript, labels, titles or clipboard contents.
internal sealed record DeliveryObservation(DateTimeOffset ObservedAt, string Outcome,
    bool StopAccepted, bool StoppedByEnter, bool SendRequested, string CaptureStatus,
    bool DestinationChanged, long CapturedFocusEpoch, long FinalFocusEpoch,
    bool PasteAttempted, bool PasteConfirmed, bool SendAccepted, bool ClipboardRecovery,
    bool PasteRequested, string PasteReadback, int? CaptureControlTypeId, int? CaptureAttempts,
    string? PasteCaptureRejection, int PasteCaptureGaps, bool EnterTargetReconfirmed, bool ClipboardRestorationFailed);

public partial class MainWindow
{
    private DeliveryObservation? lastDelivery;
    private DeliveryObservation? lastRetainedDelivery;
    private void ObserveDelivery(DictationSession current, bool retainOnly, bool pasteAttempted, bool stoppedByEnter)
    {
        var requested = submitOnComplete || pendingSubmission.IsRequested;
        var outcome = current.State == SessionState.Cancelled ? "Cancelled"
            : current.State == SessionState.Failed ? "Failed"
            : retainOnly ? recordingInterrupted ? "InterruptedRetained" : "LimitRetained"
            : liveDraft?.ManuallyEdited == true ? "ManualEditRetained"
            : target == null ? "NoCapturedField"
            : pasteAttempted && insertion.LastSubmissionAccepted ? "Sent"
            : pasteAttempted && insertion.LastPasteConfirmed && requested ? "PasteConfirmedSendWithheld"
            : pasteAttempted && insertion.LastPasteConfirmed ? "PasteConfirmed"
            : destinationContext.DestinationChanged ? "FocusChanged"
            : pasteAttempted && current.State == SessionState.Inserted ? "PasteUnconfirmed"
            : pasteAttempted ? "InsertionRefused"
            : current.State == SessionState.Inserted ? "Inserted"
            : "RetainedForReview";
        var capturedEpoch = recordingCapture?.Target?.FocusEpoch ?? -1;
        lastDelivery = new(DateTimeOffset.UtcNow, outcome, true, stoppedByEnter, requested,
            recordingCapture?.Status.ToString() ?? "Unknown", destinationContext.DestinationChanged,
            capturedEpoch, insertion.FocusEpoch, pasteAttempted,
            pasteAttempted && insertion.LastPasteConfirmed,
            pasteAttempted && insertion.LastSubmissionAccepted, fallbackCopied,
            pasteAttempted && insertion.LastPasteRequested, pasteAttempted ? insertion.LastPasteReadback.ToString() : "NotRequested",
            recordingCapture?.ControlTypeId, recordingCapture?.CaptureAttempts,
            pasteAttempted ? insertion.LastPasteCaptureRejection?.ToString() : null,
            pasteAttempted ? insertion.LastPasteCaptureGaps : 0,
            stoppedByEnter && capturedEpoch >= 0 && target is { } deliveryTarget && deliveryTarget.FocusEpoch != capturedEpoch,
            pasteAttempted && insertion.LastClipboardRestorationFailed);
        if ((preferences.AutoInsert || requested) && outcome is
            ("NoCapturedField" or "FocusChanged" or "InsertionRefused" or "PasteUnconfirmed" or "PasteConfirmedSendWithheld"))
            lastRetainedDelivery = lastDelivery;
    }
}
