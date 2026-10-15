using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Wave;
using Sona.App;
using Sona.App.Audio;
using Sona.Core;

internal static class RecordingAudioFixture
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    public static object Run(string file, string output)
    {
        file = System.IO.Path.GetFullPath(file); output = System.IO.Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh recording evidence directory.");
        using var reader = new WaveFileReader(file);
        if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm || reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Channels != 1 || reader.Length < 128000 || reader.Length > 960000)
            throw new IOException("Use a licensed 4–30 second 16 kHz mono PCM16 WAV.");
        var pcm = new byte[reader.Length]; reader.ReadExactly(pcm);
        Directory.CreateDirectory(output); Directory.CreateDirectory(System.IO.Path.Combine(output, "frames"));
        var profile = System.IO.Path.Combine(output, "profile"); Environment.SetEnvironmentVariable("SONA_DATA", profile);
        new Preferences { Theme = "amethyst", Model = "turbo", Language = "en", LiveTranscription = true, AutoInsert = false, CopyToClipboard = false, EnterToSend = false, CleanupEnabled = false, KeepHistory = false, SoundEffects = false, RecordingDisplay = "Capsule" }.Save(System.IO.Path.Combine(profile, "preferences.json"));
        var checks = new List<string>(); var levels = new List<object>(); var transcripts = new List<object>(); var frames = new List<object>();
        double stopRequestedAt = 0, completedAt = 0;
        Exception? failure = null; string final = ""; int? workerPid = null; bool workerExited = false;
        var foreground = GetForegroundWindow();
        void Check(bool condition, string text) { if (!condition) throw new IOException(text); checks.Add(text); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null; Transcriber? engine = null; DispatcherTimer? drawing = null;
                var clock = new Stopwatch();
                try
                {
                    Check(app.StartupCallbackObserved && app.MainWindow == null, "Production startup suppressed");
                    // Never Show the main window: no native source, observer, agent pipe or tray.
                    window = new MainWindow(["--test-ui"]); var overlay = window.OverlayForTests; overlay.SuppressNativeDisplayForTests = true;
                    var capture = new FakeCapture(); using var recorder = new Recorder(_ => capture);
                    window.RecorderForTests = () => recorder; window.PreparationForTests = _ => Task.CompletedTask;
                    engine = (Transcriber)typeof(MainWindow).GetField("transcriber", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    await engine.ReconcileAsync(new Preferences { Model = "turbo", Language = "en" }).WaitAsync(TimeSpan.FromSeconds(90));
                    workerPid = engine.ResidentProcessId; Check(workerPid != null, "Actual Whisper Turbo worker is loaded before capture");
                    Check(window.RecognitionForTests == null, "No fake recognizer or fabricated transcript is installed");
                    var original = (TextBox)window.FindName("Original");
                    original.TextChanged += (_, _) => { if (original.Text.Length > 0) transcripts.Add(new { Seconds = clock.Elapsed.TotalSeconds, Text = original.Text, State = window.SessionForTests?.ToString() }); };
                    var shell = (Border)overlay.FindName("Shell"); var bars = (StackPanel)overlay.FindName("CapsuleWave");
                    var heights = new List<double>(); double lastFrame = 0;
                    void Frame()
                    {
                        var now = clock.Elapsed.TotalSeconds; overlay.AdvanceVisuals(now - lastFrame); lastFrame = now;
                        shell.Measure(new Size(overlay.Width, double.PositiveInfinity)); shell.Arrange(new Rect(0, 0, overlay.Width, shell.DesiredSize.Height)); shell.UpdateLayout();
                        CheckHeight(shell.ActualHeight);
                        var name = $"frames/{frames.Count:D5}.png";
                        var bitmap = new RenderTargetBitmap(272, 160, 96, 96, PixelFormats.Pbgra32);
                        var visual = new DrawingVisual(); using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(shell) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0,0,shell.ActualWidth,shell.ActualHeight) }, null, new Rect(0,0,shell.ActualWidth,shell.ActualHeight));
                        bitmap.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(System.IO.Path.Combine(output, name))) png.Save(stream);
                        var max = bars.Children.Cast<Rectangle>().Max(b => b.Height); heights.Add(max);
                        frames.Add(new { Seconds = now, File = name, Height = shell.ActualHeight, MaxBarHeight = max, Text = ((TextBlock)overlay.FindName("LiveTranscript")).Text, Status = ((TextBlock)overlay.FindName("CapsuleLabel")).Text });
                    }
                    void CheckHeight(double h) { if (h > 160) throw new IOException("Capsule exceeds capture canvas"); }
                    Check(await window.BeginForTestsAsync(), "Real MainWindow live pipeline starts only the prerecorded capture");
                    overlay.UpdateTarget("Demo app"); clock.Start();
                    drawing = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
                    drawing.Tick += (_, _) => { try { Frame(); } catch (Exception ex) { failure ??= ex; drawing.Stop(); } }; drawing.Start();
                    for (var offset = 0; offset < pcm.Length; offset += 1920)
                    {
                        var due = offset / 32000.0 - clock.Elapsed.TotalSeconds; if (due > 0) await Task.Delay(TimeSpan.FromSeconds(due));
                        var chunk = pcm.AsSpan(offset, Math.Min(1920, pcm.Length - offset)).ToArray(); capture.Emit(chunk); Array.Clear(chunk);
                        levels.Add(new { Seconds = clock.Elapsed.TotalSeconds, AudioSeconds = offset / 32000.0, Rms = recorder.Level, Mapped = Overlay.MapLevel(recorder.Level) });
                    }
                    var remaining = pcm.Length / 32000.0 - clock.Elapsed.TotalSeconds; if (remaining > 0) await Task.Delay(TimeSpan.FromSeconds(remaining));
                    Check(transcripts.Count > 0, "Actual provisional recognition arrives before stopping");
                    stopRequestedAt = clock.Elapsed.TotalSeconds;
                    await window.StopForTestsAsync().WaitAsync(TimeSpan.FromSeconds(30)); drawing.Stop(); completedAt = clock.Elapsed.TotalSeconds; Frame();
                    final = original.Text;
                    Check(window.SessionForTests == SessionState.Ready && capture.Disposed, "Final draft is ready and capture released without insertion");
                    Check(final.Contains("review the design tomorrow", StringComparison.OrdinalIgnoreCase), "Actual final recognizer contains the supplied spoken phrase");
                    Check(heights.Max() - heights.Min() > 6, "Rendered audio-driven waveform spans more than six DIP");
                    Check(GetForegroundWindow() == foreground, "Foreground window preserved");
                    Check(!File.Exists(System.IO.Path.Combine(profile, "history.json")), "No transcript history is persisted implicitly");
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    drawing?.Stop(); Array.Clear(pcm);
                    try { if (engine != null) { await engine.UnloadAsync(); workerExited = engine.ResidentProcessId == null && !engine.ResidentCleanupPending; } }
                    catch (Exception ex) { failure ??= ex; }
                    if (window != null) window.RequestQuit(); else app.Shutdown();
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Sona prerecorded real recognition and overlay" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(150))) throw new TimeoutException("Real recording fixture exceeded 150 seconds");
        if (failure == null) Check(workerExited, "Task-owned speech worker exited");
        var report = new { Checks = checks, Failure = failure?.ToString(), Model = "Whisper Turbo", AudioSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), AssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Overlay).Assembly.Location))), Final = final, StopRequestedSeconds = stopRequestedAt, CompletedSeconds = completedAt, Levels = levels, Transcripts = transcripts, Frames = frames, WorkerPid = workerPid, WorkerExited = workerExited, MicrophoneOpened = false, GlobalInputSent = false, ClipboardChanged = false, Evidence = "Real Recorder PCM, real MainWindow live polling and Whisper recognition; actual offscreen WPF frames at measured timestamps; no audible playback or production profile" };
        File.WriteAllText(System.IO.Path.Combine(output, "recording-proof.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (failure != null) throw new IOException("Real recording fixture failed; see recording-proof.json", failure);
        return new { Passed = checks.Count, Final = final, Frames = frames.Count, Output = output, WorkerExited = workerExited };
    }
}
