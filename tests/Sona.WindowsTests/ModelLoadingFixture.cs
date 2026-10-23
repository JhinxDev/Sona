using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sona.App;
using Sona.App.Audio;
using Sona.Core;

internal static class ModelLoadingFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh model-loading evidence directory.");
        Directory.CreateDirectory(output);
        var prior = Environment.GetEnvironmentVariable("SONA_DATA");
        Environment.SetEnvironmentVariable("SONA_DATA", Path.Combine(output, "profile"));
        new Preferences { SoundEffects = false, AutoInsert = false, CleanupEnabled = true, CleanupMode = "Always" }.Save(Path.Combine(output, "profile", "preferences.json"));
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool ok, string name) => (ok ? checks : failures).Add(name);
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.BeginInvoke(new Action(async () =>
            {
            MainWindow? window = null;
            try
            {
                foreach (var display in new[] { "Capsule", "Panel" })
                {
                    var overlay = new Overlay { SuppressNativeDisplayForTests = true };
                    try
                    {
                        overlay.SetDisplay(display);
                        overlay.Update("Loading model", "Speech model into memory · Esc cancels");
                        var shell = (Border)overlay.FindName("Shell"); Layout(shell, overlay.Width);
                        Check(((ProgressBar)overlay.FindName(display + "Busy")).Visibility == Visibility.Visible, display + " loading has a real busy indicator");
                        Check(((TextBlock)overlay.FindName(display + "Hint")).Text.Contains("memory"), display + " explains the first-use wait");
                        var label = (TextBlock)overlay.FindName(display + "Label");
                        var desired = new FormattedText(label.Text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch), label.FontSize, Brushes.White, 1);
                        Check(label.ActualWidth >= desired.Width, display + " loading title is fully readable");
                        Check(((Button)overlay.FindName(display + "Stop")).Visibility != Visibility.Visible, display + " loading never pretends capture is active");
                        Check(!overlay.IsVisible && !overlay.IsLoaded, display + " fixture never shows native UI");
                        Save(shell, Path.Combine(output, display.ToLowerInvariant() + "-loading.png"));
                    }
                    finally { overlay.Close(); }
                }
                Check(typeof(ResidentModel).GetProperty("LoadStage") != null, "Speech loader exposes actual preparation state");
                Check(typeof(CleanupModel).GetProperty("LoadStage") != null, "Cleanup loader exposes actual preparation state");
                await CheckLoadersAsync(Check);
                window = new MainWindow(["--test-ui"]);
                await CheckFlowAsync(window, output, Check);
            }
            catch (Exception ex) { failures.Add(ex.ToString()); }
            finally { window?.RequestQuit(); app.Shutdown(); }
            })); app.Run();
        }) { IsBackground = true };
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); if (!thread.Join(TimeSpan.FromSeconds(25))) throw new TimeoutException("Model-loading fixture exceeded its bound."); }
        finally { Environment.SetEnvironmentVariable("SONA_DATA", prior); }
        var report = new { Checks = checks, Failures = failures, MicrophoneOpened = false, NativeWindowsShown = false, SystemInputSent = false, ProductionProfileChanged = false };
        File.WriteAllText(Path.Combine(output, "model-loading-proof.json"), System.Text.Json.JsonSerializer.Serialize(report));
        return report;
    }
    private static async Task CheckLoadersAsync(Action<bool, string> check)
    {
        foreach (var cleanup in new[] { false, true })
        {
            using var cancel = new CancellationTokenSource();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> Verify(string _, CancellationToken token) { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return false; }
            var speech = new ResidentModel(verifyModel: Verify);
            var polish = new CleanupModel(verifyAssets: Verify);
            var preferences = new Preferences { Model = "tiny", Engine = "FastCpu", CleanupEnabled = true, CleanupEngine = "Cpu" };
            var work = cleanup ? polish.WarmAsync(preferences, cancel.Token) : speech.WarmAsync(preferences, cancel.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            check((cleanup ? polish.LoadStage : speech.LoadStage) == ModelLoadStage.Verifying, (cleanup ? "Cleanup" : "Speech") + " reports gated integrity verification before any worker starts");
            check(polish.ProcessId == null && speech.ProcessId == null, "Verification fixture never starts a model worker");
            cancel.Cancel();
            try { await work; check(false, "Cancelled verification throws"); } catch (OperationCanceledException) { check(true, "Cancelled verification throws"); }
            check((cleanup ? polish.LoadStage : speech.LoadStage) == ModelLoadStage.None, "Cancelled loader clears its preparation state");
            await speech.UnloadAsync(); await polish.UnloadAsync();
        }
        var invalid = new ResidentModel(verifyModel: (_, _) => Task.FromResult(false));
        try { await invalid.WarmAsync(new Preferences { Model = "tiny", Engine = "FastCpu" }, CancellationToken.None); } catch (IOException) { }
        check(invalid.LoadStage == ModelLoadStage.None, "Failed speech verification clears progress");
        var invalidCleanup = new CleanupModel(verifyAssets: (_, _) => Task.FromResult(false));
        try { await invalidCleanup.WarmAsync(new Preferences { CleanupEnabled = true, CleanupEngine = "Cpu" }, CancellationToken.None); } catch (IOException) { }
        check(invalidCleanup.LoadStage == ModelLoadStage.None, "Failed cleanup verification clears progress");
    }

    private static async Task CheckFlowAsync(MainWindow window, string output, Action<bool, string> check)
    {
        var stage = ModelLoadStage.Verifying; var purpose = ModelPurpose.Speech;
        window.ModelLoadStageForTests = kind => kind == purpose ? stage : ModelLoadStage.None;
        window.PreparationForTests = _ => Task.CompletedTask;
        Capture input = new(); window.RecorderForTests = () => new Recorder(_ => input);
        var recognized = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var polished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.RecognitionForTests = (_, ct) => recognized.Task.WaitAsync(ct);
        window.CleanupForTests = (_, ct) => polished.Task.WaitAsync(ct);
        var overlay = window.OverlayForTests;
        var label = (TextBlock)overlay.FindName("CapsuleLabel");
        var hint = (TextBlock)overlay.FindName("CapsuleHint");
        var pcm = new byte[64000]; for (var i = 1; i < pcm.Length; i += 2) pcm[i] = 8;
        check(await window.BeginForTestsAsync(), "Loading fixture admits only fake capture"); input.Emit(pcm);
        var stopping = window.StopForTestsAsync();
        await Wait(() => label.Text == "Checking model");
        check(label.Text == "Checking model" && hint.Text.Contains("speech"), "Final recognition shows the actual verification wait");
        stage = ModelLoadStage.Loading; await Wait(() => label.Text == "Loading model");
        check(label.Text == "Loading model" && hint.Text.Contains("Speech"), "Speech loading into memory has distinct copy");
        Layout((Border)overlay.FindName("Shell"), overlay.Width); Save((Border)overlay.FindName("Shell"), Path.Combine(output, "flow-speech-loading.png"));
        check(window.SessionForTests == SessionState.Transcribing && !stopping.IsCompleted, "Loading never completes or delivers a partial draft");
        stage = ModelLoadStage.None; await Wait(() => label.Text == "Transcribing");
        check(label.Text == "Transcribing", "Ready speech resumes transcription without a fake loading delay");
        purpose = ModelPurpose.Cleanup; stage = ModelLoadStage.Loading;
        recognized.SetResult("Please keep all the wording in this complete test draft.");
        await Wait(() => label.Text == "Loading model" && hint.Text.Contains("Cleanup"));
        check(label.Text == "Loading model" && hint.Text.Contains("Cleanup"), "Cleanup loading has its own actual model identity");
        stage = ModelLoadStage.None; await Wait(() => label.Text == "Polishing");
        check(label.Text == "Polishing", "Ready cleanup resumes polishing without a fake loading delay");
        polished.SetResult("Please keep the complete test draft."); await stopping;
        check(((TextBox)window.FindName("Draft")).Text == "Please keep the complete test draft.", "Loading feedback preserves final complete text");
        stage = ModelLoadStage.None; purpose = ModelPurpose.Speech;
        recognized = new(TaskCreationOptions.RunContinuationsAsynchronously); polished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        input = new(); await window.BeginForTestsAsync(); input.Emit(pcm); stopping = window.StopForTestsAsync();
        check(label.Text == "Transcribing", "Warm speech skips both model preparation labels immediately");
        purpose = ModelPurpose.Cleanup; recognized.SetResult("Please keep this complete warm draft with all its words.");
        await Wait(() => label.Text == "Polishing"); check(label.Text == "Polishing", "Warm cleanup immediately shows polishing");
        polished.SetResult("A complete warm draft."); await stopping;
        stage = ModelLoadStage.Loading; purpose = ModelPurpose.Speech;
        recognized = new(TaskCreationOptions.RunContinuationsAsynchronously);
        input = new(); await window.BeginForTestsAsync(); input.Emit(pcm); stopping = window.StopForTestsAsync();
        window.CancelForTests(); await stopping;
        var afterCancel = label.Text; await Task.Delay(150);
        check(label.Text == afterCancel && window.SessionForTests == SessionState.Cancelled, "Cancelled loading cannot revive an old pill status");
        var prefs = (Preferences)typeof(MainWindow).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        prefs.LiveTranscription = true;
        input = new(); await window.BeginForTestsAsync();
        typeof(MainWindow).GetMethod("RecordingTick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
        check(label.Text == "Recording" && hint.Visibility == Visibility.Visible && hint.Text.Contains("memory") && ((Button)overlay.FindName("CapsuleStop")).Visibility == Visibility.Visible, "Live-preview loading keeps recording controls and shows a secondary memory hint");
        check(((TextBlock)overlay.FindName("CapsuleTime")).Text.Length > 0, "Live-preview loading preserves elapsed recording time");
        Layout((Border)overlay.FindName("Shell"), overlay.Width); Save((Border)overlay.FindName("Shell"), Path.Combine(output, "recording-with-loading.png"));
        window.CancelForTests(); await Task.Delay(40);
        check(!window.IsVisible && !overlay.IsVisible, "All model-progress flow checks remain offscreen");
        Array.Clear(pcm);
    }
    private static async Task Wait(Func<bool> ready) { for (var i = 0; i < 40 && !ready(); i++) await Task.Delay(20); }
    private sealed class Capture : IWaveIn
    {
        public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording() { }
        public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
        public void Dispose() { }
        public void Emit(byte[] bytes) => DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
    }
    private static void Layout(FrameworkElement element, double width) { element.Measure(new Size(width - 6, double.PositiveInfinity)); element.Arrange(new Rect(0, 0, width - 6, element.DesiredSize.Height)); element.UpdateLayout(); }
    private static void Save(FrameworkElement element, string path) { var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); png.Save(file); }
}
