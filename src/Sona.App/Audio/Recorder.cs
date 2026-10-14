using NAudio.Wave;
namespace Sona.App.Audio;

public sealed class Recorder : IDisposable
{
    private readonly Func<int, IWaveIn> inputFactory;
    private readonly TimeProvider clock;
    private IWaveIn? input;
    private readonly MemoryStream data = new();
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate = new();
    private bool disposed, stopRequested, unexpectedReported;
    private long startedAt, lastFrame;
    public event Action<string>? UnexpectedStop;
    public double Level { get; private set; }
    public double Seconds { get { lock (gate) return disposed ? 0 : data.Length / 32000.0; } }
    public double Elapsed => clock.GetElapsedTime(startedAt).TotalSeconds;
    public Recorder() : this(device => new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 60, NumberOfBuffers = 3 }) { }
    public Recorder(Func<int, IWaveIn> inputFactory, TimeProvider? clock = null) { this.inputFactory = inputFactory; this.clock = clock ?? TimeProvider.System; }
    public static IReadOnlyList<(int Id, string Name)> Devices() => Enumerable.Range(0, WaveIn.DeviceCount).Select(i => (i, WaveIn.GetCapabilities(i).ProductName)).ToList();
    public void Start(int device)
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); if (input != null) throw new InvalidOperationException("This recording has already started."); input = inputFactory(device); startedAt = lastFrame = clock.GetTimestamp(); }
        input.DataAvailable += (_, e) =>
        {
            lock (gate)
            {
                if (disposed || stopRequested || e.BytesRecorded == 0) return;
                lastFrame = clock.GetTimestamp();
                if (data.Length + e.BytesRecorded <= 32000 * 120) data.Write(e.Buffer, 0, e.BytesRecorded);
                double sum = 0;
                for (var i = 0; i + 1 < e.BytesRecorded; i += 2) { var sample = BitConverter.ToInt16(e.Buffer, i) / 32768.0; sum += sample * sample; }
                Level = Math.Sqrt(sum / Math.Max(1, e.BytesRecorded / 2));
            }
        };
        input.RecordingStopped += (_, e) =>
        {
            bool unexpected; lock (gate) { if (disposed) return; unexpected = !stopRequested; }
            if (unexpected) ReportUnexpected("The microphone stopped or became unavailable.", e.Exception);
            else if (e.Exception != null) { stopped.TrySetException(new IOException("The microphone became unavailable.", e.Exception)); _ = stopped.Task.Exception; }
            else stopped.TrySetResult();
        };
        try { input.StartRecording(); } catch { Dispose(); throw; }
    }
    public void CheckHealth()
    {
        lock (gate) { if (disposed || stopRequested || unexpectedReported || input == null || clock.GetElapsedTime(lastFrame).TotalSeconds < 5) return; }
        ReportUnexpected("The microphone stopped delivering audio. Check the connection and try again.");
    }
    private void ReportUnexpected(string message, Exception? cause = null)
    {
        lock (gate) { if (disposed || unexpectedReported) return; unexpectedReported = true; }
        stopped.TrySetException(new IOException(message, cause)); _ = stopped.Task.Exception;
        UnexpectedStop?.Invoke(message);
    }
    public async Task<byte[]> StopAsync()
    {
        IWaveIn? capture; lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); stopRequested = true; capture = input; }
        try { capture?.StopRecording(); await stopped.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
        finally { capture?.Dispose(); lock (gate) { if (ReferenceEquals(input, capture)) input = null; } }
        byte[] pcm; lock (gate) pcm = data.ToArray();
        try
        {
            if (pcm.Length < 9600) throw new IOException("Recording was too short. Hold the shortcut while you speak.");
            double energy = 0;
            for (var i = 0; i + 1 < pcm.Length; i += 2) { var sample = BitConverter.ToInt16(pcm, i) / 32768.0; energy += sample * sample; }
            if (Math.Sqrt(energy / (pcm.Length / 2)) < 0.002) throw new IOException("The recording was silent. Check your microphone selection.");
            using var wav = new MemoryStream();
            using (var writer = new WaveFileWriter(new NAudio.Utils.IgnoreDisposeStream(wav), new WaveFormat(16000, 16, 1))) writer.Write(pcm, 0, pcm.Length);
            return wav.ToArray();
        }
        finally { Array.Clear(pcm); }
    }
    public void Dispose()
    {
        IWaveIn? capture;
        lock (gate) { if (disposed) return; disposed = true; stopRequested = true; capture = input; input = null; if (data.TryGetBuffer(out var buffer)) Array.Clear(buffer.Array!); data.Dispose(); }
        stopped.TrySetCanceled(); capture?.Dispose();
    }
}
