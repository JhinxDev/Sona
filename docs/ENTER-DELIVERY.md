# Enter completion and full-draft delivery

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
