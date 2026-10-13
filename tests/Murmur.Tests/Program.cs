using Murmur.Core;
int count = 0;
void Check(bool pass, string name) { if (!pass) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
var preferences = new Preferences();
Check(TextPipeline.Apply("Um, review codex with github.", preferences) == "review Codex with GitHub.", "Fillers and explicit vocabulary");
Check(TextPipeline.Apply("Keep `um github` and https://example.com/um plus foo_bar.", preferences) == "Keep `um github` and https://example.com/um plus foo_bar.", "Protected code paths and URLs");
Check(TextPipeline.Apply("euh, corrige le PowerShell.", preferences) == "corrige le PowerShell.", "French filler and technical term");
Check(TextPipeline.Apply("review my changes.", preferences) == preferences.Snippets[0].Expansion, "Explicit full utterance snippet");
Check(TextPipeline.Apply("Do not review my changes.", preferences) == "Do not review my changes.", "Snippet embedded in speech does not expand");
Check(TextPipeline.Apply("Use A. correction: Use B.", preferences) == "Use B.", "Explicit correction marker");
Check(TextPipeline.Apply("Actually use A, no, use B.", preferences) == "Actually use A, no, use B.", "Ambiguous correction is retained for review");
preferences.Style = "Verbatim";
Check(TextPipeline.Apply("Um, github.", preferences) == "Um, github.", "Verbatim preserves original words");
preferences.Style = "Notes";
Check(TextPipeline.Apply("First. new paragraph Second.", preferences).Contains("\n\n"), "Notes paragraph command");
var s = new DictationSession(); Check(!s.Stop(), "Cannot stop idle session"); Check(s.Begin() && !s.Begin(), "No duplicate recording"); Check(s.Stop(), "Recording to transcription"); s.Cancel(); Check(!s.Complete("late", "late") && s.State == SessionState.Cancelled, "Cancelled completion ignored");
var d = new DictationSession(); d.Begin(); d.Stop(); d.Complete("original", "draft"); Check(d.MarkInserted() && !d.MarkInserted(), "Duplicate insertion refused"); Check(d.Original == "original", "Original transcript retained after insertion");
var empty = new DictationSession(); empty.Begin(); empty.Stop(); Check(!empty.Complete("", "") && empty.State == SessionState.Failed, "Empty output cannot insert");
var path = Path.Combine(Path.GetTempPath(), "murmur-settings-" + Guid.NewGuid() + ".json"); preferences.Save(path); Check(Preferences.Load(path).Style == "Notes", "Saved preferences read back"); File.Delete(path);
File.WriteAllText(path, "{\"Vocabulary\":null,\"Snippets\":null,\"Language\":null,\"Style\":\"unsafe\",\"Device\":999}");
var repaired = Preferences.Load(path); Check(repaired.Vocabulary.Count > 0 && repaired.Snippets.Count > 0 && repaired.Language == "auto" && repaired.Style == "Clean" && repaired.Device == -1, "Null structures and invalid preferences normalize");
File.WriteAllText(path, "{\"Vocabulary\":[null,{\"Spoken\":null,\"Written\":\"bad\"},{\"Spoken\":\"good\",\"Written\":\"GOOD\"}],\"Snippets\":[null]}");
repaired = Preferences.Load(path); Check(repaired.Vocabulary.Count == 1 && repaired.Vocabulary[0].Written == "GOOD" && repaired.Snippets.Count == 0, "Null list entries normalize without losing valid replacements"); File.Delete(path);
var rules = VocabularyRules.Parse("FooBar\npower shell = PowerShell");
Check(rules.Terms.SequenceEqual(new[] { "FooBar" }) && rules.Replacements.Single() == new Replacement("power shell", "PowerShell"), "Recognition hint and explicit replacement have separate records");
preferences = new Preferences { Terms = rules.Terms, Vocabulary = rules.Replacements };
Check(TextPipeline.Apply("foo bar with power shell", preferences) == "foo bar with PowerShell", "Recognition hint does not invent a spelling replacement");
bool InvalidVocabulary(string text) { try { VocabularyRules.Parse(text); return false; } catch (FormatException) { return true; } }
Check(InvalidVocabulary("FooBar\nfoobar = FooBar"), "Conflicting hint and correction are rejected");
Check(InvalidVocabulary("term = " + new string('a', 121)), "Overlong vocabulary cannot silently disappear on reload");
preferences.Save(path); Check(Preferences.Load(path).Terms.SequenceEqual(rules.Terms), "Exact vocabulary hint persists"); File.Delete(path);
foreach (var ctrl in new[] { 0xA2, 0xA3 }) foreach (var win in new[] { 0x5B, 0x5C }) foreach (var ctrlFirst in new[] { false, true }) foreach (var ctrlReleasedFirst in new[] { false, true })
{
    var hold = new HoldGesture(); var first = ctrlFirst ? ctrl : win; var second = ctrlFirst ? win : ctrl;
    Check(!hold.Key(first, true, 0).Suppress && !hold.Key(second, true, 10).Suppress && hold.Tick(209).Action == HoldAction.None, $"Ctrl+Win {ctrl:X}/{win:X}/{ctrlFirst}/{ctrlReleasedFirst} preserves original downs and waits");
    var started = hold.Tick(210); var repeat = hold.Key(win, true, 211);
    Check(started.Action == HoldAction.Begin && hold.Tick(220).Action == HoldAction.None && repeat.Suppress, "One begin per held gesture; Windows repeats suppressed");
    var releasedFirst = ctrlReleasedFirst ? ctrl : win; var releasedSecond = ctrlReleasedFirst ? win : ctrl;
    var stopped = hold.Key(releasedFirst, false, 230); var drainRepeat = hold.Key(releasedSecond, true, 231); var drained = hold.Key(releasedSecond, false, 232);
    Check(stopped.Action == HoldAction.Finish && stopped.Generation == started.Generation && hold.Current == 0 && !stopped.Suppress && !drained.Suppress && drained.Action == HoldAction.None && drainRepeat.Suppress == ctrlReleasedFirst, "One finish, generation revoked, physical ups forwarded, drain repeats bounded");
}
var gesture = new HoldGesture(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 1); gesture.Key(0xA2, false, 100);
Check(gesture.Tick(300).Action == HoldAction.None && gesture.Current == 0, "Short Ctrl+Win tap never admits a microphone begin");
gesture.Key(0x5B, false, 301); gesture.Key(0x41, true, 302); gesture.Key(0xA2, true, 303); gesture.Key(0x5B, true, 304); gesture.Key(0x41, false, 305);
Check(gesture.Tick(600).Action == HoldAction.None, "Pre-held other key and its release cannot arm a modifier shortcut");
gesture = new(); gesture.Baseline(new[] { 0xA3, 0x5C }); gesture.Key(0xA3, true, 0);
Check(gesture.Tick(500).Action == HoldAction.None, "Pre-held modifiers at startup or resume require a clean release");
gesture.Key(0xA3, false, 501); gesture.Key(0x5C, false, 502); gesture.Key(0xA3, true, 503); gesture.Key(0x5C, true, 504);
var queued = gesture.Tick(704); gesture.Invalidate();
Check(queued.Action == HoldAction.Begin && gesture.Current == 0 && gesture.Tick(900).Action == HoldAction.None, "Cancel invalidates queued begin and cannot rearm while held");
gesture = new(); gesture.Key(0xA2, true, 0, true); gesture.Key(0x5B, true, 1, true);
Check(gesture.Tick(300).Action == HoldAction.None, "Injected modifiers never initiate dictation");
foreach (var foreign in new[] { 0xA0, 0xA5, 0x41, 0x1B })
{
    gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 1); gesture.Key(foreign, true, 100);
    Check(gesture.Tick(300).Action == HoldAction.None, "Shift, Alt/AltGr, another key or Esc before dwell cancels arming");
    gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 1); var begun = gesture.Tick(201);
    var aborted = gesture.Key(foreign, true, 202); var modifierUp = gesture.Key(0xA2, false, 203);
    Check(begun.Action == HoldAction.Begin && aborted.Action == HoldAction.Cancel && aborted.Generation == begun.Generation && !aborted.Suppress && modifierUp.Action == HoldAction.None && gesture.Current == 0, "Other key cancels once, forwards the key, and produces no transcription finish");
}
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0xA3, true, 1); gesture.Key(0x5B, true, 2); gesture.Key(0x5C, true, 3); gesture.Tick(202);
Check(gesture.Key(0xA2, false, 203).Action == HoldAction.None && gesture.Key(0x5B, false, 204).Action == HoldAction.None && gesture.Key(0xA3, false, 205).Action == HoldAction.Finish, "Sibling modifier sides keep a hold active until a family is fully released");
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 1); var admitted = gesture.Tick(201); var terminal = gesture.Key(0xA2, false, 202);
var watchdogBeforeUiDelivery = gesture.Reconcile(admitted.Generation, false);
Check(watchdogBeforeUiDelivery.Action == HoldAction.None && terminal.Action == HoldAction.Finish && terminal.Generation == admitted.Generation, "Queued normal Finish wins over watchdog reconciliation before UI delivery");
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 1); admitted = gesture.Tick(201);
Check(gesture.Reconcile(admitted.Generation + 1, false).Action == HoldAction.None && gesture.Reconcile(admitted.Generation, true).Action == HoldAction.None && gesture.Reconcile(admitted.Generation, false).Action == HoldAction.Cancel && gesture.Reconcile(admitted.Generation, false).Action == HoldAction.None, "Lost-release reconciliation cancels only the admitted generation once");
preferences = new Preferences { Engine = "FastCpu" }; preferences.Save(path);
Check(Preferences.Load(path).Engine == "FastCpu", "Explicit CPU engine preference persists across relaunch");
File.WriteAllText(path, "{\"Engine\":null}");
Check(Preferences.Load(path).Engine == "Auto", "Malformed engine preference normalizes to the installed default"); File.Delete(path);
foreach (var sample in new[] { new Snippet("path", @"C:\new-project"), new Snippet("literal", @"keep \n literal"), new Snippet("lines", "First.\r\n  Indented.\nLast."), new Snippet("a=b", "x=y"), new Snippet("spacing", "  keep both ends  ") })
    Check(SnippetRules.Parse(SnippetRules.Format(new[] { sample })).Single() == sample, "Bulk snippet round trip preserves " + sample.Trigger);

foreach (var ctrl in new[] { 0xA2, 0xA3 }) foreach (var win in new[] { 0x5B, 0x5C })
{
    gesture = new(); gesture.Key(ctrl, true, 0, toggle: true); var startToggle = gesture.Key(win, true, 1, toggle: true);
    var releaseToggle = gesture.Key(ctrl, false, 20, toggle: true); gesture.Key(win, false, 21, toggle: true);
    Check(startToggle.Action == HoldAction.Begin && startToggle.Toggle && releaseToggle.Action == HoldAction.Finish && releaseToggle.Toggle && gesture.Tick(300).Action == HoldAction.None, "Toggle admits once and identifies release separately from a hold finish");
}
gesture = new(); gesture.Key(0xA2, true, 0, true, true); gesture.Key(0x5B, true, 1, true, true);
Check(gesture.Tick(300).Action == HoldAction.None, "Injected toggle modifiers cannot admit recording");
gesture = new(); gesture.Baseline(new[] { 0xA2, 0x5B });
Check(gesture.Key(0x5B, true, 0, toggle: true).Action == HoldAction.None, "Pre-held toggle modifiers do not start at startup or resume");
var h = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Bonjour le monde", "Hello Codex", true);
gesture = new(); gesture.Key(0xA2, true, 0, toggle: true); var delayedToggle = gesture.Key(0x5B, true, 1, toggle: true);
gesture.Key(0xA2, false, 2, toggle: true); gesture.Key(0x5B, false, 3, toggle: true);
Check(gesture.Current == 0 && gesture.PendingToggle == delayedToggle.Generation, "Normal Toggle release preserves admission through a delayed target inspection");
gesture.Key(0x1B, true, 4, toggle: true);
Check(gesture.PendingToggle == 0 && !gesture.ConsumeToggle(delayedToggle.Generation), "Esc during target inspection revokes Toggle admission before capture");
gesture = new(); gesture.Key(0xA2, true, 0, toggle: true); delayedToggle = gesture.Key(0x5B, true, 1, toggle: true); gesture.Invalidate();
Check(gesture.PendingToggle == 0, "Cancellation or reset invalidates an undelivered Toggle begin");
gesture = new(); gesture.Key(0xA2, true, 0, toggle: true); delayedToggle = gesture.Key(0x5B, true, 1, toggle: true);
gesture.Key(0xA2, false, 2, toggle: true); gesture.Key(0x5B, false, 3, toggle: true);
Check(gesture.ConsumeToggle(delayedToggle.Generation) && !gesture.ConsumeToggle(delayedToggle.Generation), "Toggle admission is consumed once after recording begins");
Check(gesture.Key(0x41, true, 4, toggle: true).Action == HoldAction.None, "Ordinary typing after admitted Toggle release cannot cancel the ongoing recording");
var historyPath = Path.Combine(Path.GetTempPath(), "sona-history-" + Guid.NewGuid() + ".json");
Check(HistoryStore.Load(historyPath).Count == 0 && !File.Exists(historyPath), "Absent history is read without creating a transcript file");
HistoryStore.Save(historyPath, new[] { h });
Check(HistoryStore.Load(historyPath).Single() == h, "Opt-in history preserves original, draft and insertion state");
Check(HistoryStore.Search(new[] { h }, "  BONJOUR ").Single() == h && HistoryStore.Search(new[] { h }, "absent").Count == 0, "History search includes original and cleaned text, ignoring case and query padding");
Check(HistoryStore.Bounded(new[] { h, h, h with { Id = Guid.Empty }, h with { Id = Guid.NewGuid(), Draft = new string('x',6001) } }).Count == 1, "Duplicate identities and malformed or overlong records are excluded");
Check(HistoryStore.Bounded(Enumerable.Range(0,250).Select(_ => h with { Id = Guid.NewGuid() })).Count == 200, "History retention has a finite 200 entry capacity");
File.WriteAllText(historyPath, "{malformed"); var invalidHistory = false;
try { HistoryStore.Load(historyPath); } catch (System.Text.Json.JsonException) { invalidHistory = true; }
Check(invalidHistory && File.ReadAllText(historyPath) == "{malformed", "Malformed history is preserved instead of silently replaced"); File.Delete(historyPath);
preferences = new Preferences { ShortcutMode = "Toggle", KeepHistory = true }; preferences.Save(path);
Check(Preferences.Load(path).ShortcutMode == "Toggle" && Preferences.Load(path).KeepHistory, "Explicit toggle mode and opt-in history survive reload"); File.Delete(path);
Console.WriteLine($"{count} checks passed.");
