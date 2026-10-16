using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Audio;
using Sona.Core;

internal static class LiveDictationFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh fixture output directory.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile");
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        new Preferences { Theme = "amethyst", LiveTranscription = true, AutoInsert = false, CleanupEnabled = true, KeepHistory = false }.Save(Path.Combine(profile, "preferences.json"));
        var checks = new List<string>(); Exception? failure = null;
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); File.WriteAllLines(Path.Combine(output, "progress.txt"), checks); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent();
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null; TaskCompletionSource<string>? pending = null;
                try
                {
                    Check(app.StartupCallbackObserved && app.MainWindow == null, "Production startup is intercepted");
                    window = new MainWindow(["--test-ui"]); window.ShowActivated = false; window.Show();
                    var capture = new FakeCapture(); var calls = 0;
                    window.RecorderForTests = () => new Recorder(_ => capture);
                    window.PreparationForTests = _ => Task.CompletedTask;
                    window.RecognitionForTests = (wav, token) => { calls++; Check(wav.Length > 32000, "Preview contains actual captured PCM in a WAV"); return Task.FromResult(calls == 1 ? "Let's review the draft together." : "Let's review the draft together and keep the correction."); };
                    var draft = (TextBox)window.FindName("Draft"); var original = (TextBox)window.FindName("Original");
                    var pcm = new byte[64000]; for (var i = 1; i < pcm.Length; i += 2) pcm[i] = 8;
                    async Task Wait(Func<bool> done)
                    {
                        for (var i = 0; i < 100 && !done(); i++) await Task.Delay(50);
                        Check(done(), "Expected native pipeline state arrived within five seconds");
                    }
                    void Capture(string name)
                    {
                        window.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output, name)); png.Save(stream);
                    }
                    Check(await window.BeginForTestsAsync(), "Intentional test gesture starts only the fake recorder");
                    capture.Emit(pcm); await Wait(() => calls >= 1 && draft.Text.Length > 0);
                    Check(window.SessionForTests == SessionState.Recording && draft.Text.StartsWith("Let's review"), "Real recording pipeline displays a provisional draft before stop");
                    Capture("live-provisional.png");
                    draft.Text = "My correction stays exactly here."; capture.Emit(pcm);
                    await Wait(() => calls >= 2);
                    Check(draft.Text == "My correction stays exactly here." && original.Text.Contains("keep the correction"), "New recognition updates raw text while preserving manual draft edits");
                    var cleanupReady = new TaskCompletionSource<string>(); var cleanupStarted = false;
                    window.CleanupForTests = (_, _) => { cleanupStarted = true; return cleanupReady.Task; };
                    var stopLatency = System.Diagnostics.Stopwatch.StartNew();
                    var stopping = window.StopForTestsAsync(); await Wait(() => cleanupStarted || stopping.IsCompleted);
                    if (!cleanupStarted)
                    {
                        await stopping;
                        throw new InvalidOperationException("Final cleanup was skipped: " + window.SessionForTests + "; " + ((TextBlock)window.FindName("StatusDetail")).Text);
                    }
                    Check(stopLatency.Elapsed < TimeSpan.FromSeconds(1), "Stopping does not wait for the next two-second preview polling interval");
                    draft.Text = "My correction survives cleanup too.";
                    cleanupReady.SetResult("Let's review the draft together and keep the correction."); await stopping;
                    Check(window.SessionForTests == SessionState.Ready && draft.Text == "My correction survives cleanup too.", "Final pipeline preserves manual draft and keeps it ready for review");
                    Check(((TextBox)window.FindName("CleanedSuggestion")).Text.Contains("keep the correction"), "Final cleanup result remains available separately");
                    Check(capture.Disposed, "Stop releases capture"); Capture("live-final-edited.png");
                    var history = (List<HistoryItem>)typeof(MainWindow).GetField("historyEntries", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
                    var firstId = history[0].Id;
                    var other = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Other original", "Other draft", false);
                    typeof(MainWindow).GetMethod("RestoreHistoryDraft", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [other]);
                    typeof(MainWindow).GetMethod("UseCleaned_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
                    Check(draft.Text == "Other draft" && ((Expander)window.FindName("CleanedReview")).Visibility == Visibility.Collapsed, "Opening another draft invalidates a stale cleaned suggestion");
                    capture = new FakeCapture();
                    window.RecognitionForTests = (_, _) => Task.FromResult("A separate provisional draft.");
                    Check(await window.BeginForTestsAsync(), "Second live session starts independently");
                    capture.Emit(pcm); await Wait(() => draft.Text.Contains("separate provisional"));
                    window.CancelForTests(); await Wait(() => capture.Disposed);
                    capture = new FakeCapture();
                    Check(await window.BeginForTestsAsync(), "Starting after cancelled preview retains its draft separately");
                    Check(history.Single(x => x.Id == firstId).Draft == "My correction survives cleanup too." && history.Any(x => x.Draft.Contains("separate provisional")), "Cancelled preview never overwrites the earlier history entry");
                    Check(((Expander)window.FindName("CleanedReview")).Visibility == Visibility.Collapsed, "New session clears the previous cleaned suggestion");
                    window.CancelForTests(); await Wait(() => capture.Disposed);
                    capture = new FakeCapture(); pending = new TaskCompletionSource<string>(); var pendingStarted = false;
                    window.RecognitionForTests = (_, _) => { pendingStarted = true; return pending.Task; };
                    Check(await window.BeginForTestsAsync(), "Another session starts after completion");
                    capture.Emit(pcm); await Wait(() => pendingStarted);
                    window.CancelForTests(); await Wait(() => window.SessionForTests == SessionState.Cancelled && capture.Disposed);
                    pending.SetResult("Late words must not appear"); await window.LiveOperationForTests;
                    Check(draft.Text == "" && window.SessionForTests == SessionState.Cancelled, "Cancellation rejects a late recognizer result and clears no external content");
                    Check(!File.Exists(Path.Combine(profile, "history.json")), "Live updates never create saved history when disabled");
                    window.Width = 740; window.Height = 530; Capture("live-compact.png");
                }
                catch (Exception ex) { failure = ex; File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); }
                finally { pending?.TrySetCanceled(); if (window != null) window.RequestQuit(); else app.Shutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Sona live draft fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(35))) throw new TimeoutException("Live fixture did not quit within 35 seconds.");
        if (failure != null) throw new InvalidOperationException("Live draft fixture failed", failure);
        return new { Passed = checks.Count, Checks = checks, MicrophoneOpened = false, GlobalInputSent = false, Recognition = "deterministic fake at the real native pipeline boundary", Output = output };
    }
}
