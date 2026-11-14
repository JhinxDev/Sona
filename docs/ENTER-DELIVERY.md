# Current behavior in 0.6.2

Hold Ctrl+Windows, speak, and release to paste an unsent draft. Double-tap for
hands-free recording. With Enter-to-send enabled, a bare Enter in the original
app window stops capture and requests delivery. If the editor was unavailable
at the start, Enter still stops capture, but the result stays in Sona.

Initial capture may recheck a noneditable pane whose focus changed while the
same foreground window remained proven. It uses that first capture's final
focus stamp. Later window, process, field or focus changes still refuse output.
This does not acquire a new editor at the end of a failed recording capture.
The pill says **Draft only** as soon as recording has no verified destination.

## Clipboard rules

Keep **Always replace the clipboard with completed dictation** off to preserve
what you copied before speaking. This optional explicit override remains off
by default. The completed-paste path temporarily stages the draft, then restores
the previous clipboard, including when Enter is withheld or readback is uncertain.
Temporary text opts out of Windows clipboard history and cloud clipboard sync.
A newer clipboard copy made during processing or restoration always wins.
Restoration retries a temporary clipboard lock at most twice; a persistent
failure is reported instead of being silently treated as success.

Clipboard writes check ownership while holding the Windows clipboard lock.
The previous text, rich-text/HTML, file lists and common bitmap formats are
preserved together, including browser HTML/source metadata. The snapshot is
bounded to 64 MiB and 128 formats. An unsupported application-specific format or an oversized
clipboard refuses the paste route before modification. Sona can then insert a
single-line draft of at most 6,000 UTF-16 characters directly as Unicode, keeping
the clipboard untouched. Control characters are not converted or injected.
Full input acceptance, exact target/epoch, released keys and complete before/after
readback are required before one Enter. Zero or partial input never triggers
retry, send or automatic clipboard recovery. Longer or multiline drafts remain
available in Sona if clipboard capture is unavailable. Temporary paste does not promise support
for every application's private clipboard objects.

0.6.1 corrects a 0.6.0 compatibility regression: Windows' `DataObject` and
`Ole Private Data` bookkeeping is excluded from snapshots, while the actual
payload formats remain preserved. This allows ordinary text copied by WPF
apps, including Sona's Copy draft, to be borrowed and restored. Delayed
rendering uses the snapshot's final sequence instead of treating Windows'
own materialization as a new user copy.

With recovery enabled, **Draft copied** means no paste was attempted and normal
Ctrl+V can recover the result. **Check the draft** means insertion was attempted;
look in the field first, and use Sona's Copy draft only if the text is missing.
**Draft pasted** means paste was verified but sending was withheld. Press Enter
yourself after reviewing it. A missing send acknowledgement never justifies
replacing the clipboard with an already-pasted draft.

Completed clipboard paste accepts up to 32,000 characters. Full before/after
readback, unchanged field/window identity, released keys and clipboard ownership
still guard automatic Enter. Unicode typing retains its smaller single-line limit.

## Interrupted audio

Stop includes the last audio buffers delivered before the device acknowledges
stopping. A device error, unexpected disconnect or stop timeout uses the audio
already captured to make a reviewable draft, without automatic copying, paste or
send. **Draft recovered** identifies an interruption; the five-minute recording
limit has its own message. Silent or too-short recordings still fail clearly.

The older dated sections below describe historical releases. These current
clipboard and interruption rules supersede their recovery descriptions.

# Enter completion and full-draft delivery

## Hold and release

Focus the field, hold Ctrl+Windows while you speak, then release. Sona pastes
the finished draft once into the same compatible field. Read or edit it there
and press Enter yourself when ready.

If Sona shows **Check the draft**, look in the field first. Sona already
requested a paste and couldn't confirm the result. If the field is empty,
paste the recovery copy if enabled or open Sona to copy the draft. **Draft
copied** means Sona skipped the paste and saved a recovery copy.

## 0.5.7 Enter after returning to the original composer

If you switch away during hands-free dictation, return to the original text
field before pressing Enter. Sona can then confirm the same window and editor
and finish the requested paste and send. It checks the focus at the Enter
gesture and requires the exact original field identity. A different field or a
change after Enter leaves the draft available for recovery. Sona verifies the
complete paste before sending.

This recheck applies when Enter ends active hands-free recording. Releasing a
Hold shortcut after a focus change preserves the draft for review.

## 0.5.6 embedded composers

Windows can prove that a deeply embedded editor belongs to the foreground
window through the native handle of its container. Sona accepts that exact
window proof without waiting to reach the top of the accessibility tree.
The captured field and focus checks still apply.

## 0.5.5 temporary accessibility gaps

A live Codex attempt requested paste but failed to re-identify the composer
during readback despite an unchanged Windows focus epoch. A later hold attempt
confirmed paste normally. Before staging the clipboard, Sona can now briefly
recheck the exact original field when the accessibility provider is unavailable.
After a paste, a temporary gap can use the existing verification window to check
the same field again. A different editable field, changed foreground window or
changed focus epoch ends the attempt. An interruption after paste can confirm
the pasted draft but cannot authorize automatic Enter.

## 0.5.4 paste verification and recovery

Hold-to-talk finishes when you release Ctrl+Windows. To finish and send with
Enter, double-tap Ctrl+Windows to start hands-free and press Enter while Sona
is listening in the focused composer. Enable Enter-to-send in Audio & typing.
Sona sends only after it can verify the full paste in the original field.

Windows editors can expose both a value and a document text view. Sona records
a baseline for each view it can read. It verifies the exact draft against the
same view after one paste. If the value is unchanged or unavailable, the document
view can supply independent proof. A changed value that disagrees with the
complete paste withholds Enter. The final check rereads the proving view and
still requires the same field, focus epoch, released keys and clipboard sequence.

- **Draft copied** means Sona retained a recovery copy without requesting a
  paste. Focus the intended field before using Ctrl+V.
- **Check the chat** means Sona requested a paste and could not confirm it. Look
  at the field first. Paste manually only if the draft is missing.
- **Draft pasted** means Sona verified the text but did not confirm a requested
  send. Check the field before pressing Enter again.

The agent keeps the most recent delivery issue across successful dictations.
The snapshot adds readback classification without retaining any text. It is
cleared when Sona exits. See [agent status](AGENT-INTERFACE.md).

## 0.4.9 completion and focus handling

The owner reported Enter leaving transcription active, with partial text in the
composer, while Ctrl+Windows later completed a retained draft. Source inspection
and isolated regressions found two Enter completion defects: field/epoch drift
could let native Enter through, and field validation in the UI stop handler could
clear shortcut state without stopping capture.

An enabled Enter gesture now belongs to the exact captured foreground window and
its owner process. Field and focus-epoch checks authorize output independently.
Once the gesture is admitted, the handler finishes active capture or queues intent
if final processing has already started. A different foreground window or owner
retains native Enter behavior. Consumed repeats and key-up stay swallowed after
focus moves. This changes no final output permission: full recognition/cleanup,
exact field identity, unchanged focus epoch, departure/return protection, released
keys, clipboard ownership, cancellation and complete/final text readback remain
required before sending. No epoch is reset or rebased.

The complete-paste verifier also handles an overlapping middle insertion. For
example, inserting `ba` at `a|b` yields `abab`. Maximal prefix/suffix trimming
previously rejected this complete payload. The new pure-insertion proof requires
exact payload-length growth, the full new payload, and preservation of every old
character. The existing strict replacement proof remains unchanged. Partial,
preexisting, unrelated and ambiguous replacement text cannot authorize sending.

When the full paste is confirmed but a later send check fails, the pill reports
**Draft pasted** and asks the owner to check the original composer. It does not
recommend a second paste. Unconfirmed requests retain the existing cautious
recovery guidance. A newer clipboard copy always wins.

`sona-agent status` adds nullable `lastDelivery`, a single in-memory observation
of the last completed stop. It records outcome, capture classification, focus
epochs, destination-change and paste/send/recovery flags. It contains no draft,
field text, labels, titles, clipboard contents or raw runtime identity. Reading
status performs no new focus capture and cannot start recording or submission.

## Verification and limits

Before repair, isolated regressions reproduced capture remaining active after
Enter, missing queued intent, false rejection of overlapping complete insertion,
and misleading Ctrl+V feedback after confirmed paste followed by focus drift.
The maintained native Enter fixture passes 49 checks, with fake capture, delayed
recognition, focus and paste transport. The complete-paste suite passes 25 checks,
including exhaustive agreement with 28,644 possible pure insertions and adversarial
changes over a small alphabet. Existing Core388, default Windows49, capture11,
insertion24, active-target34 and responsiveness50 checks also pass. The build
entrypoint includes the new Enter and complete-paste regressions.

An independent source/report review found no remaining release-blocking issue in
the bounded repair. Tests suppress native displays and use no real microphone,
global input, owner profile, clipboard or Discord session. They do not establish
physical-key timing or acceptance in every real composer. Duplicate Windows focus
notifications remain an unproven explanation for any later output refusal; this
repair intentionally keeps the existing final focus protections.
Exact published 0.4.9 modules from app source `6cd8e8e7` passed Core388,
Enter49, responsiveness50, completed-paste25, default Windows49, capture11,
insertion24 and active-target34. Private guarded activation verified running
version0.4.9/build07731528a9a9, all36 installed hashes, the working draft/original,
complete recent history and saved preferences. Auto cleanup and the existing
protection policy were retained. Startup warming initially withheld final
verification; encrypted recovery stayed intact until idle exact-content readback
and finite cleanup succeeded without another restart. Physical Enter in the
owner's receiving app remains separate acceptance; no microphone, global input
or real Discord behavior was tested.
