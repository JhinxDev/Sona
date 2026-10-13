using System.Diagnostics;
using System.Text.Json;
using Murmur.App.Audio;
using Murmur.App.Windows;
using NAudio.Wave;

var executable = Environment.ProcessPath!;
ProcessStartInfo Self(params string[] args) { var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true }; foreach (var arg in args) info.ArgumentList.Add(arg); return info; }
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
var passed = new List<string>();
void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed.Add(label); Console.WriteLine("PASS " + label); }
Exception? shortcutFailure = null; var shortcutStarted = false; var shortcutStopped = false; var shortcutCallbacks = 0;
var shortcutTest = new Thread(() =>
{
    try
    {
        using var window = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Murmur shortcut lifecycle test") { WindowStyle = 0, Width = 1, Height = 1 });
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
var root = Path.Combine(Path.GetTempPath(), "murmur-lifetime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("MURMUR_DATA", root);
var scratch = Path.Combine(Transcriber.ScratchRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch); File.WriteAllText(Path.Combine(scratch, "owner.marker"), "Murmur scratch v1");
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
    scratch = Path.Combine(Transcriber.ScratchRoot, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(scratch); File.WriteAllText(Path.Combine(scratch, "owner.marker"), "Murmur scratch v1");
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
    public void StartRecording() { }
    public void StopRecording() => RecordingStopped?.Invoke(this, new StoppedEventArgs());
    public void Emit(byte[] pcm) => DataAvailable?.Invoke(this, new WaveInEventArgs(pcm, pcm.Length));
    public void Disconnect(Exception? cause) => RecordingStopped?.Invoke(this, new StoppedEventArgs(cause));
    public void Dispose() => Disposed = true;
}
