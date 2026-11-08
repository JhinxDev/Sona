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
        var deafenedVoice = new FakeVoice { State = new(true, false, true) };
        var deafenedController = new MicProtection(deafenedVoice);
        await deafenedController.ConfigureAsync(false, 0, default);
        await deafenedController.SetSourcesAsync(["deafened-recording"], default);
        Check(deafenedVoice.State.Deafened && !deafenedVoice.State.Muted && deafenedVoice.Changes.Count == 0 && !deafenedController.OwnsChanges,
            "A deafened Discord already has the microphone off; Sona protects without sending a mute or deafen change");
        await deafenedController.SetSourcesAsync([], default);
        Check(deafenedVoice.State.Deafened && deafenedVoice.Changes.Count == 0,
            "Protection release leaves the user's deafen choice untouched");
        var staleVoice = new FakeVoice { StaleMuteAckCount = 1 };
        var staleController = new MicProtection(staleVoice);
        await staleController.ConfigureAsync(false, 0, default);
        await staleController.SetSourcesAsync(["stale-ack"], default);
        Check(staleVoice.State.Muted && staleController.OwnsChanges,
            "An unconfirmed first mute is accepted after confirming the actual Discord state");
        await staleController.SetSourcesAsync([], default);
        Check(!staleVoice.State.Muted && !staleController.OwnsChanges,
            "A state-confirmed mute is restored on release");
        var refusedVoice = new FakeVoice { RejectProtect = true };
        var refusedController = new MicProtection(refusedVoice);
        await refusedController.ConfigureAsync(false, 0, default);
        Exception? refusal = null;
        try { await refusedController.SetSourcesAsync(["refused"], default); } catch (IOException ex) { refusal = ex; }
        Check(refusal != null && refusal.Message.Contains("Undeafen", StringComparison.OrdinalIgnoreCase) && refusedController.Status.Contains("retry", StringComparison.OrdinalIgnoreCase),
            "A persistently unconfirmed mute fails with undeafen and reconnect guidance");
        var deafenDuringVoice = new FakeVoice(); var deafenDuringController = new MicProtection(deafenDuringVoice);
        await deafenDuringController.ConfigureAsync(false, 0, default);
        await deafenDuringController.SetSourcesAsync(["during"], default);
        Check(deafenDuringVoice.State.Muted && deafenDuringController.OwnsChanges, "Mute-only protection acquires before the user deafens");
        deafenDuringVoice.Observe(true, true);
        var changesBeforeRelease = deafenDuringVoice.Changes.Count;
        await deafenDuringController.SetSourcesAsync([], default);
        Check(deafenDuringVoice.State.Muted && deafenDuringVoice.State.Deafened && deafenDuringVoice.Changes.Count == changesBeforeRelease && !deafenDuringController.OwnsChanges,
            "Release while the user is deafened never sends an un-mute that would clear their deafen");
        return checks.ToArray();
    }
}

