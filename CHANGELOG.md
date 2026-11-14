# Changelog

## 0.6.1 - ordinary Windows clipboard compatibility

- Fix 0.6.0 refusing copied text from WPF apps, including Sona's Copy draft.
  Exclude only Windows' DataObject/Ole Private Data transport bookkeeping from
  snapshots; preserve actual text, HTML and other supported payload formats.
- Use the sequence captured after delayed rendering for paste ownership, while
  recovery still checks for a newer owner copy before capture.
- Expose a content-free clipboard stage in delivery diagnostics.
- Add actual Windows/WPF clipboard regression coverage in a private window
  station, run on the public repository's Windows CI runner.

## 0.6.0 - recording, delivery and recovery rework

- Separate hands-free Enter stopping from text-field capture, and recheck an
  initial same-window pane-to-editor focus race. Show draft-only capture early.
- Preserve the previous clipboard after attempted paste, including withheld
  Enter and uncertain readback. Use optional recovery only before a paste attempt;
  retry temporary restoration locks without overwriting a newer owner copy.
  Preserve browser/image formats and exclude temporary text from Windows
  clipboard history and cloud sync.
- Keep final microphone buffers and recover interrupted audio as a review-only
  draft. A healthy in-flight preview can complete without an avoidable reload.
- Support 32,000-character paste, history and saved-draft fields, retain explicit
  file-size limits, and keep older-binary rollback within its supported limits.
- Save recent history once after delivery and avoid unchanged saved-draft writes.
- Stage and verify private installations before activation, and recover aliases
  after caught publication failures so same-package retry remains safe.
- Find uploaded public preview installers during optional update checks.

## 0.5.8 - clean dictation paragraphs

- Clean joins sentence-by-sentence transcript and model lines into paragraphs,
  with sentence-boundary breaks for long prose. Existing lists, blank-line
  paragraphs, code blocks and exact snippets keep their structure.
- Clean and Notes honor English and French new-line/paragraph commands.
  Verbatim retains the original layout.
- Safe basic formatting also runs when model cleanup is rejected or over limit.
- Written numeric values and complete code blocks are protected during cleanup.

## 0.5.7 - finish hands-free dictation after returning to the composer

- Enter can reconfirm the original focused field after a switch during
  recording. Sona withholds output if the field differs or focus moves again.
- Delivery status records when Enter reconfirmed the original target.

## 0.5.6 - paste into deeply embedded editors

- Use a hosted editor's exact native window ancestry to avoid rejecting the
  foreground composer when the accessibility tree is deeply nested.
- Clarify the warning after a paste whose result could not be verified.
- Put the verified public installer and checksum links at the top of the
  README and spell out hold-to-talk review and manual send.

## 0.5.5 - tolerate temporary composer readback gaps

- Recheck a temporarily unavailable captured editor before staging the clipboard.
  After one paste, allow the original field to become readable within the
  existing verification budget. A different editable field or foreground window
  ends the attempt, and an interruption after paste withholds automatic Enter.
- Include capture rejection and gap information in text-free delivery status.

## 0.5.4 - safer paste verification and clearer recovery

- Verify a completed paste through the same field's document text when the
  value text is missing or unchanged. Require a complete change in a single
  representation and recheck that representation before Enter.
- Give manual paste guidance when no paste was requested. Show a check-first
  warning for unverified pastes even with clipboard recovery disabled.
- Keep the most recent delivery issue in text-free, in-memory agent status
  until another delivery issue or a restart.

## 0.5.3 - real model-loading information and always-resident models

- Loading feedback is now exact instead of generic. While a model really loads,
  Sona names the actual model ("Whisper Large v3 Turbo", "Spokenly Cleanup 1"),
  the actual stage ("Checking ... files" then "Loading ...") and the measured
  elapsed seconds, for example "Loading Whisper Large v3 Turbo · 2.8 s · Esc
  cancels". Warm runs show no preparation text at all. A completed cold load adds
  one measured note with its real duration.
- The engine loaders measure the real cold-load duration. Models and Text cleanup
  now show "Last load took X s" and, when Keep until quit is selected, "Keeps this
  model loaded until you quit Sona". The agent status exposes the measured
  `speechLastLoadSeconds` and `cleanupLastLoadSeconds` for diagnostics.
- Keep until quit now works for both workers. This pass sets the live profile to
  keep the speech model and the text-polish model resident, so dictation does not
  wait for a reload after an idle period; normal Quit still releases them.
- **Memory residency preference now saves during warm-up.** Choosing Keep until
  quit (or another idle time) in Dictation Models while a model is warming is no
  longer rejected: it saves, applies immediately and explains that the model stays
  loaded. Two maintained checks cover the warm-time save and its copy.
- New regressions: the model-loading fixture checks the exact copy, measurement
  formatting, no hint on warm runs and the recording-path hint, 40 checks total.

## 0.5.2 - recognizable background workers

- Windows Task Manager and Process Explorer now show **Sona Speech Engine** for the
  speech worker and **Sona Text Polish** for the optional local cleanup worker,
  instead of the upstream `whisper-server.exe` and `llama-server.exe` names (and
  **Sona Transcription** for the one-shot WAV worker). Sona launches byte-identical
  copies under those names; the hash-pinned originals stay in place and keep their
  integrity checks. Cleanup's copy lives outside the hash-verified runtime
  directory, and its working directory keeps the adjacent ggml/CUDA libraries on
  the loader path.
- Copies refresh automatically when an engine is updated, and a read-only or
  missing runtime falls back to the original binary rather than failing dictation.

## 0.5.1 - Discord deafened support

- Fix: dictation now starts when Discord is deafened. Discord already has the
  microphone off when you are deafened and refuses a separate mute command, so
  Sona now treats a deafened call as microphone protection and leaves your deafen
  choice untouched. Previously the protection preparation aborted the recording
  with "Discord did not acknowledge microphone protection."
- Harden the mute and deafen acknowledgements: if Discord replies with stale
  voice settings, Sona confirms the actual state and retries once before failing.
  Missing confirmation now explains the undeafen, reconnect and disable options.
- Never send an un-mute while your own deafen is active, so protection release
  cannot clear your deafen.
- New regressions cover the deafened path in Core and in the fake Discord
  transport fixture.

## 0.5.0 - per-app modes and first-run guidance

- Add ordered **per-app modes**. A captured app identity can select its own draft
  style and local cleanup tone. Matching uses process, friendly and executable
  identities only; unmatched apps use the default mode. Managed on Modes and
  saved with preferences. See docs/MODES.md.
- Add a first-run welcome card and a separate **local speech model needed**
  callout. The card links to model, settings and recording-display setup, reports
  the actual model state, and remembers dismissal. Record is disabled until a
  usable model exists.
- Add a live draft word count on Home.
- Move Esc cancellation onto its own listener thread so a busy WPF dispatcher
  cannot delay the low-level hook past the Windows hook timeout. Disarm clears
  the armed flag before unhooking, so an orphaned hook never suppresses Esc.
- A microphone that stalls while stopping now keeps the audio captured before
  the stall instead of throwing it away, and the result explains the incomplete
  stop. A failed optional live preview no longer discards recorded audio, and an
  unexpected stop-path fault resets the session instead of stranding it.
- Harden Quit: agent and Discord cleanup faults surface retry guidance instead of
  leaving the window disabled, and an unexpected quit fault keeps the app usable.
- Windows startup registration can only be written from an installed build, so a
  source build can no longer repoint the user's Run entry.
- Flush preferences, history and encrypted Discord setup to disk before atomic
  replacement (docs/VERIFICATION.md).
- Centralize the product version so the app, agent and MCP server report the same
  version.

## 0.4.11 - model loading feedback

- The recording pill distinguishes **Checking model**, **Loading model** and
  recognition or cleanup. Warm runs skip preparation feedback; live preview keeps
  the timer and Stop visible while explaining a model-loading wait.

## 0.4.10 - capture readiness and retained notices

- Guarded start retry when Windows reports a container before the editable field
  is ready. Compact centered draft notices distinguish copied, unconfirmed and
  confirmed pastes.

## 0.4.9 - Enter completion

- Enter reliably finishes an admitted recording even if the field identity
  changes, while destination, epoch and readback guards remain mandatory.
  Full-paste verification accepts overlapping complete insertions and withholds
  a misleading second-paste suggestion.

## 0.4.8 - responsiveness and delivery

- Earlier preparation feedback, narrower capsule with quick cleanup cycling,
  five-minute recording bound that retains a draft, queued Enter during
  processing, optional clipboard recovery and live-preview reuse only for
  unchanged audio.
## 0.4.7

- Discord mute and incoming-audio switches apply/save immediately and show their saved policy beside the controls. Advanced edits retain their separate Save action.
- Changing incoming silencing applies to active recordings; rollback preserves explicit pause and manual overrides without transient voice acquisition.
- Failed acquisition/restoration remains retryable, including manual overrides during awaited retries. Imported settings remain pending for review.
- Added isolated native switch/failure/persistence regressions to the maintained build checks. Real Discord call acceptance remains separate.

- Guard upgrades against losing session-only History. The explicit finite transfer preserves recent entries with History storage Off and retains recovery after an uncertain Quit.
- Keep isolated UI tests offscreen without activation; start updated apps minimized and retain the older hidden rollback route.

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
