using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Sona.App;
using Sona.Core;

internal static class HistoryControlsFixture
{
    public static object Run(string output, bool retentionOff = false)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh history-controls proof directory.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile"); var settingsPath = Path.Combine(profile, "preferences.json");
        var historyPath = Path.Combine(profile, "history.json"); var savedPath = Path.Combine(profile, "saved-drafts.json");
        var priorData = Environment.GetEnvironmentVariable("SONA_DATA"); var priorTransfer = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        Environment.SetEnvironmentVariable("SONA_DATA", profile); Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
        new Preferences { KeepHistory = !retentionOff, SoundEffects = false, AutoInsert = false }.Save(settingsPath);
        var initial = Enumerable.Range(0, 13).Select(i => new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now.AddMinutes(-i), "Synthetic original " + i, "Synthetic history " + i)).ToArray();
        HistoryStore.Save(historyPath, initial);
        var saved = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Synthetic saved original", "Synthetic explicit saved draft", Saved: true);
        HistoryStore.Save(savedPath, [saved]); var savedBytes = File.ReadAllBytes(savedPath);
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string text) { if (ok) checks.Add(text); else failures.Add(text); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var windows = new List<MainWindow>();
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                try
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    var window = new MainWindow(["--test-ui"]); windows.Add(window);
                    object? Invoke(string method, params object[] args) => typeof(MainWindow).GetMethod(method, flags)!.Invoke(window, args);
                    T Field<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(window)!;
                    void Set(string name, object value) => typeof(MainWindow).GetField(name, flags)!.SetValue(window, value);
                    var entries = Field<List<HistoryItem>>("historyEntries"); var preferences = Field<Preferences>("preferences");
                    var draft = (TextBox)window.FindName("Draft"); var original = (TextBox)window.FindName("Original");
                    var clear = (Button)window.FindName("ClearHistoryButton"); var confirm = (Button)window.FindName("ConfirmHistoryClear");
                    var active = Session("Synthetic completed dictation");
                    Set("processing", true); Set("session", active); Invoke("Render", active); Invoke("AddHistory", active);
                    Check(!clear.IsEnabled && entries.Count(x => !x.Saved) == 14, "Fourteen recent records are retained while clearing is guarded during transcription");
                    Set("processing", false); Invoke("RefreshSettingsActions");
                    Check(clear.IsEnabled, "The actual Clear button becomes enabled after dictation returns to idle without changing a filter");
                    async Task Click(Button button)
                    {
                        if (!button.IsEnabled) throw new IOException("The real History action is disabled: " + button.Name);
                        var peer = new ButtonAutomationPeer(button); var provider = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!;
                        provider.Invoke(); await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                    if (clear.IsEnabled)
                    {
                        await Click(clear);
                        Check(((FrameworkElement)window.FindName("HistoryClearConfirmation")).Visibility == Visibility.Visible && entries.Count == 15, "Real button invocation displays the fourteen-record confirmation without deleting yet");
                        await Click(confirm);
                        Invoke("RetainCurrentDraft");
                        Check(entries.Count == 1 && entries.Single().Id == saved.Id && HistoryStore.Load(historyPath).Count == 0, "Confirming clears all fourteen previewed recent records in memory and on disk");
                        Check(File.ReadAllBytes(savedPath).SequenceEqual(savedBytes) && draft.Text == active.Draft && original.Text == active.Original, "Bulk deletion preserves saved drafts and the current editable draft and original");
                    }
                    var pause = window.FindName("PauseHistoryButton") as Button;
                    var pausedProperty = typeof(Preferences).GetProperty("HistoryPaused");
                    Check(pause != null, "History exposes its own Pause history and Resume history action");
                    Check(pausedProperty != null && (bool)pausedProperty.GetValue(new Preferences())! == false, "A new profile has an explicit non-paused capture preference");
                    if (pause == null || pausedProperty == null) return;
                    bool Paused(Preferences value) => (bool)pausedProperty.GetValue(value)!;
                    var persistedSound = Preferences.Load(settingsPath).SoundEffects;
                    var soundChoice = (CheckBox)window.FindName("SoundEffects"); soundChoice.IsChecked = !persistedSound;
                    Set("processing", true); Invoke("RefreshSettingsActions");
                    Check(pause.IsEnabled, "History can be paused during processing without cancelling dictation");
                    await Click(pause);
                    Check(Paused(preferences) && Paused(Preferences.Load(settingsPath)) && pause.Content?.ToString() == "Resume history", "Pausing persists the choice and immediately exposes Resume history");
                    Check(soundChoice.IsChecked == !persistedSound && Preferences.Load(settingsPath).SoundEffects == persistedSound, "Pausing neither commits nor discards unrelated unsaved settings");
                    var beforeIds = entries.Select(x => x.Id).ToArray(); var beforeBytes = File.ReadAllBytes(historyPath);
                    var pausedSession = Session("Synthetic private paused dictation"); Set("session", pausedSession); Invoke("Render", pausedSession); Invoke("AddHistory", pausedSession);
                    draft.Text += " manually edited"; Invoke("RetainCurrentDraft"); Invoke("MarkHistoryInserted"); Invoke("SaveHistory");
                    Check(entries.Select(x => x.Id).SequenceEqual(beforeIds) && File.ReadAllBytes(historyPath).SequenceEqual(beforeBytes), "Paused completion, edits, retain, inserted marking and persistence add no recent history or disk writes");
                    Check(draft.Text.EndsWith("manually edited", StringComparison.Ordinal) && Field<bool>("processing"), "Pausing leaves the working draft and active dictation state usable");
                    var reopened = new MainWindow(["--test-ui"]); windows.Add(reopened);
                    Check(((Button)reopened.FindName("PauseHistoryButton")).Content?.ToString() == "Resume history", "A new native window restores the persisted paused choice");
                    var pendingPaused = new DictationSession(); pendingPaused.Begin(); pendingPaused.Stop(); Set("session", pendingPaused);
                    Invoke("RefreshSettingsActions");
                    await Click(pause);
                    pendingPaused.Complete("Synthetic pending original", "Synthetic pending paused draft"); Invoke("Render", pendingPaused); Invoke("AddHistory", pendingPaused);
                    Check(!entries.Any(x => x.Id == pendingPaused.Id) && File.ReadAllBytes(historyPath).SequenceEqual(beforeBytes), "Resuming before pending transcription completes never collects the paused session");
                    Set("processing", false); Invoke("RefreshSettingsActions");
                    Invoke("RetainCurrentDraft");
                    Check(!Paused(preferences) && !Paused(Preferences.Load(settingsPath)) && entries.Select(x => x.Id).SequenceEqual(beforeIds), "Resuming starts future capture without retroactively retaining the paused draft");
                    Invoke("RestoreHistoryDraft", entries.Single(x => x.Id == saved.Id)); draft.Text = "Synthetic saved edit after resume"; Invoke("RetainCurrentDraft");
                    Check(HistoryStore.Load(savedPath).Single(x => x.Id == saved.Id).Draft == draft.Text, "Opening a different saved draft after resume retains its edits normally");
                    var resumed = Session("Synthetic resumed dictation"); Set("session", resumed); Invoke("Render", resumed); Invoke("AddHistory", resumed);
                    Check(entries.Count(x => x.Id == resumed.Id) == 1 && HistoryStore.Load(historyPath).Count(x => x.Id == resumed.Id) == (retentionOff ? 0 : 1) && !entries.Any(x => x.Id == pausedSession.Id), "The next dictation after resume is retained once, with disk retention respecting its separate preference");
                    var deleteMethod = typeof(MainWindow).GetMethod("DeleteHistoryDraft_Click", flags);
                    Check(deleteMethod != null, "Recent rows expose individual draft deletion");
                    if (deleteMethod != null)
                    {
                        deleteMethod.Invoke(window, [new Button { Tag = entries.Single(x => x.Id == resumed.Id) }, new RoutedEventArgs()]);
                        Check(((FrameworkElement)window.FindName("HistoryClearConfirmation")).Visibility == Visibility.Visible, "Individual deletion previews one exact recent draft");
                        await Click(confirm);
                        Invoke("RetainCurrentDraft");
                        Check(!entries.Any(x => !x.Saved) && !HistoryStore.Load(historyPath).Any(x => x.Id == resumed.Id) && draft.Text == resumed.Draft, "Individual deletion removes the recent record without clearing the working editor");
                    }
                    Invoke("RestoreHistoryDraft", entries.Single(x => x.Id == saved.Id)); draft.Text = "Synthetic saved edit after deletion"; Invoke("RetainCurrentDraft");
                    Check(HistoryStore.Load(savedPath).Single(x => x.Id == saved.Id).Draft == draft.Text, "Deletion suppression does not discard edits to a different opened saved draft");
                    var choiceBefore = Paused(preferences); var settingsBytes = File.ReadAllBytes(settingsPath);
                    using (var locked = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) await Click(pause);
                    Check(Paused(preferences) == choiceBefore && File.ReadAllBytes(settingsPath).SequenceEqual(settingsBytes) && ((TextBlock)window.FindName("HistoryResult")).Text.Contains("could not", StringComparison.OrdinalIgnoreCase), "A blocked preferences write retains the prior pause choice and reports the failure");
                    Check(windows.All(w => !w.IsVisible && !w.IsLoaded) && new[] { "recorder", "hotkeys", "activeApplication", "agentServer" }.All(name => typeof(MainWindow).GetField(name, flags)!.GetValue(window) == null), "The regression uses unshown isolated windows without microphone, hotkeys, focus observers or servers");
                }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { foreach (var window in windows) window.RequestQuit(); app.Shutdown(); }
            }));
            app.Run();
        }) { IsBackground = true, Name = "Sona isolated history controls" };
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("History controls exceeded thirty seconds."); }
        finally { Environment.SetEnvironmentVariable("SONA_DATA", priorData); Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", priorTransfer); }
        var report = new { Version = AppVersion.Number, BuildId = AppVersion.BuildId, AppAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MainWindow).Assembly.Location))), Checks = checks, Failures = failures, RetentionOff = retentionOff, WindowShown = false, MicrophoneOpened = false, GlobalInputSent = false, ProductionProfileUsed = false };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
    private static DictationSession Session(string text) { var value = new DictationSession(); value.Begin(); value.Stop(); value.Complete(text, text); return value; }
}
