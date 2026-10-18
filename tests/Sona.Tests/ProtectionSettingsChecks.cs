using Sona.Core;

internal static class ProtectionSettingsChecks
{
    public static async Task<string[]> RunAsync()
    {
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
        var voice = new FakeVoice(); var controller = new MicProtection(voice);
        await controller.ConfigureAsync(false, 0, default);
        await controller.SetSourcesAsync(["active"], default);
        Check(voice.State.Muted && !voice.State.Deafened, "Mute-only protection does not deafen");
        await controller.ConfigureAsync(true, 0, default);
        Check(voice.State.Muted && voice.State.Deafened, "Enabling incoming silencing applies during an unchanged active source");
        await controller.ConfigureAsync(false, 0, default);
        Check(voice.State.Muted && !voice.State.Deafened && controller.OwnsChanges, "Disabling incoming silencing restores owned deafen and retains mute");
        await controller.SetSourcesAsync([], default);
        Check(!voice.State.Muted && !voice.State.Deafened, "Mute-only completion restores only its owned microphone change");
        voice.State = new(true, true, true);
        await controller.SetSourcesAsync(["manual"], default);
        await controller.ConfigureAsync(true, 0, default); await controller.ConfigureAsync(false, 0, default);
        await controller.SetSourcesAsync([], default);
        Check(voice.State.Muted && voice.State.Deafened && !controller.OwnsChanges, "Changing incoming policy preserves preexisting manual mute and deafen");
        voice.State = new(true, false, false); await controller.SetSourcesAsync(["paused"], default);
        await controller.PauseAsync(true, default); var count = voice.Changes.Count;
        await controller.ConfigureAsync(true, 0, default);
        Check(controller.Paused && voice.Changes.Count == count, "Incoming policy changes do not acquire while explicitly paused");
        await controller.PauseAsync(false, default); await controller.SetSourcesAsync([], default);
        var retryVoice = new FakeVoice { RejectProtect = true }; var retry = new MicProtection(retryVoice);
        await retry.ConfigureAsync(true, 0, default);
        var rejected = false; try { await retry.SetSourcesAsync(["retry"], default); } catch (IOException) { rejected = true; }
        Check(rejected && retry.Status.Contains("retry", StringComparison.OrdinalIgnoreCase), "Failed acquisition exposes retry state instead of Ready");
        retryVoice.RejectProtect = false; await retry.SetSourcesAsync(["retry"], default);
        Check(retryVoice.State.Muted && retryVoice.State.Deafened, "Unchanged active sources retry a failed acquisition");
        retryVoice.RejectRelease = true; rejected = false;
        try { await retry.SetSourcesAsync([], default); } catch (IOException) { rejected = true; }
        Check(rejected && retry.OwnsChanges && retry.Status.Contains("retry", StringComparison.OrdinalIgnoreCase), "Immediate restoration failures retain ownership and visible retry state");
        retryVoice.RejectRelease = false; await retry.SetSourcesAsync([], default);
        Check(!retryVoice.State.Muted && !retryVoice.State.Deafened && !retry.OwnsChanges, "Immediate restoration recovers on unchanged idle-source retry");
        var partialVoice = new FakeVoice { RejectDeafen = true }; var partial = new MicProtection(partialVoice);
        try { await partial.SetSourcesAsync(["partial"], default); } catch (IOException) { }
        partialVoice.Observe(false, false); partialVoice.RejectDeafen = false;
        await partial.SetSourcesAsync(["partial"], default);
        Check(!partialVoice.State.Muted && !partialVoice.State.Deafened && !partial.OwnsChanges, "A manual unmute after partial acquisition cancels automatic protection retry");
        var heldVoice = new FakeVoice { RejectDeafen = true }; var heldController = new MicProtection(heldVoice);
        try { await heldController.SetSourcesAsync(["held"], default); } catch (IOException) { }
        var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readResult = new TaskCompletionSource<VoiceState>(TaskCreationOptions.RunContinuationsAsynchronously);
        heldVoice.ReadOverride = token => { readEntered.TrySetResult(); return readResult.Task.WaitAsync(token); };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var retryTask = heldController.SetSourcesAsync(["held"], deadline.Token);
        await readEntered.Task.WaitAsync(deadline.Token);
        heldVoice.Observe(false, false); heldVoice.RejectDeafen = false;
        var beforeOverride = heldVoice.Changes.Count; readResult.TrySetResult(heldVoice.State); await retryTask;
        Check(!heldVoice.State.Muted && !heldVoice.State.Deafened && heldVoice.Changes.Count == beforeOverride,
            "A manual unmute during the awaited retry read cancels further protection commands");
        return checks.ToArray();
    }
}

