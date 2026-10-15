# Sound feedback

Sona generates four original, short interface cues on this device. **Settings →
General → Sound effects** enables or disables the set; save preferences to apply
the choice. The default is enabled. Test UI mode suppresses actual playback
independently of this preference.

The initial glassy cues were rejected in owner listening as squeaky. The revised
set uses low, damped tones with an 18 ms smooth attack and a 70 ms soft release.
Fundamentals stay between 174 and 294 Hz, with a restrained lower body and a
brief, quiet upper component below 441 Hz. There are no pitch sweeps or high
bell-like overtones. This targets smoother, less intrusive feedback; listening
acceptance belongs to the owner.

| Cue | Design | Duration |
| --- | --- | ---: |
| Open | Warm overlapping body | 200 ms |
| Start | Rounded single pulse | 140 ms |
| Ready | Soft overlapping confirmation | 220 ms |
| Sent | Low, settled finish | 180 ms |

The first and last samples are zero. A 0.05 full-scale ceiling keeps every cue
below -26 dBFS; actual loudness depends on Windows volume, device and listener.
The cues use 24 kHz mono, signed 16-bit PCM WAV data synthesized in memory.
There are no downloaded recordings, remote services, added packages or
third-party sound assets.

## Playback boundary

`SoundFeedback.PlayAsync(FeedbackCue, CancellationToken)` runs synchronous Windows
playback on a background thread. There is one playback gate. Open, Ready and Sent
skip an overlapping request instead of accumulating a queue. Start waits for the
current cue, then finishes its own cue before its task completes. The recording
pipeline awaits Start and rechecks cancellation/admission before opening the
microphone, so cue playback completes before capture is requested.

Playback errors are nonfatal. Cancellation suppresses a waiting cue; disposal
cancels waiting playback, asks the current native player to stop, and suppresses
future cues. Cancellation is a no-op result for feedback, so callers must keep
their own cancellation check before starting capture. The optional injected
`Action<byte[]>` is synchronous, allowing tests to inspect PCM and hold the gate
without opening an audio device. A fake action that is already executing must
return under the test's control; disposal cannot abort arbitrary injected code.

The root application owns event placement: opening Sona, recording preparation,
completed draft, and accepted Enter submission. A Sent cue indicates that Sona
accepted the submission action; it is not a server delivery receipt.

## Silent verification, October 15, 2026

Twenty-two checks passed for RIFF/header/data integrity, sample format, the
250-millisecond limit, peak/fade bounds, four distinct waveforms, background
playback, skipped overlap, Start ordering, nonfatal errors, cancellation and
disposal. Every playback test injected a fake action. No sound was played, no
audio device was opened and no microphone was used.

The original waveform receipt belongs to the rejected first design. The revised
waveforms rerun the same silent integrity, level, fade and concurrency checks.
Subjective listening and physical-device behavior remain owner checks.

With the README's isolated SDK environment:

```powershell
& $sonaDotnet build .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false
& $sonaDotnet .\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.dll --sound-feedback-checks
```
