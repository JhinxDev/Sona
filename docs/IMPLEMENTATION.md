# Local dictation implementation gates

Reliability repairs and acceptance limits: [DAILY-USE-AUDIT.md](DAILY-USE-AUDIT.md). Current draft, insertion and history behavior: [PERSONALIZATION.md](PERSONALIZATION.md). Earlier daily-use repair and live-draft behavior: [DAILY-USE.md](DAILY-USE.md).

The primary owner integrates the app changes and a scoped independent review. Existing owners retain standalone Hush, media, shared catalogs and Vault Git control.

1. Build `Sona.Core` for text transformation, original preservation and a single session state machine. Run the executable regression harness in `tests/Sona.Tests`.
2. Build `Sona.App` for native recording, nonactivating overlay, shortcut registration, reference-based sidebar/settings/history UI, safe focus-bound insertion and bounded worker lifecycle. Keep all runtime/download/build output outside the synced Vault.
3. Qualify the frozen CPU and explicitly installed CUDA engine/model candidates. Use `tools/benchmark.py` with a caller-owned synthetic/licensed corpus. Record hashes, WER limitations, latency, peak worker RAM and process exit. Rerun only affected cases or missing proof.
4. Use the task-owned native UI for saved settings/readback, narrow layout, cancellation and clean quit. Add test lifecycle bounds so verification never leaves a worker or hidden test window.
5. Prepare a safe owner-triggered insertion/voice exercise in an empty Codex draft. Verify opt-in clipboard backup or preservation when disabled, focus drift, no duplicate text, no submission without explicit opt-in Enter intent, and call-protection restoration. No background microphone test is permitted.
6. Freeze the candidate, obtain one integrated correctness/security review, fix actual findings, audit dependencies, install the exact private candidate and compare artifact/source identities. Startup remains opt in.
7. Keep reviewed source history, compare local/remote heads, update `Projects/local-dictation/current-state.json`, generate/validate projections, and coordinate the shared documentation packet through Tool Shed. Report any actual enforcement blocker.

The current source candidate identity is Sona 0.4.6; source projects and namespaces use Sona. Existing private profiles and single-instance signals have narrow compatibility handling. Ordinary private installation follows qualified source/package identity and a current-user encrypted pre-quit draft checkpoint with verified recovery.

Parakeet qualification uses `Sona.WindowsTests --speech-fixture <corpus> <fresh
JSON> <pinned CUDA ZIP> <pinned CPU ZIP>` and `--speech-memory-fixture <fresh JSON>`.
The licensed corpus stays outside Git. The fixture retains completed short-clip
measurements when only the lifecycle harness changes; it never opens a microphone.
`tools/models-ui-smoke.ps1` checks provider/license/status and real model actions
at normal and compact sizes. See PARAKEET.md for pins, native compatibility
source, the long-recording repair and the existing Whisper mixed-language limit.

Optional cleanup uses shared Core preservation rules and processing orchestration,
one contained llama.cpp worker, pinned separately downloaded assets, and the same
validated Settings/CLI/MCP layer. Qualification runs `--cleanup-fixture <fresh JSON>`
from the Windows test executable against caller-provisioned verified runtimes.
`tools/cleanup-ui-smoke.ps1` checks affected native controls at normal and compact
sizes; `tools/agent-smoke.ps1` checks the live CLI/MCP tool and settings contract. Reuse
unchanged speech, theme and update-recovery evidence rather than repeating it.

`tools/theme-persistence-smoke.ps1` owns immediate appearance saves, relaunch,
failed general/theme-save recovery, and preservation of unrelated unsaved form
values. It uses a separate profile and disables microphone/startup control.

Remaining strategic boundaries are optional cloud authentication/spend and owner live microphone/real-call/creative acceptance. A build or synthetic replay is not completion of those gates.


## Live upgrade delivery rule

The owner's October 16 standing instruction requires qualified new app versions
and functional updates to reach the installed, currently running app. Routine
private live upgrades do not need renewed permission. Complete the guarded
`tools/upgrade.ps1` path: preserve a current-user encrypted recovery checkpoint,
quit normally when safe, launch through the Explorer-owned desktop bootstrap,
and read back the live version/build and exact EXE, package hashes, draft,
original and settings. Keep recovery until that readback succeeds.

Before normal Quit, the upgrader now performs the launcher's read-only desktop
profile preflight. If the caller can see retained Discord setup but Explorer
cannot, it stops while the current app is still running. Resolve the verified
profile split with the no-overwrite encrypted recovery procedure in
[Discord setup](DISCORD-SETUP.md#retained-setup-missing-after-a-desktop-handoff).
Terminal-side file existence alone does not prove desktop profile visibility.

Documentation-only commits do not trigger app activation. Separately qualified
launcher/tooling changes can reuse the accepted app binaries without rebuilding
them; retain their tooling checks and the current live identity.

Busy recording, owned voice changes and unsaved edits must be preserved until
activation is safe. Record any pending live step honestly. Source-only changes
and `-NextLaunch` staging remain incomplete delivery; staging is an interim or
explicitly requested mode, followed by the guarded live upgrade when safe.

## Stage an already-qualified package for the next launch

`tools/install.ps1 -PackageDirectory <verified-package> -NextLaunch` copies and
hash-verifies a separate version directory, then updates the owned Start Menu
shortcut and current junction. It never quits or launches Sona, reads a live
draft, or changes the profile. The running executable must match its installation
receipt; the destination cannot be a running version directory. `-Launch` and
`-NextLaunch` are mutually exclusive. The ordinary installer still refuses a
running instance without this explicit mode.

The running copy remains the current session until a normal Quit; `-NextLaunch`
alone never replaces it.
Opening Sona again while it is running still signals that existing instance.
Startup registration is not changed; this operation controls Start Menu and the
current-path launcher, not an independently pinned old executable or Run entry.
The isolated upgrade rehearsal covers staging, unchanged live synthetic drafts,
invalid combinations, running-directory rejection and existing upgrade recovery
(18 checks). No production restart is part of that rehearsal.

## Desktop-owned installation and upgrade launches

`install.ps1 -Launch` and both the candidate and recovery launches in
`upgrade.ps1` now use `start-sona.ps1`. It delegates a hidden bootstrap to the
actual Explorer desktop through `start-desktop-app.ps1`. Starting a fresh
`Shell.Application` object alone or using `Start-Process` does not establish
independence from an agent terminal's Windows cleanup job.

The bootstrap receives only a short-lived, current-user DPAPI request path on
its command line. It validates the installed target, atomically claims the
request, reads the existing encrypted draft checkpoint and passes its contents
through `SONA_DRAFT_TRANSFER` in the child's private environment. Supported
profile/runtime overrides are transferred explicitly. No draft enters command
arguments, logs or a plaintext handoff file. The updater still verifies the live
draft before removing its own recovery checkpoint. Failed or uncertain launches
retain recovery; an ambiguous acknowledgement never triggers a second launch.
Expired and consumed requests cannot launch another instance. An interactive
Explorer desktop is required; there is no terminal-child fallback.

This changes installation tooling, not Sona's speech-worker containment. The
app still owns its worker jobs and releases them on exit. Existing app packages
can use the repaired scripts; no new app build or user restart is required to
install this tooling repair. `-NextLaunch` remains non-launching.

The Windows implementation follows the actual-desktop dispatch pattern described
by [Microsoft](https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643).
A bounded native regression launches isolated Sona instances, closes the launcher's
kill-on-close job and verifies the direct control dies while the desktop-launched
instance survives, restores its synthetic draft and then quits normally:

```powershell
.\tools\desktop-launch-rehearsal.ps1 -PackageDirectory <verified-package> `
  -OutputRoot <fresh-path-ending-in-sona-upgrade-rehearsal-and-32-hex-digits> `
  -TestExecutable .\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe
```

The 13 checks include private transfer, request cleanup/expiry/replay rejection,
and preservation of the running owner instance and preferences. The existing
18-check `upgrade-rehearsal.ps1` separately exercises installer failure, fallback,
retry and draft/original restoration. Neither test closes the owner's app or opens
a microphone. These reproduce a launch-lifetime risk; they do not establish the
historical cause of any particular Codex shutdown incident.

## Physical recovery paths and verified live 0.4.4

The first qualified upgrade quit the old process, PID `52924`, normally, but
Explorer never claimed its launch request. `GetFinalPathNameByHandle` showed that
`%LOCALAPPDATA%/Sona/recovery` was virtualized into the Codex MSIX
package's `LocalCache/Local/Sona/recovery`. Explorer reported `DirectoryNotFound`
for the literal requested path. This was a cross-process path-identity failure;
the evidence did not indicate an ACL or DPAPI policy failure.

Checkpoint and launch-request handoffs must use canonical physical file paths
resolved from open file handles before passing them to Explorer. A fresh request
using the physical path and the same canonical desktop bootstrap restored Sona;
there was no terminal-child fallback. Tooling regression results are separate
from this verified live recovery.

The maintained `tools/launch-path-checks.ps1` regression first reproduced two
baseline failures: Explorer could not see the logical virtual path or read its
bytes. Eight checks then passed using the production
`tools/resolve-physical-file.ps1` resolver. `tools/start-sona.ps1` resolves the
existing recovery file before embedding its path and the actual request file
before desktop handoff. This regression used no app, profile, microphone or
input. The same eight checks also passed under Windows PowerShell 5.1, including
the actual handle-based resolver and independent Explorer byte match.
A narrow independent review found no actionable issue; DPAPI, atomic claim,
expiry and ambiguous-launch guards remain intact.
October 16 live readback at 16:26 Toronto verified Sona `0.4.4`, build
`0257d73b4eed`, PID `42712`,
at `%LOCALAPPDATA%/Programs/Sona/0.4.4-468a17fa/Sona.exe`. All 36
installed files matched their receipt hashes; draft and original hashes matched
the encrypted checkpoint after restoration,
and the preferences file was unchanged. See [live validation](PREVIEW-VALIDATION.md#october-16-live-delivery-and-recovery-044).
No microphone, global input or submission was used. This recovery supplies no
new physical-key evidence. Homepage Search automatic insertion with active
suggestions remains unresolved.

## Capture observations and browser suggestions, 0.4.4

Capture inspects a single focused element, proves exact HWND ancestry and then
rechecks foreground HWND/owner, focused provider/runtime identity and focus epoch.
Rejected controls undergo the same scope checks before receiving a specific
reason. A ListItem returns `SuggestionFocused` with copy/paste guidance; it never
becomes a destination. In particular, ARIA `aria-activedescendant` can move the
accessible focus while DOM keyboard focus stays on a combobox. A `ControllerFor`
relationship does not independently prove the editable input still owns typing.

The three intentional admission paths retain a `CaptureObservation` in memory.
Agent status returns that existing snapshot without invoking UIA. It exports
bounded classification and identity metadata, not text, labels, titles, raw
runtime IDs or transcripts. Routine insertion safety rechecks do not replace the
original attempt. No history or file is written. The passive test diagnostic can
record these observations only when explicitly run, with a 20-second bound and
optional exact HWND filter. The current API still cannot start a recording,
capture a new destination, export transcripts or submit a message.
