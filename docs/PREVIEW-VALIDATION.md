# Preview validation

## October 16 History controls, 0.4.5

The owner's disabled Clear recent button was observed on the actual idle0.4.4
app. The regression reproduced the stale busy-state UI before repair.0.4.5 adds
the idle action refresh, individual recent-row deletion and persistent history
pause/resume. The independent review's pending-completion privacy leak and
unrelated-draft edit-loss findings were reproduced, repaired and cleared in a
bounded follow-up. No remaining scoped finding.

The maintained unshown native History fixture passes23checks with automatic disk
retention on and23off. Core350/defaultWindows49/capture-policy11/status10 also
pass, builds have0warnings/errors, and the dependency audit has no known vulnerable
packages. Existing calendar/filter, retention-off layout and pending-settings
checks passed. These use synthetic profiles without microphone/global input;
they do not establish physical speech or Homepage suggestion insertion acceptance.
[Full History behavior and tests](HISTORY-CONTROLS.md).

## October 16 live delivery and recovery, 0.4.4

Live readback at 16:26 Toronto verified the installed and running Sona `0.4.4`, build
`0257d73b4eed`, PID `42712`, with exact executable
`%LOCALAPPDATA%/Programs/Sona/0.4.4-468a17fa/Sona.exe`. All 36
installed files matched their receipt hashes. Restored draft and original hashes
matched the encrypted checkpoint; the preferences file remained unchanged.

The first qualified upgrader had quit old PID `52924` normally, but its desktop
launch request remained unclaimed. Windows `GetFinalPathNameByHandle` resolved
the requested `%LOCALAPPDATA%/Sona/recovery` directory into the
Codex MSIX package's `LocalCache/Local/Sona/recovery`. Explorer saw the literal
requested path as `DirectoryNotFound`. The observed cause was path virtualization,
not an ACL or DPAPI policy failure. A fresh physical-path request through the
same canonical Explorer desktop bootstrap restored the live app with draft and
original intact. The original failed launch evidence is retained.

A separate maintained `tools/launch-path-checks.ps1` regression recorded two
baseline failures for Explorer visibility/readback of the logical virtual path,
then eight passing checks with the production physical-file resolver. It used no
app, profile, microphone or input. The same eight checks also passed under Windows PowerShell 5.1, including
the actual handle-based resolver and independent Explorer byte match.
A narrow independent review found no actionable issue; DPAPI, atomic claim,
expiry and ambiguous-launch guards remain intact. The launcher/tooling repair changes no app binary; the verified live
0.4.4 runtime remains current without rebuilding it.
The owner now requires qualified versions and functional updates to reach the
live app as part of delivery, with routine guarded upgrades already authorized.
See [the delivery contract](IMPLEMENTATION.md#live-upgrade-delivery-rule).
This recovery used no microphone, global input or submission. It adds no
physical-key acceptance claim: automatic insertion in Homepage Search while
suggestions are active remains unresolved.

## October 16 daily-use candidate, 0.4.3

The consolidated Release build passed with zero warnings/errors. The current
dependency audit reported no known vulnerable packages. Verification includes:

- 350 Core checks, including malformed and semantically invalid persisted-history
  preservation, valid updates and temporary-file cleanup.
- 49 default Windows lifecycle/transport checks; 24 focused insertion checks,
  14 completed-paste checks and 17 active-app state checks.
- 35 model preflight failure/deadline/cancellation checks with no worker started.
- Five native Quit checks, including real UIA containment between two windows
  in one process and actual Close after faulted model/cleanup preparation.
- 88 native pending-settings checks, including edits during actual protection
  configuration, exact persisted-byte preservation, protection rollback and a
  later clean patch. The independent review's async-conflict finding was
  reproduced before repair and passed afterward.
- 27 fake-capture/recognition native pipeline checks, 22 draft-personalization
  checks plus nine reopen checks, 22 retention-off checks, and 18 layout checks.
- 22 actual local CLI/MCP checks against an isolated app/profile.

The first consolidated live-draft fixture timed out waiting for cleanup to start.
Its original receipt is retained. The fixture now also observes an unexpectedly
completed stop task and reports the actual terminal state instead of an opaque
wait. A bounded diagnostic rerun passed all 27 checks. The initial cause was not
established; this is not evidence of a repaired production regression.

These native fixtures use synthetic text, fake audio/recognition or fake external
boundaries and isolated profiles. They do not open a microphone, inject global
input, change the production profile or qualify a real Discord call. The hosted
provider policy has independent safety coverage; Homepage Search still needs an
owner-started check on the exact candidate. [Audit](DAILY-USE-AUDIT.md).

Maintained affected commands after `tools/build.ps1`:

```powershell
$tests = '.\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe'
& $tests --insertion-fixture
& $tests --completed-paste-fixture
& $tests --active-app-state-fixture
& $tests --speech-preflight-checks
& $tests --quit-preparation-fixture <fresh-directory>
& $tests --agent-settings-conflict-fixture <fresh-directory>
& $tests --live-draft-fixture <fresh-directory>
& $tests --personalization-fixture <fresh-directory>
& $tests --personalization-reopen <same-personalization-directory>
& $tests --history-retention-off-fixture <fresh-directory>
& $tests --layout-polish-fixture <fresh-directory>
.\tools\agent-smoke.ps1 -OutputDirectory <fresh-directory>
```

Earlier qualified speech benchmarks, media and launcher/upgrade evidence below
remain tied to their original source candidates. The engines, dependencies,
inference flags and desktop launcher did not change in this pass. No new live
latency, WER, hardware, call or visual-acceptance claim is made.

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

## Final 0.4.2 owner check and documentation capture

On October 15, the owner confirmed real hands-free Enter-to-send in Codex on
installed build `aa8b429e480b`, source `05f838c87641507d7eb7f81e755559de7bd3627f`.
The final capsule removes Open draft and the provisional stage label, while
retaining its enlarged waveform and bounded recent text. The affected final
recording-display fixture passed 35 checks. This supplements the earlier
qualification above; other composers, Parakeet-specific microphone coverage and
live Discord calls are not implied by this one successful send.

The README now uses current native WPF client captures from isolated profiles.
See [image provenance](images/README.md). No live microphone, private transcript,
foreground change or real message submission was used to produce those images.


## Recording refinement from trailer review

The affected native display fixture passes 51 checks, including distinct speech
amplitudes, silence, explicit recording state, Stop invocation, bounded inset text,
and no empty completion inset. A new maintained real-audio fixture passes 11
checks through Recorder, the real MainWindow live polling and Whisper Turbo,
using the accepted prerecorded phrase “Let's review the design tomorrow.”
It records actual provisional/final text and timestamps, level/frame histories,
input/module hashes and confirmed worker exit. No live microphone, audible
playback, global input, clipboard change or production profile is involved.

The earlier staged waveform screenshots were layout evidence. They do not prove
speech responsiveness. The new real-audio receipt supplies that separate proof;
media may replay its measured events through the production animation step for
smooth high-resolution capture, with that replay clearly identified.

## Settings and Models layout follow-up

The native layout fixture (`--layout-polish-fixture <fresh-output>`) checks the
microphone label against the actual selector center at normal and minimum window
sizes. It also measures Models extent/viewport and automatic scrollbar state at
1280 x 900, 1280 x 1100 and 740 x 530, and verifies nonempty feedback remains
visible and reachable. Eighteen checks pass without microphone input or changing
the foreground application; captures use an isolated profile and real WPF controls.

The original 900-DIP view really overflowed by 51.46 DIP: the runtime status and
an empty feedback line extended below the visible engine selector. Pairing the
runtime status with that selector and collapsing empty feedback gives a measured
668.85-DIP extent in a 672.43-DIP viewport on the capture machine. Smaller windows
still scroll, and longer feedback can legitimately restore the scrollbar. The
microphone label's former 21.47-DIP center offset is now below 0.5 DIP.

Recognition timing is unchanged; fast completions are not artificially delayed.

## Public trailer replacement — October 16, 2026

The updated [0.4.2 trailer](https://www.youtube.com/watch?v=Xyz4TkNP58w) is public
on Jhinx Dev, with the approved 4K cover, completed 4K processing and saved English
captions. Native publication readback and anonymous metadata confirmed the exact
video and channel. The README, repository homepage and social preview now point
to the replacement. The public cover bytes match the SHA-256 recorded in
[image provenance](images/README.md).

The previous trailer was deleted after the replacement was public and the
repository links were verified. Native channel content and anonymous metadata
confirmed the old video was unavailable while the replacement remained public.
Local original media and the verified editable release archive were retained.

This completes the media replacement, not a public binary release. The trailer
uses the documented prerecorded recognition and measured-event replay; it does
not establish physical shortcut, live microphone or receiving-field acceptance.

## Desktop launch lifetime — October 16, 2026

A native isolated test confirms Sona launched directly by a contained terminal
belongs to that terminal's cleanup job and exits with it. The repaired desktop
bootstrap launches outside that job and survives its closure with the synthetic
draft intact. Thirteen checks cover the two launch routes, private transfer,
request expiry/replay, exact test cleanup and production preservation. Eighteen
upgrade-rehearsal checks cover the successful and fallback restoration paths.

The first rehearsal exposed timestamp conversion between Windows PowerShell and
PowerShell 7; identity checking now compares UTC ticks. A separate harness issue
inherited PowerShell 7's module path into Windows PowerShell; the test launcher
now lets Windows PowerShell resolve its own modules. Failed receipts were retained.
No production process was restarted and no model, microphone or external input
was used. Existing speech-worker job containment is unchanged. See
[desktop launch implementation](IMPLEMENTATION.md#desktop-owned-installation-and-upgrade-launches).

## Destination capture follow-up, 0.4.4

The new capture policy first reproduced nine failed assertions; the actual
unshown MainWindow/status fixture reproduced two. After repair, 11 policy and
10 status checks pass. Status reads neither inspect focus nor start services.
The affected insertion (24), completed-paste (14), active-app state (17), and
default Windows (49) suites also pass. Build completed without warnings/errors.
An independent source/fixture review found no actionable issue in the bounded
patch. Existing 0.4.3 Core 350, speech-preflight, settings, history and shutdown
evidence remains applicable; these unchanged paths were not needlessly replayed.

Under a separate, explicitly authorized desktop window, actual installed 0.4.3
alternate paste inserted the full synthetic sentence into empty Homepage Search,
without Enter or microphone. The query lowercased it, so exact-case readback was
unconfirmed. Selected suggestions report ListItem accessibility focus despite
the input caret. Synthetic Ctrl+Windows modifiers left that identity unchanged;
the recording listener ignores injected events. This is a compatibility finding,
not a successful physical-shortcut test or live-speech proof.

Version 0.4.4 improves guidance and captures actual-attempt metadata. It does not
authorize suggestion controllers as text destinations. Automatic insertion with
active suggestions remains unresolved. See [audit](DAILY-USE-AUDIT.md) and the
[read-only status contract](AGENT-INTERFACE.md).

## October 18 Discord settings candidate, 0.4.7

The Off-versus-saved-policy mismatch was reproduced in the native isolated
fixture before repair. A Core regression separately reproduced incoming silencing
not applying to an unchanged active source. Source review caught import applying
before review, paused rollback acquiring transiently, and manual unmute during an
awaited retry. Their regressions failed before correction and then passed.

Core362, new native19, default Windows49, fake Disconnect10, closed Discord11,
Settings23 and agent pending-settings88 passed. Normal/minimum-size synthetic
captures were inspected. The maintained build now includes the new native mode.
All Discord calls in these tests target unique fake pipes and fixture credentials.
No real microphone, Discord account/session action or global input was used.

The owner is in a public call and prohibited actions affecting that session.
The qualified candidate must remain unactivated until that constraint is cleared.
A local build/package is not proof of a live0.4.7 upgrade or owner-call acceptance.


## Session History across guarded updates (October 18)

The isolated four-entry restart and failure fixtures cover recent classification,
exact preferences/working draft/original, finite plaintext and DPAPI cleanup,
changed-history refusal, and uncertain-Quit recovery retention. The original
18-check upgrade rehearsal remains applicable. Run the session fixture against
the qualified package with the documented failure modes before changing this
transfer. A fixture marker proves the intended fault occurred after staging;
an earlier failure is not evidence that this fault path passed. Test windows stay
offscreen and never activate; no production microphone or Discord test is implied.

## October 20 responsiveness and delivery candidate, 0.4.8

Core388, native responsiveness/delivery50 and completed-paste21 checks pass on the
candidate. New checks first reproduced missing Auto bypass, old timer/layout/help,
native/agent mode activation and missing pending-send intent. Final review also
reproduced Enter after pill Stop, a manual stop before the limit timer, and a newer
copy during recognition; their regressions now pass. Enter remains intercepted
through full-paste verification, with late intent still requiring every send guard.
Tests use isolated
profiles, fake audio and paste transport, no global input and no owner clipboard.
The pill and both tooltip palettes were rendered and visually inspected offscreen.
See [the scope and explicit limits](RESPONSIVENESS.md). Exact published-module,
guarded live activation and physical owner acceptance remain separate gates.

The live-update preparation also exposed a pending-retention save defect before
Quit. Both interrupted-save directions were reproduced against the old helper.
The repaired native helper passed the 18-check successful session upgrade and
15-check pre-Quit failure fixture against the exact 0.4.8 package. These tooling
changes reuse the unchanged published app modules from source `ff6a72a7`.
