# Installed-app reference findings

2026-10-12. Owner-authorized static inspection of installed Wispr Flow 1.6.1102 and Spokenly 1.0.9 with the pinned on-demand REA 6.4.0 toolkit. Feature artifacts were parsed without running their code or starting recordings. Proprietary implementation and assets remain outside this source repository.

Flow's client declares separate hold/toggle/cancel paths and recording/processing states. Its vocabulary form separates recognition hints from explicit spelling replacements and rejects conflicting keys; snippets validate triggers and payloads. Raw ASR text remains separate from optional formatting, and failed polish can fall back to the raw version. Its insertion helper uses a request identity, verification response and recovery path, although the native enforcement and full clipboard guarantees were not established.

Spokenly's native PE export tables expose transcription model load/free, language detection, raw/full text and backend queries. A separate cleanup library exposes load/answer/unload. These establish distinct ABI boundaries, not actual model choice or idle-memory behavior.

Murmur independently implements intentional sessions, raw-text preservation, separate local vocabulary hints/replacements, exact whole-utterance snippets and on-demand worker exit. It uses no reference app binary, asset, endpoint or credentials. Bounded optional target context and local AI rewriting remain future design choices that need measured benefit.

| Reference | Artifact identity |
| --- | --- |
| Flow app.asar | SHA256 `5a35c867858fd0c14c643bf3530b0ba928869ff01a1383a437eb255db4969a74` |
| Spokenly executable | SHA256 `6a1bf684ee091f1fcd2affc37212644a40003dfb1db80b455e50c87408b926b7` |
| Spokenly transcription DLL | SHA256 `9e85686f8a2629caefc3fc6ba8ee3711b5bc73ea88d99e09b9957aacebdbc3af` |
| Spokenly cleanup DLL | SHA256 `1567169715398d8eb20031d8bf7443e16f5cd86ca08933a415703bef9c02927d` |

REA controls evidence: `ev_1fe85721a25eba6666dc5cef28145b6037e3ab535818e3d67f51bb23ef700276`. Spokenly native/managed boundary: `ev_89c8c9ee5a74cc87c26897f76e7f122c991c4c068ecff4205a81af846e902d2e`. Internal finding ledger: `E:/Temp/qazws/local-dictation-build/DICTATION-REFERENCE-CONTRACTS.md`, with exact offsets, module hashes and unresolved runtime questions. These IDs identify saved evidence files; they are not hosted links.

Accuracy, contextual rewrite/self-correction algorithms, remote retention, actual reference idle-unloading, native clipboard restoration and mixed-language behavior were not reconstructed. Static artifact analysis is not runtime acceptance.
