# Draft review and completed insertion, 0.4.2

Save draft on Home keeps its original transcript, generated version and current
edit in `saved-drafts.json` in the existing local profile. It does not turn on
automatic history. Explicit saved drafts and recent history each have a 200-record
bound and a 6,000-character per-text bound. History can reopen the saved record;
removing it leaves the current editable text in Home. Export draft writes one
review JSON file to a location chosen by the user. Nothing is uploaded.

Review corrections compares the generated version with the edited draft. A short
word or phrase replacement can be proposed, for example `codecs` to `Codex`.
Only clicking Learn correction adds that rule. Existing rules are not silently
overwritten, stale suggestions cannot be applied, and Vocabulary can remove the
rule. Sona is learning approved text substitutions, not fine-tuning an acoustic
model or inferring missing facts from private drafts.

## One completed paste

Paste completed draft is on by default. Recording previews remain inside Sona.
After final recognition and optional cleanup, the original destination receives
one Ctrl+V gesture with the whole draft. The recording display shows a bounded
processing state while it waits. Turning this option off retains the older Unicode
insertion path. Neither path moves focus or retries automatically.

The clipboard is temporarily used for paste. Sona captures its existing data
object, checks for concurrent copies, and attempts restoration only while its
clipboard sequence still owns the content. A newer copy is preserved. Clipboard
backup remains a separate preference. Clipboard availability and application
consumption cannot be guaranteed; the complete editable draft stays in Sona.

## Explicit Enter to finish and send

Enable Enter to finish and send in Settings and save. Its default is off. During
double-tap hands-free recording or configured Toggle recording, a bare physical
Enter finishes recording and requests submission after final processing. The
original key, repeats and subsequent bare Enter presses during that pending
request are consumed while the destination remains valid. Shift+Enter retains
its normal meaning. Ordinary hold/release and Stop do not request submission.

Submission requires the original field to remain focused, no manual draft edit
or destination drift, released modifiers, and accessibility readback confirming
the complete newly inserted payload and an unchanged final composer snapshot.
Readback that is missing, partial or ambiguous withholds Enter. The user then
reviews the composer and sends normally. There is no automatic retry. Windows
accepting input is not proof that the receiving app delivered a message.

## History by local day

History groups recent dictations by their local calendar day, with explicitly
saved drafts in a separate leading group. Choose Today, Last 7 days, Last 30 days,
All recent, or a specific day. Seven and thirty days include today and the six
or twenty-nine prior local dates. Search narrows the displayed records and the
clear scope; saved drafts remain outside the recent date range.

Clear recent first previews the exact matching count and asks for confirmation
inside Sona. Cancel or changing the range, day or search cancels that preview.
Confirmation removes only the previewed recent records, preserving saved drafts
and the current editable draft. Failed reads or atomic writes abort without
removing session records. New records arriving after the preview are not added
to its deletion scope.

Turning history retention off stops new dictations from being stored. Previously
stored history stays visible and can still be cleared by date; explicitly saved
drafts remain on this device. These operations are local and never open the
microphone or insert text into another application.

## Verification boundaries

Core checks cover the gesture reducer, reviewed corrections and serialization.
Windows fake-boundary checks cover one paste, complete-readback guards,
cancellation, focus/clipboard drift and one explicit Enter. Native isolated
fixtures exercise Home save/reopen, model-family navigation and the actual Stop
pipeline with fake capture and fake insertion. These tests never record a live
microphone, manipulate the production clipboard or send a real message.

On October 15, 2026, the owner confirmed that physical Enter during hands-free
dictation sent the completed message in Codex on installed 0.4.2 build
`aa8b429e480b` (source `05f838c87641507d7eb7f81e755559de7bd3627f`). This
is a real owner-reported smoke test, not coverage of every composer or speech
condition. Discord submission and live-call acceptance remain separate checks.
See [owner checks](../tools/owner-test.md), [recording display](RECORDING-DISPLAY.md),
and [cleanup model evaluation](CLEANUP-MODEL-EVALUATION.md).
