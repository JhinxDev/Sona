# Local cleanup feasibility, October 13, 2026

Historical CPU feasibility study. The later optional Sona 0.3.2 integration and
GPU qualification are documented in [Local cleanup](LOCAL-CLEANUP.md). The
0.3.1 installed boundary and pilot failures below remain historical evidence.

The owner tested hold/release and hands-free recording in 0.3.1 and reported
that it broadly worked, then requested investigation of cleaner text without
losing local execution or speed. This is research and an isolated pilot;
Sona 0.3.1's installed processing remains the existing deterministic TextPipeline.
No model cleanup setting or production integration is claimed here.

## What exists today

TextPipeline removes selected filler words, adjusts spacing, applies explicit
vocabulary corrections and handles explicit correction/snippet triggers. It
does not contain a language model for natural self-correction or grammar.

Wispr Flow documents Light cleanup for filler words/grammar and Medium for
more concise phrasing. Its help pages do not establish the exact model used.
Spokenly documents a separate local cleanup stage, and publishes its own
Spokenly Cleanup 1 weights under Apache 2.0. This Qwen3-0.6B-Base fine-tune
has an official 8-bit GGUF for llama.cpp. No proprietary app code or artwork
was extracted or reused. Plain app/tone/text/out inputs and greedy decoding
follow its published model card. These are public model usage details, not
proof that Sona already implements the feature.

Primary sources:

- https://docs.wisprflow.ai/articles/4283510616-Auto-Cleanup:-control-how-much-Flow-edits-your-dictation
- https://spokenly.app/docs/local-model-cleanup
- https://huggingface.co/spokenly/cleanup-1
- https://huggingface.co/spokenly/cleanup-1-gguf
- https://github.com/ggml-org/llama.cpp/releases/tag/b11583

## Measured isolated pilot

One pinned model and official CPU runtime were downloaded into the task-owned
runtime, with published sizes/digests verified and MIT/Apache notices retained.
No weights, binaries, synthetic transcript responses or user data are bundled
into Sona's app package. Model download: 639,442,784 bytes. Model revision:
79936c95e5c0ea9162ae7a435e64cb3127020e36. Model SHA-256:
66509e0fe1578d2ac4b0773c79a6c83e8d6f218618dfe11e4532beacd999b6e8.
llama.cpp b11583 CPU archive SHA-256:
2bceb716d9d93b950c008c9d81701650965c33e957de9e1d4da325c35c41abfe.

Windows, AMD Ryzen 9 7900X, six CPU threads, no GPU offload, 4,096-token context,
greedy generation, no prompt cache reuse. The one process stayed resident across
the cases, with three sequential runs per case. A randomly keyed loopback API
was private to the harness, proxies disabled; the worker belonged to a
kill-on-close Windows job with a 2 GiB per-process limit. It exited after testing.
No microphone, owner transcript, cloud inference, foreground interaction or
live Sona settings were used or changed.

Process/model readiness took 935 ms including warm-up. The OS file cache was
already warm from download; this is not a disk-cold reboot measurement. Peak
working set was 1,195.5 MiB. End-to-end cleanup request medians follow, excluding
speech recognition, loading and insertion. This is a small synthetic sample,
not a comprehensive language-quality or simultaneous-game-load benchmark.

| Case | Input words | Median cleanup |
| --- | ---: | ---: |
| filler | 20 | 0.368 s |
| self correction | 16 | 0.319 s |
| meaningful words | 14 | 0.375 s |
| project names | 14 | 0.398 s |
| numbers | 18 | 0.449 s |
| literals | 10 | 0.366 s |
| french | 20 | 0.549 s |
| french correction | 12 | 0.292 s |
| mixed | 15 | 0.371 s |
| untrusted instruction | 10 | 0.232 s |
| question | 6 | 0.162 s |
| long | 134 | 2.703 s |

Short English examples lost fillers/repeats and gained punctuation. Explicit
Tuesday-to-Wednesday and Alice-to-Ben corrections resolved as intended. A French
Tuesday-to-Thursday correction and digits worked. A question remained a question;
an instruction embedded in the transcript remained text rather than being obeyed.
Project names and a command/path survived the tested unprotected examples.

One mixed-language failure is material: "sans cloud" became "sans Claude".
That changes meaning. Do not infer reliability from the otherwise good examples.

Four targeted follow-up cases replaced exact names, a technical word and literal
spans with [TERM_n] placeholders. All placeholders survived in order in all three
runs, including the case that previously changed cloud. Replacing that placeholder
with its original spelling restores cloud. Medians were 0.309 to 0.653 seconds.
This is a limited preservation proof, not a guarantee against all hallucinations,
changed negation, omitted facts or multilingual errors. No production protector,
validator or rejection/fallback policy is implemented by this experiment.

## Recommended implementation boundary

Add optional local Light cleanup alongside the existing rule cleanup and Verbatim
choice. Keep one cleanup model warm with a configurable idle timeout and explicit
unload, subject to resource limits. The extra time scales with output length;
the 134-word sample needed 2.703 seconds, so do not promise instant long results.

Protect exact vocabulary, commands, paths, URLs and identifiers before the model.
Validate restoration and output bounds, check meaningful facts/negation and refuse
unsafe changes where possible. Retain the raw original for the existing Use
original action. A timeout, malformed response or failed preservation check must
fall back to the rule-cleaned draft. Cancelled recording must not paste late text.
Apply the existing final target-focus check and clipboard behavior after cleanup.
Models and runtime downloads need pinned provenance/license notices, explicit
opt-in and removal controls. Future model training is separate work.

## Reproduction and evidence

Maintained Windows harness: tools/cleanup-pilot.py. It accepts a caller-prepared
pinned runtime/model directory and a fresh result path, verifies the model digest,
and runs only the fixed synthetic cases. It needs Python 3.11+ and no new packages.

```powershell
python tools/cleanup-pilot.py --pilot-root <prepared-directory> --output <new-results.json> --hardware-label "CPU model"
```

Recorded owner-machine receipts, retained outside source and not an application
integration: E:/Temp/qazws/sona-cleanup-pilot-results.json,
E:/Temp/qazws/sona-cleanup-guard-results.json and the runtime's provenance.json.
The measured fixed-path prototype and the maintained argument-based harness share
inference/containment logic. The portable CLI wrapper received syntax verification;
the fixed-path prototype produced the measurements. Unchanged 0.3.1 application
tests were not rerun. No installed Sona update was required for this investigation.
