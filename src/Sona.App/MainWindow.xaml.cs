using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;
namespace Sona.App;

public partial class MainWindow : Window
{
    private readonly string preferencesPath;
    private readonly Preferences preferences;
    private readonly PreferencesLoadResult preferencesLoad;
    internal bool PreferencesAvailable => preferencesLoad.CanSave;
    private readonly Transcriber transcriber = new();
    private volatile bool windowClosed;
    private readonly Insertion insertion;
    private ActiveApplicationObserver? activeApplication;
    private readonly RecordingDestinationContext destinationContext = new();
    private static readonly DependencyPropertyKey ActiveAppNamePropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActiveAppName), typeof(string), typeof(MainWindow), new PropertyMetadata("No active app"));
    public static readonly DependencyProperty ActiveAppNameProperty = ActiveAppNamePropertyKey.DependencyProperty;
    public string ActiveAppName => (string)GetValue(ActiveAppNameProperty);
    private readonly EscapeGuard escape;
    private readonly Overlay overlay = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private readonly DispatcherTimer barPreview = new() { Interval = TimeSpan.FromSeconds(5) };
    private Hotkeys? hotkeys;
    private Recorder? recorder;
    private CancellationTokenSource? cancellation;
    private DictationSession? session;
    private DraftTarget? target;
    private TargetCaptureResult? recordingCapture;
    private int shortcutToggleMode;
    private int enterToSendEnabled;
    private bool submitOnComplete;
    private System.Windows.Forms.NotifyIcon? tray;
    private bool quitting;
    private bool rendering;
    private bool processing;
    private bool shutdownPending;
    private bool preparedInsertionTest;
    private bool preparing;
    internal Func<CancellationToken, Task>? PreparationForTests { get; set; }
    internal Func<Recorder>? RecorderForTests { get; set; }
    internal Overlay OverlayForTests => overlay;
    internal Task<bool> BeginForTestsAsync() => arguments.Contains("--test-ui") ? BeginRecordingAsync() : Task.FromResult(false);
    internal Task<bool> BeginShortcutForTestsAsync(bool hold) => BeginShortcutAsync(hold);
    internal void PrepareShortcutForTests() => PrepareTest_Click(this, new RoutedEventArgs());
    internal void CancelForTests() => RequestCancel();
    internal SessionState? SessionForTests => session?.State;
    private bool insertionTestHolding;
    private Task operation = Task.CompletedTask;
    private readonly string[] arguments;
    public MainWindow(string[] arguments) : this(arguments, null) { }
    internal MainWindow(string[] arguments, Insertion? testInsertion)
    {
        this.arguments = arguments;
        // UI previews never observe system focus or dispatch text input.
        insertion = arguments.Contains("--test-ui") ? testInsertion ?? new Insertion(() => null, () => false, _ => 0) : new();
        preferencesPath = Path.Combine(AppPaths.DataRoot, "preferences.json");
        preferencesLoad = Preferences.LoadWithStatus(preferencesPath); preferences = preferencesLoad.Value;
        InitializeComponent();
        CommandBindings.Add(new System.Windows.Input.CommandBinding(RepositoryLinks.Open, OpenRepository));
        escape = new EscapeGuard(RequestCancel);
        if (arguments.Contains("--test-ui")) { RecordButton.IsEnabled = false; StartWithWindows.IsEnabled = false; if (arguments.Contains("--compact")) { Width = 740; Height = 530; } }
        Vocabulary.Text = string.Join("\n", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Spoken + " = " + x.Written)));
        Snippets.Text = SnippetRules.Format(preferences.Snippets);
        SpeechLanguage.SelectedIndex = preferences.Language == "en" ? 1 : preferences.Language == "fr" ? 2 : 0;
        TranscriptionEngine.SelectedIndex = preferences.Engine == "FastCpu" ? 1 : 0;
        DraftStyle.SelectedIndex = preferences.Style == "Verbatim" ? 1 : preferences.Style == "Notes" ? 2 : 0;
        SoundEffects.IsChecked = preferences.SoundEffects; PasteCompletedDraft.IsChecked = preferences.PasteCompletedDraft; EnterToSend.IsChecked = preferences.EnterToSend; enterToSendEnabled = preferences.EnterToSend ? 1 : 0;
        LiveTranscription.IsChecked = preferences.LiveTranscription; AutoInsert.IsChecked = preferences.AutoInsert; CopyToClipboard.IsChecked = preferences.CopyToClipboard; CheckUpdates.IsChecked = preferences.CheckForUpdates; CloseToTray.IsChecked = preferences.CloseToTray; StartWithWindows.IsChecked = preferences.Startup;
        ShortcutMode.SelectedIndex = preferences.ShortcutMode == "Toggle" ? 1 : 0; KeepHistory.IsChecked = preferences.KeepHistory;
        shortcutToggleMode = preferences.ShortcutMode == "Toggle" ? 1 : 0; LoadHistory();
        ModelStatus.Text = transcriber.Available ? "Installed local runtime is ready." : "Run setup-runtime.ps1 to install the local runtime.";
        RefreshDevices();
        RefreshEditors(); RestoreTransferredDraft();
        RefreshModelCards();
        InitializeAppearance(); InitializeCleanup();
        overlay.ReviewRequested += OpenDraftFromOverlay; overlay.StopRequested += StartStop; overlay.CancelRequested += RequestCancel;
        barPreview.Tick += (_, _) => { barPreview.Stop(); if (recorder == null && !processing && !insertionTestHolding) overlay.Hide(); };
        if (!transcriber.Available) { Status.Text = "Local engine not installed"; StatusDetail.Text = "Run setup-runtime.ps1 before recording."; }
        SourceInitialized += (_, _) => InitializeRuntime();
        timer.Tick += (_, _) => { if (recorder == null) return; overlay.Update("Listening", "Esc to cancel", recorder.Elapsed, recorder.Level); if (recorder.Elapsed >= 120) StartStop(); else recorder.CheckHealth(); };
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.SessionSwitch += SessionChanged;
        Closed += (_, _) => { SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.SessionSwitch -= SessionChanged; Cancel(); windowClosed = true; soundFeedback.Dispose(); activeApplication?.Dispose(); escape.Dispose(); hotkeys?.Dispose(); insertion.Dispose(); overlay.Close(); livePolling?.Dispose(); tray?.Dispose(); cancellation?.Dispose(); cancellation = null; };
        Loaded += (_, _) => { if (!arguments.Contains("--start-hidden")) _ = PlayFeedbackAsync(FeedbackCue.Open); };
        Closing += ClosingWindow;
        InitializeSettingsActions();
        ProfileIdentity.Text = "Build " + AppVersion.BuildId + "\nPreferences: " + preferencesPath + "\n" + preferencesLoad.Status;
        if (!preferencesLoad.CanSave)
        {
            ProfileWarning.Text = preferencesLoad.Message; ProfileWarning.Visibility = Visibility.Visible;
            RecordButton.IsEnabled = false; StartWithWindows.IsEnabled = false;
        }
    }
    private void InitializeRuntime()
    {
        activeApplication = new ActiveApplicationObserver(Dispatcher, () => insertion.FocusEpoch);
        activeApplication.Changed += ApplyActiveApplication;
        activeApplication.Start();
        agentServer = new AgentServer(HandleAgentAsync); agentServer.Start();
        ThemeManager.ApplyTitleBar(this);
        InitializeUpdateChecks();
        if (!arguments.Contains("--test-ui") && preferencesLoad.CanSave)
        {
            try { Startup.MigrateLegacy(preferences.Startup); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { SettingsResult.Text = ex.Message; }
            InitializeProtection();
            modelIdleTimer.Start(); modelPreparation = WarmAllSelectedAsync();
            try { hotkeys = new Hotkeys(new WindowInteropHelper(this).Handle, BeginShortcutAsync, FinishShortcut, RequestCancel, () => { if (!processing) operation = InsertReadyAsync(); }, () => Volatile.Read(ref shortcutToggleMode) == 1 && !Volatile.Read(ref preparedInsertionTest), FinishAndSendShortcut, () => Volatile.Read(ref enterToSendEnabled) == 1 && !Volatile.Read(ref preparedInsertionTest) && insertion.IsForegroundCurrent(target)); ShortcutStatus.Text = "Ctrl + Win listener is ready. " + hotkeys.LegacyWarning; }
            catch (IOException ex) { Status.Text = "Shortcut unavailable"; StatusDetail.Text = ex.Message; ShortcutStatus.Text = ex.Message; }
            tray = new System.Windows.Forms.NotifyIcon { Text = "Sona local dictation", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Visible = true };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Sona", null, (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); _ = PlayFeedbackAsync(FeedbackCue.Open); }));
            menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(RequestQuit)); tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); _ = PlayFeedbackAsync(FeedbackCue.Open); });
        }
        var replayIndex = Array.IndexOf(arguments, "--replay");
        if (replayIndex >= 0 && replayIndex + 1 < arguments.Length) operation = ReplayAsync(arguments[replayIndex + 1]);
    }
    private void ApplyActiveApplication(ActiveApplication app)
    {
        destinationContext.Observe(app); SetValue(ActiveAppNamePropertyKey, app.Name); overlay.UpdateTarget(destinationContext.Label); overlay.UpdateAppIcon(destinationContext.Icon);
    }
    private async Task<bool> BeginShortcutAsync(bool hold)
    {
        if (preparedInsertionTest && !hold)
        {
            ShortcutStatus.Text = "Insertion test: hold Ctrl + Windows, then release. Double-tap does not use the microphone.";
            return false;
        }
        if (!hold && session?.State == SessionState.Recording) { StartStop(); return true; }
        if (processing || shutdownPending || session?.State == SessionState.Recording) return false;
        foreach (var name in new[] { "Wispr Flow", "WisprFlow", "Spokenly" })
        {
            var competitors = System.Diagnostics.Process.GetProcessesByName(name);
            var running = competitors.Length != 0;
            foreach (var process in competitors) process.Dispose();
            if (running) { ShowShortcutNotice("Shortcut conflict", "Quit " + name + " to use Sona"); return false; }
        }
        if (hold)
        {
            if (preparedInsertionTest && session?.State == SessionState.Ready)
            {
                var capture = CaptureDestination("test-shortcut");
                target = capture.Target;
                if (hotkeys?.CanBeginHold != true) return false;
                if (target == null) { ShowShortcutNotice("Test shortcut: field unavailable", capture.Detail); return false; }
                activeApplication?.Refresh(); destinationContext.Begin(target, Insertion.AppName(target), activeApplication?.Current?.Window == target.Window && activeApplication.Current.ProcessId == target.OwnerProcessId ? activeApplication.Current.Icon : null); overlay.UpdateTarget(destinationContext.Label); overlay.UpdateAppIcon(destinationContext.Icon);
                barPreview.Stop(); overlay.Update("Test shortcut", "No microphone"); overlay.ShowFor(target.Window);
                insertionTestHolding = true; return true;
            }
        }
        var begin = BeginRecordingAsync(hold ? () => hotkeys?.CanBeginHold == true : hotkeys?.IsToggleAdmission == true ? () => hotkeys?.CanBeginToggle == true : null);
        operation = begin; return await begin;
    }
    private void FinishAndSendShortcut()
    {
        if (recorder == null || processing || session?.State != SessionState.Recording || !Insertion.Same(target, insertion.Capture())) { hotkeys?.Recording(false); return; }
        submitOnComplete = true; StartStop();
    }
    private void FinishShortcut()
    {
        if (insertionTestHolding)
        {
            insertionTestHolding = false;
            if (!processing && target != null) operation = InsertReadyAsync(target);
        }
        else StartStop();
    }
    private async Task<bool> BeginRecordingAsync(Func<bool>? admission = null)
    {
        if (!preferencesLoad.CanSave) { StatusDetail.Text = preferencesLoad.Message; return false; }
        if (arguments.Contains("--test-ui") && RecorderForTests == null) return false;
        if (processing || shutdownPending || preferencesApplying || preparing) { StatusDetail.Text = "The previous operation is finishing. Try again in a moment."; return false; }
        if (session?.State is SessionState.Recording or SessionState.Transcribing) return false;
        if (transcriber.CleanupPending) { ReportError("A temporary recording still needs cleanup. Use Quit to retry before recording again."); return false; }
        if (transcriber.ResidentCleanupPending) { StatusDetail.Text = "The previous model still needs cleanup. Use Unload now before recording again."; return false; }
        if (!transcriber.AvailableFor(preferences)) { ReportError("Install the selected local engine before recording."); return false; }
        cancellation?.Dispose(); cancellation = new(); submitOnComplete = false;
        recordingCapture = CaptureDestination("recording");
        target = recordingCapture.Target;
        activeApplication?.Refresh(); destinationContext.Begin(target, Insertion.AppName(target), target != null && activeApplication?.Current?.Window == target.Window && activeApplication.Current.ProcessId == target.OwnerProcessId ? activeApplication.Current.Icon : null);
        barPreview.Stop();
        RetainCurrentDraft(); historySelection = null;
        preparedInsertionTest = insertionTestHolding = false;
        session = new(); session.Begin();
        var captureSession = session; var captureCancellation = cancellation;
        preparing = true;
        try
        {
            if (!arguments.Contains("--test-ui")) escape.Arm(true);
            if (admission != null && !admission()) { Cancel(); return false; }
            if (arguments.Contains("--test-ui") && PreparationForTests != null) await PreparationForTests(captureCancellation.Token);
            else if (protection != null)
            {
                Status.Text = "Preparing call protection";
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(captureCancellation.Token); deadline.CancelAfter(TimeSpan.FromSeconds(12));
                await protection.BeginOwnRecordingAsync(deadline.Token);
            }
            captureCancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(session, captureSession) || captureSession.State != SessionState.Recording || shutdownPending) return false;
            if (admission != null && !admission()) { Cancel(); return false; }
            await PlayFeedbackAsync(FeedbackCue.Start, captureCancellation.Token);
            captureCancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(session, captureSession) || captureSession.State != SessionState.Recording || shutdownPending) return false;
            if (admission != null && !admission()) { Cancel(); return false; }
            var capture = arguments.Contains("--test-ui") ? RecorderForTests!() : new Recorder(); recorder = capture;
            capture.UnexpectedStop += error => Dispatcher.BeginInvoke(() => { if (ReferenceEquals(recorder, capture)) { session?.Fail(error); ReportError(error); } });
            if (admission != null && !admission()) { Cancel(); return false; }
            capture.Start(preferences.Device);
            if (admission != null && !admission()) { Cancel(); return false; }
            hotkeys?.Recording(true); timer.Start();
            Status.Text = "Listening"; StatusDetail.Text = target == null ? recordingCapture.Detail : "Destination: " + Insertion.AppName(target) + ". Keep that composer focused. Esc cancels."; RecordButton.Content = "Stop";
            overlay.UpdateTarget(destinationContext.Label);
            overlay.UpdateAppIcon(destinationContext.Icon);
            overlay.Update("Listening", "Esc to cancel"); overlay.ShowFor(target?.Window ?? 0);
            BeginLiveDraft(capture, captureSession, captureCancellation.Token);
            return true;
        }
        catch (OperationCanceledException) { if (ReferenceEquals(session, captureSession)) Cancel(); return false; }
        catch (Exception ex) when (ex is IOException or NAudio.MmException or InvalidOperationException or System.Text.Json.JsonException) { if (ReferenceEquals(session, captureSession)) { recorder?.Dispose(); recorder = null; captureSession.Fail(ex.Message); ReportError(ex is IOException ? ex.Message : "Microphone or call protection unavailable. Check Settings and reconnect Discord."); } return false; }
        finally { preparing = false; if (recorder == null) protectionReleaseOperation = ReleaseOwnProtectionAsync(); }
    }
    private void StartStop() { if (!processing && session?.State == SessionState.Recording) { if (recorder == null) RequestCancel(); else operation = StopAsync(); } }
    private async Task StopAsync()
    {
        var current = session;
        if (current == null || !current.Stop() || recorder == null) return;
        FeedbackCue? completedFeedback = null;
        var active = recorder; recorder = null; livePolling?.Cancel();
        processing = true;
        timer.Stop(); RecordButton.Content = "Record";
        try
        {
            Status.Text = "Making your draft"; StatusDetail.Text = "Transcribing locally. Esc still cancels."; overlay.Update("Transcribing", "Esc cancels");
            var wav = await active.StopAsync(); active.Dispose();
            await ReleaseOwnProtectionAsync();
            // Drain the one in-flight preview before final recognition. No partial
            // requests queue behind the final request; cancellation stops both.
            try { await liveOperation; cancellation!.Token.ThrowIfCancellationRequested(); }
            catch { Array.Clear(wav); throw; }
            var original = await RecognizeAsync(wav, cancellation!.Token);
            if (current.State == SessionState.Cancelled || !ReferenceEquals(current, session)) return;
            ShowFinalRaw(original);
            var cleanupResult = await PolishAsync(original, cancellation!.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(current, session) || current.State == SessionState.Cancelled) return;
            if (!current.Complete(original, CompleteLiveDraft(original, cleanupResult.Text, cleanupResult.Applied))) { Render(current); Status.Text = "No draft to insert"; StatusDetail.Text = liveDraft?.ManuallyEdited == true ? "Your empty edit was kept. The original and cleanup suggestion remain available for review." : current.Error; return; }
            Render(current); AddHistory(current); completedFeedback = FeedbackCue.Ready;
            var clipboardDetail = cleanupResult.Applied ? " Polished locally." : "";
            if (preferences.CopyToClipboard)
            {
                try { Clipboard.SetText(current.Draft); clipboardDetail += " Copied to clipboard."; }
                catch (COMException) { clipboardDetail += " Clipboard is busy; use Copy draft to retry."; }
            }
            activeApplication?.Refresh();
            if ((preferences.AutoInsert || submitOnComplete) && target != null && !destinationContext.DestinationChanged && liveDraft?.ManuallyEdited != true)
            {
                overlay.Update("Preparing your draft", submitOnComplete ? "Paste, then Enter in the captured composer" : "Inserting the complete draft");
                var result = preferences.PasteCompletedDraft || submitOnComplete
                    ? await insertion.PasteAsync(target, current.Draft, submitOnComplete, cancellation!.Token)
                    : await insertion.InsertAsync(target, current.Draft, cancellation!.Token);
                if (result.Inserted) { current.MarkInserted(); MarkHistoryInserted(); }
                if (submitOnComplete && insertion.LastSubmissionAccepted) completedFeedback = FeedbackCue.Sent;
                Status.Text = result.Inserted ? "Draft inserted" : "Draft ready"; StatusDetail.Text = result.Detail + clipboardDetail;
            }
            else { Status.Text = "Draft ready"; StatusDetail.Text = (liveDraft?.ManuallyEdited == true ? "Your edits are protected. Review the final original and cleaned suggestion below." : target == null ? recordingCapture?.Detail ?? "The destination could not be captured. Your draft stays in Sona." : destinationContext.DestinationChanged ? "Focus changed. Your draft stayed in Sona instead of going into another field." : "Review it here, or copy it when you are ready.") + clipboardDetail; }
        }
        catch (OperationCanceledException) { current.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException or NAudio.MmException) { current.Fail(ex.Message); Status.Text = "Your draft was left untouched"; StatusDetail.Text = ex.Message; }
        finally
        {
            active.Dispose(); await ReleaseOwnProtectionAsync(); processing = false; escape.Arm(false); hotkeys?.Recording(false); overlay.Hide(); destinationContext.End();
            if (current.State == SessionState.Ready && !shutdownPending) ShowRetainedDraftNotice();
            if (completedFeedback.HasValue && current.State is SessionState.Ready or SessionState.Inserted) _ = PlayFeedbackAsync(completedFeedback.Value);
        }
    }
    private async Task InsertReadyAsync(DraftTarget? suppliedTarget = null)
    {
        if (shutdownPending || processing || recorder != null || session?.State != SessionState.Ready) { StatusDetail.Text = "Choose a new ready draft first. Already inserted drafts cannot be inserted twice."; return; }
        var current = session;
        var capture = suppliedTarget == null ? CaptureDestination("ready-draft") : new TargetCaptureResult(suppliedTarget, TargetCaptureStatus.Ready);
        var destination = capture.Target;
        if (destination == null) { Status.Text = "Draft ready"; StatusDetail.Text = capture.Detail; return; }
        activeApplication?.Refresh();
        if (suppliedTarget != null && destinationContext.DestinationChanged) { Status.Text = "Draft ready"; StatusDetail.Text = "Focus changed. Your draft stayed in Sona."; overlay.Hide(); destinationContext.End(); return; }
        cancellation?.Dispose(); cancellation = new();
        try { escape.Arm(true); } catch (IOException ex) { StatusDetail.Text = ex.Message; return; }
        processing = true;
        try
        {
            var result = preferences.PasteCompletedDraft ? await insertion.PasteAsync(destination, current.Draft, false, cancellation.Token) : await insertion.InsertAsync(destination, current.Draft, cancellation.Token);
            if (result.Inserted) { current.MarkInserted(); MarkHistoryInserted(); preparedInsertionTest = false; }
            Status.Text = result.Inserted ? "Draft inserted" : "Draft ready"; StatusDetail.Text = result.Detail;
        }
        catch (OperationCanceledException) { current.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        finally { processing = false; escape.Arm(false); hotkeys?.Recording(false); overlay.Hide(); destinationContext.End(); }
    }
    private void PrepareTest_Click(object sender, RoutedEventArgs e)
    {
        if (processing || recorder != null || preparing) return;
        RetainCurrentDraft(); historySelection = null;
        session = new(); session.Begin(); session.Stop();
        const string test = "Sona insertion test: bonjour Codex, foo_bar = 42. This is an unsent draft.";
        session.Complete(test, test); Render(session);
        ResetLiveReview(); preparedInsertionTest = true;
        Pages.SelectedIndex = 0;
        Status.Text = "Insertion test ready"; StatusDetail.Text = "Click an empty Discord or Codex composer, hold Ctrl + Windows briefly, then release. No microphone is used. Check the draft stays unsent.";
    }
    private async Task ReplayAsync(string path)
    {
        ResetLiveReview(); RetainCurrentDraft(); historySelection = null;
        // Deterministic file test. Never captures a microphone or inserts into any destination.
        processing = true;
        cancellation = new(); var current = session = new(); current.Begin(); current.Stop();
        try
        {
            escape.Arm(true);
            Status.Text = "Testing local transcription";
            var raw = await transcriber.RunAsync(await File.ReadAllBytesAsync(path), preferences, cancellation.Token);
            var polished = await PolishAsync(raw, cancellation.Token, "Sona");
            cancellation.Token.ThrowIfCancellationRequested();
            if (session != current || current.State == SessionState.Cancelled) return;
            current.Complete(raw, polished.Text); Render(current); AddHistory(current);
            Status.Text = "File test complete"; StatusDetail.Text = "No microphone was opened and nothing was inserted.";
            if (arguments.Contains("--test-ui"))
            {
                var output = Path.Combine(Path.GetDirectoryName(preferencesPath)!, "replay-result.json"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new { session.Original, session.Draft, State = session.State.ToString() }));
            }
        }
        catch (OperationCanceledException) { session.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { ReportError(ex.Message); }
        finally { processing = false; escape.Arm(false); }
    }
    private void Cancel()
    {
        if (windowClosed) return;
        barPreview.Stop();
        preparedInsertionTest = insertionTestHolding = false;
        destinationContext.End();
        try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
        session?.Cancel(); liveDraft?.Cancel(); livePolling?.Cancel(); timer.Stop(); recorder?.Dispose(); recorder = null;
        protectionReleaseOperation = ReleaseOwnProtectionAsync();
        escape.Arm(false); hotkeys?.Recording(false); overlay.Hide(); RecordButton.Content = "Record";
        Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted.";
        if (liveDraft != null) DraftStage.Text = "Cancelled · review text retained";
    }
    private void RequestCancel() { if (windowClosed) return; try { cancellation?.Cancel(); } catch (ObjectDisposedException) { return; } session?.Cancel(); if (!windowClosed && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(Cancel); }
    public void ReportError(string detail) { Cancel(); Status.Text = "Something needs attention"; StatusDetail.Text = detail; }
    private void Render(DictationSession current) { reviewBaseline = CleanedSuggestion.Text.Length > 0 ? CleanedSuggestion.Text : current.Draft; CorrectionSuggestions.ItemsSource = null; rendering = true; Original.Text = current.Original; Draft.Text = current.Draft; CopyButton.IsEnabled = !string.IsNullOrEmpty(current.Draft); rendering = false; }
    private void Record_Click(object sender, RoutedEventArgs e) { if (session?.State == SessionState.Recording) StartStop(); else operation = BeginRecordingAsync(); }
    private void Draft_Changed(object sender, TextChangedEventArgs e) { if (!rendering)
        {
            session?.Edit(Draft.Text);
            if (liveDraft != null && session?.State is SessionState.Recording or SessionState.Transcribing)
            { liveDraft.Edit(Draft.Text); DraftStage.Text = "Your edits protected"; }
        } if (CopyButton != null) CopyButton.IsEnabled = !string.IsNullOrWhiteSpace(Draft.Text); }
    private void Copy_Click(object sender, RoutedEventArgs e) { try { Clipboard.SetText(Draft.Text); StatusDetail.Text = "Draft copied at your request."; } catch (System.Runtime.InteropServices.COMException) { StatusDetail.Text = "The clipboard is busy. Try again."; } }
    private void Original_Click(object sender, RoutedEventArgs e) { Draft.Text = Original.Text; }
    private void Clear_Click(object sender, RoutedEventArgs e) { Cancel(); ResetLiveReview(); session = null; historySelection = null; Draft.Clear(); Original.Clear(); Status.Text = "Ready to dictate"; StatusDetail.Text = "Your draft was cleared. History is still available."; }
    private void ShowShortcutNotice(string status, string detail) { Status.Text = status; StatusDetail.Text = detail; ShortcutStatus.Text = detail; barPreview.Stop(); overlay.Update(status, detail); overlay.ShowFor(0); barPreview.Start(); }
    private void Vocabulary_Click(object sender, RoutedEventArgs e) { if (processing || recorder != null) { VocabularyResult.Text = "Finish or cancel the current dictation before saving."; return; } try { SaveVocabulary(VocabularyRules.Parse(Vocabulary.Text)); VocabularyResult.Text = "Vocabulary saved on this device."; } catch (Exception ex) when (ex is FormatException or IOException) { VocabularyResult.Text = ex.Message; } }
    private void Snippets_Click(object sender, RoutedEventArgs e) { if (processing || recorder != null) { SnippetResult.Text = "Finish or cancel the current dictation before saving."; return; } try { SaveSnippets(SnippetRules.Parse(Snippets.Text)); SnippetResult.Text = "Snippets saved on this device."; } catch (Exception ex) when (ex is FormatException or IOException) { SnippetResult.Text = ex.Message; } }
    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SettingsBusy) { SettingsResult.Text = "Finish or cancel your current operation before changing preferences."; return; }
            var priorKeepHistory = preferences.KeepHistory;
            var next = ReadGeneralPreferences();
            var startupChanged = next.Startup != preferences.Startup;
            if (startupChanged) Startup.Set(next.Startup);
            try { next.Save(preferencesPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                if (startupChanged)
                {
                    try { Startup.Set(preferences.Startup); }
                    catch (Exception restore) when (restore is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    { throw new IOException("Preferences could not be saved, and Windows startup could not be restored. Check Launch at startup before trying again.", restore); }
                }
                throw;
            }
            // Keep active preferences committed: a failed Save must not leak
            // unsaved form values into a later theme-only save or dictation.
            preferences.Language = next.Language; preferences.Engine = next.Engine; preferences.Style = next.Style; preferences.Device = next.Device;
            preferences.SoundEffects = next.SoundEffects; preferences.PasteCompletedDraft = next.PasteCompletedDraft; preferences.EnterToSend = next.EnterToSend; Volatile.Write(ref enterToSendEnabled, next.EnterToSend ? 1 : 0);
            preferences.LiveTranscription = next.LiveTranscription; preferences.AutoInsert = next.AutoInsert; preferences.CopyToClipboard = next.CopyToClipboard; preferences.CheckForUpdates = next.CheckForUpdates; preferences.CloseToTray = next.CloseToTray; preferences.Startup = next.Startup;
            preferences.ShortcutMode = next.ShortcutMode; preferences.KeepHistory = next.KeepHistory; preferences.Theme = next.Theme; preferences.RecordingDisplay = next.RecordingDisplay; preferences.ModelIdleMinutes = next.ModelIdleMinutes;
            preferences.CleanupEnabled = next.CleanupEnabled; preferences.CleanupTone = next.CleanupTone; preferences.CleanupEngine = next.CleanupEngine; preferences.CleanupIdleMinutes = next.CleanupIdleMinutes;
            ThemeManager.Apply(preferences.Theme); overlay.SetDisplay(preferences.RecordingDisplay);
            RefreshModelCards(); modelPreparation = WarmAllSelectedAsync();
            Volatile.Write(ref shortcutToggleMode, preferences.ShortcutMode == "Toggle" ? 1 : 0); hotkeys?.ResetBaseline();
            if (!priorKeepHistory && preferences.KeepHistory)
            {
                try { var saved = HistoryStore.Load(HistoryPath); var combined = historyEntries.Where(x => x.Saved).Concat(HistoryStore.Bounded(historyEntries.Where(x => !x.Saved).Concat(saved).Where(x => !historyEntries.Any(p => p.Saved && p.Id == x.Id)))).ToList(); historyEntries.Clear(); historyEntries.AddRange(combined); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { historyStorageFailed = true; HistoryResult.Text = "Existing history could not be read and was preserved."; }
            }
            SaveHistory(); RefreshHistory();
            SettingsResult.Text = ModeResult.Text = ModelResult.Text = "Preferences saved.";
            if (!preferences.KeepHistory && File.Exists(HistoryPath)) HistoryResult.Text = "Saving is off. Use Clear history to remove previously saved text.";
            StatusDetail.Text = preferences.ShortcutMode == "Toggle" ? "Press Ctrl + Windows to start, then press it again to finish. Esc cancels." : "Hold Ctrl + Windows while you speak. Release to finish. Double-tap for hands-free; tap again to finish.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { SettingsResult.Text = ex.Message; }
        finally { RefreshSettingsActions(); }
    }
    private sealed record DeviceOption(int Id, string Name) { public override string ToString() => Name; }
    private void RefreshDevices()
    {
        Device.Items.Clear(); Device.Items.Add(new DeviceOption(-1, "System default"));
        try { foreach (var device in Recorder.Devices()) Device.Items.Add(new DeviceOption(device.Id, device.Name)); }
        catch (Exception ex) when (ex is NAudio.MmException or InvalidOperationException) { SettingsResult.Text = "Microphone list is unavailable. Windows default remains selected."; }
        var selected = Device.Items.OfType<DeviceOption>().FirstOrDefault(x => x.Id == preferences.Device);
        if (selected == null) preferences.Device = -1;
        Device.SelectedItem = selected ?? Device.Items[0];
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();
    private void PreviewBar_Click(object sender, RoutedEventArgs e) { if (recorder != null || processing || insertionTestHolding) return; barPreview.Stop(); barPreview.Interval = TimeSpan.FromSeconds(5); overlay.UpdateTranscript("", ""); overlay.UpdateTarget("Preview"); overlay.Update("Voice bar preview", "No microphone"); overlay.ShowFor(new WindowInteropHelper(this).Handle); barPreview.Start(); }
    private void Quit_Click(object sender, RoutedEventArgs e) => RequestQuit();
    public void RequestQuit() => Dispatcher.BeginInvoke(() => _ = QuitAsync());
    // A failed preparation task is already finished. Observe its error, then
    // still run the owning resource cleanup below. A genuinely pending task
    // retains the bounded wait and keeps Quit retryable instead of hiding it.
    private static async Task DrainForQuitAsync(Task pending)
    {
        try { await pending.WaitAsync(TimeSpan.FromSeconds(7)); }
        catch (Exception) when (pending.IsFaulted || pending.IsCanceled) { _ = pending.Exception; }
    }
    private async Task QuitAsync()
    {
        if (shutdownPending) return;
        shutdownPending = true; IsEnabled = false; Cancel();
        modelLifetime.Cancel(); modelIdleTimer.Stop();
        try { await DrainForQuitAsync(liveOperation); }
        catch (TimeoutException) { ShowCleanupFailure("Waiting for live preview cleanup", "Try Quit again shortly."); return; }
        try { await DrainForQuitAsync(operation); }
        catch (TimeoutException) { ShowCleanupFailure("Waiting for local cleanup", "Windows is still releasing worker files. Try Quit again shortly."); return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException) { /* Always retry recorded cleanup failures below, even when the operation faulted. */ }
        try { await transcriber.RetryCleanupAsync(); }
        catch (ScratchCleanupException ex) { ShowCleanupFailure("Temporary recording needs cleanup", ex.Message + " Try Quit again after the file is released."); return; }
        if (transcriber.CleanupPending) { ShowCleanupFailure("Temporary recording needs cleanup", "Recording files remain on this computer. Try Quit again to retry cleanup."); return; }
        try { await DrainForQuitAsync(modelPreparation); await transcriber.UnloadAsync().WaitAsync(TimeSpan.FromSeconds(7)); }
        catch (Exception ex) when (ex is IOException or TimeoutException) { ShowCleanupFailure("Model memory needs cleanup", "Try Quit again once the local model is released."); return; }
        try { await DrainForQuitAsync(cleanupPreparation); await cleanupModel.UnloadAsync().WaitAsync(TimeSpan.FromSeconds(7)); }
        catch (Exception ex) when (ex is IOException or TimeoutException) { ShowCleanupFailure("Cleanup memory needs attention", "Try Quit again once the cleanup model is released."); return; }
        cleanupInstallCancellation?.Cancel();
        try { await DrainForQuitAsync(cleanupInstallOperation); }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or System.Net.Http.HttpRequestException) { }
        catch (TimeoutException) { ShowCleanupFailure("Waiting for cleanup setup", "Try Quit again shortly."); return; }
        try { await CleanupAssets.RetryCleanupAsync(); }
        catch (IOException) { ShowCleanupFailure("Cleanup setup needs attention", "A setup file is still locked. Try Quit again shortly."); return; }
        if (CleanupAssets.CleanupPending) { ShowCleanupFailure("Cleanup setup needs attention", "Try Quit again to release remaining setup files."); return; }
        modelDownloadCancellation?.Cancel();
        try { await DrainForQuitAsync(modelDownloadOperation); }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or System.Net.Http.HttpRequestException) { }
        catch (TimeoutException) { ShowCleanupFailure("Waiting for model download cleanup", "Try Quit again shortly."); return; }
        try { await modelStore.RetryCleanupAsync(); await ParakeetAssets.RetryCleanupAsync().WaitAsync(TimeSpan.FromSeconds(7)); }
        catch (IOException) { ShowCleanupFailure("Partial model download needs cleanup", "A model file is still locked. Try Quit again shortly."); return; }
        catch (TimeoutException) { ShowCleanupFailure("Parakeet setup needs cleanup", "Try Quit again after the runtime files finish releasing."); return; }
        if (agentServer != null) { await agentServer.DisposeAsync(); agentServer = null; }
        if (protection != null)
        {
            try { await DrainForQuitAsync(protectionReleaseOperation); using var restoreDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(7)); await protection.RestoreForQuitAsync(restoreDeadline.Token); }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or TimeoutException) { ShowCleanupFailure("Discord restoration needs attention", "Sona could not confirm restoration. Reconnect Discord or pause protection, then try Quit again."); return; }
            protectionTimer.Stop(); protectionHotkey?.Dispose(); await protection.DisposeAsync(); protection = null;
        }
        quitting = true; Close(); Application.Current.Shutdown();
    }
    private void ShowCleanupFailure(string status, string detail) { if (modelLifetime.IsCancellationRequested) { modelLifetime.Dispose(); modelLifetime = new(); } if (!arguments.Contains("--test-ui")) modelIdleTimer.Start(); shutdownPending = false; IsEnabled = true; Status.Text = status; StatusDetail.Text = detail; ShowActivated = false; WindowState = WindowState.Normal; Show(); }
    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (quitting) return;
        e.Cancel = true;
        if (tray != null && preferences.CloseToTray && !arguments.Contains("--test-ui")) { if (recorder != null || processing || insertionTestHolding || preparing) Cancel(); else { barPreview.Stop(); overlay.Hide(); } Dispatcher.BeginInvoke(Hide); }
        else RequestQuit();
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(() => { if (e.Mode == PowerModes.Suspend) Cancel(); hotkeys?.ResetBaseline(); });
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(() => { if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect) Cancel(); hotkeys?.ResetBaseline(); });
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
