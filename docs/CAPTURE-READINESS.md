# Initial field readiness and retained-draft notices, 0.4.10

The owner reported an empty Codex composer and a retained-draft notice asking for
Ctrl+V. The last intentional recording observation showed a stable unsupported
Windows Pane, followed by NoCapturedField with no paste attempted and clipboard
recovery available. It did not show a partial or failed paste. Whether the text
box was already focused is unknown; a transient accessibility container remains
a hypothesis, not an established explanation for that attempt.

At recording start, Sona can recheck the directly focused element after a stable
unsupported Pane. Three cancellable 40 ms waits allow at most four total captures.
An immediately ready field incurs no wait. This is 120 ms of scheduled delay at
most, not a deadline on synchronous Windows accessibility calls.

Every retry stays within the initial exact HWND, HWND owner and focus epoch.
Repeated Pane samples must retain their provider and runtime identity. Missing
scope metadata, a known read-only Pane, password, disabled, suggestion or other
ineligible controls cannot enable more retries. A later editor must pass the
existing direct-field policy. Sona never guesses a child or accepts a container
as writable, rebases an epoch, acquires another destination after recording starts,
or retries paste/send. A cancelled or released gesture cannot open the microphone.
Preparation owns its session before its first asynchronous wait, and queued
cancellation must finish before another start can take ownership.

The retained notice now uses a centered 228 DIP layout with no recording controls,
cleanup button or waveform. It shows a real app icon when one is available. Clicking
its text opens the draft explicitly; X only dismisses the notice. New recording
restores the chosen display and existing translucent material.

| Delivery facts | Notice |
| --- | --- |
| Recovery copy, no paste request | Draft copied / Ctrl+V to paste. |
| Unconfirmed paste request | Check the chat / Check before pasting again. |
| Confirmed paste, send not accepted | Draft pasted / Check the chat before sending. |
| No recovery copy | Draft ready / Open to review or copy. |
| Recording limit | Draft ready / Recording limit reached. |

`sona-agent status` adds initialStatus, captureAttempts and nullable readOnly to
its existing single capture observation. It still contains no transcript, field
text, labels, titles, clipboard contents or raw runtime IDs. Reading status never
performs a focus capture or starts recording.

## Verification and limits

The maintained offscreen native fixtures cover readiness exhaustion and success
on every allowed capture, original-scope drift, missing metadata, read-only and
terminal rejection, cancellation, released admission, overlapping starts, clearing,
shutdown and stale diagnostics. Notice checks cover delivery facts, theme/layout
reset, centered sizing, explicit review and dismissal without cancellation. They
render the real WPF controls at 96, 144 and 192 DPI without showing a window.
The build entrypoint includes both fixtures. The exact private 0.4.10 package
passed 59 readiness checks, 92 notice checks, 51 existing overlay checks, 10 capture
status checks and five Quit-preparation checks. Core388, Enter49, responsiveness50,
completed-paste25, default Windows49, capture-policy11, insertion24 and active-target34
also passed against its published modules. Builds had zero warnings/errors.
The same package passed the offscreen 18-check encrypted draft/session-history
upgrade rehearsal. Unchanged updater status15 and abort15 evidence was reused
from October20 after verifying those tool paths were byte-identical.

An independent source review found and closed the queued-cancellation recorder
cleanup race and an initial-status diagnostic mismatch. A final admission-boundary
regression also reproduced and repaired preparation remaining active after a
released gesture. No remaining scoped review finding was reported.

These tests use fake capture/recognition and isolated profiles. No live microphone,
global input, owner composer or Discord behavior is exercised. Normal Codex insertion
and the owner's physical notice acceptance remain unverified. If Windows still
cannot prove a focused editor, the safe draft-only result is intentional. Focus
the message box before starting dictation; the retry cannot replace that gesture.
Private guarded activation verified running version0.4.10/buildd7041c37462a,
all36installed receipt hashes, the draft/original, complete session History and
saved/active preferences. The replacement stayed minimized. Startup model warming
initially retained recovery; exact draft and native History serialization matched
the protected checkpoints after idle, and only this update's proven-owned temporary
files were removed without another restart. OLED, enabled Auto cleanup, Enter and
clipboard recovery were retained; History storage and incoming silencing remain Off.
This installation proof does not qualify physical keyboard or real-composer input.