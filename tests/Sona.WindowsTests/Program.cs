using System.Diagnostics;
using System.Text.Json;
using Sona.App.Audio;
using Sona.App.Windows;
using NAudio.Wave;

var executable = Environment.ProcessPath!;
if (args.Length == 3 && args[0] == "--recording-audio-fixture") { BoundedProcess.ContainApplication(); Console.WriteLine(JsonSerializer.Serialize(RecordingAudioFixture.Run(args[1], args[2]))); return; }
if (args.Length == 1 && args[0] == "--sound-feedback-checks") { Console.WriteLine($"{await SoundFeedbackChecks.RunAsync()} silent sound-feedback checks passed."); return; }
if (args.Length == 1 && args[0] == "--history-range-checks") { Console.WriteLine($"{HistoryFilteringChecks.Run()} history range checks passed."); return; }
if (args.Length == 2 && args[0] == "--discord-settings-fixture") { Console.WriteLine(JsonSerializer.Serialize(DiscordSettingsFixture.Run(args[1]))); return; }
if (args.Length == 2 && args[0] == "--history-filter-fixture") { Console.WriteLine(JsonSerializer.Serialize(HistoryFilteringFixture.Run(args[1]))); return; }
if (args.Length == 2 && args[0] == "--history-retention-off-fixture") { Console.WriteLine(JsonSerializer.Serialize(HistoryFilteringFixture.Run(args[1], true))); return; }
if (args.Length == 1 && args[0] == "--discord-disconnect-checks") { Console.WriteLine($"{await DiscordDisconnectChecks.RunAsync()} Discord disconnect checks passed."); return; }
if (args.Length == 1 && args[0] == "--active-app-state-fixture") { Console.WriteLine($"{await ActiveApplicationChecks.RunAsync(false)} active-app state checks passed."); return; }
if (args.Length == 2 && args[0] == "--shared-button-fixture") { Console.WriteLine(JsonSerializer.Serialize(SharedButtonContentFixture.Run(args[1]))); return; }
if (args.Length == 2 && args[0] is "--personalization-fixture" or "--personalization-reopen") { Console.WriteLine(JsonSerializer.Serialize(PersonalizationFixture.Run(args[1], args[0].EndsWith("reopen")))); return; }
if (args.Length == 1 && args[0] == "--completed-paste-fixture") { Console.WriteLine($"{await CompletedDraftPasteChecks.RunAsync()} completed-paste checks passed."); return; }
if (args.Length == 2 && args[0] == "--overlay-fixture") { Console.WriteLine(JsonSerializer.Serialize(OverlayVisualFixture.Run(args[1]))); return; }
if (args.Length == 2 && args[0] == "--native-app-icons") { Console.WriteLine(JsonSerializer.Serialize(await NativeApplicationIconFixture.RunAsync(args[1]))); return; }
if (args.Length == 1 && args[0] == "--application-icon-fixture") { Console.WriteLine($"{await ApplicationIconChecks.RunAsync()} icon checks passed."); return; }
if (args.Length == 3 && args[0] == "--live-speech-fixture") { BoundedProcess.ContainApplication(); Console.WriteLine(JsonSerializer.Serialize(await LiveSpeechFixture.RunAsync(args[1], args[2]))); return; }
if (args.Length == 2 && args[0] == "--live-draft-fixture") { Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(LiveDictationFixture.Run(args[1]))); return; }
if (args.Length == 1 && args[0] == "--insertion-fixture") { Console.WriteLine($"{await InsertionChecks.RunAsync()} insertion checks passed."); return; }
if (args.Length == 1 && args[0] == "--presentation-fixture")
{
    var report = ThemeMaterialFixture.RunPresentation();
    Console.WriteLine(JsonSerializer.Serialize(report));
    if (!report.StartupCallbackObserved || !report.ProductionStartupSuppressed || !report.AppResourcesLoaded || report.Failures.Length > 0)
        throw new InvalidOperationException("Presentation fixture failed: " + string.Join("; ", report.Failures));
    return;
}
if (args.Length == 1 && args[0] == "--active-target-fixture") { Console.WriteLine($"{await ActiveApplicationChecks.RunAsync()} active-target checks passed."); return; }
if (args.Length == 1 && args[0] == "--theme-material-fixture")
{
    var report = ThemeMaterialFixture.RunIsolated();
    Console.WriteLine(JsonSerializer.Serialize(report));
    if (!report.StartupCallbackObserved || !report.ProductionStartupSuppressed || report.Failures.Length > 0)
        throw new InvalidOperationException("Theme material fixture failed: " + string.Join("; ", report.Failures));
    return;
}
if (args.Length == 1 && args[0] == "--theme-startup-fixture")
{
    var report = ThemeMaterialFixture.RunStartupInterception();
    Console.WriteLine(JsonSerializer.Serialize(report));
    if (!report.StartupCallbackObserved || !report.ProductionStartupSuppressed || !report.AppResourcesLoaded || report.Failures.Length > 0)
        throw new InvalidOperationException("Theme startup fixture failed: " + string.Join("; ", report.Failures));
    return;
}
ProcessStartInfo Self(params string[] args) { var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true }; foreach (var arg in args) info.ArgumentList.Add(arg); return info; }
if (args.Length == 2 && args[0] == "--cleanup-fixture")
{
    BoundedProcess.ContainApplication();
    await CleanupIntegrationChecks.RunAsync(args[1]); return;
}
if (args.Length == 5 && args[0] == "--speech-fixture")
{
    BoundedProcess.ContainApplication();
    await ParakeetIntegrationChecks.RunAsync(args[1], args[2], args[3], args[4]); return;
}
if (args.Length == 2 && args[0] == "--speech-memory-fixture")
{
    BoundedProcess.ContainApplication();
    await ParakeetIntegrationChecks.MemoryAsync(args[1]); return;
}
if (args.Length > 0 && args[0] == "--held-file")
{
    using var held = new FileStream(args[1], FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    File.WriteAllText(args[2], Environment.ProcessId.ToString()); await Task.Delay(TimeSpan.FromSeconds(60)); return;
}
if (args.Length > 0 && args[0] == "--contain-parent")
{
    BoundedProcess.ContainApplication();
    using var worker = new BoundedProcess(Self("--held-file", Path.Combine(args[1], "held.wav"), Path.Combine(args[1], "child.pid")));
    File.WriteAllText(Path.Combine(args[1], "parent.json"), JsonSerializer.Serialize(new { Child = worker.Process.Id, Inherited = BoundedProcess.InApplicationJob(worker.Process.Handle) }));
    await Task.Delay(TimeSpan.FromSeconds(60)); return;
}
if (args.Length == 2 && args[0] == "--resident-fixture")
{
    BoundedProcess.ContainApplication(); var clock = new TestClock(); var resident = new ResidentModel(clock);
    var p = new Sona.Core.Preferences { Model = "tiny", Engine = "FastCpu", Language = "fr", ModelIdleMinutes = 1 };
    var checks = new List<string>(); void Proof(bool value, string name) { if (!value) throw new Exception(name); checks.Add(name); }
    var timings = new List<double>();
    try
    {
        var load = Stopwatch.StartNew(); await resident.WarmAsync(p, CancellationToken.None); load.Stop();
        var firstPid = resident.ProcessId; Proof(firstPid != null && resident.LoadedModel == "tiny", "Selected Tiny model loads without microphone capture");
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var input = await File.ReadAllBytesAsync(args[1]); var elapsed = Stopwatch.StartNew(); var result = await resident.RunAsync(input, p, CancellationToken.None); elapsed.Stop(); timings.Add(elapsed.Elapsed.TotalMilliseconds);
            Proof(result.Length > 0 && input.All(b => b == 0), "Fixture transcription returns text and clears input audio " + iteration);
            Proof(resident.ProcessId == firstPid, "Two dictations reuse the same resident worker " + iteration);
        }
        clock.Advance(59); await resident.ExpireAsync(); Proof(resident.ProcessId == firstPid, "Idle timeout retains model before deadline");
        clock.Advance(2); await resident.ExpireAsync(); Proof(resident.LoadedModel == null && resident.ProcessId == null, "Idle timeout unloads model after deadline");
        p.ModelIdleMinutes = 0; await resident.WarmAsync(p, CancellationToken.None); var retainedPid = resident.ProcessId;
        clock.Advance(3600); await resident.ExpireAsync(); Proof(resident.ProcessId == retainedPid, "Keep until quit retains model after long inactivity");
        p.Model = "small"; await resident.WarmAsync(p, CancellationToken.None);
        Proof(resident.LoadedModel == "small" && resident.ProcessId != retainedPid, "Switching models replaces the resident worker");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var cancelledAudio = await File.ReadAllBytesAsync(args[1]);
        try { await resident.RunAsync(cancelledAudio, p, cancelled.Token); throw new Exception("Cancellation was ignored"); } catch (OperationCanceledException) { }
        Proof(cancelledAudio.All(b => b == 0), "Cancelled queued transcription clears audio without running");
        using var midRequest = new CancellationTokenSource(TimeSpan.FromMilliseconds(20)); var activeAudio = await File.ReadAllBytesAsync(args[1]);
        try { await resident.RunAsync(activeAudio, p, midRequest.Token); throw new Exception("In-flight cancellation was ignored"); } catch (OperationCanceledException) { }
        Proof(activeAudio.All(b => b == 0), "In-flight cancellation clears the audio buffer");
        Proof(resident.ProcessId == null && resident.LoadedModel == null && !resident.CleanupPending, "In-flight cancellation confirms resident worker release");
        await resident.UnloadAsync(); Proof(resident.LoadedModel == null && resident.ProcessId == null, "Explicit unload releases worker");
        if (Transcriber.CudaEngineAvailable && Transcriber.Models.Present("turbo")) { p.Model = "turbo"; p.Engine = "Auto"; await resident.WarmAsync(p, CancellationToken.None); Proof(resident.LoadedModel == "turbo", "Resident Turbo confirms real CUDA backend"); await resident.UnloadAsync(); }
        Console.WriteLine(JsonSerializer.Serialize(new { Passed = checks.Count, Checks = checks, InitialLoadMilliseconds = load.Elapsed.TotalMilliseconds, WarmFixtureMilliseconds = timings, MicrophoneOpened = false, GlobalInputSent = false }));
    }
    finally { await resident.UnloadAsync(); }
    return;
}
if (args.Length != 0)
{
    Console.Error.WriteLine("Unknown or malformed fixture arguments. No integration checks were started.");
    Environment.ExitCode = 2;
    return;
}
var passed = new List<string>();
void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed.Add(label); Console.WriteLine("PASS " + label); }
var credentialTestPath = Path.Combine(Path.GetTempPath(), "sona-credential-test-" + Guid.NewGuid().ToString("N"), "credentials.dpapi");
var fixtureVault = new ProtectedCredentials(credentialTestPath);
fixtureVault.Save(new DiscordCredentials { ClientId = "123456789", ClientSecret = "fixture-secret-only", AccessToken = "fixture-access-only", ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600 });
Check(fixtureVault.Load().ClientSecret == "fixture-secret-only" && !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(credentialTestPath)).Contains("fixture-secret-only"), "Discord fixture setup round trips through current-user Windows encryption");
var fakePipePrefix = "sona-test-discord-" + Guid.NewGuid().ToString("N") + "-";
using var fakeServerStop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var fakeSubscriptions = new List<string>(); var fakeState = new Sona.Core.VoiceState(true, false, false); int fakeCommands = 0;
using var fakePipe = new System.IO.Pipes.NamedPipeServerStream(fakePipePrefix + "0", System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
async Task<(int Opcode, JsonElement Data)> ReadFakeFrame()
{
    var header = new byte[8]; await fakePipe.ReadExactlyAsync(header, fakeServerStop.Token);
    var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4)); if (length is < 1 or > 65536) throw new IOException();
    var payload = new byte[length]; await fakePipe.ReadExactlyAsync(payload, fakeServerStop.Token); using var doc = JsonDocument.Parse(payload); return (BitConverter.ToInt32(header), doc.RootElement.Clone());
}
async Task WriteFakeFrame(int opcode, object payload)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(payload); var header = new byte[8]; BitConverter.GetBytes(opcode).CopyTo(header, 0); BitConverter.GetBytes(bytes.Length).CopyTo(header, 4);
    await fakePipe.WriteAsync(header, fakeServerStop.Token); await fakePipe.WriteAsync(bytes, fakeServerStop.Token); await fakePipe.FlushAsync(fakeServerStop.Token);
}
var fakeServerTask = Task.Run(async () =>
{
    try
    {
        await fakePipe.WaitForConnectionAsync(fakeServerStop.Token); var handshake = await ReadFakeFrame();
        if (handshake.Opcode != 0 || handshake.Data.GetProperty("v").GetInt32() != 1) throw new IOException("Invalid fixture handshake");
        await WriteFakeFrame(1, new { evt = "READY", data = new { } });
        while (!fakeServerStop.IsCancellationRequested)
        {
            var frame = await ReadFakeFrame(); if (frame.Opcode != 1) continue;
            var command = frame.Data.GetProperty("cmd").GetString(); var nonce = frame.Data.GetProperty("nonce").GetString(); fakeCommands++;
            object data = new { };
            if (command == "SUBSCRIBE") fakeSubscriptions.Add(frame.Data.GetProperty("evt").GetString()!);
            else if (command == "GET_SELECTED_VOICE_CHANNEL") data = new { id = "fixture-channel" };
            else if (command == "GET_VOICE_SETTINGS") data = new { mute = fakeState.Muted, deaf = fakeState.Deafened };
            else if (command == "SET_VOICE_SETTINGS")
            {
                var a = frame.Data.GetProperty("args");
                if (a.TryGetProperty("mute", out var mute)) fakeState = fakeState with { Muted = mute.GetBoolean() };
                if (a.TryGetProperty("deaf", out var deaf)) fakeState = fakeState with { Deafened = deaf.GetBoolean() };
                data = new { mute = fakeState.Muted, deaf = fakeState.Deafened };
            }
            await WriteFakeFrame(1, new { cmd = command, nonce, data });
        }
    }
    catch (Exception ex) when (ex is OperationCanceledException or EndOfStreamException or IOException or ObjectDisposedException) { }
});
var fixtureSources = new HashSet<string> { "fixture-external-app" };
await using (var service = new ProtectionService(fixtureVault, fakePipePrefix, _ => new HashSet<string>(fixtureSources)))
{
    await service.ConnectAsync(false, fakeServerStop.Token);
    Check(service.Connected && fakeSubscriptions.SequenceEqual(new[] { "VOICE_SETTINGS_UPDATE", "VOICE_CHANNEL_SELECT" }), "Discord fixture connection authenticates and subscribes to both voice events");
    await service.PauseAsync(false, fakeServerStop.Token);
    Check(!fakeState.Muted && !fakeState.Deafened && !service.OwnsChanges, "Connecting and resuming while protection is Off never changes voice state despite an active watched source");
    await service.ConfigureAsync(new Sona.Core.Preferences { ProtectionEnabled = true, ProtectionReleaseDelayMs = 0 }, fakeServerStop.Token);
    await service.BeginOwnRecordingAsync(fakeServerStop.Token);
    Check(fakeState.Muted && fakeState.Deafened && service.OwnsChanges && fakeCommands >= 7, "Own recording preparation awaits framed Discord mute and deafen acknowledgements");
    await service.EndOwnRecordingAsync(fakeServerStop.Token);
    Check(fakeState.Muted && fakeState.Deafened, "Ending Sona recording retains protection while a watched app remains active");
    fixtureSources.Clear(); await service.EndOwnRecordingAsync(fakeServerStop.Token);
    Check(!fakeState.Muted && !fakeState.Deafened && !service.OwnsChanges, "Recording cleanup restores owned voice changes after the final source ends");
    await service.RestoreForQuitAsync(fakeServerStop.Token);
}
fakeServerStop.Cancel(); await fakeServerTask; File.Delete(credentialTestPath); Directory.Delete(Path.GetDirectoryName(credentialTestPath)!);
Exception? shortcutFailure = null; var shortcutStarted = false; var shortcutStopped = false; var shortcutCallbacks = 0;
var shortcutTest = new Thread(() =>
{
    try
    {
        using var window = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Sona shortcut lifecycle test") { WindowStyle = 0, Width = 1, Height = 1 });
        using var hotkeys = new Hotkeys(window.Handle, _ => { shortcutCallbacks++; return false; }, () => shortcutCallbacks++, () => shortcutCallbacks++, () => shortcutCallbacks++, false);
        shortcutStarted = hotkeys.ListenerAlive;
        hotkeys.ResetBaseline(); hotkeys.Recording(false); hotkeys.Dispose(); shortcutStopped = !hotkeys.ListenerAlive;
    }
    catch (Exception ex) { shortcutFailure = ex; }
}) { IsBackground = true };
shortcutTest.SetApartmentState(ApartmentState.STA); shortcutTest.Start();
if (!shortcutTest.Join(TimeSpan.FromSeconds(6))) throw new TimeoutException("Shortcut listener lifecycle test exceeded six seconds");
if (shortcutFailure != null) throw new Exception("Native shortcut listener test failed", shortcutFailure);
Check(shortcutStarted, "Dedicated native keyboard hook and message timer initialize without a microphone");
Check(shortcutStopped && shortcutCallbacks == 0, "Shortcut reset and disposal stop the listener without starting capture or sending input");
Exception? preparationFailure = null; bool replacementBlocked = false, cancelledBeforeCapture = false, handsFreeTestSafe = false, themeMaterialSafe = false, themeStartupIntercepted = false;
var themeMaterialFailureDetails = new List<string>();
var preparationRoot = Path.Combine(Path.GetTempPath(), "sona-preparation-test-" + Guid.NewGuid().ToString("N"));
var preparationPriorData = Environment.GetEnvironmentVariable("SONA_DATA"); Environment.SetEnvironmentVariable("SONA_DATA", preparationRoot);
var preparationThread = new Thread(() =>
{
    var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
    SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
    var application = new Sona.App.ThemeResourceTestApplication(); application.InitializeComponent();
    dispatcher.BeginInvoke(new Action(async () =>
    {
        try
        {
            themeStartupIntercepted = application.StartupCallbackObserved && application.MainWindow is null && application.Windows.Count == 0;
            if (!themeStartupIntercepted) themeMaterialFailureDetails.Add("WPF startup callback was not intercepted before production window initialization");
            themeMaterialFailureDetails.AddRange(ThemeMaterialFixture.CheckResources(application));
            themeMaterialSafe = themeMaterialFailureDetails.Count == 0;
            Sona.App.ThemeManager.Apply(Sona.Core.ThemeCatalog.DefaultId);
            var window = new Sona.App.MainWindow(new[] { "--test-ui" });
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var fake = new FakeCapture();
            window.PreparationForTests = _ => barrier.Task; window.RecorderForTests = () => new Recorder(_ => fake);
            var begin = window.BeginForTestsAsync(); window.PrepareShortcutForTests();
            replacementBlocked = window.SessionForTests == Sona.Core.SessionState.Recording;
            window.CancelForTests(); barrier.SetResult();
            cancelledBeforeCapture = !await begin && !fake.Started;
            window.PrepareShortcutForTests();
            handsFreeTestSafe = !await window.BeginShortcutForTestsAsync(false) && !fake.Started && window.SessionForTests == Sona.Core.SessionState.Ready;
        }
        catch (Exception ex) { preparationFailure = ex; }
        finally { application.Shutdown(); dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send); }
    }));
    System.Windows.Threading.Dispatcher.Run();
}) { IsBackground = true };
preparationThread.SetApartmentState(ApartmentState.STA); preparationThread.Start();
if (!preparationThread.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("Preparation regression exceeded its deadline");
Environment.SetEnvironmentVariable("SONA_DATA", preparationPriorData);
if (preparationFailure != null) throw new Exception("Preparation regression failed", preparationFailure);
Check(replacementBlocked, "Shortcut-test preparation cannot replace a session awaiting voice protection");
Check(cancelledBeforeCapture, "Cancelling during asynchronous preparation prevents even a fake microphone from starting");
Check(handsFreeTestSafe, "Double-tap cannot start a microphone during the explicitly microphone-free insertion test");
Check(themeStartupIntercepted, "Theme resource checks intercept WPF startup before production window or service initialization");
Check(themeMaterialSafe, "All eight WPF themes create palette-derived glass materials with stable overlay surface and edge resources" + (themeMaterialFailureDetails.Count == 0 ? "" : ": " + string.Join("; ", themeMaterialFailureDetails)));
if (Directory.Exists(preparationRoot)) Directory.Delete(preparationRoot, true);
var time = new TestClock(); var capture = new FakeCapture();
using (var recorder = new Recorder(_ => capture, time))
{
    var reports = 0; recorder.UnexpectedStop += _ => reports++;
    recorder.Start(-1); capture.Disconnect(null); recorder.CheckHealth();
    Check(reports == 1, "Unexpected null-error device stop is surfaced exactly once");
}
Check(capture.Disposed, "Disconnected device is disposed");
time = new(); capture = new();
using (var recorder = new Recorder(_ => capture, time))
{
    var reports = 0; recorder.UnexpectedStop += _ => reports++; recorder.Start(-1);
    time.Advance(4); recorder.CheckHealth(); Check(reports == 0, "Transient capture delay does not fail early");
    time.Advance(2); recorder.CheckHealth(); recorder.CheckHealth(); Check(reports == 1 && recorder.Elapsed >= 6, "Stalled capture is bounded even when sample count never increases");
}
capture = new();
using (var recorder = new Recorder(_ => capture))
{
    var reports = 0; recorder.UnexpectedStop += _ => reports++; recorder.Start(-1); capture.Emit(new byte[32000]);
    try { await recorder.StopAsync(); throw new Exception("Silence was accepted"); } catch (IOException ex) when (ex.Message.Contains("silent", StringComparison.Ordinal)) { }
    Check(reports == 0 && capture.Disposed, "Intentional silent stop fails safely and releases device");
}
capture = new();
using (var recorder = new Recorder(_ => capture))
{
    var reports = 0; recorder.UnexpectedStop += _ => reports++; recorder.Start(-1);
    var pcm = new byte[32000]; for (var i = 0; i < pcm.Length; i += 2) { pcm[i] = 0; pcm[i + 1] = 8; }
    capture.Emit(pcm); var wav = await recorder.StopAsync(); Check(wav.Length > pcm.Length && reports == 0, "Intentional audio stop produces WAV without false disconnect"); Array.Clear(wav);
    recorder.Dispose(); recorder.Dispose(); Check(capture.Disposed, "Recorder disposal is idempotent");
}

BoundedProcess.ContainApplication();
var destination = new DraftTarget((nint)1, 10, [1, 2], 0); var sends = 0;
using (var cancelled = new CancellationTokenSource())
using (var insertion = new Insertion(() => { cancelled.Cancel(); return destination; }, () => false, _ => { sends++; return 2; }))
{
    try { await insertion.InsertAsync(destination, "test", cancelled.Token); throw new Exception("Cancelled target read was accepted"); } catch (OperationCanceledException) { }
    Check(sends == 0, "Cancellation during final target inspection sends no input");
}
using (var insertion = new Insertion(() => destination with { FocusEpoch = 1 }, () => false, _ => { sends++; return 2; }))
{
    Check(!(await insertion.InsertAsync(destination, "test")).Inserted && sends == 0, "Changed focus epoch refuses all input");
    Check(!(await insertion.InsertAsync(destination, "test\nsubmit")).Inserted && sends == 0, "Line break payload never reaches input sink");
}
var root = Path.Combine(Path.GetTempPath(), "sona-lifetime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("SONA_DATA", root);
var scratch = Path.Combine(Transcriber.ScratchRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch); File.WriteAllText(Path.Combine(scratch, "owner.marker"), Sona.Core.AppPaths.ScratchMarker);
async Task WaitFile(string path) { for (var i = 0; i < 100 && !File.Exists(path); i++) await Task.Delay(30); if (!File.Exists(path)) throw new TimeoutException(path); }
try
{
    int childId;
    using (var worker = new BoundedProcess(Self("--held-file", Path.Combine(scratch, "audio.wav"), Path.Combine(root, "worker.pid"))))
    {
        childId = worker.Process.Id; await WaitFile(Path.Combine(root, "worker.pid"));
        Check(BoundedProcess.InApplicationJob(worker.Process.Handle), "Created worker inherits containment before operation-job assignment");
    }
    bool Gone(int id) { try { using var process = Process.GetProcessById(id); return process.HasExited; } catch (ArgumentException) { return true; } }
    Check(Gone(childId), "Worker disposal confirms process termination"); await Transcriber.CleanupScratchAsync(scratch);
    Check(!Directory.Exists(scratch), "Scratch audio and recovery marker removed after worker file-lock release");
    scratch = Path.Combine(Transcriber.ScratchRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch); File.WriteAllText(Path.Combine(scratch, "owner.marker"), Sona.Core.AppPaths.ScratchMarker);
    var transcriber = new Transcriber();
    using (var held = new FileStream(Path.Combine(scratch, "audio.wav"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
    {
        try { await transcriber.CleanupOrRetainAsync(scratch); throw new Exception("Persistent lock was ignored"); } catch (ScratchCleanupException) { }
        Check(transcriber.CleanupPending && File.Exists(Path.Combine(scratch, "owner.marker")) && File.Exists(Path.Combine(scratch, "audio.wav")), "Persistent audio lock retains recovery marker and distinct cleanup failure");
    }
    await transcriber.RetryCleanupAsync(); Check(!transcriber.CleanupPending && !Directory.Exists(scratch), "Retained audio cleanup can be retried after lock release");
    using var parent = Process.Start(Self("--contain-parent", root))!;
    try
    {
        await WaitFile(Path.Combine(root, "parent.json")); await WaitFile(Path.Combine(root, "child.pid"));
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "parent.json"))); childId = state.RootElement.GetProperty("Child").GetInt32();
        Check(state.RootElement.GetProperty("Inherited").GetBoolean(), "Child owned by abrupt-exit test parent is contained");
        parent.Kill(); await parent.WaitForExitAsync();
        for (var i = 0; i < 100 && !Gone(childId); i++) await Task.Delay(30);
        Check(Gone(childId), "Abrupt app termination leaves no model worker");
    }
    finally { if (!parent.HasExited) { parent.Kill(true); await parent.WaitForExitAsync(); } }
}
finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
CleanupModelChecks.Pure(Check);
Console.WriteLine(JsonSerializer.Serialize(new { Passed = passed.Count, Checks = passed, MicrophoneOpened = false, GlobalInputSent = false }));

sealed class TestClock : TimeProvider
{
    private long now;
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => now;
    public void Advance(int seconds) => now += seconds * 1000;
}
sealed class FakeCapture : IWaveIn
{
    public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;
    public bool Disposed { get; private set; }
    public bool Started { get; private set; }
    public void StartRecording() { Started = true; }
    public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
    public void Emit(byte[] pcm) => DataAvailable?.Invoke(this, new WaveInEventArgs(pcm, pcm.Length));
    public void Disconnect(Exception? cause) => RecordingStopped?.Invoke(this, new StoppedEventArgs(cause));
    public void Dispose() => Disposed = true;
}
