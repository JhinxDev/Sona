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
foreach (var ctrl in new[] { 0xA2, 0xA3 }) foreach (var win in new[] { 0x5B, 0x5C }) foreach (var ctrlFirst in new[] { false, true }) foreach (var releaseCtrl in new[] { false, true })
{
    var combo = new HoldGesture(); var first = ctrlFirst ? ctrl : win; var second = ctrlFirst ? win : ctrl;
    var upFirst = releaseCtrl ? ctrl : win; var upSecond = releaseCtrl ? win : ctrl;
    combo.Key(first, true, 0); combo.Key(second, true, 10);
    Check(combo.Key(upFirst, false, 60).Action == HoldAction.None, "First quick tap leaves microphone closed"); combo.Key(upSecond, false, 70);
    combo.Key(first, true, 140); combo.Key(second, true, 150);
    var hf = combo.Key(upFirst, false, 190); combo.Key(upSecond, false, 200);
    Check(hf.Action == HoldAction.Begin && hf.Toggle && hf.HandsFree && combo.PendingToggle == hf.Generation && combo.Current == 0, "Double quick tap admits handsfree after release across modifier variants");
    Check(combo.ConsumeToggle(hf.Generation) && combo.Key(0x41, true, 220).Action == HoldAction.None, "Admitted handsfree permits ordinary typing"); combo.Key(0x41, false, 230);
    combo.Key(first, true, 300); var hfStop = combo.Key(second, true, 310);
    Check(hfStop.Action == HoldAction.Finish && hfStop.HandsFree && hfStop.Generation == hf.Generation && combo.Key(second, true, 320).Action == HoldAction.None && combo.Tick(600).Action == HoldAction.None, "Next single chord finishes handsfree once and cannot rearm while held");
}
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 10); gesture.Key(0xA2, false, 60); gesture.Key(0x5B, false, 70);
gesture.Key(0xA2, true, 500); gesture.Key(0x5B, true, 510);
Check(gesture.Key(0xA2, false, 560).Action == HoldAction.None && gesture.PendingToggle == 0, "Expired double tap remains silent");
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 10); gesture.Key(0xA2, false, 60); gesture.Key(0x5B, false, 70);
gesture.Key(0xA2, true, 140); gesture.Key(0x5B, true, 150); var heldSecond = gesture.Tick(350);
Check(heldSecond.Action == HoldAction.Begin && !heldSecond.Toggle && gesture.Key(0xA2, false, 360).Action == HoldAction.Finish, "Holding second tap uses normal hold and release");
foreach (var interrupt in new[] { 0x41, 0x1B, 0xA0, 0xA5 })
{
    gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 10); gesture.Key(0xA2, false, 60); gesture.Key(0x5B, false, 70);
    gesture.Key(interrupt, true, 80); gesture.Key(interrupt, false, 90); gesture.Key(0xA2, true, 140); gesture.Key(0x5B, true, 150);
    Check(gesture.Key(0xA2, false, 190).Action == HoldAction.None, "Foreign key between taps revokes double tap intent");
}
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 10); gesture.Key(0xA2, false, 60); gesture.Key(0x5B, false, 70); gesture.Invalidate();
gesture.Key(0xA2, true, 140); gesture.Key(0x5B, true, 150);
Check(gesture.Key(0xA2, false, 190).Action == HoldAction.None, "Reset revokes pending double tap"); gesture.Key(0x5B, false, 200);
gesture.Key(0xA2, true, 240); gesture.Key(0x5B, true, 250); var pendingHandsFree = gesture.Key(0xA2, false, 290); gesture.Key(0x5B, false, 300);
gesture.Key(0xA2, true, 340); var pendingStop = gesture.Key(0x5B, true, 350);
Check(pendingHandsFree.HandsFree && pendingStop.Action == HoldAction.Finish && gesture.PendingToggle == 0 && !gesture.ConsumeToggle(pendingHandsFree.Generation), "Stop during delayed handsfree admission revokes microphone permission");
gesture = new(); gesture.Key(0xA2, true, 0); gesture.Key(0x5B, true, 10); gesture.Key(0xA2, false, 60); gesture.Key(0x5B, false, 70);
gesture.Key(0xA2, true, 140, injected: true); gesture.Key(0x5B, true, 150, injected: true);
Check(gesture.Key(0xA2, false, 190, injected: true).Action == HoldAction.None && gesture.PendingToggle == 0 && gesture.Tick(400).Action == HoldAction.None, "Injected second tap cannot complete a physical first tap");
gesture.Key(0xA2, true, 450); gesture.Key(0x5B, true, 460); gesture.Key(0xA2, false, 500); gesture.Key(0x5B, false, 510);
gesture.Key(0xA2, true, 550); gesture.Key(0x5B, true, 560); var inspectingHandsFree = gesture.Key(0xA2, false, 600); gesture.Key(0x5B, false, 610);
var interruptedHandsFree = gesture.Key(0x1B, true, 620);
Check(inspectingHandsFree.HandsFree && interruptedHandsFree.Action == HoldAction.Cancel && interruptedHandsFree.Generation == inspectingHandsFree.Generation && !gesture.ConsumeToggle(inspectingHandsFree.Generation), "Esc while handsfree target inspection is pending revokes admission");
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
Check(ModelCatalog.All.Select(m => m.Id).Distinct().Count() == ModelCatalog.All.Count && ModelCatalog.All.All(m => m.Sha256.Length == 64 && m.Bytes > 0 && m.DownloadUrl.Contains(m.Revision)), "Speech model identities pin their provider conversion revision, file size and SHA256");
SpeechProtocolChecks.Run(Check);
SpeechAudioChecks.Run(Check);
bool InvalidPatch(string json) { try { using var j = System.Text.Json.JsonDocument.Parse(json); AgentSettings.Apply(new(), j.RootElement); return false; } catch (ArgumentException) { return true; } }
Check(InvalidPatch("{\"language\":\"secret-input\"}") && InvalidPatch("{\"startup\":true}") && InvalidPatch("{\"device\":256}") && InvalidPatch("{\"autoInsert\":\"false\"}") && InvalidPatch("{\"model\":\"../../other\"}") && InvalidPatch("{\"language\":\"en\",\"language\":\"fr\"}"), "Agent settings reject unknown, mistyped, overbound, duplicate and path-like values");
using (var j = System.Text.Json.JsonDocument.Parse("{\"language\":\"fr\",\"model\":\"tiny\",\"autoInsert\":false}"))
{
    var originalPreferences = new Preferences(); var patched = AgentSettings.Apply(originalPreferences, j.RootElement);
    Check(patched.Language == "fr" && patched.Model == "tiny" && !patched.AutoInsert && originalPreferences.Language == "auto" && originalPreferences.Model == "auto", "Validated agent patch preserves original state until committed");
}
using (var stream = new MemoryStream())
{
    await AgentProtocol.WriteAsync(stream, new AgentReply(true, new { state = "idle" }), default); stream.Position = 0;
    Check((await AgentProtocol.ReadAsync<AgentReply>(stream, default)).Ok, "Agent messages round trip with finite length framing");
}
using (var stream = new MemoryStream(BitConverter.GetBytes(AgentProtocol.MaximumBytes + 1)))
{
    var rejected = false; try { await AgentProtocol.ReadAsync<AgentReply>(stream, default); } catch (IOException) { rejected = true; }
    Check(rejected, "Oversized agent frames fail before payload allocation");
}
var modelTestRoot = Path.Combine(Path.GetTempPath(), "sona-model-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(modelTestRoot); var store = new ModelStore(modelTestRoot);
File.WriteAllText(store.PathFor("tiny"), "unrelated existing bytes");
var preservedModel = false; try { await store.InstallAsync("tiny", null, default); } catch (IOException) { preservedModel = true; }
Check(preservedModel && File.ReadAllText(store.PathFor("tiny")) == "unrelated existing bytes" && Directory.GetFiles(modelTestRoot).Length == 1, "Model installation preserves mismatched existing files before any network request");
File.Delete(store.PathFor("tiny")); Directory.Delete(modelTestRoot);
var voice = new FakeVoice(); var protectionController = new MicProtection(voice);
await protectionController.ConfigureAsync(true, 0, default);
await protectionController.SetSourcesAsync(new[] { "sona" }, default);
Check(voice.State.Muted && voice.State.Deafened && protectionController.OwnsChanges, "Protection acquires acknowledged mute and deafen before own recording");
await protectionController.SetSourcesAsync(new[] { "sona", "other" }, default);
await protectionController.SetSourcesAsync(new[] { "other" }, default);
Check(voice.State.Muted && voice.State.Deafened, "Overlapping microphone sources retain protection");
await protectionController.SetSourcesAsync(Array.Empty<string>(), default);
Check(!voice.State.Muted && !voice.State.Deafened && voice.Changes.TakeLast(2).SequenceEqual(new[] { "deafen:False", "mute:False" }), "Final source restores deafen then mute");
voice.State = new(true, true, true); await protectionController.SetSourcesAsync(new[] { "manual" }, default); await protectionController.SetSourcesAsync(Array.Empty<string>(), default);
Check(voice.State.Muted && voice.State.Deafened && !protectionController.OwnsChanges, "Pre-existing manual mute and deafen remain untouched");
voice.State = new(true, false, false); await protectionController.SetSourcesAsync(new[] { "active" }, default); await protectionController.PauseAsync(true, default);
Check(!voice.State.Muted && !voice.State.Deafened, "Pause restores only protection-owned changes");
await protectionController.PauseAsync(false, default);
Check(voice.State.Muted && voice.State.Deafened, "Resume reconciles sources that remain active");
voice.State = new(true, false, false); await protectionController.ObserveAsync(voice.State, default);
await protectionController.SetSourcesAsync(new[] { "active" }, default);
Check(!voice.State.Muted && !protectionController.OwnsChanges, "Manual unmute is not overwritten by unchanged microphone polling");
voice.State = new(true, true, true); await protectionController.SetSourcesAsync(Array.Empty<string>(), default);
Check(voice.State.Muted && voice.State.Deafened, "Manual re-mute after ownership revocation is preserved");
voice.State = new(true, false, false); await protectionController.SetSourcesAsync(new[] { "close" }, default); await protectionController.CloseAsync(default);
Check(!voice.State.Muted && !voice.State.Deafened && !protectionController.OwnsChanges, "Orderly quit acknowledges owned voice restoration");
var orderedVoice = new FakeVoice(); var orderedController = new MicProtection(orderedVoice); await orderedController.ConfigureAsync(true, 0, default);
await orderedController.SetSourcesAsync(new[] { "ordered" }, default);
orderedVoice.Observe(false, false); orderedVoice.Observe(true, true);
await orderedController.SetSourcesAsync(Array.Empty<string>(), default);
Check(orderedVoice.State.Muted && orderedVoice.State.Deafened && !orderedController.OwnsChanges, "Ordered unmute then manual remute revokes ownership even when the latest state is muted");
var duringReleaseVoice = new FakeVoice(); var duringReleaseController = new MicProtection(duringReleaseVoice); await duringReleaseController.ConfigureAsync(true, 0, default); await duringReleaseController.SetSourcesAsync(new[] { "during-release" }, default);
duringReleaseVoice.DuringUndeafen = () => { duringReleaseVoice.Observe(false, false); duringReleaseVoice.Observe(true, false); };
await duringReleaseController.SetSourcesAsync(Array.Empty<string>(), default);
Check(duringReleaseVoice.State.Muted && !duringReleaseVoice.State.Deafened && !duringReleaseController.OwnsChanges, "Manual re-mute during undeafen acknowledgement is preserved before deciding to unmute");
var retryVoice = new FakeVoice(); var retryController = new MicProtection(retryVoice); await retryController.ConfigureAsync(true, 1, default); await retryController.SetSourcesAsync(new[] { "retry" }, default);
retryVoice.RejectRelease = true; await retryController.SetSourcesAsync(Array.Empty<string>(), default); await Task.Delay(30);
Check(retryController.OwnsChanges && retryController.Status.Contains("retry"), "Failed delayed restoration retains ownership and surfaces retry state");
retryVoice.RejectRelease = false; await retryController.SetSourcesAsync(Array.Empty<string>(), default);
Check(!retryVoice.State.Muted && !retryVoice.State.Deafened && !retryController.OwnsChanges, "Idle polling retries failed delayed voice restoration");
var joinVoice = new FakeVoice { State = new(false, false, false) }; var joinController = new MicProtection(joinVoice); await joinController.SetSourcesAsync(new[] { "existing-recording" }, default);
joinVoice.State = new(true, false, false); await joinController.EnsureProtectedAsync(default);
Check(joinVoice.State.Muted && joinVoice.State.Deafened, "Call-entry reconciliation protects a recording that began outside a call");
Check(ProtectionShortcut.TryParse("ctrl+shift+h", out var pauseModifiers, out var pauseKey) && pauseModifiers == 0x4006 && pauseKey == 0x48 && ProtectionShortcut.TryParse("alt+F24", out _, out _) && !ProtectionShortcut.TryParse("ctrl+ctrl+h", out _, out _) && !ProtectionShortcut.TryParse("ctrl+win", out _, out _) && !ProtectionShortcut.TryParse("shift+h", out _, out _), "Pause shortcut accepts bounded keyed combinations and rejects duplicate or modifier-only shortcuts");
using (var appearancePatch = System.Text.Json.JsonDocument.Parse("{\"theme\":\"amethyst\",\"recordingDisplay\":\"Panel\",\"modelIdleMinutes\":30}"))
{
    var originalSettings = new Preferences(); var changedSettings = AgentSettings.Apply(originalSettings, appearancePatch.RootElement);
    Check(changedSettings.Theme == "amethyst" && changedSettings.RecordingDisplay == "Panel" && changedSettings.ModelIdleMinutes == 30 && originalSettings.Theme == "charcoal", "Agent appearance and idle preferences clone without mutating current settings");
}
Check(ThemeCatalog.All.Count == 8 && ThemeCatalog.All.Count(t => t.IsDark) == 5 && ThemeCatalog.All.Select(t => t.Id).Distinct().Count() == 8, "Theme catalog contains eight distinct palettes grouped into five dark and three light");
var invalidAppearance = new Preferences { Theme = "unknown", RecordingDisplay = "unknown", ModelIdleMinutes = -1 }; invalidAppearance.Normalize();
Check(invalidAppearance.Theme == "charcoal" && invalidAppearance.RecordingDisplay == "Capsule" && invalidAppearance.ModelIdleMinutes == 0, "Malformed saved appearance and memory preferences normalize safely");
using (var fullSettingsPatch = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(AgentSettings.View(new Preferences()), AgentProtocol.Json)))
{
    var fullSettings = AgentSettings.Apply(new Preferences(), fullSettingsPatch.RootElement);
    Check(fullSettings.ModelIdleMinutes == 10 && fullSettings.Theme == "charcoal" && fullSettings.RecordingDisplay == "Capsule", "The complete twenty-one-field settings response round trips through a validated agent patch");
}
using (var clipboardPatch = System.Text.Json.JsonDocument.Parse("{\"copyToClipboard\":true}"))
{
    var originalClipboard = new Preferences(); var clipboardSettings = AgentSettings.Apply(originalClipboard, clipboardPatch.RootElement);
    Check(clipboardSettings.CopyToClipboard && !originalClipboard.CopyToClipboard, "Clipboard backup is explicitly configurable and does not mutate the old settings");
}
CleanupRulesChecks.Run(Check);
var cleanupPrefs = new Preferences { CleanupEnabled = true, Terms = ["Sona"] };
var modelCalls = 0;
var intact = await CleanupProcessor.RunAsync("um keep Sona", cleanupPrefs, "Codex", (prompt, ct) => { modelCalls++; return Task.FromResult("Keep [TERM_0]."); }, CancellationToken.None);
Check(intact.Applied && intact.Text == "Keep Sona." && modelCalls == 1, "Production cleanup processor restores terms after polishing");
var drift = await CleanupProcessor.RunAsync("um keep Sona", cleanupPrefs, "Codex", (prompt, ct) => Task.FromResult("Keep another app."), CancellationToken.None);
Check(!drift.Applied && drift.Text == "um keep Sona" && drift.Outcome == "preserved", "Protected-content drift falls back to the untouched original");
var verbatim = await CleanupProcessor.RunAsync("um keep Sona", new Preferences { CleanupEnabled = true, Style = "Verbatim" }, "Codex", (prompt, ct) => throw new Exception("Verbatim called model"), CancellationToken.None);
Check(!verbatim.Applied && verbatim.Text == "um keep Sona", "Verbatim never invokes the cleanup model");
var unavailableCleanup = await CleanupProcessor.RunAsync("um keep Sona", cleanupPrefs, "Codex", (prompt, ct) => throw new IOException("fixture failure"), CancellationToken.None);
Check(!unavailableCleanup.Applied && unavailableCleanup.Outcome == "unavailable", "Model failure retains a usable basic draft");
using (var lateCancel = new CancellationTokenSource())
{
    var rejected = false;
    try { await CleanupProcessor.RunAsync("keep Sona", cleanupPrefs, "Codex", (prompt, ct) => { lateCancel.Cancel(); return Task.FromResult("Keep [TERM_0]."); }, lateCancel.Token); } catch (OperationCanceledException) { rejected = true; }
    Check(rejected, "Cancellation after inference cannot return a late polished result");
}
ReleaseVersionChecks.Run(Check);
Console.WriteLine($"{count} checks passed.");

sealed class FakeVoice : IVoiceProtectionTransport
{
    public VoiceState State { get; set; } = new(true, false, false);
    public List<string> Changes { get; } = [];
    public bool RejectRelease { get; set; }
    public Action? DuringUndeafen { get; set; }
    private readonly Queue<VoiceObservation> observations = new();
    public long ObservationVersion { get; private set; }
    public void Observe(bool mute, bool deaf) { State = State with { Muted = mute, Deafened = deaf }; observations.Enqueue(new(++ObservationVersion, mute, deaf, false)); }
    public IReadOnlyList<VoiceObservation> DrainObservations() { var result = observations.ToArray(); observations.Clear(); return result; }
    public Task<VoiceState> ReadAsync(CancellationToken cancellation) => Task.FromResult(State);
    public Task<bool> SetMuteAsync(bool value, CancellationToken cancellation) { if (!value && RejectRelease) return Task.FromResult(false); State = State with { Muted = value }; Changes.Add("mute:" + value); return Task.FromResult(true); }
    public Task<bool> SetDeafenAsync(bool value, CancellationToken cancellation) { if (!value && RejectRelease) return Task.FromResult(false); State = State with { Deafened = value }; Changes.Add("deafen:" + value); if (!value) DuringUndeafen?.Invoke(); return Task.FromResult(true); }
}
