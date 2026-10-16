using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Sona.Core;
using Sona.App.Windows;
namespace Sona.App.Audio;

// One contained resident worker. The loopback route is a fresh 256-bit capability,
// not a public agent API. Audio remains in memory; neither worker stream is logged.
internal sealed class ResidentModel
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HttpClient http = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 1024 * 1024 };
    private readonly TimeProvider clock;
    private readonly Func<string, CancellationToken, Task<bool>> verifyModel;
    private readonly TimeSpan startupTimeout;
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
    private bool parakeet;
    private string? apiKey;
    public bool CleanupPending => Volatile.Read(ref cleanupPending);
    public string? LoadedModel => Volatile.Read(ref loaded);
    public int? ProcessId => Volatile.Read(ref processId) is var id && id > 0 ? id : null;
    public ResidentModel(TimeProvider? clock = null, Func<string, CancellationToken, Task<bool>>? verifyModel = null, TimeSpan? startupTimeout = null)
    {
        this.clock = clock ?? TimeProvider.System;
        this.verifyModel = verifyModel ?? ((model, token) => Transcriber.Models.VerifyAsync(model, token));
        this.startupTimeout = startupTimeout ?? TimeSpan.FromSeconds(60);
    }
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
                string text;
                if (parakeet)
                {
                    var parts = SpeechAudio.Plan(wav);
                    var results = new List<string>(); var characters = 0;
                    foreach (var part in parts)
                    {
                        var input = parts.Count == 1 ? wav : SpeechAudio.Build(wav, part);
                        try
                        {
                            var result = await InferAsync(input, p, deadline.Token).ConfigureAwait(false);
                            characters += result.Length + (results.Count > 0 ? 1 : 0);
                            if (characters > 24000) throw new IOException("Local speech text exceeded its limit.");
                            if (result.Length != 0) results.Add(result);
                        }
                        finally { if (!ReferenceEquals(input, wav)) Array.Clear(input); }
                    }
                    text = string.Join(" ", results);
                }
                else text = await InferAsync(wav, p, deadline.Token).ConfigureAwait(false);
                lastUse = clock.GetTimestamp(); return text;
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
    private async Task<string> InferAsync(byte[] wav, Preferences p, CancellationToken token)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(wav), "file", "dictation.wav");
        body.Add(new StringContent(p.Language), "language");
        body.Add(new StringContent("json"), "response_format");
        var terms = p.Terms.Concat(p.Vocabulary.Select(x => x.Written)).Distinct().Take(60).ToArray();
        if (parakeet)
        {
            body.Add(new StringContent("true"), "automatic_punctuation");
            body.Add(new StringContent("true"), "verbatim");
            if (terms.Length > 0) body.Add(new StringContent(JsonSerializer.Serialize(new[] { new { phrases = terms, boost = 3.0 } })), "speech_contexts");
        }
        else
        {
            body.Add(new StringContent("true"), "no_context");
            body.Add(new StringContent("true"), "suppress_nst");
            body.Add(new StringContent(string.Join(", ", terms)), "prompt");
        }
        using var request = Request(HttpMethod.Post, parakeet ? "v1/audio/transcriptions" : "inference"); request.Content = body;
        using var reply = await http.SendAsync(request, token).ConfigureAwait(false);
        if (!reply.IsSuccessStatusCode) throw new IOException("Local transcription failed. Your draft was left untouched.");
        var bytes = await reply.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        try { return SpeechProtocol.Text(bytes); }
        finally { Array.Clear(bytes); }
    }
    private async Task EnsureAsync(Preferences p, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var model = Transcriber.SelectedModel(p);
        var isParakeet = ModelCatalog.Get(model).IsParakeet;
        var cuda = isParakeet ? Transcriber.ParakeetUsesCuda(p) : p.Engine != "FastCpu" && Transcriber.CudaEngineAvailable;
        idleMinutes = p.ModelIdleMinutes;
        if (CleanupPending) throw new IOException("The previous model is still releasing memory. Use Unload now to retry cleanup.");
        var nextKey = model + (cuda ? ":cuda" : ":cpu");
        if (worker != null && key == nextKey && !worker.Process.HasExited) { lastUse = clock.GetTimestamp(); return; }
        await ReleaseAsync().ConfigureAwait(false);
        // Verification is part of startup, including automatic warming after launch.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(startupTimeout);
        try
        {
            var verified = await verifyModel(model, deadline.Token).ConfigureAwait(false);
            deadline.Token.ThrowIfCancellationRequested();
            if (!verified) throw new IOException("Install and verify the selected local model first.");
            var executable = isParakeet ? ParakeetAssets.Executable(cuda) : Path.Combine(Transcriber.RuntimeRoot, cuda ? "whisper-cuda" : "whisper", "Release", "whisper-server.exe");
            if (!File.Exists(executable)) throw new IOException("The resident local engine is missing. Repair the local runtime.");
            if (isParakeet && !await ParakeetAssets.VerifyAsync(cuda, deadline.Token).ConfigureAwait(false)) throw new IOException("The Parakeet runtime failed integrity verification. Existing files were preserved.");
            deadline.Token.ThrowIfCancellationRequested();
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var capability = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            parakeet = isParakeet; apiKey = isParakeet ? capability : null;
            endpoint = new Uri(isParakeet ? $"http://127.0.0.1:{port}/" : $"http://127.0.0.1:{port}/{capability}/");
            var info = new ProcessStartInfo(executable);
            if (isParakeet)
            {
                foreach (var name in info.Environment.Keys.Where(n => n.StartsWith("NEMO_SPEECH_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(name);
                info.Environment["NEMO_SPEECH_HTTP_API_KEY"] = apiKey;
                foreach (var arg in new[] { "serve", "--asr-model", Transcriber.Models.PathFor(model), "--device", cuda ? "cuda:0" : "cpu", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--no-ui", "--threads", "1", "--max-upload-mb", "8", "--read-timeout", "90", "--write-timeout", "90", "--asr.batching.enabled=false", "--asr.batching.max_batch_size", "1", "--asr.batching.state_arena_slots", "1" }) info.ArgumentList.Add(arg);
            }
            else
            {
                foreach (var arg in new[] { "-m", Transcriber.Models.PathFor(model), "-t", "6", "-bs", cuda ? "5" : "1", "-bo", cuda ? "5" : "1", "-l", "auto", "-nf", "-sns", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--request-path", "/" + capability, "--public", Path.Combine(Transcriber.RuntimeRoot, "no-public-files") }) info.ArgumentList.Add(arg);
                if (!cuda) info.ArgumentList.Add("-ng");
            }
            Volatile.Write(ref usedCuda, 0);
            worker = new BoundedProcess(info, cuda || isParakeet ? 3072 : 1536);
            Volatile.Write(ref processId, worker.Process.Id);
            drainOutput = DrainAsync(worker.Process.StandardOutput, false);
            drainError = DrainAsync(worker.Process.StandardError, true);
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (worker.Process.HasExited) throw new IOException("The local model could not load within its memory budget.");
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); probe.CancelAfter(TimeSpan.FromSeconds(1));
                    using var response = await http.GetAsync(new Uri(endpoint, isParakeet ? "ready" : "health"), probe.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var healthBytes = await response.Content.ReadAsByteArrayAsync(probe.Token).ConfigureAwait(false);
                        try
                        {
                            if (isParakeet)
                            {
                                if (SpeechProtocol.ParakeetReady(healthBytes, cuda)) break;
                            }
                            else
                            {
                                using var health = JsonDocument.Parse(healthBytes);
                                if (health.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok") break;
                            }
                        }
                        finally { Array.Clear(healthBytes); }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !deadline.IsCancellationRequested) { }
                await Task.Delay(80, deadline.Token).ConfigureAwait(false);
            }
            if (isParakeet)
            {
                using var versionReply = await http.GetAsync(new Uri(endpoint, "version"), deadline.Token).ConfigureAwait(false);
                using var inventoryRequest = Request(HttpMethod.Get, "v1/models");
                using var inventoryReply = await http.SendAsync(inventoryRequest, deadline.Token).ConfigureAwait(false);
                if (!versionReply.IsSuccessStatusCode || !inventoryReply.IsSuccessStatusCode) throw new IOException("Parakeet runtime identity could not be confirmed.");
                var versionBytes = await versionReply.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
                var inventoryBytes = await inventoryReply.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
                try { if (!SpeechProtocol.ParakeetIdentity(versionBytes, inventoryBytes)) throw new IOException("Parakeet returned an unexpected runtime or loaded model."); }
                finally { Array.Clear(versionBytes); Array.Clear(inventoryBytes); }
                Volatile.Write(ref usedCuda, cuda ? 1 : 0);
            }
            else if (cuda && Volatile.Read(ref usedCuda) == 0) throw new IOException("The resident engine did not confirm CUDA. Select CPU or repair its runtime.");
            deadline.Token.ThrowIfCancellationRequested(); key = nextKey; Volatile.Write(ref loaded, model); lastUse = clock.GetTimestamp();
        }
        catch (Exception ex)
        {
            await ReleaseAsync().ConfigureAwait(false);
            if (ex is OperationCanceledException && !cancellation.IsCancellationRequested)
                throw new IOException("The local model reached its verification and startup time limit. Try again or check the selected model and runtime files.", ex);
            if (ex is UnauthorizedAccessException or System.Security.SecurityException)
                throw new IOException("The local model or runtime could not be read. Restore access and try again. Existing files were preserved.", ex);
            if (ex is System.ComponentModel.Win32Exception or InvalidOperationException or JsonException or HttpRequestException)
                throw new IOException("The local model could not start safely. Check its files and runtime.", ex);
            throw;
        }
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(endpoint!, path));
        if (parakeet) request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return request;
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
        worker = null; Volatile.Write(ref loaded, null); Volatile.Write(ref processId, 0); key = null; endpoint = null; apiKey = null; parakeet = false;
        Volatile.Write(ref cleanupPending, false);
    }
}
