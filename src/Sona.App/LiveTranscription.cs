using System.Windows;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    internal Func<byte[], CancellationToken, Task<string>>? RecognitionForTests { get; set; }
    internal Task StopForTestsAsync() => arguments.Contains("--test-ui") ? StopAsync() : Task.CompletedTask;
    private async Task<string> RecognizeAsync(byte[] wav, CancellationToken token)
    {
        try { return arguments.Contains("--test-ui") && RecognitionForTests != null ? await RecognitionForTests(wav, token) : await transcriber.RunAsync(wav, preferences, token); }
        finally { Array.Clear(wav); }
    }
    internal Task LiveOperationForTests => liveOperation;
    private LiveDraft? liveDraft;
    private Task liveOperation = Task.CompletedTask;
    private CancellationTokenSource? livePolling;
    private bool liveVisible;
    private long previewAudioBytes = -1;
    private string? previewOriginal;
    private long liveRevision;
    private Guid? suggestionSession;
    private void ResetLiveReview()
    {
        liveDraft?.Cancel(); liveDraft = null; suggestionSession = null; previewAudioBytes = -1; previewOriginal = null;
        CleanedSuggestion.Text = ""; CleanedReview.Visibility = Visibility.Collapsed;
        DraftStage.Text = "Editable"; overlay.UpdateTranscript("", "");
    }

    private void BeginLiveDraft(Recorder capture, DictationSession current, CancellationToken token)
    {
        livePolling?.Cancel(); livePolling?.Dispose(); livePolling = CancellationTokenSource.CreateLinkedTokenSource(token);
        ResetLiveReview(); liveDraft = new(); liveRevision = 0; liveVisible = preferences.LiveTranscription;
        CleanedSuggestion.Text = ""; CleanedReview.Visibility = Visibility.Collapsed;
        if (liveVisible)
        {
            rendering = true; Draft.Clear(); Original.Clear(); rendering = false;
            DraftStage.Text = "Live raw · provisional";
            overlay.UpdateTranscript("Listening for speech…", "Provisional · updates locally every few seconds");
            liveOperation = RunLivePreviewAsync(capture, current, liveDraft, token, livePolling.Token);
        }
        else { DraftStage.Text = "Editable"; overlay.UpdateTranscript("", ""); liveOperation = Task.CompletedTask; }
    }

    private async Task RunLivePreviewAsync(Recorder capture, DictationSession current, LiveDraft review, CancellationToken token, CancellationToken polling)
    {
        try
        {
            while (ReferenceEquals(recorder, capture) && current.State == SessionState.Recording)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), polling);
                if (!ReferenceEquals(recorder, capture) || current.State != SessionState.Recording) break;
                if (capture.CapturedBytes == previewAudioBytes) continue;
                var wav = capture.Snapshot(out var snapshotBytes);
                if (wav == null) continue;
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TimeSpan.FromSeconds(12));
                    var raw = await RecognizeAsync(wav, deadline.Token);
                    token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(session, current) || !ReferenceEquals(liveDraft, review) || current.State != SessionState.Recording) break;
                    previewAudioBytes = snapshotBytes; previewOriginal = raw;
                    if (review.Preview(++liveRevision, raw))
                    {
                        rendering = true; Original.Text = raw; if (!review.ManuallyEdited) Draft.Text = review.Draft; rendering = false;
                        DraftStage.Text = review.ManuallyEdited ? "Your edits protected" : "Live raw · provisional";
                        overlay.UpdateTranscript(raw, "Provisional · final recognition may change these words");
                    }
                }
                finally { Array.Clear(wav); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || polling.IsCancellationRequested) { }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            if (ReferenceEquals(session, current) && current.State == SessionState.Recording)
            {
                DraftStage.Text = "Live preview paused · final transcript on stop";
                overlay.UpdateTranscript(review.Raw, "Preview unavailable · recording continues, final text on stop");
            }
        }
    }

    private void ShowFinalRaw(string original)
    {
        if (liveDraft == null) return;
        liveDraft.Preview(++liveRevision, original);
        rendering = true; Original.Text = original;
        if (!liveDraft.ManuallyEdited) Draft.Text = original;
        rendering = false;
        DraftStage.Text = liveDraft.ManuallyEdited ? "Your edits protected · final raw available" : "Final raw · preparing cleanup";
        if (liveVisible) overlay.UpdateTranscript(original, "Final raw · cleanup runs locally after recording");
    }

    private string CompleteLiveDraft(string original, string cleaned, bool polished)
    {
        if (liveDraft == null) return cleaned;
        liveDraft.Complete(original, cleaned);
        DraftStage.Text = liveDraft.ManuallyEdited ? "Your edits protected · review before insertion" : polished ? "Cleaned · review wording" : "Final draft";
        suggestionSession = session?.Id; CleanedSuggestion.Text = cleaned;
        CleanedReview.Visibility = liveDraft.ManuallyEdited && cleaned != liveDraft.Draft ? Visibility.Visible : Visibility.Collapsed;
        return liveDraft.Draft;
    }

    private void UseCleaned_Click(object sender, RoutedEventArgs e)
    {
        if (processing || recorder != null || session?.State is not (SessionState.Ready or SessionState.Failed) || session.Id != suggestionSession) return;
        if (session.State == SessionState.Failed)
        {
            var replacement = new DictationSession(); replacement.Begin(); replacement.Stop();
            if (!replacement.Complete(Original.Text, CleanedSuggestion.Text)) return;
            session = replacement; Render(replacement); AddHistory(replacement);
        }
        else Draft.Text = CleanedSuggestion.Text;
        CleanedReview.Visibility = Visibility.Collapsed;
        DraftStage.Text = "Cleaned suggestion applied · editable";
    }

    private void OpenDraftFromOverlay()
    {
        // Only an explicit click activates Sona. This intentionally ends automatic
        // insertion eligibility; recognition continues for review here.
        Pages.SelectedIndex = 0; Show(); WindowState = WindowState.Normal; Activate(); Draft.Focus();
    }

    private void ShowRetainedDraftNotice()
    {
        overlay.UpdateTranscript("", "");
        var pastedNotSent = lastDelivery?.Outcome == "PasteConfirmedSendWithheld";
        overlay.Update(pastedNotSent ? "Draft pasted" : "Draft ready", pastedNotSent ? "The complete draft was pasted; sending was withheld. Check the original composer before retrying." : recordingLimitReached ? "Five-minute limit reached. Open draft to review or copy; nothing was pasted or sent." : fallbackCopied ? "Draft is on the clipboard. Check the composer, then use normal Ctrl+V if needed." : "Open draft to review or copy. To insert later, focus the composer and press Ctrl + Alt + Shift + V.");
        overlay.SetReviewAction(true); overlay.ShowFor(target?.Window ?? 0);
        barPreview.Interval = TimeSpan.FromSeconds(12); barPreview.Start();
    }
}
