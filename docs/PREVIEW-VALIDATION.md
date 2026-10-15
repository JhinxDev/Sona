# Preview validation

## October 15 daily-use candidate, 0.4.1

The affected pass has 311 core checks, 8 focused insertion checks, 27 isolated
native live-draft pipeline checks, 12 native theme save/relaunch checks, six
profile-recovery checks, 19 agent-interface checks, 31 active-target checks and a
four-check actual Parakeet audio-snapshot replay. The native pipeline uses fake
capture and recognition, with delayed cleanup and cancellation. The real engine
replay uses an existing licensed WAV and fake capture; it returns provisional
text before stopping, then final text, clears audio copies and unloads its worker.
These are separate forms of evidence, not physical microphone or Discord proof.

Two initial native live-fixture runs reached their functional assertions but
timed out because the custom test application's Dispatcher.Run loop was not
explicitly stopped after Application.Exit. The repaired fixture exits cleanly.
No production process was closed, input injected, clipboard changed or microphone
started. The first focus regression failed before the filter repair and passed
afterward. The owner's exact earlier Charcoal reset remains unproven; failed
settings-load overwrite is a repaired independently reproduced failure mode.

[Daily-use behavior and remaining acceptance](DAILY-USE.md).


Sona 0.4.0 is an early source preview. The implementation was independently
reviewed on October 13, 2026. Publishing the source does not turn fixture results
into personal microphone or target-app acceptance.

## What the evidence covers

- 277 core checks and 47 Windows integration checks passed in the retained
  qualification receipts. The Windows set includes fake Discord behavior; it
  does not open a physical microphone or inject the global shortcut.
- 19 native speech checks cover verified CPU/CUDA assets, model lifecycle and a
  restored 80.88-second synthetic long-tail sentinel. Exact chunk coverage is
  separate from recognition quality across natural speech boundaries.
- 19 agent-interface checks cover the JSON CLI and its 18 MCP tools. Normal and
  compact model UI checks passed; prior theme/cleanup checks were reused where
  their implementation stayed unchanged.
- Retained initial trailer/screenshots used the original 0.4.0 application assembly, a
  separate demo profile and synthetic text. The supplied generated narration was
  transcribed through the real local-model path. No live microphone, clipboard
  insertion or Discord call was recorded. The demo's disconnected Discord state
  is expected when Discord is closed.

The shared glass style and recording display changes were checked separately on
October 14. All eight themes use palette-derived materials. The former
amethyst-glass setting resolves to Amethyst, preserving existing preferences.
280 core checks passed. The current Release candidate passed 31 focused active-app,
destination and installed-icon checks, plus 76 isolated material/contrast checks.
A separate startup check loads the actual App.xaml resources while intercepting
production startup, so resource tests cannot accidentally open the app or its
services. This pass did not rerun the entire earlier Windows suite.

Native checks cover all eight palettes, the real appearance selector, guarded Mica,
opaque fallback and both recording display sizes. A later 22-check capture pass
verifies the actual installed Codex icon and its dark plate on light themes,
normal Sona title, long labels and retained-draft warnings. Captures use separate
synthetic profiles. Foreground, owner preferences and the installed app are
preserved. The source keeps Charcoal as the default palette; glass is now the
shared material rather than a separate optional theme. The source title remains
Sona in both normal and microphone-disabled previews.

The current host is Windows 11 x64. Other Windows versions and an actual
high-contrast policy change were not physically tested. Source guards provide
opaque/system-color fallbacks; this does not claim every Windows configuration
passed. macOS and Linux desktop builds are not implemented.

Run `tools/theme-material-smoke.ps1` with explicit candidate, output and SDK paths
for a bounded native material check. It requires the existing `winapp` capture CLI
and uses a microphone-disabled test profile.

The public README screenshots show real native controls and actual Parakeet /
cleanup residency. They are demonstrations, not an accuracy or latency promise.

## October 14 presentation qualification

The final October 14 candidate passed a Release build with zero warnings/errors,
31 isolated presentation/settings checks and 76 material/contrast checks. Earlier
280 core and 31 active-target/icon checks retain their original provenance because
their covered behavior did not change. New presentation checks cover neutral
disabled actions and readable labels across all eight palettes. Unknown or
malformed fixture arguments now stop before the broader Windows suite; an earlier
mistyped invocation and worker-start timeout are retained as failed harness
evidence, not a full-suite pass.

Fresh native captures cover all eight palettes, fixed headings, contextual actions,
compact unsaved settings, History Open draft, actual Parakeet/cleanup residency,
and both popup sizes with retained-target warnings. They use isolated synthetic
profiles and the software renderer with the native Mica path retained. The default
hardware helper produced a blank WGC main-client capture despite valid WPF
content; software rendering repaired that capture. Hardware presentation on the
unlocked owner desktop remains unverified. Foreground APIs returned zero in this
capture session, so this later pass claims no foreground-preservation acceptance.
Owner preferences and installed app bytes were checked unchanged and all owned
helper/worker processes exited. The normal title is Sona without a test suffix.

The private Google design review scored the final application 9.1/10 across
29 screenshot contexts, including reused unchanged page captures. This is a
design critique, separate from functional, accessibility and owner acceptance.
Public screenshots use the same dark Amethyst palette, size and normal title.

## Open checks and known limits

1. Personal microphone, accent, focus/clipboard handling and insertion into the
   intended text field still need an owner-started test of the new Parakeet path.
   Follow [the owner test](../tools/owner-test.md); leave test messages unsubmitted.
2. The pinned Whisper b5454 CUDA engine crashed in a mixed English/French
   resident sequence. Disabling CUDA graphs did not resolve that recorded case.
   This is a known runtime issue, not a claim that every Whisper request fails.
3. Automatic model integrity hashing happens before the worker's 60-second
   startup deadline. A bounded verification deadline and a focused regression
   are appropriate follow-up work; a slow-disk stall was not observed here.
4. The cancellation fixture uses an already-cancelled token. Actual queued
   cancellation and forced readiness/request timeout expiry need stronger native
   receipts. Source deadlines already exist.
5. Natural continuous speech across the independent long-recording chunks needs
   a licensed 60–120-second fixture with per-boundary omissions/duplications
   before changing chunking or selecting models from the small benchmark alone.

See [verification](VERIFICATION.md), [Parakeet](PARAKEET.md) and
[cleanup](LOCAL-CLEANUP.md) for retained methodology and runtime provenance.
There is no published binary release yet. Contributions and feature suggestions
are welcome; the main README links to the current build instructions.


## October 15, 0.4.2 qualification

The 0.4.2 wave adds explicit saved-draft review, approved correction learning,
whole-draft paste, opt-in physical Enter-to-send, compact model families,
sectioned Settings, persistent Discord connection controls and calendar History.
Production microphone, actual chat submission and live-call acceptance are
separate owner-started checks; no automated test sends a real message.

Affected checks passed: 336 core checks, 14 completed-paste checks, 23 native
personalization checks plus ten checks in a fresh native process, and 22 CLI/MCP
checks. Native display evidence covers 17 icon checks, 17 active-app state checks,
33 overlay/switch checks and four shared button-content checks. Discord passed
ten fake transport/service checks and 23 native Settings checks. History's
calendar checks passed 17 cases and its final retention-off native fixture passed
22 cases; see [Personalization](PERSONALIZATION.md). Sound feedback passed 22
silent PCM/concurrency checks, and native integration verified capture waits for
the start cue and the off setting survives relaunch. No automated test played
audible audio; listening quality remains an owner check. The guarded upgrade
rehearsal passed 14 checks without touching the installed production profile.
The previous 27 live-draft checks passed earlier in this wave and retain their
provenance; unchanged speech-engine qualification was not replayed.

Native screenshots were visually inspected for section navigation, description
spacing, round switches, compact Whisper/Parakeet cards, connected Discord and
bounded recording tails. These use isolated synthetic profiles and fake input
boundaries. Actual installed Codex, Discord and Edge icon metadata was separately
read without foreground changes. No claim of browser-tab favicon extraction is made.

Review fixes include preventing an existing matching composer substring from
authorizing a partial paste, preserving a copy made before the clipboard write,
explicit uncertainty after partial Enter input, preventing deleted saved drafts
from reappearing through a stale recent-history copy, and avoiding automatic
History selection during refresh. Readback ambiguity withholds submission.

`tools/package-smoke.ps1` verifies every packaged file against its receipt,
launches the exact package with an isolated profile, checks native version/build
and saved preferences across relaunch, then quits through About. The guarded
upgrader retains an encrypted draft checkpoint, refuses busy dictation/owned
voice changes, and keeps the prior installed version. Installation and source
convergence receipts belong to the private candidate checkpoint.

See [Discord setup](DISCORD-SETUP.md), [recording display](RECORDING-DISPLAY.md),
[cleanup evaluation](CLEANUP-MODEL-EVALUATION.md) and [owner checks](../tools/owner-test.md).
