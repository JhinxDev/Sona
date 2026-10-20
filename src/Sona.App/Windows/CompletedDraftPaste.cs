using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace Sona.App.Windows;

internal interface ICompletedDraftTransport
{
    object? SaveClipboard();
    uint SetClipboard(string text);
    uint ClipboardSequence { get; }
    void RestoreClipboard(object? previous);
    bool Paste();
    bool Enter();
    bool EnterHeld { get; }
    string? ReadText(DraftTarget target);
    Task Delay(int milliseconds);
}

public sealed partial class Insertion
{
    internal bool LastSubmissionAccepted { get; private set; }
    internal bool LastPasteConfirmed { get; private set; }
    internal bool LastDraftOnClipboard { get; private set; }
    internal uint LastDraftClipboardSequence { get; private set; }
    internal ICompletedDraftTransport? PasteTransportForTests { get; set; }
    public async Task<(bool Inserted, string Detail)> PasteAsync(DraftTarget? target, string text, bool submit, CancellationToken cancellation = default, bool keepOnFailure = false, uint? recoverySequence = null, Func<bool>? requestSubmission = null)
    {
        LastSubmissionAccepted = LastPasteConfirmed = LastDraftOnClipboard = false;
        if (target == null || string.IsNullOrWhiteSpace(text) || text.Length > 6000 || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            return (false, "No supported draft or focused composer. Your text stays in Sona.");
        var transport = PasteTransportForTests ?? new NativeDraftTransport();
        for (var i = 0; i < 40 && (ModifiersDown() || transport.EnterHeld); i++) await transport.Delay(20);
        cancellation.ThrowIfCancellationRequested();
        if (ModifiersDown() || transport.EnterHeld || !Same(target, Capture())) return (false, "Focus or held keys changed. Your draft stays in Sona.");
        object? previous = null; uint sequence = 0; bool changed = false, pasted = false, confirmed = false, allowRecovery = false;
        try
        {
            var initialSequence = transport.ClipboardSequence;
            allowRecovery = keepOnFailure && (!recoverySequence.HasValue || initialSequence == recoverySequence.Value);
            previous = transport.SaveClipboard();
            var before = transport.ReadText(target);
            cancellation.ThrowIfCancellationRequested();
            if (!Same(target, Capture()) || ModifiersDown() || transport.ClipboardSequence != initialSequence) return (false, "Focus, keys or clipboard changed before paste. Your draft stays in Sona.");
            sequence = transport.SetClipboard(text); changed = true;
            if (!Same(target, Capture()) || ModifiersDown() || transport.ClipboardSequence != sequence || cancellation.IsCancellationRequested)
                return (false, "Paste cancelled because focus, keys or clipboard changed. Your draft stays in Sona.");
            // Exactly one paste gesture, never per-character typing or retries.
            if (!transport.Paste()) return (false, "Windows did not accept the full paste shortcut. Check the destination before retrying.");
            pasted = true;
            string? confirmedText = null;
            for (var i = 0; i < 12; i++)
            {
                await transport.Delay(100);
                if (!Same(target, Capture()) || cancellation.IsCancellationRequested) break;
                var after = transport.ReadText(target);
                confirmed = before != null && after != null && ConfirmsCompleteInsertion(before, after, text);
                if (confirmed) { confirmedText = NormalizePasted(after!); break; }
            }
            LastPasteConfirmed = confirmed;
            submit |= requestSubmission?.Invoke() == true;
            if (!submit) return (true, confirmed ? "Complete draft pasted once. Nothing was submitted." : "Paste requested once. Check the result; nothing was submitted.");
            if (!confirmed || cancellation.IsCancellationRequested || !Same(target, Capture()) || ModifiersDown() || transport.EnterHeld || transport.ClipboardSequence != sequence)
                return (true, "Paste requested, but sending was withheld because the full draft, focus or key state could not be confirmed. Review the composer and press Enter yourself.");
            // Re-read immediately before Return. No old or partial draft is sent.
            var final = transport.ReadText(target);
            if (final == null || NormalizePasted(final) != confirmedText || !Same(target, Capture()) || ModifiersDown() || transport.EnterHeld || transport.ClipboardSequence != sequence || cancellation.IsCancellationRequested)
                return (true, "Composer verification changed. Nothing was submitted by Sona.");
            LastSubmissionAccepted = transport.Enter();
            return LastSubmissionAccepted ? (true, "Complete draft pasted, then Enter sent once to the same composer.") : (true, "Draft pasted. Windows did not confirm the complete Enter keystroke. Submission may have occurred; check the composer before retrying.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        { return (pasted, pasted ? "Paste was requested but verification failed. Review the composer; Sona did not retry." : "Clipboard is busy or unavailable. Your complete draft stays in Sona."); }
        finally
        {
            // A newer owner copy always wins. Never restore across a clipboard change.
            if (changed && transport.ClipboardSequence == sequence)
            {
                if (allowRecovery && !cancellation.IsCancellationRequested && (!pasted || !confirmed || submit && !LastSubmissionAccepted)) { LastDraftOnClipboard = true; LastDraftClipboardSequence = sequence; }
                else try { transport.RestoreClipboard(previous); }
                catch (Exception ex) when (ex is ExternalException or InvalidOperationException) { /* The complete draft remains on clipboard; never erase it on failed restoration. */ }
            }
        }
    }
    internal static bool ConfirmsCompleteInsertion(string before, string after, string text)
    {
        before = NormalizePasted(before); after = NormalizePasted(after); text = NormalizePasted(text);
        int start = 0, oldEnd = before.Length, newEnd = after.Length;
        while (start < oldEnd && start < newEnd && before[start] == after[start]) start++;
        while (oldEnd > start && newEnd > start && before[oldEnd - 1] == after[newEnd - 1]) { oldEnd--; newEnd--; }
        // Require a newly inserted full payload. Existing occurrences, partial
        // paste, unrelated edits and ambiguous selection overlap cannot authorize Enter.
        return text.Length > 0 && after[start..newEnd] == text;
    }
    private static string NormalizePasted(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private sealed class NativeDraftTransport : ICompletedDraftTransport
    {
        public object? SaveClipboard() => Clipboard.GetDataObject();
        public uint SetClipboard(string text) { Clipboard.SetText(text, TextDataFormat.UnicodeText); return ClipboardSequence; }
        public uint ClipboardSequence => GetClipboardSequenceNumber();
        public void RestoreClipboard(object? previous) { if (previous == null) Clipboard.Clear(); else Clipboard.SetDataObject(previous, true); }
        public bool EnterHeld => (GetAsyncKeyState(0x0D) & 0x8000) != 0;
        public Task Delay(int milliseconds) => Task.Delay(milliseconds);
        public bool Paste() => SendKeys([Key(0x11), Key(0x56), Key(0x56, true), Key(0x11, true)]);
        public bool Enter() => SendKeys([Key(0x0D), Key(0x0D, true)]);
        private static Input Key(ushort key, bool up = false) => new() { Type = 1, Data = new Union { Keyboard = new Keyboard { Key = key, Flags = up ? 2u : 0u } } };
        private static bool SendKeys(Input[] keys)
        {
            var sent = SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>());
            if (sent > 0 && sent != keys.Length)
            {
                var releases = keys.Where(k => k.Data.Keyboard.Flags == 2).ToArray();
                SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            }
            return sent == keys.Length;
        }
        public string? ReadText(DraftTarget target)
        {
            try
            {
                var element = AutomationElement.FocusedElement;
                if (element == null || element.Current.IsPassword || element.Current.ProcessId != target.ProcessId || !element.GetRuntimeId().SequenceEqual(target.RuntimeId)) return null;
                var text = element.TryGetCurrentPattern(ValuePattern.Pattern, out var value) ? ((ValuePattern)value).Current.Value
                    : element.TryGetCurrentPattern(TextPattern.Pattern, out var document) ? ((TextPattern)document).DocumentRange.GetText(24001) : null;
                return text?.Length <= 24000 ? text : null;
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { return null; }
        }
        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    }
}
