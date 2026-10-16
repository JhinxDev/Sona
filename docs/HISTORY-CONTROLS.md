# History controls, 0.4.5

The owner reported that Clear 14 recent did nothing. Actual native readback showed
the button disabled while Sona was idle. AddHistory had refreshed it while
transcription was busy, and the subsequent idle action refresh omitted History.
The maintained refresh now updates History actions without rebuilding the list.

## Behavior

- Clear recent previews the current date range and search. Confirm removes only
  those exact recent IDs. Saved drafts and the current editable draft/original
  stay. Storage failures preserve memory and the original file.
- Every row has Delete draft. Recent rows preview one exact ID; explicitly saved
  rows retain their existing removal path. Deleting the currently opened record
  does not silently retain it again when the next dictation begins.
- Pause history stops automatic new entries and updates in both session History
  and saved automatic history. It does not stop dictation, remove existing rows,
  or prevent an explicit Save draft. Keep history remains the separate choice
  to retain automatic records across app restarts.
- The pause choice persists. Resume starts future dictations only. Session
  suppression survives an in-flight transcription or cleanup finishing after
  Resume. Opening another existing draft establishes its own edit-retention
  scope, so its edits are not lost because a previous draft was paused/deleted.
- Pause changes only its committed preference. It preserves unrelated unsaved
  form choices and reports storage failure without changing the prior choice.

## Qualification

The initial native regression reproduced the disabled idle Clear button and
missing pause controls before repair. An independent review found a pending
completion privacy leak and unrelated-draft edit suppression. New regressions
failed before both lifecycle repairs, then passed.

The maintained unshown native fixture passed 23 assertions with automatic disk
retention enabled and 23 with it off. It invokes the actual button automation
provider and covers fourteen-record confirmation/deletion, saved/current-text
preservation, pause persistence, pending completion, resume without replay,
individual deletion, edit retention and locked preferences. The default build
also passes Core350, Windows49, capture-policy11 and capture-status10; the
dependency audit reported no known vulnerable packages. Existing calendar/range,
retention-off layout and pending-settings checks passed in the same repair.
No microphone or global input is used. Physical speech and receiving-field
acceptance remain separate. The live-update contract in IMPLEMENTATION.md applies.
