using Sona.App.Windows;
internal static class CompletedDraftPasteChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0; void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
        Check(!Insertion.ConfirmsCompleteInsertion("Complete draft", "Complete draftCom", "Complete draft"), "Preexisting full text plus partial paste cannot authorize send");
        Check(Insertion.ConfirmsCompleteInsertion("Prior: ", "Prior: Complete draft", "Complete draft"), "A newly appended complete payload verifies exactly");
        Check(!Insertion.ConfirmsCompleteInsertion("Prior: ", "Prior: Complete draft plus", "Complete draft"), "Extra unrequested changes revoke confirmation");
        var target = new DraftTarget(1, 10, [1, 2], 0); DraftTarget? current = target; bool modifiers = false;
        using var insertion = new Insertion(() => current, () => modifiers, _ => throw new Exception("Unicode fallback must not run"));
        var transport = new FakePaste(); insertion.PasteTransportForTests = transport;
        var longDraft = string.Join("\n", Enumerable.Repeat("Bonjour Codex, keep every word of this complete draft.", 90));
        var result = await insertion.PasteAsync(target, longDraft, true);
        Check(result.Inserted && transport.Pastes == 1 && transport.Enters == 1 && transport.Restores == 1 && transport.Payload == longDraft, "Long multiline draft pastes exactly once, confirms full text, sends one Enter and restores clipboard");
        transport = new() { UnsupportedReadback = true }; insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", true);
        Check(result.Inserted && transport.Pastes == 1 && transport.Enters == 0, "No accessibility readback means paste only, never send");
        transport = new() { Partial = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "complete message", true);
        Check(transport.Enters == 0 && transport.Pastes == 1, "Partial destination content cannot submit or trigger a second paste");
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
        Check(!(await insertion.PasteAsync(target, new string('x', 6001), true)).Inserted && transport.Saves == 0, "Oversize paste has no side effects");
        transport = new() { AfterPaste = () => modifiers = true }; insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", true);
        Check(transport.Enters == 0, "Modifier change before final Enter prevents submission"); modifiers = false;
        transport = new(); insertion.PasteTransportForTests = transport;
        await insertion.PasteAsync(target, "hello", false);
        Check(transport.Pastes == 1 && transport.Enters == 0, "Ordinary completed draft never submits");
        transport = new(); transport.BeforePasteRead = () => transport.Sequence++; insertion.PasteTransportForTests = transport;
        result = await insertion.PasteAsync(target, "hello", true);
        Check(!result.Inserted && transport.Payload == "" && transport.Restores == 0, "Copy during destination readback is preserved before any Sona clipboard write");
        return count;
    }
    private sealed class FakePaste : ICompletedDraftTransport
    {
        public int Saves, Pastes, Enters, Restores; public uint Sequence = 1;
        public string Payload = ""; public bool UnsupportedReadback, Partial;
        public Action? AfterPaste, BeforePasteRead;
        public object SaveClipboard() { Saves++; return "private prior formats"; }
        public uint SetClipboard(string text) { Payload = text; return ++Sequence; }
        public uint ClipboardSequence => Sequence;
        public void RestoreClipboard(object? previous) { if ((string?)previous != "private prior formats") throw new Exception("Clipboard snapshot changed"); Restores++; }
        public bool Paste() { Pastes++; AfterPaste?.Invoke(); return true; }
        public bool Enter() { Enters++; return true; }
        public bool EnterHeld => false;
        public string? ReadText(DraftTarget target) { if (Pastes == 0) BeforePasteRead?.Invoke(); return UnsupportedReadback ? null : Pastes == 0 ? "before" : Partial ? "partial" : Payload; }
        public Task Delay(int milliseconds) => Task.CompletedTask;
    }
}
