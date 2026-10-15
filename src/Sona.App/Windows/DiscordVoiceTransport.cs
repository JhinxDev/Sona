using System.IO.Pipes;
using System.Net.Http;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json;
using Sona.Core;
namespace Sona.App.Windows;

internal sealed class DiscordVoiceTransport(ProtectedCredentials vault, string pipePrefix = "discord-ipc-") : IVoiceProtectionTransport, IAsyncDisposable
{
    private NamedPipeClientStream? pipe;
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> pending = new();
    private CancellationTokenSource? readerStop;
    private Task? reader;
    private bool authenticated;
    private long observationVersion;
    private readonly ConcurrentQueue<VoiceObservation> observations = new();
    public long ObservationVersion => Interlocked.Read(ref observationVersion);
    public IReadOnlyList<VoiceObservation> DrainObservations()
    {
        var result = new List<VoiceObservation>(); while (observations.TryDequeue(out var observation)) result.Add(observation); return result;
    }
    private bool TransportReady => pipe?.IsConnected == true && reader is { IsCompleted: false };
    public bool Connected => authenticated && TransportReady;
    public event Action? VoiceChanged;
    public async Task ConnectAsync(bool allowAuthorization, CancellationToken cancellation)
    {
        await DisposeConnectionAsync();
        try
        {
        var credentials = vault.Load();
        for (var index = 0; index < 10; index++)
        {
            var candidate = new NamedPipeClientStream(".", pipePrefix + index, PipeDirection.InOut, PipeOptions.Asynchronous);
            try { await candidate.ConnectAsync(150, cancellation); pipe = candidate; break; }
            catch (TimeoutException) { candidate.Dispose(); }
            catch (IOException) { candidate.Dispose(); }
        }
        if (pipe == null) throw new IOException("Discord is not running.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(8));
        await WriteFrameAsync(0, JsonSerializer.SerializeToElement(new { v = 1, client_id = credentials.ClientId }), deadline.Token);
        var ready = await ReadFrameAsync(deadline.Token);
        if (ready.Opcode != 1 || !ready.Data.TryGetProperty("evt", out var evt) || evt.GetString() != "READY") throw new IOException("Discord handshake failed.");
        readerStop = new(); reader = ReadLoopAsync(readerStop.Token);
        if (credentials.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60)
            credentials = await RefreshOrAuthorizeAsync(credentials, allowAuthorization, cancellation);
        try { await CommandAsync("AUTHENTICATE", new { access_token = credentials.AccessToken }, cancellation); }
        catch (IOException) when (allowAuthorization || credentials.RefreshToken.Length > 0)
        {
            credentials = await RefreshOrAuthorizeAsync(credentials, allowAuthorization, cancellation);
            await CommandAsync("AUTHENTICATE", new { access_token = credentials.AccessToken }, cancellation);
        }
        await CommandAsync("SUBSCRIBE", new { }, cancellation, "VOICE_SETTINGS_UPDATE");
        await CommandAsync("SUBSCRIBE", new { }, cancellation, "VOICE_CHANNEL_SELECT");
        authenticated = true;
        }
        catch (HttpRequestException) { await DisposeConnectionAsync(); throw new IOException("Discord authorization network request failed."); }
        catch { await DisposeConnectionAsync(); throw; }
    }
    private async Task<DiscordCredentials> RefreshOrAuthorizeAsync(DiscordCredentials credentials, bool allowAuthorization, CancellationToken cancellation)
    {
        var form = new Dictionary<string, string> { ["client_id"] = credentials.ClientId, ["client_secret"] = credentials.ClientSecret };
        if (credentials.RefreshToken.Length > 0) { form["grant_type"] = "refresh_token"; form["refresh_token"] = credentials.RefreshToken; }
        else
        {
            if (!allowAuthorization) throw new IOException("Reconnect Discord from Sona to authorize protection.");
            var auth = await CommandAsync("AUTHORIZE", new { client_id = credentials.ClientId, scopes = new[] { "rpc", "rpc.voice.write" } }, cancellation, timeoutSeconds: 60);
            form["grant_type"] = "authorization_code"; form["code"] = auth.GetProperty("code").GetString()!; form["redirect_uri"] = "http://127.0.0.1";
        }
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var response = await client.PostAsync("https://discord.com/api/oauth2/token", new FormUrlEncodedContent(form), cancellation);
        if (!response.IsSuccessStatusCode)
        {
            if (allowAuthorization && form["grant_type"] == "refresh_token") { credentials.RefreshToken = ""; return await RefreshOrAuthorizeAsync(credentials, true, cancellation); }
            throw new IOException("Discord authorization failed. Reconnect or update your local setup.");
        }
        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        var bodyBuffer = new byte[65537]; var bodyLength = 0;
        while (bodyLength < bodyBuffer.Length)
        {
            var read = await input.ReadAsync(bodyBuffer.AsMemory(bodyLength), cancellation); if (read == 0) break; bodyLength += read;
        }
        if (bodyLength > 65536) throw new IOException("Discord returned an invalid authorization response.");
        var body = bodyBuffer.AsMemory(0, bodyLength);
        using var token = JsonDocument.Parse(body);
        credentials.AccessToken = token.RootElement.GetProperty("access_token").GetString()!;
        if (token.RootElement.TryGetProperty("refresh_token", out var refresh)) credentials.RefreshToken = refresh.GetString()!;
        credentials.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + token.RootElement.GetProperty("expires_in").GetInt32(); vault.Save(credentials); return credentials;
    }
    public async Task<VoiceState> ReadAsync(CancellationToken cancellation)
    {
        var channel = await CommandAsync("GET_SELECTED_VOICE_CHANNEL", new { }, cancellation);
        var state = await CommandAsync("GET_VOICE_SETTINGS", new { }, cancellation);
        return new(channel.ValueKind != JsonValueKind.Null, state.GetProperty("mute").GetBoolean(), state.GetProperty("deaf").GetBoolean());
    }
    public async Task<bool> SetMuteAsync(bool muted, CancellationToken cancellation) => (await CommandAsync("SET_VOICE_SETTINGS", new { mute = muted }, cancellation)).TryGetProperty("mute", out var value) && value.GetBoolean() == muted;
    public async Task<bool> SetDeafenAsync(bool deafened, CancellationToken cancellation) => (await CommandAsync("SET_VOICE_SETTINGS", new { deaf = deafened }, cancellation)).TryGetProperty("deaf", out var value) && value.GetBoolean() == deafened;
    private async Task<JsonElement> CommandAsync(string command, object arguments, CancellationToken cancellation, string? evt = null, int timeoutSeconds = 5)
    {
        if (!TransportReady) throw new IOException("Discord connection is unavailable.");
        var nonce = Guid.NewGuid().ToString("N"); var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(nonce, completion)) throw new IOException("Discord request could not be queued.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await WriteFrameAsync(1, JsonSerializer.SerializeToElement(new { cmd = command, args = arguments, nonce, evt }), deadline.Token);
            var response = await completion.Task.WaitAsync(deadline.Token);
            if (response.TryGetProperty("evt", out var error) && error.GetString() == "ERROR") throw new IOException("Discord rejected the requested operation.");
            return response.GetProperty("data");
        }
        finally { pending.TryRemove(nonce, out _); }
    }
    private async Task ReadLoopAsync(CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(cancellation);
                if (frame.Opcode == 3) { await WriteFrameAsync(4, frame.Data, cancellation); continue; }
                if (frame.Opcode == 2) throw new IOException("Discord disconnected.");
                if (frame.Opcode != 1) continue;
                if (frame.Data.TryGetProperty("nonce", out var nonce) && nonce.ValueKind == JsonValueKind.String && pending.TryGetValue(nonce.GetString()!, out var completion)) completion.TrySetResult(frame.Data);
                else if (frame.Data.TryGetProperty("evt", out var evt) && evt.GetString() is "VOICE_SETTINGS_UPDATE" or "VOICE_CHANNEL_SELECT")
                {
                    var channel = evt.GetString() == "VOICE_CHANNEL_SELECT";
                    var data = frame.Data.TryGetProperty("data", out var d) ? d : default;
                    bool? muted = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("mute", out var m) && m.ValueKind is JsonValueKind.True or JsonValueKind.False ? m.GetBoolean() : null;
                    bool? deafened = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("deaf", out var f) && f.ValueKind is JsonValueKind.True or JsonValueKind.False ? f.GetBoolean() : null;
                    if (observations.Count >= 4096) throw new IOException("Discord voice observation capacity exceeded.");
                    observations.Enqueue(new(Interlocked.Increment(ref observationVersion), muted, deafened, channel)); VoiceChanged?.Invoke();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            authenticated = false;
            foreach (var completion in pending.Values) completion.TrySetException(new IOException("Discord connection ended."));
        }
        finally { authenticated = false; pipe?.Dispose(); }
    }
    private async Task WriteFrameAsync(int opcode, JsonElement payload, CancellationToken cancellation)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload); if (bytes.Length > 65536) throw new IOException("Discord frame exceeded its size limit.");
        var header = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(header, opcode); BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), bytes.Length);
        await writes.WaitAsync(cancellation);
        try { if (pipe == null) throw new IOException("Discord is unavailable."); await pipe.WriteAsync(header, cancellation); await pipe.WriteAsync(bytes, cancellation); await pipe.FlushAsync(cancellation); }
        finally { writes.Release(); }
    }
    private async Task<(int Opcode, JsonElement Data)> ReadFrameAsync(CancellationToken cancellation)
    {
        if (pipe == null) throw new IOException("Discord is unavailable.");
        var header = new byte[8]; await pipe.ReadExactlyAsync(header, cancellation);
        var opcode = BinaryPrimitives.ReadInt32LittleEndian(header); var size = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (size < 1 || size > 65536) throw new IOException("Invalid Discord frame size.");
        var bytes = new byte[size]; await pipe.ReadExactlyAsync(bytes, cancellation); using var document = JsonDocument.Parse(bytes); return (opcode, document.RootElement.Clone());
    }
    private async Task DisposeConnectionAsync()
    {
        authenticated = false; readerStop?.Cancel(); pipe?.Dispose();
        if (reader != null) await reader; readerStop?.Dispose(); readerStop = null; reader = null; pipe = null;
    }
    public ValueTask DisconnectAsync() => new(DisposeConnectionAsync());
    public ValueTask DisposeAsync() => new(DisposeConnectionAsync());
}
