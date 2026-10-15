using Sona.Core;
internal static class PersonalizationChecks
{
    public static void Run(Action<bool, string> check)
    {
        HoldGesture HandsFree()
        {
            var g = new HoldGesture();
            g.Key(0xA2, true, 0); g.Key(0x5B, true, 10); g.Key(0xA2, false, 60); g.Key(0x5B, false, 70);
            g.Key(0xA2, true, 140); g.Key(0x5B, true, 150); var begin = g.Key(0xA2, false, 190); g.Key(0x5B, false, 200);
            check(g.ConsumeToggle(begin.Generation), "Double-tap admission is consumed before Enter"); return g;
        }
        var gesture = HandsFree(); var finish = gesture.Key(13, true, 300, finishOnEnter: true);
        check(finish.Action == HoldAction.Finish && finish.Submit && finish.Suppress && finish.HandsFree, "Bare physical Enter ends admitted hands-free with explicit submit intent");
        check(gesture.Key(13, true, 310, finishOnEnter: true).Suppress && gesture.Key(13, false, 320, finishOnEnter: true).Suppress, "Enter repeat and key-up cannot escape into the old composer");
        check(gesture.Key(13, true, 400, finishOnEnter: true).Suppress && gesture.Key(13, false, 410, finishOnEnter: true).Suppress, "A second Enter while preparing cannot send a stale composer");
        gesture.Invalidate(); check(!gesture.Key(13, true, 500, finishOnEnter: true).Suppress, "Enter resumes normal behavior after completion/reset");
        gesture = HandsFree(); check(!gesture.Key(13, true, 300).Suppress, "Setting off leaves Enter untouched");
        gesture = HandsFree(); gesture.Key(0xA0, true, 290); check(!gesture.Key(13, true, 300, finishOnEnter: true).Suppress, "Shift+Enter is not a submit gesture");
        gesture = HandsFree(); check(!gesture.Key(13, true, 300, injected: true, finishOnEnter: true).Suppress, "Injected Enter cannot finish or submit recording");
        gesture = HandsFree(); gesture.Invalidate(); check(!gesture.Key(13, true, 300, finishOnEnter: true).Suppress, "Cancelled generation cannot later submit");
        gesture = new(); gesture.Key(0xA2, true, 0, toggle: true); var toggle = gesture.Key(0x5B, true, 10, toggle: true);
        gesture.Key(0xA2, false, 20, toggle: true); gesture.Key(0x5B, false, 30, toggle: true); gesture.ConsumeToggle(toggle.Generation);
        var toggleEnter = gesture.Key(13, true, 100, toggle: true, finishOnEnter: true);
        check(toggleEnter.Suppress && toggleEnter.Submit && toggleEnter.Action == HoldAction.Finish, "Configured Toggle also consumes Enter to finish and send");
        var suggestion = CorrectionLearning.Suggest("Please open codecs.", "Please open Codex.");
        check(suggestion.Single() == new Replacement("codecs", "Codex"), "Short explicit edit proposes a personal spelling correction");
        check(CorrectionLearning.Suggest("Use open ai today.", "Use OpenAI today.").Single() == new Replacement("open ai", "OpenAI"), "Bounded phrase correction can join words");
        check(CorrectionLearning.Suggest("Bonjour Montreal.", "Bonjour Montréal.").Count == 1, "French accented correction remains Unicode");
        foreach(var pair in new[]{("Use foo_bar.","Use foo_baz."),("Go https://a.test", "Go https://b.test"),("Code 1234", "Code 5678"),("Please send this entire message today.", "Keep a completely different request instead.")})
            check(CorrectionLearning.Suggest(pair.Item1, pair.Item2).Count == 0, "Code identifiers, URLs, numbers and broad rewrites are not learned");
        check(CorrectionLearning.Suggest("Hello.", "Hello!").Count == 0, "Punctuation-only edits do not create dictionary entries");
        var preferences = new Preferences { Vocabulary = [new("codecs", "Codex")] };
        check(TextPipeline.Apply("open codecs now", preferences) == "open Codex now", "Approved correction changes a future draft");
        var path = Path.Combine(Path.GetTempPath(), "sona-saved-fixture-" + Guid.NewGuid() + ".json");
        try
        {
            var item = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "open codecs", "Open Codex.", Saved: true, Generated: "Open codecs.");
            HistoryStore.Save(path, [item]); check(HistoryStore.Load(path).Single() == item, "Saved original, generated and edited text round-trip exactly");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        check(new Preferences().PasteCompletedDraft && !new Preferences().EnterToSend, "Whole-draft transport defaults on while Enter sending requires explicit enablement");
    }
}
