# Changelog

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
- Smaller translucent recording displays with bounded recent phrases, installed
  app icons and quiet unknown-focus states. The capsule retains a readable
  waveform and removes the Open draft button and provisional-text label.
- Softer original cues for opening, recording, draft readiness and explicit Enter,
  with a Sound effects setting. Sent feedback denotes input, not server delivery.
- One GitHub link in the sidebar.
- Clear Discord connection status and Connect/Disconnect controls, preserving
  credentials and restoring owned voice changes before disconnecting.

### Qualification and limits

The owner confirmed Enter-to-send in Codex on build `aa8b429e480b`, source
`05f838c87641507d7eb7f81e755559de7bd3627f`. Synthetic and native isolated checks
cover insertion guards, clipboard/focus changes, drafts, history, settings, icons,
sounds and upgrade recovery. Other apps, accents and live calls need their own
acceptance. [Verification details](docs/PREVIEW-VALIDATION.md).

Spokenly Cleanup 1 remains the cleanup model after a bounded comparison with two
alternatives; this is not a universal best-model claim.
[Evaluation](docs/CLEANUP-MODEL-EVALUATION.md).
