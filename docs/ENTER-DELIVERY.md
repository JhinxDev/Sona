# Enter completion and full-draft delivery, 0.4.9

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
