# Daily-use repair and live draft, 0.4.1

The October 15 wave covers recording, draft review, insertion recovery, theme
persistence and cancellation/history ownership. It does not claim that the
owner's microphone/accent or Discord composer has passed acceptance.

## Destination and recovery

Sona captures the focused editable field when recording starts. Pointer hover
does not select a composer. It never restores another app's focus or follows
the pointer. Background accessibility focus events no longer invalidate an
unchanged foreground composer. Foreground-window and field changes still revoke
insertion, including leaving and returning; unknown events remain fail closed.
Capture verifies the foreground process and rejects exposed read-only fields.

When insertion is unavailable the recording display keeps an actionable review
notice for 12 seconds. Open draft activates Sona only when clicked. Copy remains
explicit unless clipboard backup was enabled. Ctrl+Alt+Shift+V can insert a ready
draft into the currently focused composer when that alternate shortcut is
registered. There is no automatic retry, Enter/Send, or partial-text insertion.
Windows accepting Unicode input is not proof that an application consumed it.

## Live transcription

Enable Show live transcription in Settings and save preferences. Default is off.
Every two seconds after the preceding request, Sona takes an independent
in-memory snapshot of captured PCM and asks the selected local speech model for
provisional text. Only one preview request can be in flight. Silence is skipped;
the existing 120-second recording limit still applies. This is periodic local
recognition, not native token streaming. Preview inference has a 12-second limit;
if unavailable it pauses preview and retains recording for final recognition.

Stopping drains the one preview before final recognition and optional local
cleanup. No cloud provider is used. The text/state labels distinguish provisional
raw words, final raw, and a cleaned result. Cleanup currently runs after stop.

Opening Home to edit intentionally changes focus and keeps the result in Sona.
The first manual edit protects the entire editable draft; subsequent raw words
continue separately. Final cleanup becomes a separate suggestion instead of
overwriting that draft. Use cleaned suggestion is explicit and session-bound.
Cancellation rejects pending results. A cancelled preview is retained separately
from an earlier history entry; preview updates never create saved history.

## Preferences and build identity

Legacy profile/environment compatibility remains intact. Failed reads now return
an explicit unavailable state, block saving defaults over the saved file, and
show recovery guidance. Corruption discovered during a save also preserves the
existing file. Repair access/the file and restart to reload. Startup and recording
services do not act on fallback defaults. A failed load cannot hide a window with
no tray recovery route. Theme changes still save immediately without committing
unrelated unsaved form choices.

Settings About identifies the running module build ID and exact preferences path.
The CLI status exposes build ID/load state without private text or filesystem paths.
No earlier profile was migrated, deleted or rewritten by this repair.

## Reproduce the affected checks

Use the documented isolated SDK/runtime environment and build flags. Core tests
include live draft ordering, manual edits and preference read/write failures.
The Windows executable supports:

```
--insertion-fixture
--live-draft-fixture <fresh-output-directory>
--live-speech-fixture <licensed-16kHz-mono-wav> <fresh-receipt.json>
```

The first two use deterministic fake system boundaries. The speech fixture uses
the existing pinned Parakeet runtime and fake capture, never a microphone.
`tools/theme-persistence-smoke.ps1` checks native selection, relaunch and failed
saves. `tools/profile-recovery-smoke.ps1` checks unavailable startup and quit.
`tools/owner-test.md` provides the short actual-target acceptance run.

Primary Win32 contracts consulted:
[focus/foreground events](https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants)
and [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput).
