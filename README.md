# Sona

Local Windows dictation with editable drafts, a compact voice bar, downloadable
Whisper models, Discord call protection and CLI/MCP access. No Sona account or
subscription is required. Hold **Ctrl + Windows** to record and release to finish,
or choose **Toggle** in Settings. **Esc** cancels. Sona inserts an unsent draft
and never presses Enter to submit it. Closing the window keeps it in the tray.

The owner accepted Sona 0.1.1's dark sidebar and grouped settings. Version 0.2.0
extends that interface. Source qualification, installed behavior and physical
acceptance are separate; see [verification](docs/VERIFICATION.md).

## Dictation and models

Open Sona from Start and focus an editable composer. Home owns the current draft;
Settings, Modes, Dictation Models, Transcribe File, History, Vocabulary and Snippets
have dedicated pages. Clean, Verbatim and Notes control text cleanup. Recognition
hints, heard-as corrections and whole-utterance snippets have separate records.

Dictation Models offers Tiny, Base, Small, Medium, Large v3 Turbo and Large v3.
Explicit downloads show progress, support cancellation, and require pinned size
and SHA256. Differing existing files are preserved. Verify & use selects a model.
Download size is not memory use. Medium/Turbo/Large need the separately installed
CUDA engine. Workers have bounded time/memory and unload after each utterance;
failure leaves the previous draft intact.

Only Small CPU and Turbo CUDA have measured Sona benchmarks: eight licensed
French and eight English FLEURS clips. Other models are clearly unmeasured. Tiny
passed a download/verification/selection and one French file-replay pilot, which
is not an accuracy benchmark. [Benchmark context](docs/VERIFICATION.md).

History stays in memory by default. Explicit local text saving is capped at 200
entries and stores no audio. Turning saving off preserves earlier records until
Clear history; clearing preserves the current draft. Transcribe File accepts short
16 kHz mono PCM WAVs and never inserts automatically.

## Call protection

Settings can mute Discord during Sona or watched-app recording, optionally
silencing incoming Discord audio. Sona waits for protection before opening its
microphone. External apps use Windows activity polling with about 500 ms delay.
Overlapping recordings retain protection until the final source ends. Manual
mute/deafen is preserved; pause and quit restore only acknowledged owned changes.
Failed restoration remains visible and can be retried.

Import Hush setup protects its credentials with Windows current-user encryption
and lets you review watched apps/release preferences. Pause and quit Hush before
connecting Sona; both must not control Discord together. Alternatively enter your
own Discord developer application in the protected setup section. Discord RPC
requires an authorized app/tester account; Sona ships no shared secrets. Legacy
retirement and real-call acceptance require separate verification.

## Agent integrations and optional accounts

`sona-agent` returns JSON status, validated settings patches, model
discovery/download/verification and protection pause/resume. `--mcp` exposes the
same named operations over MCP stdio. It connects to the running app through a
current-user-only named pipe, without a public HTTP port. Credentials, history
and microphone-start control are outside this API.

```powershell
sona-agent status
sona-agent settings patch '{"language":"fr","shortcutMode":"Toggle"}'
sona-agent models list
sona-agent models install tiny
sona-agent protection status
```

[Agent setup and MCP configuration](docs/AGENT-INTERFACE.md).

Google sign-in and sync are not implemented or enabled. A future optional desktop
OAuth integration could store opted-in settings/vocabulary in Drive's private
app-data folder without a Sona email database. Audio, transcripts and credentials
are excluded. [Integration design](docs/INTEGRATED-SONA.md).

## Build and installation

Windows x64 and the .NET 8 desktop runtime are required.
`tools/setup-runtime.ps1 -BuildTools` explicitly prepares pinned CPU runtime and
SDK files. Default runtime is `%LOCALAPPDATA%\Sona\runtime`, with compatibility
for the existing development runtime. `SONA_RUNTIME` overrides it;
`MURMUR_RUNTIME` remains supported. CUDA setup is separate. Models, SDKs and CUDA
redistributables are not bundled in the application package.

`tools/build.ps1` builds app/agent, runs safety checks and queries NuGet advisories.
`tools/native-smoke.ps1` checks isolated UI; `tools/agent-smoke.ps1` checks CLI/MCP,
optionally adding one explicit download with `-DownloadTiny`. Tests use no real
microphone or global input. Native test instances have a 180-second lifetime.

`tools/prepare-release.ps1` requires reviewed clean source and creates a hashed
app/agent package. `tools/install.ps1` verifies files and creates the Start shortcut.
Startup stays opt in. Internal Murmur names/data paths preserve preferences. There
is no automatic cloud transcription, analytics, sound effects or submission.
The installer makes a verified `Programs\Sona\current` junction and adds it once
to the user's PATH so `sona-agent` works from new shells. The machine PATH and
existing version directories are preserved; MCP clients can use that stable full path.

The repository remains private while preparing eventual open-source distribution.
Public release must exclude private history, user data, credentials and runtime
redistributables. [Third-party notices](THIRD-PARTY-NOTICES.md),
[owner test](tools/owner-test.md).
