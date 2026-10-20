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
        if (!preferences.CopyOnInsertionFailure || string.IsNullOrWhiteSpace(text)) return;
        if (rejected == TargetCaptureStatus.PasswordField) { StatusDetail.Text += " Password fields stay protected; automatic recovery does not copy this draft."; return; }
        if (usedPaste && insertion.LastDraftOnClipboard && ClipboardSequence() == insertion.LastDraftClipboardSequence) { fallbackCopied = true; StatusDetail.Text += " The finished draft stays on the clipboard for Ctrl+V. Check the composer before pasting again."; return; }
        // A deliberate newer owner copy wins over automatic recovery.
        if (ClipboardSequence() != expected) { StatusDetail.Text += " The clipboard changed; use Copy draft if you still need this text."; return; }
        try
        {
            if (arguments.Contains("--test-ui")) { if (ClipboardForTests == null) return; ClipboardForTests(text); }
            else Clipboard.SetText(text);
            fallbackCopied = true; StatusDetail.Text += " Finished text copied to the normal clipboard. Check the composer, then press Ctrl+V if needed.";
        }
        catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { StatusDetail.Text += " Clipboard is busy. Use Copy draft to retry."; }
    }
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
}
