using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class PersonalizationFixture
{
    public static object Run(string output, bool reopen)
    {
        output = Path.GetFullPath(output); var profile = Path.Combine(output, "profile");
        if (!reopen)
        {
            if (Directory.Exists(output)) throw new IOException("Use fresh evidence directory");
            Directory.CreateDirectory(output);
            new Preferences { Theme = "amethyst", KeepHistory = false, AutoInsert = false, Model = "turbo" }.Save(Path.Combine(profile, "preferences.json"));
        }
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        var checks = new List<string>(); Exception? failure = null;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); File.WriteAllLines(Path.Combine(output, reopen ? "reopen-progress.txt" : "progress.txt"), checks); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent();
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    var fakeTarget = new DraftTarget(1, 11, [7, 8], 0);
                    var fakePaste = new NativeFlowPaste();
                    var boundary = new Insertion(() => fakeTarget, () => false, _ => throw new Exception("No global typing")) { PasteTransportForTests = fakePaste };
                    window = new MainWindow(["--test-ui"], boundary); window.ShowActivated = false; window.Show();
                    object Field(string name) => window.FindName(name);
                    object? Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
                    void Click(string method, object? sender = null) => Invoke(method, sender ?? window, new RoutedEventArgs());
                    void Capture(string name)
                    {
                        window.UpdateLayout(); var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(window);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(Path.Combine(output, name)); png.Save(stream);
                    }
                    var pages = (TabControl)Field("Pages"); pages.ApplyTemplate(); window.UpdateLayout();
                    var repositoryButton = (Button)pages.Template.FindName("SidebarRepository", pages);
                    var starButton = (Button)pages.Template.FindName("SidebarStar", pages);
                    foreach (var link in new[] { repositoryButton, starButton })
                    {
                        ((System.Windows.Input.RoutedCommand)link.Command).Execute(link.CommandParameter, link);
                        Check(window.LastRepositoryLinkForTests?.AbsoluteUri == "https://github.com/JhinxDev/Sona", "Sidebar " + link.Name + " routes to the exact repository without opening a browser in tests");
                    }
                    Check(repositoryButton.ActualWidth > 0 && starButton.ActualWidth > 0, "Repository and Star actions are visible in the sidebar footer");
                    var history = (ListBox)Field("History");
                    if (reopen)
                    {
                        Check(!((CheckBox)Field("SoundEffects")).IsChecked!.Value, "Sound effects off choice survives a fresh app process");
                        var saved = history.Items.OfType<HistoryItem>().Single();
                        Check(saved.Saved && saved.Draft == "Please open Codex." && saved.Generated == "Please open codecs.", "Fresh native process restores explicit saved draft while automatic history remains off");
                        Check(Preferences.Load(Path.Combine(profile, "preferences.json")).Vocabulary.Any(x => x == new Replacement("codecs", "Codex")), "Approved correction survives real app relaunch");
                        Invoke("RestoreHistoryDraft", saved);
                        Check(((TextBox)Field("Original")).Text == "please open codecs" && ((TextBox)Field("Draft")).Text == saved.Draft, "Saved raw and corrected versions reopen exactly");
                        Capture("saved-reopened.png");
                        HistoryStore.Save(Path.Combine(profile, "history.json"), [saved with { Saved = false, Draft = "old version" }]);
                        Click("DeleteSavedDraft_Click", new Button { Tag = saved });
                        Check(HistoryStore.Load(Path.Combine(profile, "history.json")).Count == 0, "Deleting saved draft removes old recent-history mirror so it cannot resurrect on relaunch");
                        Check(HistoryStore.Load(Path.Combine(profile, "saved-drafts.json")).Count == 0 && ((TextBox)Field("Draft")).Text == saved.Draft, "Explicit saved-record removal preserves current editable text");
                        var preferences = (Preferences)typeof(MainWindow).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                        preferences.KeepHistory = true;
                        var entries = (List<HistoryItem>)typeof(MainWindow).GetField("historyEntries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                        var recent = saved with { Id = Guid.NewGuid(), Saved = false };
                        entries.Add(recent); HistoryStore.Save(Path.Combine(profile, "history.json"), [recent]);
                        Invoke("RestoreHistoryDraft", recent); Click("SaveDraft_Click");
                        Check(HistoryStore.Load(Path.Combine(profile, "history.json")).Count == 0 && HistoryStore.Load(Path.Combine(profile, "saved-drafts.json")).Single().Id == recent.Id, "Saving a recent draft moves its persisted record out of automatic history");
                    }
                    else
                    {
                        var initial = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "please open codecs", "Please open codecs.", Generated: "Please open codecs.");
                        Invoke("RestoreHistoryDraft", initial);
                        ((TextBox)Field("Draft")).Text = "Please open Codex.";
                        Click("SaveDraft_Click");
                        var saved = HistoryStore.Load(Path.Combine(profile, "saved-drafts.json")).Single();
                        Check(saved.Saved && saved.Original == initial.Original && saved.Generated == initial.Generated && saved.Draft == "Please open Codex.", "Save draft persists original, generated and manual edit exactly");
                        Check(!File.Exists(Path.Combine(profile, "history.json")) && !Preferences.Load(Path.Combine(profile, "preferences.json")).KeepHistory, "Explicit save does not enable automatic recording history");
                        var suggestions = ((ItemsControl)Field("CorrectionSuggestions")).Items.Cast<Replacement>().ToArray();
                        Check(suggestions.Single() == new Replacement("codecs", "Codex"), "Native save proposes the actual edited spelling");
                        Check(!Preferences.Load(Path.Combine(profile, "preferences.json")).Vocabulary.Any(x => x.Spoken == "codecs"), "Suggestion does not silently teach before approval");
                        Capture("draft-review.png");
                        Click("LearnCorrection_Click", new Button { Tag = suggestions.Single() });
                        Check(TextPipeline.Apply("open codecs", Preferences.Load(Path.Combine(profile, "preferences.json"))) == "open Codex", "Explicit Learn correction changes future formatting");
                        Click("ClearHistory_Click");
                        Check(history.Items.OfType<HistoryItem>().Count(x => x.Saved) == 1 && HistoryStore.Load(Path.Combine(profile, "saved-drafts.json")).Count == 1, "Clearing recent history preserves saved drafts");
                        ((TabControl)Field("Pages")).SelectedIndex = 3; await Task.Delay(50); window.UpdateLayout();
                        var families = (WrapPanel)Field("ModelFamilies");
                        Check(families.Children.Count == 2 && ((TextBlock)Field("SelectedModelSummary")).Text.Contains("Whisper Large"), "Family navigation and selected-model summary are above scrolling content");
                        Check(((WrapPanel)Field("ModelCards")).Children.Count == 6 && ((WrapPanel)Field("ModelCards")).Children.OfType<FrameworkElement>().All(x => x.ActualWidth <= 304), "Whisper renders six compact model cards");
                        Capture("models-whisper.png");
                        ((Button)families.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(20);
                        Check(((WrapPanel)Field("ModelCards")).Children.Count == 1 && ((TextBlock)Field("SelectedModelSummary")).Text.Contains("Whisper"), "Browsing Parakeet does not change the selected Whisper model");
                        Capture("models-parakeet.png");
                        ((TabControl)Field("Pages")).SelectedIndex = 1;
                        ((RadioButton)Field("SettingsAudioNav")).IsChecked = true;
                        window.UpdateLayout(); ((ScrollViewer)Field("SettingsScroll")).ScrollToVerticalOffset(0); await Task.Delay(30);
                        Capture("settings-spacing.png");
                        ((RadioButton)Field("SettingsGeneralNav")).IsChecked = true; window.UpdateLayout(); Capture("settings-sound.png");
                        Check(((FrameworkElement)Field("LiveTranscription")).Parent is StackPanel live && live.Margin.Bottom >= 14, "Live-description group has explicit bottom breathing room");
                        window.Width = 740; window.Height = 530; ((TabControl)Field("Pages")).SelectedIndex = 3; await Task.Delay(30); Capture("models-compact.png");
                        Check(((ScrollViewer)Field("ModelsScroll")).ScrollableWidth == 0, "Model family remains usable without horizontal scrolling at minimum size");
                        var starOrigin = starButton.TranslatePoint(new Point(), window);
                        Check(starOrigin.Y >= 0 && starOrigin.Y + starButton.ActualHeight <= window.ActualHeight && starOrigin.X + starButton.ActualWidth <= 210, "Sidebar repository actions remain inside the footer at minimum window size");
                        var capture = new FakeCapture(); window.RecorderForTests = () => new Recorder(_ => capture); window.PreparationForTests = _ => Task.CompletedTask;
                        var complete = string.Join("\n", Enumerable.Repeat("Bonjour Codex, keep the entire draft together.", 50));
                        var recognized = new TaskCompletionSource<string>(); window.RecognitionForTests = (_, _) => recognized.Task;
                        var cues = new List<FeedbackCue>(); var startCue = new TaskCompletionSource();
                        window.FeedbackForTests = cue => { cues.Add(cue); return cue == FeedbackCue.Start ? startCue.Task : Task.CompletedTask; };
                        var starting = window.BeginForTestsAsync(); await Task.Delay(10);
                        Check(cues.SequenceEqual(new[] { FeedbackCue.Start }) && !capture.Started, "Recording waits for the start cue to finish before microphone capture");
                        startCue.SetResult();
                        Check(await starting, "Native send-flow starts only a fake microphone");
                        var pcm = new byte[64000]; for(int i = 1; i < pcm.Length; i += 2) pcm[i] = 8; capture.Emit(pcm);
                        Invoke("FinishAndSendShortcut");
                        await Task.Delay(50);
                        Check(fakePaste.Pastes == 0 && fakePaste.Enters == 0, "No partial external text while final recognition is pending");
                        recognized.SetResult(complete);
                        for(var i = 0; i < 100 && fakePaste.Enters == 0; i++) await Task.Delay(20);
                        Check(fakePaste.Pastes == 1 && fakePaste.Enters == 1 && fakePaste.Text == complete, "Actual native Stop pipeline pastes one complete multiline draft and sends once after explicit Enter intent");
                        for(var i = 0; i < 100 && window.SessionForTests != SessionState.Inserted; i++) await Task.Delay(20);
                        Check(window.SessionForTests == SessionState.Inserted, "Native send-flow reaches inserted terminal state without duplicate retries");
                        for(var i = 0; i < 30 && !cues.Contains(FeedbackCue.Sent); i++) await Task.Delay(10);
                        Check(cues.Count(c => c == FeedbackCue.Sent) == 1 && !cues.Contains(FeedbackCue.Ready), "Verified explicit Enter completion chooses one Sent cue rather than duplicate success cues");
                        ((CheckBox)Field("SoundEffects")).IsChecked = false; Click("Preferences_Click");
                        Check(!Preferences.Load(Path.Combine(profile, "preferences.json")).SoundEffects, "Sound effects switch saves its off choice");
                        var priorCueCount = cues.Count; await (Task)Invoke("PlayFeedbackAsync", FeedbackCue.Open, CancellationToken.None)!;
                        Check(cues.Count == priorCueCount, "Disabled sound effects suppress playback requests");
                    }
                }
                catch(Exception ex) { failure = ex; File.WriteAllText(Path.Combine(output, "failure" + (reopen ? "-reopen" : "") + ".txt"), ex.ToString()); }
                finally { if (window != null) window.RequestQuit(); else app.Shutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(35))) throw new TimeoutException("Personalization UI did not exit");
        if (failure != null) throw new InvalidOperationException("Personalization fixture failed", failure);
        return new { Passed = checks.Count, Checks = checks, Reopen = reopen, MicrophoneOpened = false, ClipboardTouched = false, GlobalInputSent = false };
    }
    private sealed class NativeFlowPaste : ICompletedDraftTransport
    {
        public int Pastes, Enters; public string Text = "";
        public object? SaveClipboard() => null;
        public uint SetClipboard(string text) { Text = text; return 1; }
        public uint ClipboardSequence => 1;
        public void RestoreClipboard(object? previous) { }
        public bool Paste() { Pastes++; return true; }
        public bool Enter() { Enters++; return true; }
        public bool EnterHeld => false;
        public string? ReadText(DraftTarget target) => Pastes == 0 ? "" : Text;
        public Task Delay(int milliseconds) => Task.CompletedTask;
    }

}
