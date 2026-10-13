# Candidate verification

## 0.3.3 compact controls and saved appearance, October 13

The owner rejected the oversized local-cleanup selectors and reported Amethyst
reverting after updates. The prior ThemeChanged handler applied a palette only
as a preview; native reproduction selected Amethyst while both active and disk
preferences still said charcoal. The update was faithfully loading that saved
value. Theme selection now commits immediately without saving unrelated form
edits. A write failure restores the prior selection and palette.

The three cleanup selectors are exactly 32x176 DIP, centered with 8 DIP vertical
space in each 48 DIP row and a shared right edge. Normal 1000x700 and minimum
740x530 native fixtures each passed 15 affected checks. Both Settings screenshots
were inspected; the values fit without clipping or filling the row. The short
GPU / VRAM label retains an explanatory NVIDIA tooltip.

A narrow review found that general Save mutated active preferences before the
file commit. General Save now commits a cloned candidate first and copies only
its changed form fields into active preferences after success. Startup is
restored if its change preceded a failed file save; restoration failure is
reported. The reviewer closed this finding from the repaired source.

The maintained theme fixture passed 12 native checks: immediate theme save,
unsaved-field preservation, actual relaunch, failed general Save leaving active
and persisted settings intact, a later theme save preserving those values,
failed theme write rollback, and successful retry. No microphone, global input
or Discord connection was used. Earlier fixture failures were harness errors:
WPF InvokePattern queues Click, so the file lock must remain until the handler
reports its result; Windows reports a generic access-denied message. Failed
evidence is retained and is not a product regression pass.

Affected builds were warning/error free. The 256 Core checks pass with matching
0.3.3 App/Agent versions; speech, cleanup runtime, measured timing, MCP and
encrypted-update behavior are unchanged and their prior evidence is reused.
Evidence: `E:/Temp/qazws/sona033-settings-repair-build.log`, `sona033-core.log`,
`sona033-cleanup-ui-final/cleanup-ui-smoke.json`,
`sona033-cleanup-ui-compact/cleanup-ui-smoke.json`, and
`sona033-theme-verified/theme-persistence-smoke.json`.
Owner theme-only restoration is recorded in
`E:/Temp/qazws/sona033-owner-theme-before-update.json` without personal text.
Installation/source identity and actual update readback remain a separate gate;
personal visual acceptance remains the owner's test.

## 0.3.2 private installation and local cleanup, October 13

The first qualified 0.3.2 update at source
40577a32bc4deed59fcfa51fe380ea77e899d4f0 used the maintained encrypted updater
and confirmed the owner's draft transfer. All 29 installed package files,
stable CLI/startup paths, 52 vocabulary terms, five prior corrections, three
watched identities and the existing clipboard/theme/history/model preferences
verified. The owner profile enabled formal cleanup on the actual CUDA backend
with a ten-minute idle timeout and a 604.15 MiB model-weight buffer. Discord
remained connected, enabled, unpaused and owned no voice changes.

Actual installed UI readback found that the memory-status accessible name was
a static description rather than its visible text. The final narrow correction
binds that name to current text and adds a polite live-region hint. A warning-free
affected WPF build and 13 native checks pass, including actual accessible memory
state. The cleanup worker, rules and timing evidence below remain unchanged.
Final installed identity is recorded in `E:/Temp/qazws/sona032-installed-readback.json`;
personal microphone/formatting acceptance remains separate.

The owner requested the small local formatting model and GPU/VRAM preference
with a CPU/RAM alternative. The source adds optional Spokenly Cleanup 1,
default off for new installations, with protected vocabulary/literals,
raw-original retention, Verbatim/snippet bypass, bounded cancellation/fallback,
separate warm residency and Settings/CLI/MCP controls. WAV transcription shares
the same processing stage. The owner configuration will be enabled only after
the qualified update. The installed owner profile is enabled; new users remain off.

The consolidated source gate passed 256 Core and 47 Windows checks with zero
warnings/errors and no reported vulnerable NuGet dependencies. A real-runtime
synthetic fixture passed 48 checks, including confirmed CUDA model offload,
11 short English/French/technical cases, three formatting choices, a 134-word
paragraph, resident reuse, queued/in-flight cancellation, idle expiry, Keep until
quit, disabling cleanup and GPU-to-CPU release. No microphone, owner text or
global input was used. Eleven short GPU requests took 41–98 ms; the longer
paragraph took 373 ms. GPU preparation was 2.27 s with filesystem cache warm,
model-weight VRAM 604.15 MiB, peak host working set 1115.65 MiB. These are
cleanup-only samples, not complete live dictation timing or semantic guarantees.

The actual CLI/MCP smoke passed 17 checks for 18 named tools and 21 supported
settings. Twelve affected native control/version checks passed at each of
1000x700 and 740x530 DIP, including UI-save/CLI readback. Both cleanup screenshots
were visually inspected. The first visual harness relied on an unreliable WPF
IsOffscreen value and captured the top of Settings; the repaired maintained
test uses actual control/window bounds and scrolls the cleanup group into view.
The earlier valid preference checks remain separate from that missing visual
proof. Unchanged theme, waveform, speech and encrypted-update evidence is reused.

Two correctness-review findings were fixed and closed: retry retained setup
partials before setup, provide a retry action and prevent quit while release is
pending; and unload the old worker when selecting an unavailable backend.
Interoperability failures are retained: the pinned runtime needs log verbosity 4
to emit model-buffer proof, and uses modern stop_type=eos/truncated completion
fields rather than the legacy stopped_limit field. Focused parser tests cover
both formats and reject contradictory or truncated output.

Evidence: `E:/Temp/qazws/sona032-build-release.log`,
`sona032-cleanup-final.json`, `sona032-agent/agent-smoke.json`,
`sona032-cleanup-ui-visible/cleanup-ui-smoke.json` and
`sona032-cleanup-ui-compact-visible/cleanup-ui-smoke.json`.
See [Local cleanup](LOCAL-CLEANUP.md) for exact asset pins and model limitations.
The accessibility follow-up is `sona032-accessibility-build.log` and
`sona032-cleanup-ui-accessible/cleanup-ui-smoke.json`. Personal microphone,
formatting and end-to-end delay remain owner acceptance checks.

## Installed 0.3.1 readback, October 13

Installed source c0e1a0d50105209c33f42fdf7bed5b9317ca51cf matched all
27 package files. The maintained encrypted updater confirmed the current draft
transfer, without microphone capture or global test input. The app reports 0.3.1
and its current-user agent remains available. This successful update does not
retroactively recover the unconfirmed original 0.3.0 draft loss described below.

Owner settings read back clipboard backup enabled, combined Hold mode, only the
three ChatGPT/Codex executable/package identities watched, 52 imported project
and technical terms and all five previous corrections retained. No personal
vocabulary is committed into generic application defaults or showcase images.
Discord reports connected, enabled, unpaused, owns no changes, and Ready.

The manual GitHub check completed with "No public release is available yet."
Automatic checks were then enabled for this owner; new installations remain
opt-in. There is no published release, signed binary or automatic installation.
Owner physical hold/double-tap, focus-changed clipboard and Discord restoration
remain pending, separate from native fixture proof.

## 0.3.1 qualification, October 13

The combined physical Ctrl+Win gesture supports hold/release and two quick taps
for hands-free followed by one tap to finish. Injected events are rejected;
interrupted/expired taps, delayed admission and listener cleanup have focused
regressions. Double-tap while the insertion test is prepared cannot open the
microphone. The consolidated gate passed 231 core and 28 Windows checks with
zero compiler warnings/errors and no reported vulnerable NuGet dependencies.
No real microphone or global synthetic input was used by these checks.

Native appearance proof passed ten affected display/version/model cases. Model
badges and both measured-waveform previews were inspected visually. Three native
showcase screenshots use a separate profile with synthetic draft text, no owner
vocabulary, microphone capture, credentials or call state. Eight unchanged theme
checks from 0.3.0 remain applicable. Screenshots are illustrations, not speed or
accuracy benchmarks.

Clipboard backup is explicit, default-off and exposed through UI/CLI/MCP.
The owner requested it enabled. The default watched list now contains ChatGPT
and Codex identities only, preserving direct protection for Sona itself.
The local whisper-server hint list is bounded to sixty unique entries; vocabulary
storage remains bounded to two hundred. Other call platforms are not supported.

GitHub release checks use an optional startup/12-hour poll of public metadata,
a total ten-second deadline, a 64 KiB bound, strict stable-version and release URL
validation, no credentials or redirects, and no automatic installation. Eighteen
offline parser checks cover malformed, foreign and prerelease metadata. Until
the repository has a public release, a 404 is reported as unavailable, not current.
No account database or hosting is required. Owner live hold/double-tap, changed
focus clipboard paste and real Discord restoration remain physical acceptance.

The GitHub repository was renamed to JhinxDev/Sona and About metadata populated.
It remains private. MIT applies to original source, with separate third-party
notices. Private source/package verification is not a public distribution claim.


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
