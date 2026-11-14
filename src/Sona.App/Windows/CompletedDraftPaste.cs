using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using Sona.Core;

namespace Sona.App.Windows;

internal interface ICompletedDraftTransport
{
    object? SaveClipboard();
    uint SavedClipboardSequence(object? previous) => ClipboardSequence;
    uint SavedClipboardInitialSequence(object? previous) => SavedClipboardSequence(previous);
    ClipboardChange SetClipboard(object? previous, string text);
    uint ClipboardSequence { get; }
    ClipboardChange RestoreClipboard(object? previous, uint expectedSequence);
    bool Paste();
    uint Unicode(string text) => 0;
    bool Enter();
    bool EnterHeld { get; }
    string? ReadText(DraftTarget target);
    string? ReadDocumentText(DraftTarget target) => null;
    Task Delay(int milliseconds);
}

internal enum PasteReadback { NotRequested, GestureRejected, Cancelled, FocusChanged, TargetUnavailable, Unavailable, Unchanged, Mismatch, ValueConfirmed, DocumentConfirmed }

public sealed partial class Insertion
{
    internal bool LastSubmissionAccepted { get; private set; }
    internal bool LastPasteConfirmed { get; private set; }
    internal PasteReadback LastPasteReadback { get; private set; }
    internal TargetCaptureStatus? LastPasteCaptureRejection { get; private set; }
    internal int LastPasteCaptureGaps { get; private set; }
    internal bool LastPasteRequested => !LastUnicodeAttempted && LastPasteReadback != PasteReadback.NotRequested;
    internal string LastDeliveryRoute { get; private set; } = "None";
    internal bool LastUnicodeAttempted { get; private set; }
    internal bool LastUnicodeConfirmed { get; private set; }
    internal string? LastClipboardFailureCode { get; private set; }
    internal bool LastDraftOnClipboard { get; private set; }
    internal bool LastClipboardRestorationFailed { get; private set; }
    internal string LastClipboardStatus { get; private set; } = "NotUsed";
    internal uint LastDraftClipboardSequence { get; private set; }
    internal ICompletedDraftTransport? PasteTransportForTests { get; set; }
    public async Task<(bool Inserted, string Detail)> PasteAsync(DraftTarget? target, string text, bool submit, CancellationToken cancellation = default, bool keepOnFailure = false, uint? recoverySequence = null, Func<bool>? requestSubmission = null)
    {
        LastSubmissionAccepted = LastPasteConfirmed = LastDraftOnClipboard = LastClipboardRestorationFailed = false;
        LastPasteReadback = PasteReadback.NotRequested;
        LastClipboardStatus = "NotUsed";
        LastDeliveryRoute = "None";
        LastUnicodeAttempted = LastUnicodeConfirmed = false;
        LastClipboardFailureCode = null;
        LastPasteCaptureRejection = null;
        LastPasteCaptureGaps = 0;
        if (target == null || string.IsNullOrWhiteSpace(text) || text.Length > DraftLimits.MaximumCharacters || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            return (false, "No supported draft or focused composer. Your text stays in Sona.");
        var transport = PasteTransportForTests ?? new NativeDraftTransport();
        for (var i = 0; i < 40 && (ModifiersDown() || transport.EnterHeld); i++) await transport.Delay(20);
        cancellation.ThrowIfCancellationRequested();
        if (ModifiersDown() || transport.EnterHeld) return (false, "Release the held keys. Your draft stays in Sona.");
        var captured = CapturePasteTarget(target);
        for (var attempt = 0; attempt < 3 && CanWaitForPasteTarget(target, captured, afterPaste: false); attempt++)
        {
            LastPasteCaptureGaps++;
            await transport.Delay(40);
            cancellation.ThrowIfCancellationRequested();
            if (ModifiersDown() || transport.EnterHeld) break;
            captured = CapturePasteTarget(target);
        }
        if (ModifiersDown() || transport.EnterHeld || !Same(target, captured.Target))
            return (false, "Focus or held keys changed. Your draft stays in Sona.");
        object? previous = null; uint sequence = 0; bool changed = false, pasted = false, confirmed = false, allowRecovery = false, unicode = false;
        try
        {
            LastClipboardStatus = "Capturing";
            try { previous = transport.SaveClipboard(); }
            catch (Exception error) when (error is ExternalException or InvalidOperationException)
            {
                // Capture has not performed EmptyClipboard, SetClipboardData or
                // any paste gesture. Only this boundary may choose direct input.
                LastClipboardFailureCode = ClassifyClipboardCaptureFailure(error);
                LastClipboardStatus = "CaptureFailed";
                if (!CanSendUnicodeBatch(text)) return (false, "The clipboard is unavailable and this draft needs clipboard paste. Your complete draft stays in Sona.");
                unicode = true;
            }
            // Reading delayed clipboard formats may legitimately advance its
            // sequence while our capture owns the lock. Use that snapshot's
            // final sequence, then reject any change after it was captured.
            var initialSequence = unicode ? 0 : transport.SavedClipboardSequence(previous);
            if (!unicode) LastClipboardStatus = "Captured";
            allowRecovery = !unicode && keepOnFailure && (!recoverySequence.HasValue || transport.SavedClipboardInitialSequence(previous) == recoverySequence.Value);
            var before = transport.ReadText(target);
            var documentBefore = transport.ReadDocumentText(target);
            cancellation.ThrowIfCancellationRequested();
            if (!Same(target, CapturePasteTarget(target).Target) || ModifiersDown() || transport.EnterHeld || !unicode && transport.ClipboardSequence != initialSequence) return (false, "Focus, keys or clipboard changed before insertion. Your draft stays in Sona.");
            if (unicode)
            {
                // Capture/readback can pump messages. Recheck immediately before
                // the ONE Unicode batch; no navigation/Return/Tab packets exist.
                cancellation.ThrowIfCancellationRequested();
                if (!Same(target, CapturePasteTarget(target).Target) || ModifiersDown() || transport.EnterHeld || cancellation.IsCancellationRequested)
                    return (false, "Focus or keys changed before insertion. Your draft stays in Sona.");
                LastUnicodeAttempted = true;
                LastDeliveryRoute = "UnicodeBatch";
                var accepted = transport.Unicode(text);
                if (accepted == 0) return (false, "Windows blocked text insertion. Your draft stays in Sona and the clipboard was left untouched.");
                pasted = true;
                if (accepted != text.Length * 2)
                    return (true, "Windows accepted only part of the text input. Check the composer; Sona did not retry or send Enter. Your clipboard was left untouched.");
            }
            else
            {
                var staged = transport.SetClipboard(previous, text);
                LastClipboardStatus = staged.Status == ClipboardChangeStatus.Staged ? "Staged" : "Staging" + staged.Status;
                sequence = staged.Sequence;
                changed = staged.Status is ClipboardChangeStatus.Staged or ClipboardChangeStatus.FailedOwnedPartial;
                if (staged.Status != ClipboardChangeStatus.Staged)
                {
                    allowRecovery = false;
                    return (false, "Clipboard could not be safely prepared. Your complete draft stays in Sona.");
                }
                if (!Same(target, CapturePasteTarget(target).Target) || ModifiersDown() || transport.ClipboardSequence != sequence || cancellation.IsCancellationRequested)
                    return (false, "Paste cancelled because focus, keys or clipboard changed. Your draft stays in Sona.");
                // Exactly one paste gesture, never per-character typing or retries.
                LastDeliveryRoute = "ClipboardPaste";
                LastPasteReadback = PasteReadback.GestureRejected;
                if (!transport.Paste()) return (false, "Windows did not accept the full paste shortcut. Check the destination before retrying.");
                pasted = true;
            }
            string? confirmedText = null;
            var readbackInterrupted = false;
            LastPasteReadback = PasteReadback.Unavailable;
            for (var i = 0; i < 12; i++)
            {
                await transport.Delay(100);
                if (cancellation.IsCancellationRequested)
                {
                    LastPasteReadback = PasteReadback.Cancelled;
                    break;
                }
                captured = CapturePasteTarget(target);
                if (!Same(target, captured.Target))
                {
                    if (!CanWaitForPasteTarget(target, captured, afterPaste: true))
                    {
                        LastPasteReadback = PasteReadback.FocusChanged;
                        break;
                    }
                    // Wait for proof from the original field within the existing
                    // budget. A gap can confirm paste, but cannot authorize Enter.
                    LastPasteCaptureGaps++;
                    readbackInterrupted = true;
                    LastPasteReadback = PasteReadback.TargetUnavailable;
                    continue;
                }
                var after = before == null ? null : transport.ReadText(target);
                if (before != null && after != null && ConfirmsCompleteInsertion(before, after, text))
                {
                    confirmedText = NormalizePasted(after);
                    LastPasteReadback = PasteReadback.ValueConfirmed;
                    break;
                }
                // Some rich editors expose a stale value alongside a live document.
                // Require independent proof from the same representation before and
                // after paste. A changed, contradictory value still vetoes Enter.
                if (TextChanged(before, after))
                {
                    LastPasteReadback = PasteReadback.Mismatch;
                    continue;
                }
                var documentAfter = documentBefore == null ? null : transport.ReadDocumentText(target);
                if (documentBefore != null && documentAfter != null && ConfirmsCompleteInsertion(documentBefore, documentAfter, text))
                {
                    confirmedText = NormalizePasted(documentAfter);
                    LastPasteReadback = PasteReadback.DocumentConfirmed;
                    break;
                }
                LastPasteReadback = TextChanged(documentBefore, documentAfter) ? PasteReadback.Mismatch
                    : after != null || documentAfter != null ? PasteReadback.Unchanged : PasteReadback.Unavailable;
            }
            confirmed = confirmedText != null;
            LastPasteConfirmed = !unicode && confirmed;
            LastUnicodeConfirmed = unicode && confirmed;
            submit |= requestSubmission?.Invoke() == true;
            if (!submit) return (true, confirmed ? "Complete draft inserted once. Nothing was submitted." : "Text insertion requested once. Check the result; nothing was submitted.");
            if (confirmed && readbackInterrupted)
                return (true, "Draft inserted. Check the composer before pressing Enter because the focused field was temporarily unavailable.");
            if (!confirmed || cancellation.IsCancellationRequested || !Same(target, CapturePasteTarget(target).Target) || ModifiersDown() || transport.EnterHeld || !unicode && transport.ClipboardSequence != sequence)
                return (true, "Text insertion was requested, but sending was withheld because the full draft, focus or key state could not be confirmed. Review the composer and press Enter yourself.");
            // Re-read immediately before Return. No old or partial draft is sent.
            var documentConfirmed = LastPasteReadback == PasteReadback.DocumentConfirmed;
            var final = documentConfirmed ? transport.ReadDocumentText(target) : transport.ReadText(target);
            var finalValue = documentConfirmed && before != null ? transport.ReadText(target) : null;
            var conflictingValue = TextChanged(before, finalValue) && !ConfirmsCompleteInsertion(before!, finalValue!, text);
            if (final == null || NormalizePasted(final) != confirmedText || conflictingValue || !Same(target, CapturePasteTarget(target).Target) || ModifiersDown() || transport.EnterHeld || !unicode && transport.ClipboardSequence != sequence || cancellation.IsCancellationRequested)
                return (true, "Composer verification changed. Nothing was submitted by Sona.");
            LastSubmissionAccepted = transport.Enter();
            return LastSubmissionAccepted ? (true, "Complete draft inserted, then Enter sent once to the same composer.") : (true, "Draft inserted. Windows did not confirm the complete Enter keystroke. Submission may have occurred; check the composer before retrying.");
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            if (LastClipboardStatus == "Capturing") LastClipboardStatus = "CaptureFailed";
            return (pasted, pasted ? "Text insertion was requested but verification failed. Review the composer; Sona did not retry." : "Text insertion could not be safely prepared. Your complete draft stays in Sona.");
        }
        finally
        {
            // A newer owner copy always wins. Never restore across a clipboard change.
            if (changed && transport.ClipboardSequence == sequence)
            {
                // Recovery is for an insertion that could not be attempted.
                // A missing send acknowledgement is not a failed paste. Even
                // uncertain pastes restore the prior clipboard and ask for review.
                if (allowRecovery && !cancellation.IsCancellationRequested && !LastPasteRequested) { LastDraftOnClipboard = true; LastDraftClipboardSequence = sequence; LastClipboardStatus = "RecoveryRetained"; }
                else for (var attempt = 0; attempt < 3; attempt++)
                {
                    if (transport.ClipboardSequence != sequence) { LastClipboardRestorationFailed = false; break; }
                    try
                    {
                        var restored = transport.RestoreClipboard(previous, sequence);
                        if (restored.Status is ClipboardChangeStatus.Restored or ClipboardChangeStatus.OwnershipChanged)
                        { LastClipboardRestorationFailed = false; LastClipboardStatus = restored.Status.ToString(); break; }
                        if (restored.Status == ClipboardChangeStatus.FailedOwnedPartial) sequence = restored.Sequence;
                        LastClipboardRestorationFailed = true;
                        LastClipboardStatus = "RestorationFailed";
                        if (attempt < 2) await transport.Delay(40);
                    }
                    catch (Exception ex) when (ex is ExternalException or InvalidOperationException)
                    {
                        LastClipboardRestorationFailed = true;
                        LastClipboardStatus = "RestorationFailed";
                        if (attempt < 2) await transport.Delay(40);
                    }
                }
            }
            (previous as IDisposable)?.Dispose();
        }
    }
    private static string ClassifyClipboardCaptureFailure(Exception error) => error switch
    {
        InvalidOperationException => "OwnerOrThreadUnavailable",
        ExternalException { Message: "Clipboard is unavailable." } => "OwnerOrLockUnavailable",
        ExternalException { Message: "Clipboard contains a format that cannot be safely preserved." } => "UnsupportedFormat",
        ExternalException { Message: "Clipboard has no materializable content formats." } => "NoPayloadFormats",
        ExternalException { Message: "Clipboard formats changed during capture." } => "FormatsChanged",
        ExternalException { Message: "Clipboard has too many formats." } => "FormatLimit",
        ExternalException { Message: "Clipboard enumeration failed." } => "EnumerationFailed",
        ExternalException { Message: "Clipboard format is too large or unsupported." } => "FormatSizeOrHandle",
        ExternalException { Message: "Clipboard bitmap is too large." } => "BitmapSizeLimit",
        ExternalException { Message: "Clipboard bitmap is unsupported." } => "BitmapUnsupported",
        ExternalException { Message: "Clipboard bitmap could not be preserved." } => "BitmapCopyFailed",
        ExternalException { Message: "Clipboard format is unavailable." or "Clipboard data is unavailable." } => "FormatReadFailed",
        _ => "NativeCaptureFailure"
    };
    private TargetCaptureResult CapturePasteTarget(DraftTarget target)
    {
        var captured = CaptureWithStatus();
        if (!Same(target, captured.Target)) LastPasteCaptureRejection = captured.Status;
        return captured;
    }

    private bool CanWaitForPasteTarget(DraftTarget target, TargetCaptureResult captured, bool afterPaste)
    {
        if (captured.Target != null || target.FocusEpoch != FocusEpoch || !IsForegroundWindow(target))
            return false;
        if (captured.Status is TargetCaptureStatus.NoFocusedControl or TargetCaptureStatus.ProviderUnavailable)
            return true;
        // A sampled pane or a provider race can only qualify a later readback,
        // never the initial paste or an automatic send after that observation.
        return afterPaste && captured.ForegroundWindow == target.Window.ToInt64() &&
            captured.WindowProcessId == target.OwnerProcessId &&
            (captured.Status == TargetCaptureStatus.FocusChanged ||
             captured.Status == TargetCaptureStatus.UnsupportedControl && captured.ControlTypeId == ControlType.Pane.Id);
    }

    internal static bool ConfirmsCompleteInsertion(string before, string after, string text)
    {
        before = NormalizePasted(before); after = NormalizePasted(after); text = NormalizePasted(text);
        int start = 0, oldEnd = before.Length, newEnd = after.Length;
        while (start < oldEnd && start < newEnd && before[start] == after[start]) start++;
        // Maximal prefix/suffix trimming can consume part of a valid payload
        // when it overlaps its neighbors (a|b + ba becomes abab). For pure
        // insertion, prove exact length growth and preservation of all old text.
        if (text.Length > 0 && after.Length == before.Length + text.Length)
        {
            var suffix = 0;
            while (suffix < before.Length && before[^(suffix + 1)] == after[^(suffix + 1)]) suffix++;
            var first = before.Length - suffix;
            if (first <= start)
            {
                var index = after.IndexOf(text, first, start - first + text.Length, StringComparison.Ordinal);
                if (index >= 0 && after.AsSpan(0, index).SequenceEqual(before.AsSpan(0, index)) &&
                    after.AsSpan(index + text.Length).SequenceEqual(before.AsSpan(index))) return true;
            }
            return false;
        }
        while (oldEnd > start && newEnd > start && before[oldEnd - 1] == after[newEnd - 1]) { oldEnd--; newEnd--; }
        // Require a newly inserted full payload. Existing occurrences, partial
        // paste, unrelated edits and ambiguous selection overlap cannot authorize Enter.
        return text.Length > 0 && after[start..newEnd] == text;
    }
    private static bool TextChanged(string? before, string? after) =>
        before != null && after != null && NormalizePasted(before) != NormalizePasted(after);
    private static string NormalizePasted(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private sealed class NativeDraftTransport : ICompletedDraftTransport
    {
        private readonly OwnedClipboard clipboard = new(() => Application.Current?.MainWindow is { } window
            ? new System.Windows.Interop.WindowInteropHelper(window).Handle : 0);
        public object SaveClipboard() => clipboard.Capture();
        public uint SavedClipboardSequence(object? previous) => ((OwnedClipboard.Snapshot)previous!).Sequence;
        public uint SavedClipboardInitialSequence(object? previous) => ((OwnedClipboard.Snapshot)previous!).InitialSequence;
        public ClipboardChange SetClipboard(object? previous, string text) => clipboard.TrySetText((OwnedClipboard.Snapshot)previous!, text);
        public uint ClipboardSequence => clipboard.Sequence;
        public ClipboardChange RestoreClipboard(object? previous, uint expectedSequence) => clipboard.Restore((OwnedClipboard.Snapshot)previous!, expectedSequence);
        public bool EnterHeld => (GetAsyncKeyState(0x0D) & 0x8000) != 0;
        public Task Delay(int milliseconds) => Task.Delay(milliseconds);
        public bool Paste() => SendKeys([Key(0x11), Key(0x56), Key(0x56, true), Key(0x11, true)]);
        public uint Unicode(string text) => SendUnicodeBatch(text);
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
        public string? ReadText(DraftTarget target) => Read(target, ValuePattern.Pattern, pattern => ((ValuePattern)pattern).Current.Value);
        public string? ReadDocumentText(DraftTarget target) => Read(target, TextPattern.Pattern, pattern => ((TextPattern)pattern).DocumentRange.GetText(DraftLimits.MaximumCharacters * 2 + 1));
        private static string? Read(DraftTarget target, AutomationPattern pattern, Func<object, string?> getText)
        {
            try
            {
                var element = AutomationElement.FocusedElement;
                if (element == null || element.Current.IsPassword || element.Current.ProcessId != target.ProcessId || !element.GetRuntimeId().SequenceEqual(target.RuntimeId)) return null;
                var text = element.TryGetCurrentPattern(pattern, out var provider) ? getText(provider) : null;
                return text?.Length <= DraftLimits.MaximumCharacters * 2 ? text : null;
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException) { return null; }
        }
    }
}
