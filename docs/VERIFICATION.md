# Candidate verification

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
