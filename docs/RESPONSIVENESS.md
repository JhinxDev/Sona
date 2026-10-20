# Responsiveness and delivery qualification, 0.4.8

The recording capsule now acknowledges preparation before waiting for call protection,
the start sound and microphone setup. These barriers still complete before capture.
Microphone samples update waveform data without repeatedly rebuilding unchanged
state, busy indicators or hover text. No speech model, beam setting or quality
preference was reduced.

The capsule is 228px without transcript text, previously 272px, and expands to 320px
when a live transcript is visible. Missing app logos and their spacing are hidden.
The background tint is slightly more translucent while text and controls remain
opaque; high contrast and disabled Windows transparency retain opaque fallbacks.
Fjord retains its stronger tint to meet waveform contrast over a white backdrop.
Its small button cycles Off, Auto and Always;
hover explains the current and next choice. Auto skips model inference only for
short punctuated text without recognized repair cues. Basic cleanup remains active;
Always preserves the prior model behavior. Mode changes save/apply immediately
through Settings, the pill and validated agent patches.

Live recognition reuses a completed result only for identical final captured audio
within its session. Otherwise final recognition runs again. This avoids duplicate
work without treating a provisional or incomplete tail as final.

The previous two-minute recording cutoff is five minutes. It emits a final draft
without automatic clipboard copy, insertion or sending. The recorder bounds its
last callback rather than discarding an entire buffer. PCM planning covers all
five minutes in bounded independent requests. Recognition still has a finite
90-second request deadline; five-minute performance on every backend is not claimed.

Enter-to-send can queue a bare Enter during pending transcription or cleanup. The
keyboard thread stores atomic intent through complete-paste verification; the
operation clears it on completion. Same-field,
complete-text readback, cancellation, modifier and clipboard checks remain mandatory
before one Return. This feature and clipboard recovery are off by default.

When recovery is enabled, an insertion failure offers ordinary Ctrl+V using the
complete draft. Unconfirmed paste may already have inserted text, so the UI says
to check the composer first. A newer clipboard copy takes priority. Cancellation
and forced-limit stops do not acquire recovery-copy intent. Password-field
rejection also withholds automatic clipboard recovery.

## Verification

Maintained regressions live in `ResponsivenessChecks`, `ResponsivenessFixture` and
`CompletedDraftPasteChecks`. They use fake capture, recognizer, focus, paste,
clipboard and time boundaries; no real microphone, global input or owner account.
The standard build entrypoint includes the native responsiveness fixture.

Qualification covers preparation cancellation, old and new recording deadlines,
full audio bounds, exact preview reuse and changed tails, dark/light tooltips,
capsule width, native/agent settings activation, recording-time pill changes,
pending form preservation, queued full-draft send, wrong-field refusal, clipboard
fallback, newer-copy priority and cancellation. All isolated recording displays
are suppressed centrally; render checks use offscreen WPF bitmaps.

Synthetic timing is evidence of control ordering, not measured physical-key or
microphone-to-result latency. Physical shortcut, real composer and owner experience
acceptance remain distinct from these automated checks and live package identity.
