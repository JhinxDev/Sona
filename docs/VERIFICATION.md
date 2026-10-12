# Candidate verification

2026-10-12, Windows x64. Ryzen 9 7900X, 31.66 GiB RAM, RTX 4080 SUPER; the candidate is CPU-only and does not allocate GPU model memory.

## Passed

- Warning-free native .NET 8 WPF build, locked NAudio 2.2.1 dependencies, no known NuGet advisory reported.
- 24 Core checks: session cancellation/duplicates, original retention, literal/code preservation, conservative bilingual edits, exact snippets, malformed preferences, separate vocabulary hints/replacements and persistence.
- 17 Windows checks with fake capture and injected input sink: spontaneous/errorless stop, stalled input, silent input, disposal, final-focus cancellation, focus drift and newline rejection; inherited job containment, held-audio cleanup, persistent-lock recovery, and abrupt parent exit with no orphan worker. No live microphone or input was used.
- 10 native UI checks: no-mic test mode, accessible selected-tab controls, native vocabulary save/relaunch, clean close, active file replay worker creation, quit with worker/audio removal. The first native run exposed an asynchronous save receipt; the maintained test now waits for it.
- Integrated correctness review's five initial and two follow-up findings repaired. Tray cleanup failure now shows the existing nonactivating window so retained-audio notices are visible.

## Measured ASR candidate

whisper.cpp release `b5454`, commit `d1be6fde11ac6e0407606b4e42fe72d34add8037`; multilingual `ggml-small-q5_1.bin`, 190,085,487 bytes, SHA256 `ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb`. CPU six threads, beam/best-of one, worker exits after each request. Process memory ceiling 1.5 GiB, transcription deadline 90 seconds, intentional recording limit 120 seconds, stalled capture feedback after five seconds.

Three synthetic English SAPI fixtures had exact case-insensitive expected words: 5.12 to 6.025 seconds of speech, 3.217 to 3.323 seconds of transcription, sampled worker peak 367.02 to 367.08 MiB. One 1.054-second French phrase took 1.663 seconds, 366.58 MiB. French source: [LoquaxFR, faire des phrases, CC BY 4.0](https://commons.wikimedia.org/wiki/File:LL-Q150_(fra)-LoquaxFR-faire_des_phrases.wav); original SHA256 `44eadad85313d26dcdd469011fe675685c9697e95df0c9794bee879e2edfcfdc`.

After a first native replay the app used about 135 MiB working set and 96 MiB private memory. This single sample does not establish stable idle or long-session behavior. Fixtures, receipts and failed-run repair evidence are retained under `E:/Temp/qazws/local-dictation-build`. Unchanged ASR fixtures were reused; they were not rerun to inflate confidence.

## Required owner proof

Personal English, French and mixed speech; actual Codex draft insertion/no submission/readback; clipboard preservation; held-modifier Esc, focus changes and duplicates; microphone changes/unplug and sleep/resume; Hush ducking/restoration in a real owner-started call; creative usability. See `tools/owner-test.md`. Do not call the project complete or accepted before those results.
