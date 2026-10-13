using System.Diagnostics;
using Murmur.Core;
namespace Murmur.App.Windows;

internal sealed class ProtectionService : IAsyncDisposable
{
    private readonly DiscordVoiceTransport transport;
    private readonly MicProtection controller;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private Task loop = Task.CompletedTask;
    private string[] watched = [];
    private bool enabled;
    private bool ownRecording;
    private bool connectedBefore;
    private int changed;
    private DateTime reconnectAfter;
    private int failures;
    private readonly Func<IReadOnlyList<string>, HashSet<string>> scan;
    private readonly bool production;
    public string Status { get; private set; } = "Off";
    public bool Paused => controller.Paused;
    public bool Connected => transport.Connected;
    public bool OwnsChanges => controller.OwnsChanges;
    public ProtectionService(ProtectedCredentials vault, string pipePrefix = "discord-ipc-", Func<IReadOnlyList<string>, HashSet<string>>? scan = null)
    {
        transport = new(vault, pipePrefix); controller = new(transport); this.scan = scan ?? MicrophoneSources.Scan; production = pipePrefix == "discord-ipc-";
        transport.VoiceChanged += () => Interlocked.Exchange(ref changed, 1);
    }
    public void Start() => loop = PollAsync();
    public async Task ConfigureAsync(Preferences settings, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            watched = settings.WatchedApps.ToArray();
            if (!settings.ProtectionEnabled && enabled) await controller.PauseAsync(true, cancellation);
            await controller.ConfigureAsync(settings.SilenceIncoming, settings.ProtectionReleaseDelayMs, cancellation);
            enabled = settings.ProtectionEnabled;
            if (!enabled) Status = "Off";
            else if (controller.Paused) { await controller.PauseAsync(false, cancellation); Status = "Ready"; }
        }
        finally { gate.Release(); }
    }
    public async Task ConnectAsync(bool interactive, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { await ConnectUnderGateAsync(interactive, cancellation); }
        finally { gate.Release(); }
    }
    private async Task ConnectUnderGateAsync(bool interactive, CancellationToken cancellation)
    {
        var legacy = Process.GetProcessesByName("hush"); var running = legacy.Length > 0; foreach (var process in legacy) process.Dispose();
        if (production && running) throw new IOException("Pause and quit standalone Hush before connecting Sona. Both apps must not control Discord together.");
        if (connectedBefore && !transport.Connected) await controller.ConnectionLostAsync(cancellation);
        await transport.ConnectAsync(interactive, cancellation); connectedBefore = true; failures = 0;
        await controller.ObserveAsync(await transport.ReadAsync(cancellation), cancellation);
        if (enabled && !controller.Paused) await controller.SetSourcesAsync(Sources(), cancellation);
        if (enabled && !controller.Paused && Sources().Count > 0) await controller.EnsureProtectedAsync(cancellation);
        Status = enabled ? controller.Status : "Connected; protection off";
    }
    public async Task BeginOwnRecordingAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            if (!enabled || controller.Paused) return;
            if (!transport.Connected) await ConnectUnderGateAsync(false, cancellation);
            ownRecording = true;
            await controller.SetSourcesAsync(Sources(), cancellation);
            await controller.EnsureProtectedAsync(cancellation); Status = controller.Status;
        }
        catch { ownRecording = false; throw; }
        finally { gate.Release(); }
    }
    public async Task EndOwnRecordingAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { ownRecording = false; if (enabled || controller.OwnsChanges) { await controller.SetSourcesAsync(Sources(), cancellation); Status = controller.Status; } }
        finally { gate.Release(); }
    }
    public async Task PauseAsync(bool pause, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { if (!enabled && !pause) { Status = "Off"; return; } await controller.PauseAsync(pause, cancellation); Status = controller.Status; }
        finally { gate.Release(); }
    }
    private HashSet<string> Sources()
    {
        var sources = scan(watched); if (ownRecording) sources.Add("sona-own-recording"); return sources;
    }
    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(stop.Token))
        {
            await gate.WaitAsync(stop.Token);
            try
            {
                if (!enabled) continue;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(12));
                if (!transport.Connected)
                {
                    if (DateTime.UtcNow < reconnectAfter) continue;
                    await ConnectUnderGateAsync(false, deadline.Token);
                }
                if (Interlocked.Exchange(ref changed, 0) != 0)
                {
                    var channelChanged = await controller.ReconcileVoiceEventsAsync(deadline.Token);
                    if (channelChanged && !controller.Paused && Sources().Count > 0) await controller.EnsureProtectedAsync(deadline.Token);
                }
                await controller.SetSourcesAsync(Sources(), deadline.Token); Status = controller.Status;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Text.Json.JsonException or InvalidOperationException or OperationCanceledException)
            {
                Status = "Discord protection unavailable; reconnect from Settings";
                reconnectAfter = DateTime.UtcNow.AddSeconds(new[] { 2, 5, 10, 30 }[Math.Min(failures++, 3)]);
            }
            finally { gate.Release(); }
        }
    }
    public async Task RestoreForQuitAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { await controller.CloseAsync(cancellation); enabled = false; Status = "Stopped"; }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); try { await loop; } catch (OperationCanceledException) { }
        await transport.DisposeAsync(); stop.Dispose();
    }
}
