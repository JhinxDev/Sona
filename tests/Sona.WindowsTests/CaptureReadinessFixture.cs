using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.Wave;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class CaptureReadinessFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh capture-readiness evidence path.");
        Directory.CreateDirectory(output);
        var previousData = Environment.GetEnvironmentVariable("SONA_DATA");
        var previousTransfer = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        Environment.SetEnvironmentVariable("SONA_DATA", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
        new Preferences { SoundEffects = false, AutoInsert = false, LiveTranscription = false }.Save(Path.Combine(output, "profile", "preferences.json"));
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string label) => (value ? checks : failures).Add(label);
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var field = new TargetFieldMetadata(31001, ControlType.Pane.Id, true, false, false, [1, 2, 3]);
                    TargetCaptureResult Pane() => Insertion.EvaluateCapture(field, 101, 31001, true, 4, 4);
                    TargetCaptureResult Ready() => Insertion.EvaluateCapture(field with { ControlTypeId = ControlType.Edit.Id, RuntimeId = [4, 5, 6] }, 101, 31001, true, 4, 4);
                    var raced = Ready() with { Target = null, Status = TargetCaptureStatus.FocusChanged, Stable = false };
                    Check(raced.InitialStatus == TargetCaptureStatus.FocusChanged, "A finalized first-capture race reports its actual rejection rather than an intermediate Ready status");
                    var helper = typeof(Insertion).Assembly.GetType("Sona.App.Windows.TargetCaptureReadiness");
                    var method = helper?.GetMethod("CaptureAsync", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    Check(method != null, "Recording has a bounded direct-field readiness acquisition boundary");
                    if (method == null) return;
                    async Task<TargetCaptureResult> Acquire(Func<TargetCaptureResult> capture, Func<bool>? admission = null, Func<CancellationToken, Task>? wait = null, CancellationToken token = default)
                        => await (Task<TargetCaptureResult>)method.Invoke(null, [capture, admission ?? (() => true), wait ?? (_ => Task.CompletedTask), token])!;
                    int calls = 0, waits = 0;
                    var ready = await Acquire(() => { calls++; return Ready(); }, wait: _ => { waits++; return Task.CompletedTask; });
                    Check(ready.Target != null && calls == 1 && waits == 0, "An immediately ready field has no readiness delay or duplicate capture");
                    for (var at = 2; at <= 4; at++)
                    {
                        calls = waits = 0; var eligibleAt = at;
                        ready = await Acquire(() => ++calls == eligibleAt ? Ready() : Pane(), wait: _ => { waits++; return Task.CompletedTask; });
                        Check(ready.Target != null && calls == at && waits == at - 1, $"A directly focused editor can become ready on capture {at} within the original scope");
                        var attempts = ready.GetType().GetProperty("CaptureAttempts")?.GetValue(ready);
                        var initial = ready.GetType().GetProperty("InitialStatus")?.GetValue(ready);
                        Check(attempts is int count && count == at && initial?.ToString() == "UnsupportedControl", $"Capture {at} records its initial rejection and attempt count");
                    }
                    foreach (var status in terminalAtSecond())
                    {
                        calls = 0;
                        var result = await Acquire(() => ++calls == 1 ? Pane() : Pane() with { Status = status });
                        Check(result.Target == null && result.Status == status && calls == 2, status + " ends readiness immediately on a later capture");
                    }
                    TargetCaptureStatus[] terminalAtSecond() => [TargetCaptureStatus.PasswordField, TargetCaptureStatus.DisabledField, TargetCaptureStatus.ReadOnlyField, TargetCaptureStatus.SuggestionFocused];
                    calls = 0;
                    var unknownPane = Insertion.EvaluateCapture(field with { ReadOnly = null }, 101, 31001, true, 4, 4);
                    ready = await Acquire(() => ++calls == 1 ? unknownPane : Ready());
                    Check(calls == 2 && ready.Target != null, "A pane with unknown writability can wait, but only a later direct editor can authorize output");
                    calls = waits = 0;
                    var exhausted = await Acquire(() => { calls++; return Pane(); }, wait: _ => { waits++; return Task.CompletedTask; });
                    Check(exhausted.Target == null && calls == 4 && waits == 3, "An unavailable field stops after four total direct captures");
                    var terminal = new[] { TargetCaptureStatus.PasswordField, TargetCaptureStatus.DisabledField, TargetCaptureStatus.ReadOnlyField, TargetCaptureStatus.SonaFocused, TargetCaptureStatus.SuggestionFocused, TargetCaptureStatus.NoFocusedControl, TargetCaptureStatus.ProviderUnavailable, TargetCaptureStatus.TrackingUnavailable };
                    foreach (var status in terminal)
                    {
                        calls = 0; var rejected = await Acquire(() => { calls++; return Pane() with { Status = status }; });
                        Check(rejected.Target == null && calls == 1, status + " is terminal and never polled toward an editor");
                    }
                    var readOnly = Insertion.EvaluateCapture(field with { ReadOnly = true }, 101, 31001, true, 4, 4);
                    calls = 0; await Acquire(() => { calls++; return readOnly; });
                    Check(calls == 1, "A known read-only pane is never a readiness candidate");
                    foreach (var changed in new[] {
                        Pane() with { ForegroundWindow = 102 }, Pane() with { WindowProcessId = 31002 },
                        Pane() with { FocusEpochBefore = 5, FocusEpochAfter = 5 }, Pane() with { RuntimeIdentityHash = "DIFFERENT" },
                        Pane() with { ProviderProcessId = 31002 }, Pane() with { Stable = false }, Pane() with { FieldInForegroundWindow = false }
                    })
                    {
                        calls = 0; var result = await Acquire(() => ++calls == 1 ? Pane() : changed);
                        Check(result.Target == null && calls == 2, "Changed scope or intermediate container identity ends acquisition without rebasing");
                    }
                    foreach (var incomplete in new[] {
                        Pane() with { ForegroundWindow = 0 }, Pane() with { WindowProcessId = null }, Pane() with { ProviderProcessId = null },
                        Pane() with { RuntimeIdentityHash = null }, Pane() with { Stable = false }, Pane() with { FieldInForegroundWindow = null },
                        Pane() with { ControlTypeId = ControlType.Button.Id }, Pane() with { FocusEpochAfter = 5 }
                    })
                    {
                        calls = 0; await Acquire(() => { calls++; return incomplete; });
                        Check(calls == 1, "Missing or ineligible initial container metadata cannot enable a retry");
                    }
                    foreach (var changedReady in new[] {
                        Ready() with { ForegroundWindow = 102 }, Ready() with { WindowProcessId = 31002 },
                        Ready() with { FocusEpochBefore = 5, FocusEpochAfter = 5 }
                    })
                    {
                        calls = 0; var result = await Acquire(() => ++calls == 1 ? Pane() : changedReady);
                        Check(result.Target == null && result.Status == TargetCaptureStatus.FocusChanged && calls == 2, "A ready field in a later focus scope cannot replace the original gesture destination");
                    }
                    using (var cancelled = new CancellationTokenSource())
                    {
                        calls = 0; var threw = false;
                        try { await Acquire(() => { calls++; return Pane(); }, wait: _ => { cancelled.Cancel(); return Task.CompletedTask; }, token: cancelled.Token); }
                        catch (OperationCanceledException) { threw = true; }
                        Check(threw && calls == 1, "Cancellation during the readiness wait prevents another capture");
                    }
                    var admitted = true; calls = 0;
                    try { await Acquire(() => { calls++; return Pane(); }, () => admitted, _ => { admitted = false; return Task.CompletedTask; }); Check(false, "Released shortcut cancels readiness"); }
                    catch (OperationCanceledException) { Check(calls == 1, "Released shortcut prevents another capture"); }
                    admitted = true; calls = 0;
                    try { await Acquire(() => { if (++calls == 2) admitted = false; return calls == 1 ? Pane() : Ready(); }, () => admitted); Check(false, "Admission must survive the final capture"); }
                    catch (OperationCanceledException) { Check(calls == 2, "Admission revoked inside capture cannot authorize a recorder start"); }

                    using var insertion = new Insertion(() => Ready().Target, () => false, _ => throw new Exception("No output expected during acquisition"));
                    window = new MainWindow(["--test-ui"], insertion);
                    var captureProperty = typeof(MainWindow).GetProperty("TargetCaptureForTests", flags);
                    var delayProperty = typeof(MainWindow).GetProperty("CaptureDelayForTests", flags);
                    Check(captureProperty != null && delayProperty != null, "Native recording lifecycle exposes only isolated capture and delay boundaries");
                    if (captureProperty == null || delayProperty == null) return;
                    var input = new FakeCapture(); var starts = 0;
                    window.RecorderForTests = () => { starts++; return new Recorder(_ => input); };
                    window.PreparationForTests = _ => Task.CompletedTask;
                    var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    calls = 0; captureProperty.SetValue(window, (Func<TargetCaptureResult>)(() => ++calls == 1 ? Pane() : Ready()));
                    delayProperty.SetValue(window, (Func<CancellationToken, Task>)(token => gate.Task.WaitAsync(token)));
                    var beginning = window.BeginForTestsAsync();
                    Check(!beginning.IsCompleted && starts == 0 && window.SessionForTests == SessionState.Recording, "Preparation owns a cancellable session before readiness first yields");
                    Check(!await window.BeginForTestsAsync() && starts == 0, "Overlapping starts are refused while initial field capture is pending");
                    gate.SetResult(); Check(await beginning && starts == 1 && input.Started && calls == 2, "The real MainWindow starts exactly one fake recorder after admitted field readiness");
                    var target = (DraftTarget?)typeof(MainWindow).GetField("target", flags)!.GetValue(window);
                    Check(Insertion.Same(target, Ready().Target), "The admitted direct field becomes the actual recording destination");
                    window.CancelForTests();
                    Check(!await window.BeginForTestsAsync(), "A new start waits for queued cancellation to release the old recorder");
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    input = new FakeCapture(); gate = new(TaskCreationOptions.RunContinuationsAsynchronously); calls = 0;
                    captureProperty.SetValue(window, (Func<TargetCaptureResult>)(() => { calls++; return Pane(); }));
                    beginning = window.BeginForTestsAsync(); window.CancelForTests(); gate.TrySetResult();
                    Check(!await beginning && !input.Started && starts == 1 && calls == 1, "Escape during native readiness never opens the fake microphone or retries capture");
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    gate = new(TaskCreationOptions.RunContinuationsAsynchronously); calls = 0;
                    beginning = window.BeginForTestsAsync();
                    typeof(MainWindow).GetMethod("Clear_Click", flags)!.Invoke(window, [window, new RoutedEventArgs()]); gate.TrySetResult();
                    Check(!await beginning && window.SessionForTests == null && starts == 1, "Clearing a pending recording invalidates its late capture");
                    captureProperty.SetValue(window, (Func<TargetCaptureResult>)Ready); delayProperty.SetValue(window, (Func<CancellationToken, Task>)(_ => Task.CompletedTask));
                    input = new FakeCapture(); Check(await window.BeginForTestsAsync() && starts == 2, "Clearing an old attempt does not leave future recording permanently busy");
                    window.CancelForTests(); await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    gate = new(TaskCreationOptions.RunContinuationsAsynchronously); calls = 0; captureProperty.SetValue(window, (Func<TargetCaptureResult>)(() => { calls++; return Pane(); }));
                    delayProperty.SetValue(window, (Func<CancellationToken, Task>)(token => gate.Task.WaitAsync(token)));
                    var holdAdmitted = true;
                    var privateBegin = typeof(MainWindow).GetMethod("BeginRecordingAsync", flags)!;
                    beginning = (Task<bool>)privateBegin.Invoke(window, [(Func<bool>)(() => holdAdmitted)])!;
                    holdAdmitted = false; gate.TrySetResult();
                    Check(!await beginning && starts == 2 && calls == 1, "Releasing the admitted chord during native readiness never starts capture");
                    await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    captureProperty.SetValue(window, (Func<TargetCaptureResult>)Ready);
                    var admissionReads = 0;
                    beginning = (Task<bool>)privateBegin.Invoke(window, [(Func<bool>)(() => ++admissionReads < 5)])!;
                    Check(!await beginning && starts == 2 && window.SessionForTests == SessionState.Cancelled,
                        "Losing admission immediately after capture retires preparation rather than leaving a recording session without a recorder");
                    captureProperty.SetValue(window, (Func<TargetCaptureResult>)(() => { calls++; return Pane(); }));
                    input = new FakeCapture(); calls = 0;
                    delayProperty.SetValue(window, (Func<CancellationToken, Task>)(_ => Task.CompletedTask));
                    Check(await window.BeginForTestsAsync() && starts == 3 && calls == 4 && typeof(MainWindow).GetField("target", flags)!.GetValue(window) == null,
                        "Exhausted readiness keeps intentional recording available as draft-only without an invented target");
                    window.CancelForTests(); await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                    gate = new(TaskCreationOptions.RunContinuationsAsynchronously); calls = 0;
                    delayProperty.SetValue(window, (Func<CancellationToken, Task>)(token => gate.Task.WaitAsync(token)));
                    var beforeObservation = typeof(MainWindow).GetField("lastDestinationCapture", flags)!.GetValue(window);
                    beginning = window.BeginForTestsAsync();
                    typeof(MainWindow).GetField("shutdownPending", flags)!.SetValue(window, true); gate.TrySetResult();
                    Check(!await beginning && starts == 3 && ReferenceEquals(beforeObservation, typeof(MainWindow).GetField("lastDestinationCapture", flags)!.GetValue(window)), "Shutdown invalidates pending capture without publishing stale diagnostics");
                    Check(!window.IsVisible && !window.OverlayForTests.IsVisible, "All readiness lifecycle tests remain unshown and send no input");
                }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { window?.RequestQuit(); app.Shutdown(); }
            }));
            app.Run();
        }) { IsBackground = true, Name = "Sona bounded capture-readiness fixture" };
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Capture-readiness fixture exceeded its bound."); }
        finally { Environment.SetEnvironmentVariable("SONA_DATA", previousData); Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", previousTransfer); }
        var report = new { Version = AppVersion.Number, BuildId = AppVersion.BuildId, Checks = checks, Failures = failures, MicrophoneOpened = false, GlobalInputSent = false, ProductionProfileUsed = false };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
    private sealed class FakeCapture : IWaveIn
    {
        public bool Started; public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
        public event EventHandler<WaveInEventArgs>? DataAvailable { add { } remove { } }
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void StartRecording() => Started = true;
        public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
        public void Dispose() { }
    }
}