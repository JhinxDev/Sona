# Sound feedback

Sona generates four original, short interface cues on this device. **Settings →
General → Sound effects** enables or disables the set; save preferences to apply
the choice. The default is enabled. Test UI mode suppresses actual playback
independently of this preference.

| Cue | Design | Duration | Measured digital peak |
| --- | --- | ---: | ---: |
| Open | Soft paired glass tone | 180 ms | -27.67 dBFS |
| Start | Single clear, short tone | 100 ms | -27.34 dBFS |
| Ready | Gentle ascending pair | 220 ms | -26.72 dBFS |
| Sent | Short descending pair | 180 ms | -26.41 dBFS |

The sound intent is quiet confirmation rather than an alert. Eight-millisecond
smooth attacks, 35-millisecond smooth releases and decaying harmonic tones avoid
hard sample edges. The first and last samples are zero. A 0.05 full-scale ceiling
keeps every cue below -26 dBFS; actual loudness still depends on Windows volume,
the selected device and the listener. The cues use 24 kHz mono, signed 16-bit PCM
WAV data synthesized in memory. There are no downloaded recordings, temporary
audio files, remote services, added packages or third-party sound assets.

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

The numeric owner-machine receipt is
`E:/Temp/qazws/sona-personalization/sound-feedback.json`, bound to
`SoundFeedback.cs` SHA256
`597191530875c70d2f3194cae48a0d7e0632c0f4552c0199f61d67e176a93c64`.
Its waveform hashes identify the exact generated cue bytes. Subjective listening
and physical-device behavior remain owner checks; technical PCM tests do not
claim that the cues have been auditioned.

With the README's isolated SDK environment:

```powershell
& $sonaDotnet build .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false
& $sonaDotnet .\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.dll --sound-feedback-checks
```
