# Changelog

## 0.4.7

- Discord mute and incoming-audio switches apply/save immediately and show their saved policy beside the controls. Advanced edits retain their separate Save action.
- Changing incoming silencing applies to active recordings; rollback preserves explicit pause and manual overrides without transient voice acquisition.
- Failed acquisition/restoration remains retryable, including manual overrides during awaited retries. Imported settings remain pending for review.
- Added isolated native switch/failure/persistence regressions to the maintained build checks. Real Discord call acceptance remains separate.

## 0.4.6 - full History action label

- Show Clear recent history in full, without an ellipsis or a changing count in
  the button. The exact count remains immediately below the controls and in the
  confirmation. Existing deletion and pause behavior is unchanged.

## 0.4.5 - History deletion and pause

- Re-enable Clear recent automatically when dictation returns to idle. The
  confirmation still removes only the previewed range/search records and keeps
  saved drafts plus the current editor.
- Add Delete draft to individual recent rows, alongside saved-draft deletion.
  Cleared current drafts do not silently reappear at the next dictation.
- Add persistent Pause history / Resume history on History. Pausing stops new
  session and saved automatic entries while dictation and explicit Save draft
  remain available. Resume never collects a paused recording that is still
  transcribing, or a draft made while paused.
- Keep unrelated unsaved settings and edits to another opened history draft.

[History controls and qualification](docs/HISTORY-CONTROLS.md).

## 0.4.4 - destination capture guidance

- Explain when Windows reports a selected suggestion or list item rather than
  the editable field. Offer Copy draft and manual paste without repeating the
  misleading request to reposition an already visible caret.
- Verify exact-window containment, current focused identity and focus epoch
  before classifying rejected controls as well as accepted fields.
- Report the last intentional capture as bounded, in-memory metadata through
  `status.destinationCapture`. Status reads do not inspect focus or export text.

An observed browser integration uses ARIA active descendants, so accessibility
focus can land on a suggestion while DOM input focus remains on Search. A
controller relationship alone cannot establish keyboard ownership. This version
keeps that case blocked; it does not repair automatic insertion through active
suggestions. Empty-field alternate paste was observed separately, without Enter.
[Audit and limits](docs/DAILY-USE-AUDIT.md).

## 0.4.3 — daily-use reliability

- Prove focused fields belong to the exact foreground window even when UIA is
  hosted by another process; keep separate provider and window identities.
  Accept writable-value comboboxes with the same focus and safety guards.
- Explain capture failures without logging field contents. Retained drafts stay
  available for an intentional retry.
- Include model/runtime integrity verification in the startup deadline and
  normalize access errors without changing owner cancellation.
- Continue Quit cleanup after completed preparation faults; pending operations
  retain bounded waits and retry guidance.
- Preserve history files damaged since startup, including invalid saved records.
- Reject agent settings patches while native settings have unsaved edits;
  expose `unsavedSettings` in status.

The owner-reported Homepage Search failure still needs exact-candidate acceptance.
The capture changes are independently tested compatibility repairs, not a proven
diagnosis. [Audit](docs/DAILY-USE-AUDIT.md).

## 0.4.2 — October 15, 2026

Source preview; no public binary release yet.

### Dictation and drafts

- Paste the completed draft once after recognition and optional cleanup.
- Opt-in physical Enter finishes hands-free recording and sends only after
  complete insertion readback in the original focused composer. Stop and ordinary
  hold/release do not submit; ambiguous readback withholds Enter.
- Save original, generated and edited draft text locally; reopen or export it.
- Approve short corrections as removable Vocabulary rules. No acoustic-model
  training or automatic inference of missing facts is performed.
- Group recent history by local day; filter and clear a confirmed date range
  without clearing explicitly saved drafts or the current editable draft.

### Interface

- Compact Whisper and Parakeet model families with a green selected-family marker.
- Seven settings sections, improved description spacing and round toggle thumbs.
- Microphone label aligned with its selector; compact Models footer hides empty
  feedback and keeps automatic scrolling only when the content overflows.
- Smaller translucent recording displays with bounded recent phrases, installed
  app icons and quiet unknown-focus states. The capsule retains a readable
  waveform and removes the Open draft button and provisional-text label.
- Recording indicator and neutral square Stop, integrated padded transcript inset,
  and a decibel waveform scale that preserves speech dynamics instead of
  saturating early. Verified with prerecorded speech through the real recognizer.
- More translucent recording surfaces independent of main-window Mica, with
  opaque text/controls and Windows transparency-off/high-contrast fallbacks.
- Softer original cues for opening, recording, draft readiness and explicit Enter,
  with a Sound effects setting. Sent feedback denotes input, not server delivery.
- One GitHub link in the sidebar.
- Clear Discord connection status and Connect/Disconnect controls, preserving
  credentials and restoring owned voice changes before disconnecting.

### Daily-use repair

- Installer and guarded upgrade launches now belong to the Windows desktop,
  so closing their launching terminal does not close Sona. Encrypted draft
  recovery and private transfer remain intact; speech-worker jobs are unchanged.

- Fixed Ctrl + Windows appearing to do nothing when Discord was closed and call
  protection was enabled: the protection preparation step had rejected recording
  before the capsule appeared. Closed desktop Discord now permits dictation;
  an open but unprotectable Discord connection still blocks protected recording.

### Qualification and limits

The owner confirmed Enter-to-send in Codex on build `aa8b429e480b`, source
`05f838c87641507d7eb7f81e755559de7bd3627f`. Synthetic and native isolated checks
cover insertion guards, clipboard/focus changes, drafts, history, settings, icons,
sounds and upgrade recovery. Other apps, accents and live calls need their own
acceptance. [Verification details](docs/PREVIEW-VALIDATION.md).

Spokenly Cleanup 1 remains the cleanup model after a bounded comparison with two
alternatives; this is not a universal best-model claim.
[Evaluation](docs/CLEANUP-MODEL-EVALUATION.md).
