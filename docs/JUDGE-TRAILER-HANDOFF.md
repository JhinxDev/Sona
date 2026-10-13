# Sona judge and trailer handoff

Prepared October 13, 2026. This is the active brief for the fresh successor session.

## Requested outcome and ownership

The owner explicitly requested a new chat using **GPT-6.1 Sol at maximum effort (`max`)**. First act as an independent judge of Sona: examine the implementation, every relevant test and its evidence, product quality, performance, and worthwhile improvements. Then create a polished trailer showing the actual app, including a useful synthetic Latest Draft. Deliver a narration script with **ElevenLabs v4 audio tags** for the owner to generate, and time the screen action to that script and ultimately to the returned voice recording.

Transfer status: **assigned to successor chat `01a13603-8a77-7c52-9c2b-29ff13e454bf` (Sona judge and trailer), with activation supplied by the outgoing owner's verified commit receipt**. The new chat was created with `model=gpt-6.1-sol`, `thinking=max`, on the local host. The outgoing owner `01a12f45-d73e-7191-ad2a-56a050b8dce2` retires from source/trailer mutation once that receipt is sent. Until the activation message arrives, you may inspect source/evidence and plan the review, but do not mutate or synchronize this repository. After it arrives, you are the active source and trailer owner. Do not request a second human authorization for this already requested handoff.

Read `C:/Users/qazws/AGENTS.md`, `E:/Projects/AGENTS.md`, and the repository's `AGENTS.md`. Use the live harness's permissions and supported escalation. Relevant standing coordination and private fast-forward Git authorization persist. **Keep owner updates in this chat; do not send Discord notifications.** Do not mutate the shared Vault, Tool Shed catalogs, Hush, or another project's checkout. Shared catalog publication remains deferred. Creating this successor is explicitly authorized; additional user-owned chats still require their own request.

The role is judge first, producer second. Preserve the requested model/effort. Make findings and recommendations before any broad implementation or redesign; follow current review-first authority. Do not turn the review into speculative feature development. Record any consequential change that needs the owner's decision as a concrete proposal while continuing independent trailer work.

## Exact source and installed baseline

- Source: `E:/Projects/local-dictation`. Private remote: `https://github.com/JhinxDev/Sona`. Current-product branch: `codex/local-dictation`.
- Pre-handoff local/remote HEAD: `4bb70a47b298334b0279b2d4b308559fa035768c`. Clean worktree and one registered checkout verified immediately before this brief. Subsequent handoff commits change documentation/ownership only; refresh HEAD/status before work.
- Installed qualified source: `aaeb9f2196301b46c514c5e083dcf34c23969080`. Product `src` tree at both baseline commits: `7a84feac28cc5fc423a62e1c379ebe9548597fc1`.
- Qualified package: `E:/Projects/local-dictation/artifacts/0.4.0-aaeb9f21`.
- Installed app: `C:/Users/qazws/AppData/Local/Programs/Sona/0.4.0-aaeb9f21/Sona.exe`; stable `current` junction exists. User PATH exposes `sona-agent.exe`.
- Prior recovery package: `C:/Users/qazws/AppData/Local/Programs/Sona/0.3.3-78757261`. Preserve it.
- Runtime/SDK/cache: `E:/DevelopmentTools/local-dictation`; SDK `sdk/dotnet.exe` 8.0.425. Build instructions and environment paths are in `docs/IMPLEMENTATION.md`. Use `--disable-build-servers -p:UseSharedCompilation=false`.
- Heavy media, renders, models, fixtures, caches, and logs belong outside the synced Vault and outside tracked source. Put trailer working assets under `E:/Temp/qazws/sona-trailer`; keep small durable scripts/briefs in an owned repo path if appropriate.

The installed app can be running while you review. Refresh process identity and status through the supported CLI/native route, rather than reusing historical PIDs. No task-owned HTTP/browser preview remains. Resident speech workers use a task-owned loopback service; inspect the live status rather than assuming a fixed port or readiness from process existence. This handoff does not require another installation.

## Owner preferences and product intent

Sona is a local Windows dictation app intended to become free and open source without a Sona account or subscription. It is presently in a **private repository with no public binary release**. Preserve that distinction in marketing and release recommendations. The name Sona is accepted; the earlier Murmur name survives in internal namespaces and the compatible data directory.

The owner mainly dictates English, wants fast GPU/VRAM inference when available, and keeps the selected model warm. They accepted the dark sidebar redesign and prefer **Amethyst**. They asked for professional typography, a crisp theme-aware vector mark, spacious provider logos, at least eight themes, model download/selection information, active/loaded indicators, two recording displays with real audio-reactive bars, vocabulary, and call protection. Do not reintroduce Hush or the retired Flow/Whisper Flow watched-app entries.

Saved owner settings after 0.4.0 installation: `parakeet-v3`, Auto GPU selection, Amethyst, clipboard backup enabled, 10-minute idle unload, startup/tray enabled, local GPU Polished cleanup enabled, local persistent history disabled, 52 vocabulary terms and five corrections. Watched apps are the three ChatGPT/Codex identities. Discord protection was connected, enabled, unpaused, Ready, and owned no changes at installed verification. These are snapshots, not a claim about current connection state.

Owner data resides at `%LOCALAPPDATA%/Murmur`. It contains private drafts, vocabulary, preferences, and protected Discord credentials. Preserve it; never print, copy into source, show in a trailer, or upload it. An update must preserve the draft through the maintained current-user DPAPI upgrade checkpoint, validate the exact package/runtime and all unaffected preferences, and retain recovery. All settings except the selected model matched logical checksum `110E2F925168CDE87B21A28A26EAFB60F069DB6EFB319B8DF1C9815A2C815C86` during installation. New-install defaults remain opt-in for startup, clipboard, cleanup, and saved history; the owner's chosen settings are not a reason to change everyone else's defaults.

## Current features and honest limits

- Ctrl + Windows: hold to listen, release to finish; double tap starts hands-free listening, tap again finishes. Esc cancels. Insertion creates an unsent draft and never submits with Enter.
- Captured destination-app label, clipboard backup option, non-activating Panel/Capsule overlay, measured microphone envelopes, local file transcription, editable original/latest draft, optional local history, vocabulary/corrections/snippets.
- Seven local model choices: six existing Whisper profiles plus NVIDIA Parakeet v3. Exactly one speech worker is selected/resident; switching releases the old worker. Idle expiry, Keep until quit, and explicit Load/Unload are available.
- Separate optional local cleanup model with GPU/CPU choices and its own memory/idle controls. Download size, host memory, and device VRAM are different measurements.
- Discord call protection and watched ChatGPT/Codex recording detection. Other-platform mute adapters are a possible future direction, not a shipped capability.
- CLI and stdio MCP expose 18 tools for settings/status/model/vocabulary workflows. They do not grant remote microphone control. GitHub update checking is optional and never silently installs.
- No implemented cloud speech/BYOK speech, Codex-subscription audio funding, Google sign-in, account database, or sync. Do not add mock provider rows or claim these work.
- Recording and native requests are bounded (120-second recording and 90-second request deadline). Avoid blanket claims of unlimited duration, zero limits, best accuracy, or universal device performance.

Read `docs/DESIGN.md`, `docs/IMPLEMENTATION.md`, `docs/VERIFICATION.md`, `docs/PARAKEET.md`, `docs/LOCAL-CLEANUP.md`, and `docs/AGENT-INTERFACE.md` for the owning details. Historical sections retain failed evidence and superseded claims; the dated 0.4.0 sections and exact receipts govern the current baseline.

## Judge review: priorities and decisive proof

Produce a concise verdict with severity, trigger, affected code, observed evidence, user impact, and recommendation. Distinguish confirmed defects from plausible concerns. Inspect the relevant test implementations and receipts, not only their pass counts. Reuse valid unchanged evidence; rerun only checks justified by a changed input, unresolved failure, or missing proof. One owner runs an acceptance suite. Do not repeat every benchmark merely to appear thorough.

Review these areas first:

1. Keyboard state transitions, double tap timing, release/escape, focus changes and captured-target insertion; clipboard fallback and preservation; no accidental submission.
2. Speech/cleanup worker ownership, cancellation/deadlines, warm residency and idle expiry, confirmed process exit, switch races, accurate GPU/CPU/status labels, cleanup/insertion order and text preservation.
3. The new Parakeet native scheduler repair, reproducibility/provenance, verified model/runtime downloads, partial-file cleanup, locks, authenticated loopback service, job/upload/response limits, and bounded audio splitting.
4. UI consistency at normal/minimum windows and Windows scaling, keyboard/accessibility, theme persistence, model logo spacing, active versus actually loaded indicators, compact selectors, shared About/sidebar version.
5. CLI/MCP compatibility and safe scope, update/recovery behavior, Discord mute/restoration ownership, startup/tray retirement of Hush, privacy defaults, licensing and publication readiness, README claim accuracy.

Known review targets:

- The existing Whisper b5454 CUDA server crashed with native illegal memory access in a mixed English/French resident sequence. Disabling CUDA graphs also failed. No speculative production workaround was shipped. The completed head-to-head comparison is English-only; historical cold French figures are separate evidence. Avoid reopening the same failed route without a concrete new prerequisite.
- An unsplit 80.88-second Parakeet request dropped its last 6.96 seconds. Quiet-point in-memory chunks of at most 25 seconds now cover every PCM byte once under one deadline and restore the final sentence. The narrow regression passed; personal long-dictation/boundary quality remains a real acceptance question.
- The first Whisper VRAM comparison used a contaminated baseline. Negative deltas are invalid; use the corrected paired fixture.
- Production physical microphone/Codex insertion and the owner's accent with **new Parakeet 0.4.0** remain unverified. Earlier accepted Sona UI and Discord muting do not close that gate. Do not activate the microphone or alter live call state to manufacture proof. Prepare a simple real-voice test only when useful.

Contained optimizations need before/after measurements of the actual bottleneck and focused regression proof. A clean build is not an installed-target pass. Do not broadly reset, mass-stage, delete models, unpin history, install new audit packages for documentation, publish, or change account/security state.

## Existing evidence to reuse

`E:/Temp/qazws/sona040-final-convergence.json` records baseline local/remote equality, installed source, identical product tree, and clean residuals. `sona040-installed-readback.json` and `sona040-upgrade.log` prove the qualified app, 35 package/36 CUDA members, preserved preferences/draft, About/sidebar, actual CUDA readiness, and call-protection snapshot. `sona040-installed-models.png` and `sona040-installed-settings.png` show the real installed Amethyst UI. `sona040-ready-for-test.json` is a historical idle/readiness snapshot, not current state.

| Scope | Existing receipt | Result and practical limit |
|---|---|---|
| Core | `E:/Temp/qazws/sona040-core-frozen.log` | 277 checks |
| Native general | `E:/Temp/qazws/sona040-windows-final.log` | 47 checks; no physical mic/global input, fake Discord |
| Production speech fixture | `E:/Temp/qazws/sona040-speech-final.json` and `sona040-speech-segment-qualified.log` | 19 checks; real checked CPU/CUDA runtimes, timeout/cancel/clear/unload, long tail |
| Paired memory/switch | `E:/Temp/qazws/sona040-gpu-paired-memory.json` | actual old-worker exit and paired GPU baseline |
| Live agent interface | `E:/Temp/qazws/sona040-agent-final/agent-smoke.json` | 19 checks, 18 MCP tools |
| Native model UI | `E:/Temp/qazws/sona040-models-normal-qualified/models-ui-smoke.json` and `sona040-models-compact-verified/models-ui-smoke.json` | 17 checks each, 1000x700 and 740x530 |
| Earlier unchanged layout/persistence | dated 0.3.3 sections of `docs/VERIFICATION.md` | theme persistence and compact cleanup controls reused unchanged |
| Failed cases | `E:/Temp/qazws/sona040-long-tail-failure.json`, `sona040-whisper-all-clips-probe.json`, `sona040-whisper-graphs-disabled-probe.json` | retain failures; do not label them passes |
| Native repair | `E:/Temp/qazws/sona-parakeet-probe-scheduler.json` and `src/Murmur.App/Assets/Parakeet/PROVENANCE.json` | pinned repair, authentic runtime inventory/ready protocol, finite resource budget |

Maintained tests/scripts are under `tests/Murmur.Tests`, `tests/Murmur.WindowsTests`, and `tools/*-smoke.ps1`. Native tests use separate `MURMUR_DATA` profiles, exact PID UIA/Winapp and `--test-ui` (microphone disabled). The model UI harness handles a WPF Invoke provider error only when persisted selection and the newly enabled action prove completion. Reuse that route rather than global mouse/keyboard injection. Check referenced paths before claiming evidence exists.

Benchmarks used frozen Google FLEURS CC BY 4.0, eight English clips/156 words, three timing requests per clip. Raw ASR before cleanup. Parakeet: 8 errors (5.13% WER), 20.53 ms warm median; Whisper Turbo: 9 errors (5.77%), 144.31 ms. This is a small RTX 4080 SUPER (16 GiB VRAM, about 32 GiB RAM) fixture, not a universal accuracy or end-to-end latency claim. Correct paired loaded device-wide VRAM delta: 974 versus 1000 MiB; host peak commit: 2279.30 versus 1904.36 MiB. Other GPU activity and metric distinctions still matter. Complete provenance, pinned native/model identities, rebuild script, licenses and failed attempts are in `docs/PARAKEET.md` and `tools/parakeet`.

## Trailer production brief after the judge stage

Create a real, polished product trailer, not only a storyboard or a slide deck. Default to a roughly 45-60 second English 16:9 1080p or higher YouTube trailer, with a clean SaaS visual style, Amethyst UI, readable Inter typography, crisp vector branding, motivated cuts, and restrained motion. Treat length as adjustable to comprehension and the returned narration, not an inflexible timer. The audience is Windows users and developers wanting private local dictation without a Sona subscription. The owner's truthful origin is frustration with quotas/subscriptions and wanting a local tool they can use and adapt.

Show a clear progression: brief origin/problem, actual dictation-to-useful-draft workflow, local downloadable models kept warm, cleanup/vocabulary, call protection, and CLI/MCP integration. Select the strongest few beats rather than racing through every setting. Use the actual app, preserve its identity, and show a meaningful synthetic draft, for example a short Sona release checklist with specific tasks. Keep accounts, private project names, chats, owner drafts, system tray, desktop notifications, and live Discord call information out of the captured area.

Screen recording/navigation is authorized by the user's request. Use a separate demo/test profile and exact task-owned window, semantic UI controls, and a supported non-disruptive capture route. Record the real navigation. A preview waveform or fake Discord fixture must be clearly identified as such and never sold as a real microphone/call acceptance test. For a functional transcription beat, prefer a licensed/public or deliberately synthetic local audio file through the real transcription path. Do not synthesize live owner speech or take over the owner's desktop. Screen/app output is evidence; editorial callouts may explain it but must not fabricate functionality.

Use the current catalog's `agentic-video-production` skill and its `references/brief-to-editable-master.md`; read the applicable local capture/native control and video tool instructions. If using HyperFrames, load its mandatory entry point and actual installed plugin workflow. Follow `C:/Users/qazws/Nextcloud2/Homelab Vault/How-To/Visual-Storytelling-Production-Gates.md` for readable scale, pacing, transitions, thumbnails, encoded QA, and final creative approval. This personal Sona trailer is not a Past Patterns production and has no Gemini voice lock.

Prove the difficult demo/capture section before a full render. Preserve raw task-owned recordings and an editable composition/timeline with named footage, titles, callouts, voice/music slots, fonts, assets, hashes and export settings. Use existing local tooling; do not buy/install a new editor by default or commission paid narration/music/media. Music is optional and must have known reuse rights. Keep outputs private and local.

### ElevenLabs v4 script and timing contract

The owner will generate the voice in ElevenLabs. Give them a **copy/paste-ready tagged script**, a clean untagged transcript, a pronunciation note for Sona/Parakeet/Codex if used, and a short delivery/settings note. Keep timestamps, shot numbers, UI actions, editor notes and references in a separate beat sheet so they are not spoken. Use restrained square-bracket delivery cues and purposeful pauses. No SSML or invented exact-duration pause tags.

Official sources verified October 13, 2026:

- [ElevenLabs audio-tag documentation](https://elevenlabs.io/docs/help-center/product/core-capabilities/text-to-speech/how-do-audio-tags-work-with-eleven-v3-and-v4): Eleven v4 is supported as `eleven_v4`; short square-bracket directions govern delivery.
- [Eleven v4 Audio Tags guide](https://elevenlabs.io/blog/elevenlabs-audio-tags-list): tags allow natural-language direction, and one cue per clause is a useful starting point. Verify the chosen voice's actual response rather than guaranteeing an emotion.
- [Eleven v4 pause guide](https://elevenlabs.io/blog/how-to-add-pauses-in-ai-voiceover): `[pause]` and `[long pause]` are contextual, not exact seconds; SSML breaks are unsupported.

Deliver a timed beat sheet (time window, exact spoken clause, visible UI/action, edit/callout, expected hold) and a watchable silent preview/animatic alongside the script. Send the script early enough for the owner to generate voice while the edit continues. Keep planned timecodes explicitly provisional. When the owner supplies the narration, measure its real duration/word or sentence timings and conform the screen recording, cuts and highlights to that audio. Do not call a silent/placeholder cut the final voice-synchronized master. Missing voice blocks only that final conform; finish the review, recording, editable master, script, visual preview and custom thumbnail independently.

Final delivery: judge verdict/report, prioritized measured improvement proposals, tagged/clean scripts, beat sheet, actual app footage, editable master with a reproducible render command, a preview MP4, custom thumbnail and source/rights ledger. Once audio is supplied, add the synchronized MP4 and sidecar captions. A horizontal trailer should not have burned narration captions or an embedded subtitle stream; if a separate 9:16 Short is later produced, use timed burned captions with verified platform-safe glyph bounds. Do not auto-produce extra formats merely for quantity.

Check final encoded bytes independently: complete decode, dimensions/FPS/stream inventory, first/boundary/final frames, legibility, clipping, blank/freeze behavior, and normal-speed motion. Actual audible review and user creative acceptance are separate from technical or muted playback. Do not silently play sound in the owner's background. Preserve exact output hashes and clearly name the remaining voice/creative gates. No YouTube upload, public GitHub release, repository visibility change, or outside communication is authorized by this trailer request.

## First action and finite finish

On startup, acknowledge the source/evidence identities and requested Sol 6.1 Max settings, then inspect this brief and repo ownership. Wait for the outgoing chat's activation receipt if it has not arrived. Once activated, review current status and the relevant source/tests, produce the judge verdict, and proceed into the trailer work in that order. Share meaningful updates in the new chat and ask only important missing decisions. Use ordinary reversible creative choices rather than a long questionnaire.

For any authorized changes, inspect active ownership/worktrees, stage only intentional paths, privately commit/push and prove exact remote equality. Preserve unrelated work and the installed baseline. A documentation-only handoff does not require replaying the app tests or updating the installed binary. Maintain this brief's current outcome and remaining gate rather than spawning redundant status documents. Completion of the judge and visual/script deliverables is distinct from final narration conform and human approval.
