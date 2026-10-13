using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Murmur.Core;
using Murmur.App.Windows;
namespace Murmur.App.Audio;

// One contained resident worker. The loopback route is a fresh 256-bit capability,
// not a public agent API. Audio remains in memory; neither worker stream is logged.
internal sealed class ResidentModel
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HttpClient http = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 1024 * 1024 };
    private readonly TimeProvider clock;
    private BoundedProcess? worker;
    private Uri? endpoint;
    private Task drainOutput = Task.CompletedTask, drainError = Task.CompletedTask;
    private string? key;
    private string? loaded;
    private long lastUse;
    private int idleMinutes = 10;
    private int usedCuda;
    private int processId;
    private bool cleanupPending;
    public bool CleanupPending => Volatile.Read(ref cleanupPending);
    public string? LoadedModel => Volatile.Read(ref loaded);
    public int? ProcessId => Volatile.Read(ref processId) is var id && id > 0 ? id : null;
    public ResidentModel(TimeProvider? clock = null) => this.clock = clock ?? TimeProvider.System;
    public async Task WarmAsync(Preferences p, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try { await EnsureAsync(p, cancellation).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task<string> RunAsync(byte[] wav, Preferences p, CancellationToken cancellation)
    {
        try
        {
            await gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(90));
                await EnsureAsync(p, deadline.Token).ConfigureAwait(false);
                using var body = new MultipartFormDataContent();
                body.Add(new ByteArrayContent(wav), "file", "dictation.wav");
                body.Add(new StringContent(p.Language), "language");
                body.Add(new StringContent("json"), "response_format");
                body.Add(new StringContent("true"), "no_context");
                body.Add(new StringContent("true"), "suppress_nst");
                var prompt = string.Join(", ", p.Terms.Concat(p.Vocabulary.Select(x => x.Written)).Distinct().Take(60));
                body.Add(new StringContent(prompt), "prompt");
                using var reply = await http.PostAsync(new Uri(endpoint!, "inference"), body, deadline.Token).ConfigureAwait(false);
                if (!reply.IsSuccessStatusCode) throw new IOException("Local transcription failed. Your draft was left untouched.");
                var bytes = await reply.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
                try
                {
                    if (bytes.Length > 1024 * 1024) throw new IOException("Local transcription response exceeded its limit.");
                    using var result = JsonDocument.Parse(bytes);
                    if (!result.RootElement.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String) throw new IOException("Local transcription returned an invalid response.");
                    var text = value.GetString() ?? "";
                    if (text.Length > 24000) throw new IOException("Local transcription text exceeded its limit.");
                    lastUse = clock.GetTimestamp(); return text.Trim();
                }
                finally { Array.Clear(bytes); }
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or JsonException or IOException)
            {
                await ReleaseAsync().ConfigureAwait(false);
                if (ex is OperationCanceledException && !cancellation.IsCancellationRequested) throw new IOException("Local transcription reached its 90 second limit. Try a shorter recording.");
                if (ex is HttpRequestException or JsonException) throw new IOException("Local transcription failed. Your draft was left untouched.", ex);
                throw;
            }
            finally { gate.Release(); }
        }
        finally { Array.Clear(wav); }
    }
    private async Task EnsureAsync(Preferences p, CancellationToken cancellation)
    {
        var model = Transcriber.SelectedModel(p);
        var cuda = p.Engine != "FastCpu" && Transcriber.CudaEngineAvailable;
        idleMinutes = p.ModelIdleMinutes;
        if (CleanupPending) throw new IOException("The previous model is still releasing memory. Use Unload now to retry cleanup.");
        var nextKey = model + (cuda ? ":cuda" : ":cpu");
        if (worker != null && key == nextKey && !worker.Process.HasExited) { lastUse = clock.GetTimestamp(); return; }
        await ReleaseAsync().ConfigureAwait(false);
        if (!Transcriber.Models.Present(model) || !await Transcriber.Models.VerifyAsync(model, cancellation).ConfigureAwait(false)) throw new IOException("Install and verify the selected local model first.");
        var executable = Path.Combine(Transcriber.RuntimeRoot, cuda ? "whisper-cuda" : "whisper", "Release", "whisper-server.exe");
        if (!File.Exists(executable)) throw new IOException("The resident local engine is missing. Repair the local runtime.");
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var capability = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        endpoint = new Uri($"http://127.0.0.1:{port}/{capability}/");
        var info = new ProcessStartInfo(executable);
        foreach (var arg in new[] { "-m", Transcriber.Models.PathFor(model), "-t", "6", "-bs", cuda ? "5" : "1", "-bo", cuda ? "5" : "1", "-l", "auto", "-nf", "-sns", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--request-path", "/" + capability, "--public", Path.Combine(Transcriber.RuntimeRoot, "no-public-files") }) info.ArgumentList.Add(arg);
        if (!cuda) info.ArgumentList.Add("-ng");
        try
        {
            Volatile.Write(ref usedCuda, 0);
            worker = new BoundedProcess(info, cuda ? 3072 : 1536);
            Volatile.Write(ref processId, worker.Process.Id);
            drainOutput = DrainAsync(worker.Process.StandardOutput, false);
            drainError = DrainAsync(worker.Process.StandardError, true);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(60));
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (worker.Process.HasExited) throw new IOException("The local model could not load within its memory budget.");
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); probe.CancelAfter(TimeSpan.FromSeconds(1));
                    using var response = await http.GetAsync(new Uri(endpoint, "health"), probe.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        using var health = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(probe.Token).ConfigureAwait(false));
                        if (health.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok") break;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !deadline.IsCancellationRequested) { }
                await Task.Delay(80, deadline.Token).ConfigureAwait(false);
            }
            if (cuda && Volatile.Read(ref usedCuda) == 0) throw new IOException("The resident engine did not confirm CUDA. Select CPU or repair its runtime.");
            cancellation.ThrowIfCancellationRequested(); key = nextKey; Volatile.Write(ref loaded, model); lastUse = clock.GetTimestamp();
        }
        catch (Exception ex) { await ReleaseAsync().ConfigureAwait(false); if (ex is System.ComponentModel.Win32Exception or UnauthorizedAccessException or InvalidOperationException) throw new IOException("The local model could not start safely. Check its files and runtime.", ex); throw; }
    }
    private async Task DrainAsync(StreamReader reader, bool detectCuda)
    {
        var buffer = new char[2048]; string tail = "";
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer).ConfigureAwait(false); if (count == 0) break;
                if (detectCuda) { var sample = tail + new string(buffer, 0, count); if (sample.Contains("using CUDA0 backend", StringComparison.Ordinal)) Volatile.Write(ref usedCuda, 1); tail = sample.Length > 32 ? sample[^32..] : sample; }
                Array.Clear(buffer);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        finally { Array.Clear(buffer); }
    }
    public async Task ExpireAsync(CancellationToken cancellation = default)
    {
        if (!await gate.WaitAsync(0, cancellation).ConfigureAwait(false)) return;
        try { if (worker != null && (CleanupPending || worker.Process.HasExited || idleMinutes > 0 && clock.GetElapsedTime(lastUse).TotalMinutes >= idleMinutes)) await ReleaseAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task UnloadAsync(CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try { await ReleaseAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task ReleaseAsync()
    {
        Volatile.Write(ref cleanupPending, true);
        var prior = worker;
        if (prior != null) prior.Dispose();
        try { await Task.WhenAll(drainOutput, drainError).WaitAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false); }
        catch (TimeoutException ex) { throw new IOException("The previous model's output is still closing. Retry Unload now.", ex); }
        worker = null; Volatile.Write(ref loaded, null); Volatile.Write(ref processId, 0); key = null; endpoint = null;
        Volatile.Write(ref cleanupPending, false);
    }
}
