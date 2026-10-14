namespace Sona.Core;

public sealed record VoiceState(bool InCall, bool Muted, bool Deafened);
public sealed record VoiceObservation(long Sequence, bool? Muted, bool? Deafened, bool ChannelChanged);
public interface IVoiceProtectionTransport
{
    Task<VoiceState> ReadAsync(CancellationToken cancellation);
    Task<bool> SetMuteAsync(bool muted, CancellationToken cancellation);
    Task<bool> SetDeafenAsync(bool deafened, CancellationToken cancellation);
    long ObservationVersion => 0;
    IReadOnlyList<VoiceObservation> DrainObservations() => Array.Empty<VoiceObservation>();
}

// All source changes, acknowledgements and releases are serialized. Ownership is
// granted only for successful commands and revoked when the user's state differs.
public sealed class MicProtection(IVoiceProtectionTransport transport)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<string> sources = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? delayedRelease;
    private bool paused;
    private bool closed;
    private bool ownMute;
    private bool ownDeafen;
    private bool deafen = true;
    private int delayMs = 200;
    private long muteOwnedAfter;
    private long deafenOwnedAfter;
    private bool restorationPending;
    public bool OwnsChanges => ownMute || ownDeafen;
    public bool Paused => paused;
    public int SourceCount => sources.Count;
    public string Status { get; private set; } = "Ready";
    public async Task ConfigureAsync(bool silenceIncoming, int releaseDelayMs, CancellationToken cancellation)
    {
        if (releaseDelayMs is < 0 or > 5000) throw new ArgumentException("Release delay is out of range.");
        await gate.WaitAsync(cancellation);
        try
        {
            ApplyObservations();
            deafen = silenceIncoming; delayMs = releaseDelayMs;
            if (!deafen && ownDeafen)
            {
                if (!await transport.SetDeafenAsync(false, cancellation)) throw new IOException("Discord did not acknowledge deafen restoration.");
                ownDeafen = false;
            }
        }
        finally { gate.Release(); }
    }
    public async Task SetSourcesAsync(IEnumerable<string> active, CancellationToken cancellation)
    {
        var snapshot = active.Take(101).ToArray();
        if (snapshot.Length > 100 || snapshot.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 300)) throw new ArgumentException("Invalid recording source set.");
        await gate.WaitAsync(cancellation);
        try
        {
            if (closed) throw new InvalidOperationException("Protection is closed.");
            var wasRecording = sources.Count > 0;
            if (sources.SetEquals(snapshot)) { if (sources.Count == 0 && restorationPending) await ReleaseAsync(cancellation); return; }
            sources.Clear(); sources.UnionWith(snapshot); CancelRelease();
            if (paused) return;
            if (sources.Count > 0) { if (!wasRecording) await AcquireAsync(cancellation); }
            else if (OwnsChanges && delayMs > 0)
            {
                delayedRelease = new(); var timer = delayedRelease;
                _ = ReleaseAfterAsync(timer);
            }
            else await ReleaseAsync(cancellation);
        }
        finally { gate.Release(); }
    }
    public async Task PauseAsync(bool value, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            CancelRelease(); paused = value;
            if (value) { await ReleaseAsync(cancellation); Status = "Paused"; }
            else if (sources.Count > 0) await AcquireAsync(cancellation);
            else Status = "Ready";
        }
        finally { gate.Release(); }
    }
    public async Task ObserveAsync(VoiceState state, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { if (!state.Muted) ownMute = false; if (!state.Deafened) ownDeafen = false; }
        finally { gate.Release(); }
    }
    public async Task EnsureProtectedAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { if (!paused && !closed) await AcquireAsync(cancellation); }
        finally { gate.Release(); }
    }
    public async Task<bool> ReconcileVoiceEventsAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { return ApplyObservations(); }
        finally { gate.Release(); }
    }
    private bool ApplyObservations()
    {
        var channelChanged = false;
        foreach (var observation in transport.DrainObservations())
        {
            if (observation.Muted == false && observation.Sequence > muteOwnedAfter) ownMute = false;
            if (observation.Deafened == false && observation.Sequence > deafenOwnedAfter) ownDeafen = false;
            channelChanged |= observation.ChannelChanged;
        }
        return channelChanged;
    }
    public async Task CloseAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try { CancelRelease(); paused = true; sources.Clear(); await ReleaseAsync(cancellation); closed = true; Status = "Stopped"; }
        finally { gate.Release(); }
    }
    public async Task ConnectionLostAsync(CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            // Discord documents restoring the controlling client's settings on
            // disconnect. Never claim a new user's mute after reconnect.
            CancelRelease(); ownMute = ownDeafen = false; Status = "Disconnected; check Discord voice state";
        }
        finally { gate.Release(); }
    }
    private async Task AcquireAsync(CancellationToken cancellation)
    {
        ApplyObservations();
        var state = await transport.ReadAsync(cancellation);
        ApplyObservations();
        if (!state.InCall) { Status = "No Discord call"; return; }
        if (!state.Muted)
        {
            ownMute = false;
            muteOwnedAfter = transport.ObservationVersion;
            if (!await transport.SetMuteAsync(true, cancellation)) throw new IOException("Discord did not acknowledge microphone protection.");
            ownMute = true;
        }
        if (deafen && !state.Deafened)
        {
            ownDeafen = false;
            deafenOwnedAfter = transport.ObservationVersion;
            if (!await transport.SetDeafenAsync(true, cancellation)) throw new IOException("Discord did not acknowledge incoming audio silencing.");
            ownDeafen = true;
        }
        Status = "Protecting recording";
    }
    private async Task ReleaseAsync(CancellationToken cancellation)
    {
        ApplyObservations();
        if (!OwnsChanges) { restorationPending = false; Status = paused ? "Paused" : "Ready"; return; }
        var state = await transport.ReadAsync(cancellation);
        ApplyObservations();
        if (!state.Deafened) ownDeafen = false;
        if (!state.Muted) ownMute = false;
        if (ownDeafen)
        {
            if (!await transport.SetDeafenAsync(false, cancellation)) throw new IOException("Discord did not acknowledge deafen restoration.");
            ownDeafen = false;
            ApplyObservations();
        }
        if (ownMute)
        {
            if (!await transport.SetMuteAsync(false, cancellation)) throw new IOException("Discord did not acknowledge mute restoration.");
            ownMute = false;
        }
        Status = paused ? "Paused" : "Ready";
        restorationPending = false;
    }
    private async Task ReleaseAfterAsync(CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(delayMs, timer.Token); await gate.WaitAsync(timer.Token);
            try
            {
                if (ReferenceEquals(delayedRelease, timer) && sources.Count == 0 && !paused)
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await ReleaseAsync(deadline.Token);
                }
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested) { }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { restorationPending = true; Status = "Discord restoration needs retry"; }
        finally { timer.Dispose(); }
    }
    private void CancelRelease() { var timer = delayedRelease; delayedRelease = null; if (timer != null) { try { timer.Cancel(); } catch (ObjectDisposedException) { } } }
}
