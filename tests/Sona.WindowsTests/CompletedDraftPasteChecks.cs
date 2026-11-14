using Sona.App.Windows;
internal static class CompletedDraftPasteChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0; void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
        Check(!Insertion.ConfirmsCompleteInsertion("Complete draft", "Complete draftCom", "Complete draft"), "Preexisting full text plus partial paste cannot authorize send");
        Check(Insertion.ConfirmsCompleteInsertion("Prior: ", "Prior: Complete draft", "Complete draft"), "A newly appended complete payload verifies exactly");
        Check(!Insertion.ConfirmsCompleteInsertion("Prior: ", "Prior: Complete draft plus", "Complete draft"), "Extra unrequested changes revoke confirmation");
        Check(Insertion.ConfirmsCompleteInsertion("ab", "abab", "ba"), "A complete middle insertion with overlapping neighbors verifies without truncating the proof");
        Check(!Insertion.ConfirmsCompleteInsertion("ab", "abzz", "ba"), "Equal length growth without the complete inserted payload still refuses");
        Check(Insertion.ConfirmsCompleteInsertion("aaa", "aaaaa", "aa"), "Repeated adjacent text can still prove a complete pure insertion");
        static IEnumerable<string> Words(int length)
        {
            for(var value=0;value<(1<<length);value++)yield return new string(Enumerable.Range(0,length).Select(bit=>(value&(1<<bit))==0?'a':'b').ToArray());
        }
        var exact=true;
        for(var oldLength=0;oldLength<=4;oldLength++)foreach(var before in Words(oldLength))
        for(var size=1;size<=3;size++)foreach(var payload in Words(size))foreach(var after in Words(oldLength+size))
        {
            var expected=Enumerable.Range(0,before.Length+1).Any(index=>after==before.Insert(index,payload));
            if(Insertion.ConfirmsCompleteInsertion(before,after,payload)!=expected)exact=false;
        }
        Check(exact,"Complete-paste proof agrees with every possible exact pure insertion across 28,644 overlap/adversarial cases");
        var target = new DraftTarget(1, 10, [1, 2], 0); DraftTarget? current = target; bool modifiers = false;
        using var insertion = new Insertion(() => current, () => modifiers, _ => throw new Exception("Unicode fallback must not run"));
        var transport = new FakePaste(); insertion.PasteTransportForTests = transport;
        var longDraft = string.Join("\n", Enumerable.Repeat("Bonjour Codex, keep every word of this complete draft.", 180));
        var result = await insertion.PasteAsync(target, longDraft, true);
        Check(result.Inserted && transport.Pastes == 1 && transport.Enters == 1 && transport.Restores == 1 && transport.Payload == longDraft, "Long multiline draft pastes exactly once, confirms full text, sends one Enter and restores clipboard");
        transport = new() { UnsupportedReadback = true }; insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", true);
        Check(result.Inserted && transport.Pastes == 1 && transport.Enters == 0, "No accessibility readback means paste only, never send");
        transport = new() { StaleValue = true, DocumentBefore = "before", DocumentAfter = "beforehello" };
        insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", true);
        Check(result.Inserted && transport.Pastes == 1 && transport.Enters == 1 && insertion.LastPasteConfirmed,
            "A stale ValuePattern can use exact before, after and final proof from the same TextPattern");
        transport = new() { UnsupportedReadback = true, DocumentAfter = "beforehello" };
        insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Pastes == 1 && transport.Enters == 0, "Never mix the value baseline with a document read after paste");
        transport = new() { StaleValue = true, DocumentBefore = "before", DocumentAfter = "beforehello", DocumentFinal = "beforehell" };
        insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Pastes == 1 && transport.Enters == 0, "Changed document text at the final guard withholds Enter");
        transport = new() { Partial = true, DocumentBefore = "before", DocumentAfter = "beforehello" };
        insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Pastes == 1 && transport.Enters == 0, "A changed value that disagrees with the document cannot authorize Enter");
        transport = new() { Partial = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete message", true);
        Check(transport.Enters == 0 && transport.Pastes == 1, "Partial destination content cannot submit or trigger a second paste");
        insertion.ForegroundForTests = () => (target.Window, target.OwnerProcessId);
        current = null;
        transport = new() { OnDelay = _ => current = target };
        insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", false);
        Check(result.Inserted && transport.Pastes == 1 && insertion.LastPasteConfirmed, "A temporary pre-paste provider gap can recheck the exact original field");
        foreach (var requestEnter in new[] { false, true })
        {
            var checksAfterPaste = 0;
            transport = new()
            {
                AfterPaste = () => current = null,
                OnDelay = _ => { if (++checksAfterPaste == 2) current = target; }
            };
            insertion.PasteTransportForTests = transport;
            result = await insertion.PasteAsync(target, "hello", requestEnter);
            Check(result.Inserted && transport.Pastes == 1 && insertion.LastPasteConfirmed && transport.Enters == 0,
                "A post-paste provider gap can confirm the same field later without retrying paste or sending Enter");
            current = target;
        }
        transport = new()
        {
            AfterPaste = () => current = target with { RuntimeId = [3] },
            OnDelay = _ => { }
        };
        insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(!insertion.LastPasteConfirmed && transport.Enters == 0, "A different editable field remains terminal even within the same foreground window");
        current = target;
        insertion.ForegroundForTests = () => ((nint)2, target.OwnerProcessId);
        var foregroundPolls = 0;
        transport = new()
        {
            AfterPaste = () => current = null,
            OnDelay = _ => { if (++foregroundPolls == 2) current = target; }
        };
        insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(!insertion.LastPasteConfirmed && transport.Enters == 0 && foregroundPolls == 1,
            "An unavailable field in a changed foreground window ends verification immediately");
        insertion.ForegroundForTests = null;
        current = target;
        transport = new() { AfterPaste = () => current = target with { FocusEpoch = 1 } }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Enters == 0, "Focus change after paste prevents Enter"); current = target;
        transport = new(); transport.AfterPaste = () => transport.Sequence++; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Restores == 0 && transport.Enters == 0, "Owner copying new content wins and revokes pending send");
        using var cancel = new CancellationTokenSource(); transport = new() { AfterPaste = cancel.Cancel }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true, cancel.Token);
        Check(transport.Enters == 0 && transport.Restores == 1, "Cancellation after paste does not send and restores owned clipboard");
        transport = new(); insertion.PasteTransportForTests = transport; current = target with { RuntimeId = [4] };
        Check(!(await insertion.PasteAsync(target, "hello", true)).Inserted && transport.Saves == 0, "Wrong field refuses even a clipboard write"); current = target;
        transport = new(); insertion.PasteTransportForTests = transport;
        Check(!(await insertion.PasteAsync(target, new string('x', Sona.Core.DraftLimits.MaximumCharacters + 1), true)).Inserted && transport.Saves == 0, "Oversize paste has no side effects");
        transport = new() { AfterPaste = () => modifiers = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Enters == 0, "Modifier change before final Enter prevents submission"); modifiers = false;
        transport = new(); insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", false);
        Check(transport.Pastes == 1 && transport.Enters == 0, "Ordinary completed draft never submits");
        transport = new(); transport.BeforePasteRead = () => transport.Sequence++; insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", true);
        Check(!result.Inserted && transport.Payload == "" && transport.Restores == 0, "Copy during destination readback is preserved before any Sona clipboard write");
        transport=new(){UnsupportedReadback=true};insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"finished draft",true,keepOnFailure:true);
        Check(transport.Enters==0 && transport.Restores==1 && !insertion.LastDraftOnClipboard,"Unconfirmed paste restores prior clipboard instead of replacing it with a speculative recovery copy");
        transport = new(); transport.AfterPaste = () => modifiers = true; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "pasted but not sent", true, keepOnFailure: true);
        Check(insertion.LastPasteConfirmed && transport.Enters == 0 && transport.Restores == 1 && !insertion.LastDraftOnClipboard,
            "Confirmed paste restores prior clipboard even when sending is withheld and recovery is enabled"); modifiers = false;
        transport=new();transport.AfterPaste=()=>transport.Sequence++;insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"finished draft",true,keepOnFailure:true);
        Check(transport.Restores==0 && !insertion.LastDraftOnClipboard,"New owner clipboard content wins over opted-in recovery");
        using var recoverCancel=new CancellationTokenSource();transport=new(){AfterPaste=recoverCancel.Cancel};insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"cancelled draft",true,recoverCancel.Token,keepOnFailure:true);
        Check(transport.Restores==1 && !insertion.LastDraftOnClipboard,"Cancellation restores prior clipboard even when recovery is enabled");
        transport=new();insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"complete draft",true,keepOnFailure:true);
        Check(transport.Enters==1 && transport.Restores==1 && !insertion.LastDraftOnClipboard && insertion.LastPasteConfirmed,"Verified successful send restores the prior clipboard");
        transport = new() { RestoreErrors = 2 }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete draft", false, keepOnFailure: true);
        Check(transport.Restores == 1 && transport.RestoreAttempts == 3 && !insertion.LastClipboardRestorationFailed,
            "A briefly busy clipboard is restored within the bounded retry budget");
        transport = new() { RestoreErrors = 3 }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete draft", false, keepOnFailure: true);
        Check(transport.Restores == 0 && transport.RestoreAttempts == 3 && insertion.LastClipboardRestorationFailed,
            "Persistent clipboard restoration failure is reported instead of silently claimed successful");
        transport = new() { NewCopyAtRestore = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete draft", false);
        Check(transport.Restores == 0 && transport.RestoreAttempts == 1 && !insertion.LastClipboardRestorationFailed,
            "A copy made inside the restoration boundary is preserved without a retry");
        transport = new() { StagePartial = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete draft", true, keepOnFailure: true);
        Check(transport.Pastes == 0 && transport.Enters == 0 && transport.Restores == 1 && !insertion.LastDraftOnClipboard,
            "Partial staging is restored and never mistaken for a ready recovery draft");
        transport = new() { RestorePartial = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete draft", false);
        Check(transport.Restores == 1 && transport.RestoreAttempts == 2 && !insertion.LastClipboardRestorationFailed,
            "Partial restoration retries only with its new owned sequence");
        transport=new(){Sequence=2,UnsupportedReadback=true};insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"finished draft",true,keepOnFailure:true,recoverySequence:1);
        Check(transport.Enters==0 && transport.Restores==1 && !insertion.LastDraftOnClipboard,"A newer copy before paste is restored instead of retained over by recovery");
        transport=new();insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"finished draft",false,requestSubmission:()=>true);
        Check(transport.Enters==1 && transport.Pastes==1,"Enter arriving during complete-paste verification sends once after proof");
        transport=new(){UnsupportedReadback=true};insertion.PasteTransportForTests=transport;
        await insertion.PasteAsync(target,"finished draft",false,requestSubmission:()=>true);
        Check(transport.Enters==0,"Late queued Enter still cannot send without full draft readback");
        transport = new() { MaterializeOnCapture = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "delayed rendered copy", false);
        Check(transport.Pastes == 1 && transport.Restores == 1 && insertion.LastClipboardStatus == "Restored",
            "Materializing delayed clipboard data uses the captured sequence, then restores the original");
        transport = new() { RejectCapture = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "retained draft", true);
        Check(transport.Pastes == 0 && transport.Enters == 0 && insertion.LastClipboardStatus == "CaptureFailed",
            "Clipboard capture refusal is visible in content-free delivery diagnostics");
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue }; insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "Complete fallback draft", true, keepOnFailure: true);
        Check(result.Inserted && transport.UnicodeCalls == 1 && transport.Pastes == 0 && transport.Sets == 0 && transport.RestoreAttempts == 0 && transport.Enters == 1,
            "Capture failure sends one complete Unicode batch and one verified Enter without clipboard mutation");
        Check(insertion.LastDeliveryRoute == "UnicodeBatch" && insertion.LastUnicodeAttempted && insertion.LastUnicodeConfirmed && !insertion.LastPasteRequested && !insertion.LastPasteConfirmed && !insertion.LastDraftOnClipboard && insertion.LastClipboardFailureCode == "UnsupportedFormat",
            "Unicode route, confirmation and bounded capture reason do not claim clipboard paste");
        foreach (uint accepted in new uint[] { 0, 1, 6 })
        {
            transport = new() { RejectCapture = true, UnicodeAccepted = accepted }; insertion.PasteTransportForTests = transport;
            result = await insertion.PasteAsync(target, "Complete fallback draft", true, keepOnFailure: true);
            Check(transport.UnicodeCalls == 1 && transport.Enters == 0 && transport.Pastes == 0 && transport.Sets == 0 && transport.RestoreAttempts == 0 && insertion.LastUnicodeAttempted && !insertion.LastUnicodeConfirmed && !insertion.LastDraftOnClipboard && result.Inserted == (accepted != 0),
                "Zero or partial Unicode acceptance permanently withholds Enter, retry and recovery");
        }
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue, UnsupportedReadback = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Unconfirmed fallback draft", true, keepOnFailure: true);
        Check(transport.UnicodeCalls == 1 && transport.Enters == 0 && transport.Sets == 0 && !insertion.LastUnicodeConfirmed && !insertion.LastPasteRequested,
            "Missing Unicode readback never sends or copies a recovery draft");
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue, OnCaptureFailure = () => current = target with { RuntimeId = [77] } }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Wrong target fallback", true);
        Check(transport.UnicodeCalls == 0 && transport.Enters == 0, "Capture exception reentrancy cannot send Unicode to a different field"); current = target;
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue, OnCaptureFailure = () => modifiers = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Held modifier fallback", true);
        Check(transport.UnicodeCalls == 0 && transport.Enters == 0, "Changed modifiers after capture refusal prevent Unicode output"); modifiers = false;
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue }; transport.OnCaptureFailure = () => transport.HeldEnter = true; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Held Enter fallback", true);
        Check(transport.UnicodeCalls == 0 && transport.Enters == 0, "Held Enter after capture refusal prevents Unicode output");
        using (var fallbackCancel = new CancellationTokenSource())
        {
            transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue, OnCaptureFailure = fallbackCancel.Cancel }; insertion.PasteTransportForTests = transport;
            bool cancelled = false; try { await insertion.PasteAsync(target, "Cancelled fallback", true, fallbackCancel.Token); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && transport.UnicodeCalls == 0 && transport.Enters == 0 && transport.Sets == 0, "Cancellation after capture failure prevents all output");
        }
        foreach (string guarded in new[] { "first\nsecond", "first\tsecond", new string('x', 6001) })
        {
            transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue }; insertion.PasteTransportForTests = transport;
            await insertion.PasteAsync(target, guarded, true);
            Check(transport.UnicodeCalls == 0 && transport.Enters == 0 && transport.Sets == 0, "Multiline, control and overlong drafts cannot use Unicode fallback");
        }
        transport = new() { RejectCapture = true, UnicodeAccepted = uint.MaxValue, AfterUnicode = () => current = target with { RuntimeId = [88] } }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Focus moves after Unicode", true);
        Check(transport.UnicodeCalls == 1 && transport.Enters == 0, "Focus change after Unicode prevents Enter without retry"); current = target;
        transport = new() { StagePartial = true, UnicodeAccepted = uint.MaxValue }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Never switch after mutation", true);
        Check(transport.UnicodeCalls == 0 && transport.Enters == 0 && transport.RestoreAttempts > 0, "A partially mutated clipboard can only restore, never switch to Unicode input");
        transport = new() { UnicodeAccepted = uint.MaxValue, BeforePasteRead = () => throw new System.Runtime.InteropServices.ExternalException("Synthetic provider failure") }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "Never switch after successful capture", true);
        Check(transport.UnicodeCalls == 0 && transport.Pastes == 0 && transport.Enters == 0, "Only capture failure qualifies fallback, not later readback/preparation errors");
        var packetText = "\u00e9\u6c34\U0001F600";
        var packets = Insertion.UnicodePacketsForTests(packetText);
        Check(packets.Length == packetText.Length * 2 && packets.Select((packet, index) => packet.Key == 0 && packet.Scan == packetText[index / 2] && packet.Flags == (index % 2 == 0 ? 4u : 6u)).All(value => value),
            "Unicode fallback constructs one exact UTF-16 down/up batch with no virtual navigation or Enter keys");
        return count;
    }
    private sealed class FakePaste : ICompletedDraftTransport
    {
        public int Saves, Pastes, Enters, Restores, RestoreErrors, RestoreAttempts, Sets, UnicodeCalls; public uint Sequence = 1;
        public uint? UnicodeAccepted;
        public bool HeldEnter;
        public string Payload = ""; public bool UnsupportedReadback, Partial, StaleValue, NewCopyAtRestore, StagePartial, RestorePartial, MaterializeOnCapture, RejectCapture;
        public string? DocumentBefore, DocumentAfter, DocumentFinal;
        private int documentReadsAfterPaste;
        public Action? AfterPaste, BeforePasteRead, OnCaptureFailure, AfterUnicode;
        public Action<int>? OnDelay;
        public object SaveClipboard() { Saves++; if (RejectCapture) { OnCaptureFailure?.Invoke(); throw new System.Runtime.InteropServices.ExternalException("Clipboard contains a format that cannot be safely preserved."); } if (MaterializeOnCapture) Sequence++; return "private prior formats"; }
        public ClipboardChange SetClipboard(object? previous, string text) { Sets++; Payload = text; return new(StagePartial ? ClipboardChangeStatus.FailedOwnedPartial : ClipboardChangeStatus.Staged, ++Sequence); }
        public uint ClipboardSequence => Sequence;
        public ClipboardChange RestoreClipboard(object? previous, uint expectedSequence)
        {
            RestoreAttempts++;
            if (expectedSequence != Sequence) throw new Exception("Stale restore sequence");
            if (NewCopyAtRestore) return new(ClipboardChangeStatus.OwnershipChanged, ++Sequence);
            if (RestorePartial) { RestorePartial = false; return new(ClipboardChangeStatus.FailedOwnedPartial, ++Sequence); }
            if (RestoreErrors-- > 0) throw new System.Runtime.InteropServices.COMException("Synthetic clipboard lock");
            if ((string?)previous != "private prior formats") throw new Exception("Clipboard snapshot changed");
            Restores++; return new(ClipboardChangeStatus.Restored, Sequence);
        }
        public bool Paste() { Pastes++; AfterPaste?.Invoke(); return true; }
        public uint Unicode(string text) { UnicodeCalls++; Payload = text; AfterUnicode?.Invoke(); return UnicodeAccepted == uint.MaxValue ? (uint)text.Length * 2 : UnicodeAccepted ?? 0; }
        public bool Enter() { Enters++; return true; }
        public bool EnterHeld => HeldEnter;
        public string? ReadText(DraftTarget target) { if (Pastes == 0 && UnicodeCalls == 0) BeforePasteRead?.Invoke(); return UnsupportedReadback ? null : Pastes == 0 && UnicodeCalls == 0 || StaleValue ? "before" : Partial ? "partial" : Payload; }
        public string? ReadDocumentText(DraftTarget target) => Pastes == 0 ? DocumentBefore
            : ++documentReadsAfterPaste == 1 ? DocumentAfter : DocumentFinal ?? DocumentAfter;
        public Task Delay(int milliseconds) { OnDelay?.Invoke(milliseconds); return Task.CompletedTask; }
    }
}
