using System.Runtime.InteropServices;
using System.Windows;
using Sona.App.Windows;
namespace Sona.App;

public partial class MainWindow
{
    internal Func<uint>? ClipboardSequenceForTests { get; set; }
    private uint ClipboardSequence() => arguments.Contains("--test-ui") ? ClipboardSequenceForTests?.Invoke() ?? 0 : GetClipboardSequenceNumber();
    private void RecoverClipboard(string text, uint expected, bool usedPaste, TargetCaptureStatus? rejected = null)
    {
        // Any direct-input attempt, including zero/partial/unconfirmed input,
        // ends automatic delivery. Preserve the clipboard and never retry it.
        if (usedPaste && insertion.LastUnicodeAttempted) return;
        if (!preferences.CopyOnInsertionFailure || string.IsNullOrWhiteSpace(text)) return;
        if (rejected == TargetCaptureStatus.PasswordField) { StatusDetail.Text += " Password fields stay protected; automatic recovery does not copy this draft."; return; }
        if (usedPaste && insertion.LastPasteConfirmed)
        {
            if (insertion.LastDraftOnClipboard && ClipboardSequence() == insertion.LastDraftClipboardSequence) fallbackCopied = true;
            StatusDetail.Text += " The complete draft was pasted. Check the original composer before retrying.";
            return;
        }
        var pasteRequested = usedPaste && insertion.LastPasteRequested;
        if (pasteRequested)
        {
            StatusDetail.Text += " Check the composer first. If the text is missing, open Sona and use Copy draft.";
            return;
        }
        var guidance = pasteRequested ? " Check the composer before pasting again." : " Press Ctrl+V in the intended field.";
        if (usedPaste && insertion.LastDraftOnClipboard && ClipboardSequence() == insertion.LastDraftClipboardSequence)
        {
            fallbackCopied = true;
            StatusDetail.Text += " The finished draft stays on the clipboard." + guidance;
            return;
        }
        // A deliberate newer owner copy wins over automatic recovery.
        if (ClipboardSequence() != expected) { StatusDetail.Text += " The clipboard changed; use Copy draft if you still need this text."; return; }
        try
        {
            if (arguments.Contains("--test-ui")) { if (ClipboardForTests == null) return; ClipboardForTests(text); }
            else
            {
                var clipboard = new OwnedClipboard(() => new System.Windows.Interop.WindowInteropHelper(this).Handle);
                using var previous = clipboard.Capture();
                if (previous.InitialSequence != expected) { StatusDetail.Text += " The clipboard changed; use Copy draft if you still need this text."; return; }
                var copied = clipboard.TrySetText(previous, text);
                if (copied.Status != ClipboardChangeStatus.Staged)
                {
                    for (var attempt = 0; attempt < 3 && copied.Status == ClipboardChangeStatus.FailedOwnedPartial; attempt++)
                        copied = clipboard.Restore(previous, copied.Sequence);
                    StatusDetail.Text += copied.Status == ClipboardChangeStatus.FailedOwnedPartial
                        ? " Windows could not restore all previous clipboard formats. Your draft remains in Sona."
                        : " Clipboard recovery could not safely replace the current copy. Use Copy draft if needed.";
                    return;
                }
            }
            fallbackCopied = true; StatusDetail.Text += " Finished text copied to the normal clipboard." + guidance;
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { StatusDetail.Text += " Clipboard is busy. Use Copy draft to retry."; }
    }
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
}
