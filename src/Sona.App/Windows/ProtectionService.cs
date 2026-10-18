using System.Diagnostics;
using Sona.Core;
namespace Sona.App.Windows;

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
    private bool connectionSuppressed;
    private bool pausedBeforeDisconnect;
    private int changed;
    private DateTime reconnectAfter;
    private int failures;
    private readonly Func<IReadOnlyList<string>, HashSet<string>> scan;
    private readonly bool production;
    private readonly Func<bool> isDiscordRunning;
    public string Status { get; private set; } = "Off";
    public bool Paused => controller.Paused;
    public bool Connected => transport.Connected;
    public bool OwnsChanges => controller.OwnsChanges;
    public bool ConnectionSuppressed => connectionSuppressed;
    public ProtectionService(ProtectedCredentials vault, string pipePrefix = "discord-ipc-", Func<IReadOnlyList<string>, HashSet<string>>? scan = null, Func<bool>? isDiscordRunning = null)
    {
        transport = new(vault, pipePrefix); controller = new(transport); this.scan = scan ?? MicrophoneSources.Scan; production = pipePrefix == "discord-ipc-";
        this.isDiscordRunning = isDiscordRunning ?? (production ? DiscordProcessRunning : () => true);
        transport.VoiceChanged += () => Interlocked.Exchange(ref changed, 1);
    }
    private static bool DiscordProcessRunning()
    {
        try
        {
            foreach (var name in new[] { "Discord", "DiscordPTB", "DiscordCanary", "DiscordDevelopment" })
            {
                var processes = Process.GetProcessesByName(name);
                var running = processes.Length > 0;
                foreach (var process in processes) process.Dispose();
                if (running) return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.Security.SecurityException)
        { throw new IOException("Discord process state is unavailable. Check call protection before recording.", ex); }
    }
    private bool DiscordClosed() => !transport.Connected && !isDiscordRunning();
    public void Start() => loop = PollAsync();
    public async Task ConfigureAsync(Preferences settings, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { await ConfigureUnderGateAsync(settings, null, cancellation); }
        finally { gate.Release(); }
    }
    public async Task RestoreConfigurationAsync(Preferences settings, bool paused, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { await ConfigureUnderGateAsync(settings, paused, cancellation); }
        finally { gate.Release(); }
    }
    private async Task ConfigureUnderGateAsync(Preferences settings, bool? restorePause, CancellationToken cancellation)
    {
        var wasEnabled = enabled;
        watched = settings.WatchedApps.ToArray();
        // A rollback to paused must never briefly acquire the active source.
        if (restorePause == true || !settings.ProtectionEnabled && enabled) await controller.PauseAsync(true, cancellation);
        await controller.ConfigureAsync(settings.SilenceIncoming, settings.ProtectionReleaseDelayMs, cancellation);
        if (settings.DiscordDisconnected && !connectionSuppressed) await DisconnectUnderGateAsync(cancellation);
        enabled = settings.ProtectionEnabled;
        if (connectionSuppressed) Status = "Disconnected";
        else if (!enabled) Status = "Off";
        else
        {
            if (transport.Connected) await controller.SetSourcesAsync(Sources(), cancellation);
            if ((!wasEnabled || restorePause == false) && restorePause != true && controller.Paused)
                await controller.PauseAsync(false, cancellation);
            Status = controller.Status;
        }
    }
    public async Task ConnectAsync(bool interactive, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            var resume = connectionSuppressed;
            if (!transport.Connected) await ConnectUnderGateAsync(interactive, cancellation);
            if (resume) await controller.PauseAsync(pausedBeforeDisconnect, cancellation);
            if (enabled && !controller.Paused)
            {
                await controller.SetSourcesAsync(Sources(), cancellation);
                if (Sources().Count > 0) await controller.EnsureProtectedAsync(cancellation);
            }
            connectionSuppressed = false;
            Status = enabled ? controller.Status : "Connected; protection off";
        }
        finally { gate.Release(); }
    }
    public async Task DisconnectAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { await DisconnectUnderGateAsync(cancellation); }
        finally { gate.Release(); }
    }
    private async Task DisconnectUnderGateAsync(CancellationToken cancellation)
    {
        if (!connectionSuppressed) pausedBeforeDisconnect = controller.Paused;
        // Keep the transport and owned changes on a failed restoration.
        connectionSuppressed = true;
        try
        {
            await controller.PauseAsync(true, cancellation);
            await controller.SetSourcesAsync(Array.Empty<string>(), cancellation);
            await transport.DisconnectAsync();
            connectedBefore = false; Interlocked.Exchange(ref changed, 0);
            Status = "Disconnected";
        }
        catch { Status = "Restoration needs retry; connection retained"; throw; }
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
            // Track the actual recording even when protection is off or paused.
            ownRecording = true;
            if (enabled && connectionSuppressed)
            {
                if (controller.OwnsChanges) throw new IOException("Discord restoration needs retry before recording.");
                return;
            }
            if (!enabled || controller.Paused) return;
            if (DiscordClosed())
            {
                if (controller.OwnsChanges) throw new IOException("Discord restoration needs retry before recording.");
                // Retain the source so protection can engage if Discord opens mid-dictation.
                ownRecording = true; Status = "Discord closed · dictation available"; return;
            }
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
        try { ownRecording = false; if (connectionSuppressed) { Status = "Disconnected"; return; } if (!transport.Connected && !controller.OwnsChanges) { Status = DiscordClosed() ? "Discord closed · dictation available" : "Discord protection unavailable; reconnect from Settings"; return; } if (enabled || controller.OwnsChanges) { await controller.SetSourcesAsync(Sources(), cancellation); Status = controller.Status; } }
        finally { gate.Release(); }
    }
    public async Task PauseAsync(bool pause, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            if (connectionSuppressed && !pause) { Status = "Disconnected"; return; }
            if (!enabled && !pause) { Status = "Off"; return; }
            if (!pause && controller.Paused) await controller.SetSourcesAsync(Sources(), cancellation);
            await controller.PauseAsync(pause, cancellation); Status = controller.Status;
        }
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
                if (!enabled || connectionSuppressed) continue;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); deadline.CancelAfter(TimeSpan.FromSeconds(12));
                if (!transport.Connected)
                {
                    if (DiscordClosed())
                    {
                        Status = controller.OwnsChanges ? "Discord restoration needs retry" : "Discord closed · dictation available";
                        reconnectAfter = DateTime.MinValue; failures = 0; continue;
                    }
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
