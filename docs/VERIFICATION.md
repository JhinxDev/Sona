# Candidate verification

## 0.5.3 truthful model-loading information, November 8

The 0.5.3 candidate replaces the generic loading copy with values from the real
loader state and a real stopwatch: the selected model's catalog name, the actual
stage (file verification then worker memory load) and measured elapsed seconds,
for example "Loading Whisper Large v3 Turbo · 2.8 s · Esc cancels". Warm runs show
no preparation text. The loaders now expose their measured cold-load duration and
the model pages show it with a Keep until quit note; agent status exposes
`speechLastLoadSeconds` and `cleanupLastLoadSeconds`.

Verification:

- Warning-free App build.
- Model-loading fixture: **40 checks, 0 failures**, including exact copy for both
  purposes, measured formatting, no hint on warm runs, state-gated
  verification/loading, recording-path hint and measured completion note.
- Windows default suite and the consolidated `tools/build.ps1` gate (now including
  the model-loading fixture) passed end to end with no failures and no known
  vulnerable packages.
- The maintained ResidentModel checks continue to cover that Keep until quit
  (`ModelIdleMinutes = 0`) retains a model after long inactivity. No unattended
  microphone or global input was used.

### Live activation, November 8

Guarded `tools/upgrade.ps1 -PreserveSessionHistory` installed the exact package
`artifacts/0.5.3-582164c2` (source head
`582164c28afb69bf0e2383f7450dc62dc0bd2ff0`, 36 files) and launched it. Live
readback at 02:1x Toronto:

- Running `C:\Users\qazws\AppData\Local\Programs\Sona\0.5.3-582164c2\Sona.exe`,
  PID `83732`, file version 0.5.3.0, product version
  `0.5.3+582164c28afb69bf0e2383f7450dc62dc0bd2ff0`; the agent reports 0.5.3 and
  all 36 installed hashes match. The draft was preserved and 14 session-only
  History records were transferred.
- Both residency settings survived as **Keep until quit**
  (`ModelIdleMinutes = 0`, `CleanupIdleMinutes = 0`). Startup warming loaded both
  workers and the app kept them resident: **Sona Speech Engine** PID 32960
  (Whisper Large v3 Turbo, measured cold load **1.5 s**) and **Sona Text Polish**
  PID 86304 (Spokenly Cleanup 1, measured cold load **2.4 s**). The agent status
  reported those measured values through `speechLastLoadSeconds` and
  `cleanupLastLoadSeconds`, which are the same real stopwatch values the model
  pages display.
- The loading copy itself is covered by the 40-check model-loading fixture: exact
  model name, actual stage, measured seconds, no hint on warm runs, and the
  recording-path hint. No microphone or global input was used by the automated
  checks. Physical microphone acceptance remains an owner check.

### Windows installer rehearsal, November 8

A per-user installer was added from the same accepted 0.5.3 source and rehearsed
end to end without touching the owner installation:

- Maintained tooling: `installer/Sona.iss`,
  `tools/installer/build-installer.ps1`,
  `tools/installer/rehearse-installer.ps1`; guide at
  [docs/INSTALL.md](INSTALL.md).
- Payload: self-contained win-x64 publish of Sona and `sona-agent.exe` (no
  separate .NET runtime), pinned Whisper CPU engine from whisper.cpp `b5454`
  verified against every hash in `tools/runtime-manifest.json`, notices and
  licenses. Model weights are deliberately downloaded in-app.
- Artifact: `Sona-Setup-0.5.3.exe`, 52.6 MB (55,188,916 bytes), SHA256
  `c0997819f9ced1dd1b01b73a1e95a96cdc7b8b1b459178bbab11d575137d466f`, built from
  source head `c7d508e2c4e1944a3ee3872c59818e593c4693b5`; unsigned preview, so
  SmartScreen warns by design.
- Rehearsal: isolated silent install into a temporary folder, then **23 checks,
  0 failures**: installed app and CLI files, subscription notices and licenses,
  `installation.json` with the exact `Path` and lowercase `SHA256` the startup
  ownership check expects, installed engine hashes matching the runtime manifest,
  Start Menu shortcut, self-contained app start, CLI start without a separate
  .NET runtime, normal window close, and silent uninstall removing the app,
  shortcut, uninstall registration and installer-owned engine while preserving
  profile data outside the app folder.
- The rehearsal used an isolated app id, install folder and `SONA_DATA` profile;
  it did not start the microphone, send input or alter the owner installation or
  Discord state. The owner then explicitly authorized publishing the binary
  release.

### Published release, November 8

- Public release: <https://github.com/JhinxDev/Sona/releases/tag/v0.5.3>, marked
  **Pre-release** because it is unsigned.
- Annotated tag `v0.5.3` peels to `c7d508e2c4e1944a3ee3872c59818e593c4693b5`, the
  exact app source used to build the installer; the tag itself is public.
- Assets: `Sona-Setup-0.5.3.exe` (55,188,916 bytes, API digest
  `sha256:c0997819f9ced1dd1b01b73a1e95a96cdc7b8b1b459178bbab11d575137d466f`) and
  `SHA256SUMS.txt` (88 bytes).
- Unauthenticated verification after publication: the release page returns HTTP
  200, a fresh download through the public asset URL hashes to the published
  digest, and `SHA256SUMS.txt` contains the same value. The release body, install
  steps, checksum, limitations and macOS note render on the public page. Because
  the only release is a pre-release, `/releases/latest` correctly has no stable
  release; the Releases page and direct tag URL work.
- Code signing remains open: no certificate is attached, so SmartScreen warnings
  are expected by design.

## 0.5.2 recognizable worker names, November 7

Sona now launches clearly named copies of its upstream workers so Task Manager and
Process Explorer show **Sona Speech Engine** (`whisper-server.exe`),
**Sona Transcription** (`whisper-cli.exe`) and **Sona Text Polish**
(`llama-server.exe`). The copies are byte-identical to the verified upstream
binaries. Speech copies stay in their own runtime directory; the cleanup copy is
stored under the current-user data directory because the cleanup runtime verifier
requires its directory file set to match the pinned manifest exactly. The cleanup
launch keeps the runtime directory as its working directory so the adjacent
ggml/CUDA DLLs remain on the loader search path. Copies refresh when an engine
changes, and a read-only or missing runtime falls back to the original binary.

Verification: warning-free App build; Windows default suite **60 checks** (was 55)
including five new branding checks (copy name and bytes, reuse without duplicate,
refresh after an engine change, cleanup copy outside the verified directory,
unknown names unchanged). The consolidated gate passed end to end with the full
fixture set and no known vulnerable packages.

### Live activation, November 7

Guarded `tools/upgrade.ps1 -PreserveSessionHistory` installed the exact package
`artifacts/0.5.2-8d4ba984` (source head
`8d4ba984d1fff0fbc169b7a3a97e982f8e3c82e6`, 36 files) and launched it. Live
readback at 20:5x Toronto:

- Running `C:\Users\qazws\AppData\Local\Programs\Sona\0.5.2-8d4ba984\Sona.exe`,
  PID `29732`, file version 0.5.2.0, product version
  `0.5.2+8d4ba984d1fff0fbc169b7a3a97e982f8e3c82e6`; the agent reports 0.5.2 and
  all 36 installed hashes match. The draft was preserved and 6 session-only
  History records were transferred.
- Startup warming produced **Sona Speech Engine** (PID 37544,
  `whisper-cuda\Release\Sona Speech Engine.exe`, 253 MB) and **Sona Text Polish**
  (PID 77340, `%LOCALAPPDATA%\Murmur\workers\Sona Text Polish.exe`, 1032 MB), both
  children of Sona. No `whisper-server.exe` or `llama-server.exe` process
  remained. The agent reports the same two worker PIDs, and the upstream originals
  remain present and unmodified for their integrity checks.
- No microphone or global input was used by the automated checks. Physical
  microphone and real-call acceptance remain owner checks.

## 0.5.1 Discord deafened support, November 7

The 0.5.1 source candidate fixes the reported failure where Ctrl + Windows showed
the recording pill and immediately cancelled with "Discord did not acknowledge
microphone protection." Direct local Discord RPC inspection established the
environment: with the user deafened in a Discord voice channel, `SET_VOICE_SETTINGS`
`mute=true` returned the unchanged `mute=false` state twice, while the same
command succeeded on a clean connection. Discord explicitly couples deafen and
mute, and a mute restore can clear the user's deafen choice. Sona now treats a
deafened Discord as already having the microphone off: it records without sending
voice commands and never un-mutes while the user's own deafen is active.

Verification for the frozen candidate:

- Release builds of Sona.App and Sona.Agent: zero warnings, zero errors.
- Sona.Tests: **421 checks passed** (was 414). New Core cases cover the deafened
  acquisition path without voice commands, preserving the user's deafen on
  release, stale-ack confirmation and single retry, persistent-refusal guidance,
  and refusing an un-mute that would clear the user's deafen.
- Sona.WindowsTests default suite: **55 checks passed** (was 53). New fake Discord
  transport cases prove a deafened call protects the microphone without sending
  mute or deafen commands and leaves the deafen choice untouched.
- Consolidated `tools/build.ps1` gate passed end to end with the maintained
  fixture set and no known vulnerable packages.
- No real microphone, owner profile or global input was used by the automated
  checks. Discord voice state was read over the application's own authorized local
  RPC for diagnosis only; the user's deafen state was restored and confirmed.

Physical microphone and real-call acceptance remain owner checks.

### Live activation, November 7

The guarded `tools/upgrade.ps1 -PreserveSessionHistory` path installed the exact
package `artifacts/0.5.1-4275cc58` (source head
`4275cc58f785efbbc1c8b222ca9879ebe45ba5e0`, 36 files) and launched it through the
Explorer-owned desktop bootstrap. Live readback:

- Running `C:\Users\qazws\AppData\Local\Programs\Sona\0.5.1-4275cc58\Sona.exe`,
  PID `67104`, file version 0.5.1.0, product version
  `0.5.1+4275cc58f785efbbc1c8b222ca9879ebe45ba5e0`; the agent reports 0.5.1.
- All 36 installed files match the package receipt; the draft was preserved by the
  upgraded encrypted checkpoint.
- The owner's Discord deafen was confirmed still `deaf=True`; Sona did not change
  it. While the owner tested dictation, the live app reached `recording` with
  protection status **"Protecting recording (Discord deafened)"**, which is the
  repaired path working on the real target. Protection returned to `Ready` with
  no owned voice changes after the stop.
- Preference values were preserved (OLED, Hold, Auto cleanup, Enter-to-send,
  clipboard recovery, watched apps, vocabulary and snippets). The file's byte
  hash changed only because the Disconnect/Connect recovery cycle saved the new
  default fields; logical settings matched.
- That first post-upgrade attempt ended with "The recording was silent. Check your
  microphone selection." and the draft stayed in Sona because focus changed during
  the attempt. That is a separate input/focus condition, not the protection
  failure. Physical microphone and real-call acceptance remain owner checks.

## 0.5.0 professional polish and reliability, November 6

The consolidated local gate in `tools/build.ps1` passed end to end on the release
candidate:

- Release builds of Sona.App and Sona.Agent: zero warnings, zero errors.
- Sona.Tests: **414 checks passed**.
- Sona.WindowsTests default suite: **53 checks passed**, including the new
  Esc-guard listener-thread lifecycle and stalled-stop audio-retention checks.
- Isolation fixtures: capture policy and capture status (empty failure lists),
  capture readiness 59, retained notice 92, both History retention modes,
  protection switches 19, responsiveness 44, Enter delivery 49, completed paste
  25, session readiness 18, owned startup 22, all with empty failure lists.
- New professional polish fixture: **17 checks passed** on the actual MainWindow
  from a fresh isolated profile (welcome card and copy against real model state,
  navigation, live word count, dismissal persistence, per-app rule add/save,
  unsaved bar, captured-identity session resolution and reset).
- New setup guidance fixture: **4 checks passed** with a loaded profile and an
  empty `SONA_RUNTIME` (no welcome card, visible setup callout with the exact
  runtime explanation, working Dictation Models action).
- NuGet dependency audit: no known vulnerable packages for the current App
  dependency set.

New affected coverage: Core checks cover per-app identity resolution, first-match
ordering, executable and package identities, invalid rule normalization,
persistence, onboarding state, word count, atomic replacement and rejected atomic
replacement. The Windows suite covers the Esc hook owning its own armed listener
thread, disarm without a suppressing hook, disposal, and a microphone stop that
stalls past its bounded wait while preserving the audio captured before the stall.

No real microphone, owner profile, clipboard, target application, Discord session
or global input was used. The 0.5.0 README captures are offscreen WPF renders from
the professional polish fixture.

### Live activation, November 6

The guarded `tools/upgrade.ps1` path installed the exact package
`artifacts/0.5.0-c400431d` (source head
`c400431deb1e56b628bf910d1c5c065be536ad7f`, 36 package files, version 0.5.0) and
launched it through the Explorer-owned desktop bootstrap. Verified live readback
at 02:28 Toronto:

- Running `C:\Users\qazws\AppData\Local\Programs\Sona\0.5.0-c400431d\Sona.exe`,
  PID `13932`, file version 0.5.0.0, product version
  `0.5.0+c400431deb1e56b628bf910d1c5c065be536ad7f`.
- The current-user agent reports version **0.5.0** and build `e15a6117a366`, so
  the app and agent now agree (the 0.4.11 agent still reported 0.4.2).
- All 36 installed files match the package receipt hashes; `installation.json`,
  the `current` junction, the Start Menu target and the owned Windows Run entry
  all point at the new version directory.
- The encrypted draft checkpoint restored the owner's draft
  (`DraftPreserved: true`), 13 session-only History records were transferred and
  loaded, and `preferences.json` is byte-identical to its pre-upgrade SHA-256
  `1fe927f54ae90fbd54e42f65407bb575031cf7d2020cecd689b6482c1b9c27bf`.
  OLED theme, Hold shortcut, Auto cleanup, Enter-to-send and clipboard recovery
  remain; history storage and incoming silencing remain off.
- Discord protection is enabled, connected, unpaused and owns no voice changes
  before and after activation. No microphone or global input was used by the
  upgrade, and the app is idle.

Physical key, microphone, target-composer, real-call and screen-reader acceptance
remain owner checks and are not claimed here.

## 0.4.0 Parakeet and English-first model choice, October 13

Core regression gate: 277 checks. The maintained real Windows speech fixture
passed 19 checks for verified CPU/CUDA provisioning, cancellation, tampered-file
preservation, locked-partial retry, residency/expiry/Keep until quit, CPU fallback,
long-recording completion, queued/active audio clearing and confirmed unload.
The separate paired-memory fixture also proved actual Whisper-to-Parakeet worker
replacement and confirmed owned process exit. No microphone, owner text or global
input was used. Read-only reviews closed the HTTP exception handling issue and
found no remaining blocker in native scheduler packaging or bounded audio splitting.

On the same eight distinct English FLEURS clips, Parakeet v3 had 8 word errors
out of 156 (5.13%) and 20.5 ms median warm latency; Whisper Turbo had 9 errors
(5.77%) and 144.3 ms. Each English clip had three timing requests; accuracy uses
one pass per distinct clip. Punctuation is ignored; numerical spelling differences
count. These speech-only measurements establish neither complete dictation latency
nor personal-accent accuracy. Paired device-wide loading deltas were 974 versus
1000 MiB. Peak host commit during clips was 2279 versus 1904 MiB; working sets
were 340 versus 377 MiB. Commit, working set, VRAM and download size are distinct.

Parakeet additionally completed eight French clips at 5.60% WER / 22.0 ms.
Whisper's existing b5454 CUDA server failed a mixed-language resident sequence
with native illegal-memory-access below its memory ceiling. Graph-disable failed
to repair that route; it stopped, with failed evidence retained. Its completed
comparison is English-only. No unverified production Whisper workaround was added.

The stock NeMo runtime failed an eager 1.32 GB scheduler metadata allocation.
The earlier zero-byte allocation theory was disproved. The reviewed, reproducible
ggml metadata repair retains full graph capacity, conservative copy bounds and
691 original exports while allocating from actual graph size. Startup and speech
now stay within the unchanged 3 GiB host worker limit. A single 80.88-second native
request omitted its final sentence; the exact native cause remains unestablished.
Sona's quiet-boundary segments cover every PCM sample once, use one overall
deadline and restore that final sentence (173 ms on the fixture). Eight maintained
Core cases guard sample coverage, final remainder, formats and bounds.

See PARAKEET.md and tools/parakeet for exact model/archive/binary/source/license
pins and regeneration instructions. General Windows regressions passed 47 checks;
live CLI/MCP passed 19, retaining all 18 tools. Native model-picker checks passed
17 each at 1000x700 and 740x530 DIP; both screenshots were visually inspected.
Real Use model actions switched both ways and saved Amethyst. The model footer
now describes per-model licenses and current measurement units accurately.
The first GUI harness tried an AutomationId for the title-bar close control,
then observed WPF's peer-replacement Invoke error after an action had committed.
The maintained harness closes its exact process window and requires persisted
selection plus the newly enabled action before accepting that provider error.
Compact scrolling initially passed the last model to reach the footer; direct
row alignment repaired that harness assumption. These were not layout regressions.
Installed private-package readback remains a separate exact-target receipt.
Existing 0.3.3 appearance persistence, compact cleanup layout and encrypted-update
recovery evidence are reused; no change was made to those implementations.

Evidence: `E:/Temp/qazws/sona040-core-final.log`, `sona040-speech-final.json`,
`sona040-speech-segment-qualified.log`, `sona040-gpu-paired-memory.json`,
`sona040-long-tail-failure.json`, `sona040-whisper-all-clips-probe.json`,
`sona040-whisper-graphs-disabled-probe.json`, `sona-parakeet-probe-scheduler.json`.
The initial benchmark's negative Whisper VRAM deltas had a contaminated baseline
and are not valid model measurements; the paired-memory fixture replaces them.
Additional evidence: `sona040-windows-final.log`, `sona040-agent-final/agent-smoke.json`,
`sona040-models-normal-qualified/models-ui-smoke.json`,
`sona040-models-compact-verified/models-ui-smoke.json` and their screenshots.

Installed 0.4.0 source/package identity is
`aaeb9f2196301b46c514c5e083dcf34c23969080`, with 35 package and 36 CUDA runtime
members independently matched. The encrypted updater confirmed the existing draft.
Saved Model is now parakeet-v3 and a real cuda:0 resident worker became ready.
Every other preference has the same before/after logical checksum
`110E2F925168CDE87B21A28A26EAFB60F069DB6EFB319B8DF1C9815A2C815C86`.
Amethyst, clipboard backup, vocabulary, watched app identities, 10-minute idle
policy, tray/startup behavior and GPU cleanup remain. Actual native About/sidebar
are 0.4.0; compact cleanup controls are still 32x176 DIP. Discord is connected,
unpaused, Ready and owns no changes. Screenshot inspection confirmed the loaded
Parakeet row and preserved styling. Eight retired build/diagnostic copies reclaimed
314673144 bytes; accepted source/build, originals, receipts, all model choices and
0.3.3 recovery remain. No test process remains; idle production workers may unload
normally. Later documentation-only Git convergence preserves the installed `src`
tree and does not require a second app update.

Exact target receipts: `E:/Temp/qazws/sona040-installed-readback.json`,
`sona040-upgrade.log`, `sona040-retired-build-cleanup.json`,
`sona040-installed-models.png`, `sona040-installed-settings.png`.

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


## Sona source identity, October 14, 2026

Source folders, projects, namespaces, test harnesses, app manifest, icon assets,
project references and current build commands now use Sona. App and agent
Release builds pass with zero warnings and errors. Core checks: 289 passed.
Windows fixture checks: 49 passed without microphone capture or text injection.
The isolated presentation fixture has no failures; 31 active-target checks and
PowerShell tool parsing pass. NuGet reports no known vulnerable packages for
the current app dependency set; package versions and original asset bytes are unchanged.

New private profiles use Sona. Existing legacy profiles are reused in place,
including their Windows-encrypted payloads; discovery does not copy, move or
overwrite them. SONA_DATA and SONA_RUNTIME are the current override names.
Earlier aliases, owned legacy startup/install cleanup, scratch recovery markers
and the production single-instance/open signal are retained only for compatibility.
Old and current test profile aliases address the same scoped Sona agent pipe.
These compatibility checks use isolated fixtures; the installed app and the
owner's private profile were not changed by this source cleanup.

The README links the owner-approved Sona trailer and uses its approved poster
with a consistent native Amethyst screenshot set. Existing hardware and
owner-target acceptance limits above still apply.
