using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sona.App.Windows;
using Sona.Core;

namespace Sona.App.Audio;

internal sealed class CleanupModel
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HttpClient http = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    private readonly TimeProvider clock;
    private readonly Func<string, CancellationToken, Task<bool>> verifyAssets;
    private BoundedProcess? worker;
    private Uri? endpoint;
    private string? capability;
    private string? loadedBackend;
    private int processId;
    private bool cleanupPending;
    private Task drainOutput = Task.CompletedTask, drainError = Task.CompletedTask;
    private long lastUse;
    private int idleMinutes = 10;
    private int cudaOffload, cudaBuffer;
    private double modelVramMiB = double.NaN;
    private int loadStage;
    public ModelLoadStage LoadStage => (ModelLoadStage)Volatile.Read(ref loadStage);
    public CleanupModel(TimeProvider? clock = null, Func<string, CancellationToken, Task<bool>>? verifyAssets = null)
    {
        this.clock = clock ?? TimeProvider.System;
        this.verifyAssets = verifyAssets ?? CleanupAssets.VerifyAsync;
    }
    public string? LoadedBackend => Volatile.Read(ref loadedBackend);
    public int? ProcessId => Volatile.Read(ref processId) is var id && id > 0 ? id : null;
    public bool CleanupPending => Volatile.Read(ref cleanupPending);
    public double? ModelVramMiB => LoadedBackend == "GPU" && Volatile.Read(ref modelVramMiB) is var mib && double.IsFinite(mib) ? mib : null;
    public bool AvailableFor(Preferences p) => CleanupAssets.Available(p.CleanupEngine);

    public async Task WarmAsync(Preferences p, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { if (!p.CleanupEnabled) await ReleaseAsync().ConfigureAwait(false); else await EnsureAsync(p, token).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public Task ReconcileAsync(Preferences p, CancellationToken token) => WarmAsync(p, token);

    public async Task<string> RunAsync(string prompt, Preferences p, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 24000) throw new IOException("Cleanup prompt exceeds its safe limit.");
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            try
            {
                if (!p.CleanupEnabled) { await ReleaseAsync().ConfigureAwait(false); throw new IOException("Local cleanup is disabled."); }
                await EnsureAsync(p, token).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(20));
                // Reserve output space in the fixed 4096-token context rather
                // than allowing llama.cpp to silently truncate the input.
                var tokenBytes = await PostAsync("tokenize", new { content = prompt, add_special = true }, deadline.Token).ConfigureAwait(false);
                try
                {
                    using var parsed = JsonDocument.Parse(tokenBytes);
                    if (!parsed.RootElement.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Array || tokens.GetArrayLength() > 2500)
                        throw new IOException("Cleanup text exceeds the model's safe context.");
                }
                finally { Array.Clear(tokenBytes); }
                var response = await PostAsync("completion", new { prompt, temperature = 0, n_predict = 1536, cache_prompt = false, seed = 1, stream = false }, deadline.Token).ConfigureAwait(false);
                try { var result = ParseResponse(response); lastUse = clock.GetTimestamp(); return result; }
                finally { Array.Clear(response); }
            }
            catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or JsonException or IOException or InvalidOperationException)
            {
                await ReleaseAsync().ConfigureAwait(false);
                if (ex is OperationCanceledException && !token.IsCancellationRequested) throw new IOException("Local cleanup reached its time limit. Rule cleanup remains available.", ex);
                if (ex is HttpRequestException or JsonException or InvalidOperationException) throw new IOException("Local cleanup failed. Rule cleanup remains available.", ex);
                throw;
            }
        }
        finally { gate.Release(); }
    }

    internal static string ParseResponse(byte[] bytes)
    {
        if (bytes.Length > 1024 * 1024) throw new IOException("Cleanup response exceeds its limit.");
        using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        var value = json.RootElement;
        var hasLimit = value.TryGetProperty("stopped_limit", out var limit);
        var completed = value.TryGetProperty("stop_type", out var stopType)
            ? stopType.ValueKind == JsonValueKind.String && stopType.GetString() == "eos" &&
              value.TryGetProperty("truncated", out var modernTruncated) && modernTruncated.ValueKind == JsonValueKind.False
            : hasLimit && limit.ValueKind == JsonValueKind.False;
        if (!completed || hasLimit && limit.ValueKind != JsonValueKind.False ||
            value.TryGetProperty("stop", out var stopped) && stopped.ValueKind != JsonValueKind.True ||
            value.TryGetProperty("truncated", out var truncated) && truncated.ValueKind != JsonValueKind.False ||
            !value.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            throw new IOException("Cleanup response is invalid or truncated.");
        var text = content.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text) || text.Length > 6000) throw new IOException("Cleanup result is empty or exceeds its limit.");
        return text.Trim();
    }

    private async Task EnsureAsync(Preferences p, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var backend = CleanupAssets.ResolveBackend(p.CleanupEngine);
        idleMinutes = Math.Clamp(p.CleanupIdleMinutes, 0, 240);
        if (CleanupPending) throw new IOException("Cleanup memory is still releasing. Retry Unload now.");
        if (worker != null && loadedBackend == backend && !worker.Process.HasExited) { lastUse = clock.GetTimestamp(); return; }
        await ReleaseAsync().ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            Volatile.Write(ref loadStage, (int)ModelLoadStage.Verifying);
            if (!await verifyAssets(p.CleanupEngine, deadline.Token).ConfigureAwait(false)) throw new IOException("Install and verify the local cleanup model and runtime first.");
            var directory = CleanupAssets.RuntimePath(backend);
            var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
            endpoint = new Uri($"http://127.0.0.1:{port}/");
            capability = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var info = new ProcessStartInfo(Path.Combine(directory, "llama-server.exe")) { WorkingDirectory = directory };
            foreach (var arg in new[] { "-m", CleanupAssets.ModelPath, "-c", "4096", "-np", "1", "-t", "6", "-tb", "6", "-ngl", backend == "GPU" ? "99" : "0", "--host", "127.0.0.1", "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture), "--api-key", capability, "--log-verbosity", backend == "GPU" ? "4" : "3" }) info.ArgumentList.Add(arg);
            Volatile.Write(ref cudaOffload, 0); Volatile.Write(ref cudaBuffer, 0);
            Volatile.Write(ref modelVramMiB, double.NaN);
            Volatile.Write(ref loadStage, (int)ModelLoadStage.Loading);
            worker = new BoundedProcess(info, backend == "GPU" ? 3072 : 1536);
            Volatile.Write(ref processId, worker.Process.Id);
            drainOutput = DrainAsync(worker.Process.StandardOutput, false);
            drainError = DrainAsync(worker.Process.StandardError, backend == "GPU");
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (worker.Process.HasExited) throw new IOException("Cleanup could not load within its memory budget.");
                try
                {
                    using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token); probe.CancelAfter(TimeSpan.FromSeconds(1));
                    using var request = Request(HttpMethod.Get, "health");
                    using var reply = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, probe.Token).ConfigureAwait(false);
                    if (reply.IsSuccessStatusCode)
                    {
                        var bytes = await ReadBoundedAsync(reply, probe.Token).ConfigureAwait(false);
                        try
                        {
                            using var health = JsonDocument.Parse(bytes);
                            if (health.RootElement.TryGetProperty("status", out var status) && status.GetString() == "ok" &&
                                (backend != "GPU" || Volatile.Read(ref cudaOffload) != 0 && Volatile.Read(ref cudaBuffer) != 0 && double.IsFinite(Volatile.Read(ref modelVramMiB)))) break;
                        }
                        finally { Array.Clear(bytes); }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !deadline.IsCancellationRequested) { }
                await Task.Delay(80, deadline.Token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested(); Volatile.Write(ref loadedBackend, backend); lastUse = clock.GetTimestamp();
        }
        catch (Exception ex)
        {
            await ReleaseAsync().ConfigureAwait(false);
            if (ex is OperationCanceledException && !token.IsCancellationRequested) throw new IOException("Cleanup did not confirm its selected backend within 45 seconds. Select CPU or repair the GPU runtime.", ex);
            if (ex is System.ComponentModel.Win32Exception or UnauthorizedAccessException or InvalidOperationException) throw new IOException("Cleanup could not start safely. Repair its local runtime.", ex);
            throw;
        }
        finally { Volatile.Write(ref loadStage, (int)ModelLoadStage.None); }
    }
    private HttpRequestMessage Request(HttpMethod method, string route)
    {
        var request = new HttpRequestMessage(method, new Uri(endpoint!, route));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", capability);
        return request;
    }
    private async Task<byte[]> PostAsync(string route, object value, CancellationToken token)
    {
        using var request = Request(HttpMethod.Post, route);
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        try
        {
            request.Content = new ByteArrayContent(payload); request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new IOException("Local cleanup request failed.");
            return await ReadBoundedAsync(response, token).ConfigureAwait(false);
        }
        finally { Array.Clear(payload); }
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage reply, CancellationToken token)
    {
        const int limit = 1024 * 1024;
        if (reply.Content.Headers.ContentLength > limit) throw new IOException("Cleanup response exceeds its limit.");
        await using var input = await reply.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream(); var buffer = new byte[8192];
        try
        {
            int count;
            while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                if (output.Length + count > limit) throw new IOException("Cleanup response exceeds its limit.");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        finally { Array.Clear(buffer); if (output.TryGetBuffer(out var stored)) Array.Clear(stored.Array!); }
    }
    internal static (bool Offloaded, bool Buffer) CudaProof(string sample) =>
        (Regex.IsMatch(sample, @"offloaded\s+[1-9][0-9]*/[0-9]+\s+layers\s+to GPU", RegexOptions.CultureInvariant),
         Regex.IsMatch(sample, @"CUDA[0-9]+\s+model buffer size", RegexOptions.CultureInvariant));
    internal static double? ParseModelVramMiB(string sample)
    {
        var match = Regex.Match(sample, @"CUDA0\s+model buffer size\s*=\s*([0-9]+(?:\.[0-9]+)?)\s+MiB", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var mib) && mib > 0 && mib <= 262144 ? mib : null;
    }
    private async Task DrainAsync(StreamReader reader, bool inspectCuda)
    {
        var buffer = new char[2048]; string tail = "";
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            {
                if (inspectCuda && LoadedBackend == null)
                {
                    var sample = tail + new string(buffer, 0, count); var proof = CudaProof(sample);
                    if (proof.Offloaded) Volatile.Write(ref cudaOffload, 1);
                    if (proof.Buffer) Volatile.Write(ref cudaBuffer, 1);
                    if (ParseModelVramMiB(sample) is { } mib) Volatile.Write(ref modelVramMiB, mib);
                    tail = sample.Length > 128 ? sample[^128..] : sample;
                }
                Array.Clear(buffer);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        finally { Array.Clear(buffer); }
    }
    public async Task ExpireAsync(CancellationToken token = default)
    {
        if (!await gate.WaitAsync(0, token).ConfigureAwait(false)) return;
        try { if (worker != null && (CleanupPending || worker.Process.HasExited || idleMinutes > 0 && clock.GetElapsedTime(lastUse).TotalMinutes >= idleMinutes)) await ReleaseAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task UnloadAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try { await ReleaseAsync().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    private async Task ReleaseAsync()
    {
        Volatile.Write(ref cleanupPending, true);
        // Retain worker identity and ownership on termination or drain failure.
        // BoundedProcess.Dispose confirms exit before releasing its Process.
        if (worker != null) worker.Dispose();
        try { await Task.WhenAll(drainOutput, drainError).WaitAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false); }
        catch (TimeoutException ex) { throw new IOException("Cleanup output is still closing. Retry Unload now.", ex); }
        worker = null; Volatile.Write(ref processId, 0); Volatile.Write(ref loadedBackend, null);
        Volatile.Write(ref modelVramMiB, double.NaN);
        endpoint = null; capability = null; drainOutput = drainError = Task.CompletedTask;
        Volatile.Write(ref cleanupPending, false);
    }
}
