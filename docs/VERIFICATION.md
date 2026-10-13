# Candidate verification

## Installed 0.3.0 readback and recovery limitation

Installed source is `5e9cec5693e57e7d3d01fcf9eece4b25964f2486`; all 26 package
files matched. Live app PID 45920 reports 0.3.0, idle, selected/resident Turbo,
with contained CUDA worker PID 48652. Protection is enabled, connected, unpaused,
shortcut registered and Ready. Startup and the stable current junction point at
the new receipt-owned version. Later installer/docs commits do not change this
installed source identity.

The first update attempt failed after orderly app exit: PowerShell returned the
junction Target as a string, and indexing selected its first character. The
installer now normalizes string/array targets before checking full ownership.
The temporary update helper did not retain its private transfer on failure.
History saving was off and no history file existed. Only the draft visible in
the owner's supplied screenshot was recovered; newer unsaved text and the raw
original transcript are not confirmed recovered. The installed receipt records
`DraftPreserved=false`, rather than a false successful transfer claim.

The maintained `tools/upgrade.ps1` verifies and reads back a current-user DPAPI
checkpoint before Quit, retains it on failure, and restores the prior app when
no relevant instance remains. Its isolated full-flow rehearsal passed 14 checks:
synthetic draft/original seed, disabled capture and protection, deliberate
post-Quit installer failure, encrypted retention, prior-version relaunch, exact
fallback restore, successful retry, exact candidate restore and checkpoint
cleanup. The live owner instance and production PATH/startup/shortcut/current
junction were unchanged. The two package receipt identities were explicitly
synthetic fixtures built from the same qualified source, not distinct releases.
`--test-ui --test-transfer` scopes pipes/mutexes/profile and bypasses microphone,
hotkeys, resident loading and Discord initialization while exercising the same
draft restoration path. Installation rehearsals use a guarded unique temporary
root and omit production launchers/PATH mutations. No live upgrade was repeated
to close this proof. This establishes the maintained helper's bounded recovery
flow; it does not retroactively recover the lost initial unsaved text. Receipts:
`E:/Temp/qazws/sona030-installed.json` and `sona030-recovery.json`.
Full-flow evidence root is recorded in
`E:/Temp/qazws/sona-upgrade-rehearsal-current.txt`; `rehearsal.json` contains the
14 checks. Failed harness evidence is retained separately. The initial alias
collision and collapsed Original-field lookup were repaired; these did not
involve the owner's app or data.

## Resident and appearance wave, 0.3.0, October 13

The owner reports working dictation and actual Discord muting on installed
0.2.0. Restoration was not explicitly reported. The new candidate adds resident
local models, selectable Panel/Capsule measured-wave displays, eight grouped
themes, Inter typography and theme-aware vector branding. Both version labels
derive from the running assembly rather than independent literals.

138 Core checks and 27 Windows lifecycle checks passed; 20 native checks passed,
plus 17 focused appearance checks and 10 CLI/MCP checks. Focused resident tests
passed 14 checks using licensed French fixture audio without microphone capture:
same-worker reuse for two requests, input clearing, timeout expiry, Keep until quit,
switch replacement, canceled queued and in-flight audio, explicit release and real Turbo CUDA
loading. Tiny CPU initial load was 579.6 ms; two warm fixture requests were 233.0
and 213.8 ms on this PC. This is a single fixture timing, not accuracy or a general
speed claim. Earlier corpus metrics remain historical one-shot engine evidence.

Read-only review identified and closed worker ownership loss on failed termination
and canceled idle monitoring after a failed Quit. New native/font work exposed
a queued cancellation after window closure; the affected Windows checks passed
after an idempotent closed-window guard. Failed runs are preserved. WPF's TabControl
template sidebar label is not exposed in the tool's control tree; source and
screenshots verify its shared assembly binding, while About is checked directly.

Evidence: `E:/Temp/qazws/sona030-final-gate.log`, `sona030-core-final.log`, `sona030-resident-qualified.log`,
`sona030-native/native-smoke.json`, `sona030-appearance-proof/appearance-smoke.json`
and `sona030-agent/agent-smoke.json`. Physical live waveform/focus and the new
installed runtime remain separate acceptance evidence until recorded.

## Installed cutover, October 13, 2026

Installed Sona 0.2.0 uses source `248566565d2c3b4f424d7997c5533d597e21eb82`.
All 23 package files matched the receipt. The stable `current` junction and user
PATH expose `sona-agent`; Sona starts hidden at login and keeps running in the
tray when closed. The existing 20 final native checks and 10 isolated CLI/MCP
checks passed. No further code changed after those checks.

The owner confirmed Hush protection paused and Discord disconnected. Both exact
receipt-owned Hush processes were then stopped; fresh process readback returned
zero. Hush's startup entry, taskbar pin and Start shortcut are removed. Installed
files were moved under Sona's `retired-components/Hush-0.4.0-20261013` recovery
directory. Private legacy user data and source history are preserved.

Sona's internally imported DPAPI credential configuration connected successfully
to actual Discord. CLI and native UI readback agree: enabled, connected, unpaused,
shortcut registered, ownsChanges false, Ready. No agent opened a microphone or
conducted a real-call mute/restoration test. Physical Codex insertion and personal
speech/call acceptance remain owner checks; connection is not proof of those.

Receipts: `sona-integrated-installed-readback.json`, `hush-cutover-final.json`
and `sona-cutover-protection.json` under `E:/Temp/qazws/local-dictation-build`.
The installed source identity remains fixed when later documentation-only
commits advance the repository. Google sign-in/sync and public release remain
unimplemented/unpublished.

## Integrated 0.2.0 source wave, October 13

The owner explicitly accepted 0.1.1's redesigned UI. Preserve that baseline.
The 0.2.0 source extends it with six pinned downloadable multilingual models,
local current-user CLI/MCP settings/model/protection operations and internal
Discord microphone protection. Google sign-in/sync remains an optional design,
not an implemented or enabled feature. Public publication has not occurred.

Focused proof: 134 Core checks including strict agent patch/frame bounds,
existing model preservation, overlapping voice-source ownership, manual
unmute/remute ordering (including during undeafen acknowledgement), pause/resume,
call-entry reconciliation and failed delayed restoration retry. 27 Windows checks
include DPAPI fixture protection, fake Discord authentication/subscription/Off
behavior, acknowledged mutations, overlap cleanup, and actual hidden WPF
preparation cancellation with a fake recorder. No real microphone was opened.

CLI/MCP smoke: 10 live isolated interoperability checks. The explicit Tiny model
pilot adds four checks: accepted download, completion, independently matched
catalog SHA256, and selection in isolated preferences. One licensed French WAV
replay with Tiny produced a ready draft without capture or insertion. Tiny
accuracy remains unmeasured. The existing 20 native UI checks passed on the first
integrated wave; repeat affected native checks for the final frozen candidate.

Review repaired seven concrete ownership/lifecycle paths and the residual manual
mute change during undeafen acknowledgement. Failed harness runs are retained:
fake IPC was initially blocked by the production Hush guard; its test prefix now
isolates that guard. Embedding WPF required explicit dispatcher shutdown and an
assembly-qualified icon URI. These failures did not involve actual Discord or
owner microphone/audio. Hush retirement, installed 0.2.0 readback and real-call/
physical Codex behavior remain separate gates until recorded below.

Evidence is under `E:/Temp/qazws/local-dictation-build`: `sona-agent-wave1`,
`sona-model-pilot`, `sona-tiny-replay`, `sona-integrated-native-wave1`. The model
catalog metadata is pinned at whisper.cpp conversion revision
`5359861c739e955e79d9a303bcbc70fb988958b1`; its public provenance receipt is outside
source. No owner credentials or recordings belong in source/evidence packets.

October 12, 2026, Windows x64. Sona 0.1.1 is a renamed/rebuilt candidate; Murmur 0.1.0 at source 20655f65 is retained as recovery history. Running old0.1.0 did not implement Ctrl+Win, explaining the owner's repeated no-op. Do not call a source build installed until its versioned receipt and actual running executable agree.

## Current automated scope

Warning-free .NET 8 build; 113 Core checks cover text/session safety, side-aware hold/toggle admission, queued release/cancellation/reset, preference persistence, lossless snippet encoding and bounded history search/storage. 19 Windows checks cover real hidden hook initialization/disposal with no capture, fake-capture disconnect/stall/silence, final-focus cancellation, containment and audio cleanup/recovery/abrupt-exit worker lifetime. Locked NAudio 2.2.1 audit reports no known advisories from its configured NuGet feed.

20 checks in `tools/native-smoke.ps1` verify isolated disabled-mic mode, the capsule, prepared harmless text and retention of the prior unsent draft, vocabulary add/duplicate/correction, snippet add/edit/delete, relaunch persistence, UI-saved Toggle/history opt-in, searchable loaded synthetic history, opening without automatic insertion, and clearing saved history while preserving the current draft. Exact evidence is outside the Vault under `E:/Temp/qazws/local-dictation-build`; preserve failed runs. Test isolation is data-root scoped and has a 180-second deadline. Compact mode uses `--test-ui --compact` at the app's own DIP dimensions.

Earlier integrated review repairs include paired shortcut release/watchdog handling, snippet Windows-path/newline/delimiter corruption, revocable Toggle admission during target capture, delayed quick-tap delivery, legacy Run ownership/migration, and orderly draft transfer. A guarded update passes draft/original only through the child process environment, clears it before worker creation, and reports only lengths/hashes. It never writes owner text into a receipt, clipboard or command line.

## Licensed corpus evidence reused

Google FLEURS CC BY 4.0, pinned revision `70bb2e84b976b7e960aa89f1c648e09c59f894dd`: first eight distinct transcript IDs in archive order for French and English, no outcome-based selection. Public fixture/provenance and original failed runs remain outside source.

| Engine | French WER / median latency | English WER / median latency |
| --- | --- | --- |
| Small q5_1 CPU | 38/232, 16.38%, 3.2705 s | 14/156, 8.97%, 3.244 s |
| Turbo requested GPU, actual CPU fallback | 27/232, 11.64%, 15.351 s | 9/156, 5.77%, 15.615 s |
| Turbo CUDA, beam/best-of 5 | 23/232, 9.91%, 0.9025 s | 9/156, 5.77%, 0.81 s |

True CUDA logs establish CUDA0 for all 16 fixtures. Worker working set sampled near 367 MiB, peak job commit about 1979 MiB, with roughly 1 GiB device-wide VRAM delta. The initial 1.5 GiB GPU job failed allocation; the finite 3 GiB rerun passed. NVIDIA DLLs are not Authenticode signed; provenance uses published download/member SHA256 and the packaged NVIDIA license. This is not comprehensive vulnerability clearance. One French case still has substantive errors, and strict WER includes number/name formatting differences. No corpus result establishes personal Québec/mixed/noisy dictation.

GPU engine and model profiles remain unchanged by the Sona UI/rename. Reuse `benchmark-fleurs-turbo-cuda-search5-3g.json`, CPU/UTF8-rescore receipts and production French replay/worker/audio cleanup evidence. Do not rerun the corpus solely to increase check counts.

## Required owner proof

Actual installed-version physical Ctrl+Win Hold/Toggle, visible capsule and focus retention; Codex unsent insertion/no duplicate and intact clipboard; English/French/mixed microphone speech; held-modifier Esc; focus/device/sleep behavior; Hush's Sona.exe recognition and real-call mute/restoration; owner visual/usability acceptance. See `tools/owner-test.md`. Technical proof, installation and acceptance stay distinct.
