using Sona.Core;

namespace Sona.App;

// One in-memory outcome, no transcript, labels, titles or clipboard contents.
internal sealed record DeliveryObservation(DateTimeOffset ObservedAt, string Outcome,
    bool StopAccepted, bool StoppedByEnter, bool SendRequested, string CaptureStatus,
    bool DestinationChanged, long CapturedFocusEpoch, long FinalFocusEpoch,
    bool PasteAttempted, bool PasteConfirmed, bool SendAccepted, bool ClipboardRecovery);

public partial class MainWindow
{
    private DeliveryObservation? lastDelivery;
    private void ObserveDelivery(DictationSession current, bool retainOnly, bool pasteAttempted, bool stoppedByEnter)
    {
        var requested = submitOnComplete || pendingSubmission.IsRequested;
        var outcome = current.State == SessionState.Cancelled ? "Cancelled"
            : current.State == SessionState.Failed ? "Failed"
            : retainOnly ? "LimitRetained"
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
        lastDelivery = new(DateTimeOffset.UtcNow, outcome, true, stoppedByEnter, requested,
            recordingCapture?.Status.ToString() ?? "Unknown", destinationContext.DestinationChanged,
            target?.FocusEpoch ?? -1, insertion.FocusEpoch, pasteAttempted,
            pasteAttempted && insertion.LastPasteConfirmed,
            pasteAttempted && insertion.LastSubmissionAccepted, fallbackCopied);
    }
}
