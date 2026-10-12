using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Murmur.App.Audio;
using Murmur.App.Windows;
using Murmur.Core;
namespace Murmur.App;

public partial class MainWindow : Window
{
    private readonly string preferencesPath;
    private readonly Preferences preferences;
    private readonly Transcriber transcriber = new();
    private readonly Insertion insertion = new();
    private readonly EscapeGuard escape;
    private readonly Overlay overlay = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private Hotkeys? hotkeys;
    private Recorder? recorder;
    private CancellationTokenSource? cancellation;
    private DictationSession? session;
    private DraftTarget? target;
    private readonly List<DictationSession> history = [];
    private System.Windows.Forms.NotifyIcon? tray;
    private bool quitting;
    private bool rendering;
    private bool processing;
    private bool shutdownPending;
    private Task operation = Task.CompletedTask;
    private readonly string[] arguments;
    public MainWindow(string[] arguments)
    {
        this.arguments = arguments;
        preferencesPath = Path.Combine(Environment.GetEnvironmentVariable("MURMUR_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Murmur"), "preferences.json");
        preferences = Preferences.Load(preferencesPath);
        InitializeComponent();
        escape = new EscapeGuard(RequestCancel);
        if (arguments.Contains("--test-ui")) RecordButton.IsEnabled = false;
        Vocabulary.Text = string.Join("\n", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Spoken + " = " + x.Written)));
        Snippets.Text = string.Join("\n", preferences.Snippets.Select(x => x.Trigger + " = " + x.Expansion.Replace("\n", "\\n")));
        SpeechLanguage.SelectedIndex = preferences.Language == "en" ? 1 : preferences.Language == "fr" ? 2 : 0;
        DraftStyle.SelectedIndex = preferences.Style == "Verbatim" ? 1 : preferences.Style == "Notes" ? 2 : 0;
        AutoInsert.IsChecked = preferences.AutoInsert; CloseToTray.IsChecked = preferences.CloseToTray; StartWithWindows.IsChecked = preferences.Startup;
        RefreshDevices();
        if (!transcriber.Available) { Status.Text = "Local engine not installed"; StatusDetail.Text = "Run setup-runtime.ps1 before recording."; }
        SourceInitialized += (_, _) => InitializeRuntime();
        timer.Tick += (_, _) => { if (recorder == null) return; overlay.Update("Listening", "Esc to cancel", recorder.Elapsed); if (recorder.Elapsed >= 120) StartStop(); else recorder.CheckHealth(); };
        SystemEvents.PowerModeChanged += PowerChanged;
        Closed += (_, _) => { SystemEvents.PowerModeChanged -= PowerChanged; Cancel(); escape.Dispose(); hotkeys?.Dispose(); insertion.Dispose(); overlay.Close(); tray?.Dispose(); cancellation?.Dispose(); };
        Closing += ClosingWindow;
    }
    private void InitializeRuntime()
    {
        var dark = 1; _ = DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        if (!arguments.Contains("--test-ui"))
        {
            try { hotkeys = new Hotkeys(new WindowInteropHelper(this).Handle, hold => { if (!hold && session?.State == SessionState.Recording) StartStop(); else BeginRecording(); }, StartStop, RequestCancel, () => { if (!processing) operation = InsertReadyAsync(); }); }
            catch (IOException ex) { Status.Text = "Shortcut unavailable"; StatusDetail.Text = ex.Message; }
            tray = new System.Windows.Forms.NotifyIcon { Text = "Murmur · local dictation", Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application, Visible = true };
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Murmur", null, (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }));
            menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(RequestQuit)); tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
        }
        var replayIndex = Array.IndexOf(arguments, "--replay");
        if (replayIndex >= 0 && replayIndex + 1 < arguments.Length) operation = ReplayAsync(arguments[replayIndex + 1]);
    }
    private void BeginRecording()
    {
        if (processing || shutdownPending) { StatusDetail.Text = "The previous worker is finishing. Try again in a moment."; return; }
        if (session?.State is SessionState.Recording or SessionState.Transcribing) return;
        if (transcriber.CleanupPending) { ReportError("A temporary recording still needs cleanup. Use Quit to retry before recording again."); return; }
        if (!transcriber.Available) { ReportError("Install the local engine before recording."); return; }
        cancellation?.Dispose(); cancellation = new();
        target = insertion.Capture();
        session = new(); session.Begin();
        try
        {
            escape.Arm(true);
            var capture = new Recorder(); recorder = capture;
            capture.UnexpectedStop += error => Dispatcher.BeginInvoke(() => { if (ReferenceEquals(recorder, capture)) { session?.Fail(error); ReportError(error); } });
            capture.Start(preferences.Device);
            hotkeys?.Recording(true); timer.Start();
            Status.Text = "Listening"; StatusDetail.Text = "Speak your thought. Press Esc to cancel."; RecordButton.Content = "Stop";
            overlay.Update("Listening", "Esc to cancel"); overlay.Show();
        }
        catch (Exception ex) when (ex is IOException or NAudio.MmException or InvalidOperationException) { recorder?.Dispose(); recorder = null; session.Fail(ex.Message); ReportError(ex is IOException ? ex.Message : "Microphone unavailable. Check Windows microphone permissions and the selected device."); }
    }
    private void StartStop() { if (!processing && recorder != null && session?.State == SessionState.Recording) operation = StopAsync(); }
    private async Task StopAsync()
    {
        var current = session;
        if (current == null || !current.Stop() || recorder == null) return;
        var active = recorder; recorder = null;
        processing = true;
        timer.Stop(); RecordButton.Content = "Record";
        try
        {
            Status.Text = "Making your draft"; StatusDetail.Text = "Transcribing locally. Esc still cancels."; overlay.Update("Transcribing", "On this device · Esc to cancel");
            var wav = await active.StopAsync(); active.Dispose();
            var original = await transcriber.RunAsync(wav, preferences, cancellation!.Token);
            if (current.State == SessionState.Cancelled || !ReferenceEquals(current, session)) return;
            if (!current.Complete(original, TextPipeline.Apply(original, preferences))) { Render(current); return; }
            Render(current); AddHistory(current);
            if (preferences.AutoInsert && target != null)
            {
                var result = await insertion.InsertAsync(target, current.Draft, cancellation!.Token);
                if (result.Inserted) current.MarkInserted();
                Status.Text = result.Inserted ? "Draft inserted" : "Draft ready"; StatusDetail.Text = result.Detail;
            }
            else { Status.Text = "Draft ready"; StatusDetail.Text = "Review it here, or copy it when you are ready."; }
        }
        catch (OperationCanceledException) { current.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException or NAudio.MmException) { current.Fail(ex.Message); Status.Text = "Your draft was left untouched"; StatusDetail.Text = ex.Message; }
        finally { active.Dispose(); processing = false; escape.Arm(false); hotkeys?.Recording(false); overlay.Hide(); }
    }
    private async Task InsertReadyAsync()
    {
        if (shutdownPending || processing || recorder != null || session?.State != SessionState.Ready) { StatusDetail.Text = "Choose a new ready draft first. Already inserted drafts cannot be inserted twice."; return; }
        var current = session; var destination = insertion.Capture();
        cancellation?.Dispose(); cancellation = new();
        try { escape.Arm(true); } catch (IOException ex) { StatusDetail.Text = ex.Message; return; }
        processing = true;
        try
        {
            var result = await insertion.InsertAsync(destination, current.Draft, cancellation.Token);
            if (result.Inserted) current.MarkInserted();
            Status.Text = result.Inserted ? "Draft inserted" : "Draft ready"; StatusDetail.Text = result.Detail;
        }
        catch (OperationCanceledException) { current.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        finally { processing = false; escape.Arm(false); hotkeys?.Recording(false); }
    }
    private void PrepareTest_Click(object sender, RoutedEventArgs e)
    {
        if (processing || recorder != null) return;
        session = new(); session.Begin(); session.Stop();
        const string test = "Murmur insertion test: bonjour Codex, foo_bar = 42. This is an unsent draft.";
        session.Complete(test, test); Render(session);
        Status.Text = "Insertion test ready"; StatusDetail.Text = "Focus an empty Codex draft and press Ctrl+Alt+Shift+V. No microphone is used. Check the draft stays unsent.";
    }
    private async Task ReplayAsync(string path)
    {
        // Deterministic file test. Never captures a microphone or inserts into any destination.
        processing = true;
        cancellation = new(); session = new(); session.Begin(); session.Stop();
        try
        {
            Status.Text = "Testing local transcription";
            var raw = await transcriber.RunAsync(await File.ReadAllBytesAsync(path), preferences, cancellation.Token);
            session.Complete(raw, TextPipeline.Apply(raw, preferences)); Render(session); AddHistory(session);
            Status.Text = "File test complete"; StatusDetail.Text = "No microphone was opened and nothing was inserted.";
            if (arguments.Contains("--test-ui"))
            {
                var output = Path.Combine(Path.GetDirectoryName(preferencesPath)!, "replay-result.json"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(new { session.Original, session.Draft, State = session.State.ToString() }));
            }
        }
        catch (OperationCanceledException) { session.Cancel(); Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted."; }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { ReportError(ex.Message); }
        finally { processing = false; }
    }
    private void Cancel()
    {
        cancellation?.Cancel(); session?.Cancel(); timer.Stop(); recorder?.Dispose(); recorder = null;
        escape.Arm(false); hotkeys?.Recording(false); overlay.Hide(); RecordButton.Content = "Record";
        Status.Text = "Cancelled"; StatusDetail.Text = "Nothing was inserted.";
    }
    private void RequestCancel() { cancellation?.Cancel(); session?.Cancel(); Dispatcher.BeginInvoke(Cancel); }
    public void ReportError(string detail) { Cancel(); Status.Text = "Something needs attention"; StatusDetail.Text = detail; }
    private void Render(DictationSession current) { rendering = true; Original.Text = current.Original; Draft.Text = current.Draft; CopyButton.IsEnabled = !string.IsNullOrEmpty(current.Draft); rendering = false; }
    private void AddHistory(DictationSession current) { history.Insert(0, current); if (history.Count > 10) history.RemoveAt(10); History.ItemsSource = null; History.ItemsSource = history.Select(x => x.Draft.Length <= 110 ? x.Draft : x.Draft[..110] + "…").ToList(); }
    private void Record_Click(object sender, RoutedEventArgs e) { if (session?.State == SessionState.Recording) StartStop(); else BeginRecording(); }
    private void Draft_Changed(object sender, TextChangedEventArgs e) { if (!rendering) session?.Edit(Draft.Text); if (CopyButton != null) CopyButton.IsEnabled = !string.IsNullOrWhiteSpace(Draft.Text); }
    private void Copy_Click(object sender, RoutedEventArgs e) { try { Clipboard.SetText(Draft.Text); StatusDetail.Text = "Draft copied at your request."; } catch (System.Runtime.InteropServices.COMException) { StatusDetail.Text = "The clipboard is busy. Try again."; } }
    private void Original_Click(object sender, RoutedEventArgs e) { Draft.Text = Original.Text; }
    private void Clear_Click(object sender, RoutedEventArgs e) { Cancel(); session = null; history.Clear(); History.ItemsSource = null; Draft.Clear(); Original.Clear(); Status.Text = "Ready when you are"; StatusDetail.Text = "Your session transcripts were cleared."; }
    private void History_Changed(object sender, SelectionChangedEventArgs e) { if (processing || recorder != null) return; if (History.SelectedIndex >= 0 && History.SelectedIndex < history.Count) { session = history[History.SelectedIndex]; Render(session); } }
    private static List<(string Key, string Value)> ParseLines(string source)
    {
        var result = new List<(string, string)>();
        foreach (var line in source.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var equals = line.IndexOf('=');
            if (equals < 1 || string.IsNullOrWhiteSpace(line[(equals + 1)..])) throw new FormatException("Use spoken phrase = exact spelling on every line.");
            var key = line[..equals].Trim(); var value = line[(equals + 1)..].Trim();
            if (key.Length > 120 || value.Length > 4000) throw new FormatException("Triggers must fit in 120 characters; expansions in 4000.");
            if (result.Any(x => x.Item1.Equals(key, StringComparison.OrdinalIgnoreCase))) throw new FormatException("Each trigger must be unique.");
            result.Add((key, value));
        }
        if (result.Count > 200) throw new FormatException("Keep at most 200 entries for a responsive dictionary.");
        return result;
    }
    private void Vocabulary_Click(object sender, RoutedEventArgs e) { if (processing || recorder != null) { VocabularyResult.Text = "Finish or cancel the current dictation before saving."; return; } try { var rules = VocabularyRules.Parse(Vocabulary.Text); preferences.Terms = rules.Terms; preferences.Vocabulary = rules.Replacements; preferences.Save(preferencesPath); VocabularyResult.Text = "Vocabulary saved on this device."; } catch (Exception ex) when (ex is FormatException or IOException) { VocabularyResult.Text = ex.Message; } }
    private void Snippets_Click(object sender, RoutedEventArgs e) { if (processing || recorder != null) { SnippetResult.Text = "Finish or cancel the current dictation before saving."; return; } try { preferences.Snippets = ParseLines(Snippets.Text).Select(x => new Snippet(x.Key, x.Value.Replace("\\n", "\n"))).ToList(); preferences.Save(preferencesPath); SnippetResult.Text = "Snippets saved on this device."; } catch (Exception ex) when (ex is FormatException or IOException) { SnippetResult.Text = ex.Message; } }
    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (session?.State is SessionState.Recording or SessionState.Transcribing) { SettingsResult.Text = "Finish or cancel your current dictation before changing preferences."; return; }
            var startup = StartWithWindows.IsChecked == true;
            if (startup != preferences.Startup) Startup.Set(startup);
            preferences.Language = (string)((ComboBoxItem)SpeechLanguage.SelectedItem).Tag;
            preferences.Style = (string)((ComboBoxItem)DraftStyle.SelectedItem).Content;
            preferences.Device = Device.SelectedItem is DeviceOption option ? option.Id : -1;
            preferences.AutoInsert = AutoInsert.IsChecked == true; preferences.CloseToTray = CloseToTray.IsChecked == true; preferences.Startup = startup;
            preferences.Save(preferencesPath); SettingsResult.Text = "Preferences saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { SettingsResult.Text = ex.Message; }
    }
    private sealed record DeviceOption(int Id, string Name) { public override string ToString() => Name; }
    private void RefreshDevices()
    {
        Device.Items.Clear(); Device.Items.Add(new DeviceOption(-1, "Windows default microphone"));
        try { foreach (var device in Recorder.Devices()) Device.Items.Add(new DeviceOption(device.Id, device.Name)); }
        catch (Exception ex) when (ex is NAudio.MmException or InvalidOperationException) { SettingsResult.Text = "Microphone list is unavailable. Windows default remains selected."; }
        var selected = Device.Items.OfType<DeviceOption>().FirstOrDefault(x => x.Id == preferences.Device);
        if (selected == null) preferences.Device = -1;
        Device.SelectedItem = selected ?? Device.Items[0];
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();
    private void Quit_Click(object sender, RoutedEventArgs e) => RequestQuit();
    public void RequestQuit() => Dispatcher.BeginInvoke(() => _ = QuitAsync());
    private async Task QuitAsync()
    {
        if (shutdownPending) return;
        shutdownPending = true; IsEnabled = false; Cancel();
        try { await operation.WaitAsync(TimeSpan.FromSeconds(7)); }
        catch (TimeoutException) { ShowCleanupFailure("Waiting for local cleanup", "Windows is still releasing worker files. Try Quit again shortly."); return; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or System.Text.Json.JsonException) { /* Always retry recorded cleanup failures below, even when the operation faulted. */ }
        try { await transcriber.RetryCleanupAsync(); }
        catch (ScratchCleanupException ex) { ShowCleanupFailure("Temporary recording needs cleanup", ex.Message + " Try Quit again after the file is released."); return; }
        if (transcriber.CleanupPending) { ShowCleanupFailure("Temporary recording needs cleanup", "Recording files remain on this computer. Try Quit again to retry cleanup."); return; }
        quitting = true; Close(); Application.Current.Shutdown();
    }
    private void ShowCleanupFailure(string status, string detail) { shutdownPending = false; IsEnabled = true; Status.Text = status; StatusDetail.Text = detail; ShowActivated = false; WindowState = WindowState.Normal; Show(); }
    private void ClosingWindow(object? sender, CancelEventArgs e)
    {
        if (quitting) return;
        e.Cancel = true;
        if (preferences.CloseToTray && !arguments.Contains("--test-ui")) { Cancel(); Dispatcher.BeginInvoke(Hide); }
        else RequestQuit();
    }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) { if (e.Mode == PowerModes.Suspend) Dispatcher.BeginInvoke(Cancel); }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
