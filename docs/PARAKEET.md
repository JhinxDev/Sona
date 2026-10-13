# Parakeet local speech integration

October 13, 2026. Owner approved adding Parakeet beside Whisper, testing both on
the actual Windows PC, and choosing the best primarily for English. French is
secondary; an English-only candidate is eligible when genuinely supported.
This change retains the accepted Sona UI and the 0.3.3 immediate theme save.

## Design

Use NVIDIA NeMo-Speech.cpp 0.2.0 and the official Parakeet TDT 0.6B v3 Q8_0.
The published Windows CUDA/CPU archives avoid Python and toolkit installation.
V2/unified English variants are not offered until the runtime actually supports
them. V3 is a full-utterance model, so recording/hold/double-tap behavior stays
the same. It replaces only the local transcription engine.

The native server is loopback-only, has no web UI or access logs, and takes a
fresh per-worker Bearer secret in its child environment. It starts with one
local ASR file, bounded HTTP concurrency/upload/read/write timeouts and an
explicit CPU or CUDA device. Readiness must prove ASR availability, selected
device and expected runtime version. Audio is multipart WAV held in memory;
the bounded JSON response supplies only text to Sona's existing cleanup stage.
Sona never invokes model-name downloads or the runtime's microphone command.

One ResidentModel owns either Whisper or Parakeet, so a family/backend change
releases the prior process before starting another. The existing application
and worker jobs, cancellation, 60-second loading deadline, 90-second request
deadline, idle expiry and Keep until quit behavior remain. Unconfirmed release
retains ownership and blocks replacement. Input/response buffers are cleared.

Long Parakeet recordings use independent requests of at most 25 seconds, split
at quiet PCM points between 18 and 25 seconds and stitched as text. No samples
overlap or disappear, and every generated buffer is cleared. The entire operation
keeps one 90-second deadline and one resident worker. Short recordings remain a
single full-utterance request. This fixes an observed 80.88-second fixture whose
final sentence was omitted by a single native offline request. The exact native
tail cause is not established; smaller independent requests restored that sentence.

Runtime installation verifies exact published archive bytes/hashes, safe member
paths, a local member manifest and license files. Existing differing files are
preserved. ModelStore continues explicit download and pinned model verification.
Selecting a model installs its needed runtime only by a deliberate Download/
Use action; startup never downloads. Provider, supported languages, actual size,
active versus resident state and measurements appear in the existing rows.

## Execution plan

Root owns source integration and all actual-PC/native tests. A bounded worker
owns only Audio/ParakeetAssets.cs; the existing reviewer owns one consequential
correctness review after integration. No shared Vault writer or public release.

- [x] Extend Core ModelCatalog with per-model provider/backend/license/revision
  and a pinned Parakeet URL. Preserve all Whisper IDs and downloads. Add focused
  catalog/agent selection and response/readiness validation regressions.
- [x] Implement ParakeetAssets and extend Audio/ResidentModel/Transcriber with
  authenticated warm-server dispatch, exact backend/version validation and
  cross-family release. UI ModelsPage/AgentIntegration share the same runtime
  installation/availability path and honest provider metadata.
- [x] Add a maintained Windows fixture using the real contained workers and the
  existing licensed FLEURS WAV corpus. Compare English WER and warm latency,
  record host memory and explicitly labeled device-wide VRAM deltas, and check
  worker reuse, cancellation, idle expiry, switching, unload, invalid replies,
  unauthenticated rejection and absence of web UI. No microphone or global input.
- [x] Build with the isolated SDK. Run affected Core/Windows/agent checks and
  native model-picker proof at 1000x700 and 740x530 DIP. Update actual measurements
  and choose owner default by English results; retain Whisper selectable. Do
  not infer owner's personal accent accuracy from the small corpus.
- [x] Review scoped diff, close real findings, commit/fast-forward push privately,
  package the exact source and use the encrypted draft-preserving updater.
  Compare installed/member/source hashes and saved Amethyst, clipboard, startup,
  vocabulary and call state. Verify the chosen actual GPU worker and no leftover
  test processes. Preserve current 0.3.3 recovery and record finite cleanup.

Pins: native v0.2.0 commit 6a3ca369370782acee0dd155e34394ba60b920c4;
CUDA ZIP 102183465 bytes, SHA256
9ffe5e842c39a7d3211f3b2c3b3aa1bca8c0b2455d96d961ed5f22ec5108f98d;
CPU ZIP 5528418 bytes, SHA256
82c80451086e86194aba9af19f800da77cd66d1c28d993d0efceffd6cdb49bb8.
Model revision 541d1f99c6b0c3cd0b11a95167540bb8edefd82b,
parakeet-tdt-0.6b-v3.q8_0.gguf, 713975456 bytes, SHA256
e3880d0aaaaf2c308ea2c35016b2b895c423eb3fda924c1b463d1c19b7f4d32e.
Runtime Apache-2.0; NVIDIA model CC-BY-4.0 with attribution.

Sources: [tagged API](https://github.com/NVIDIA/NeMo-Speech.cpp/blob/v0.2.0/docs/api.md),
[release](https://github.com/NVIDIA/NeMo-Speech.cpp/releases/tag/v0.2.0),
[model](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3).

## English-first results on the owner's PC

RTX 4080 SUPER (16 GiB), Windows 11, default language detection and the same
five neutral vocabulary hints. Raw recognition, without cleanup. Eight distinct
Google FLEURS test clips in original archive order, 156 reference words; three
warm requests per English clip for timing. Lowercase Unicode token edit distance
ignores punctuation but counts numeric spelling differences. This small corpus
does not establish the owner's accent/noisy-microphone accuracy.

| Model | English word errors | Median warm request | Loaded VRAM delta | Peak host commit during clips |
| --- | --- | --- | --- | --- |
| Parakeet v3 Q8_0 | 8 / 156 (5.13%) | 20.5 ms | 974 MiB | 2279 MiB |
| Whisper Turbo Q5_0 | 9 / 156 (5.77%) | 144.3 ms | 1000 MiB | 1904 MiB |

VRAM is paired device-wide sampling with each previous test worker unloaded
before baseline; other workloads are included. It is not per-process attribution.
Host commit is a Windows peak allocation/backing metric, not working-set RAM;
peak working sets were 340 MiB and 377 MiB respectively. File size is neither
measurement. Parakeet cold readiness took 1.07 s; Whisper took 1.47 s including
Sona's integrity verification. Cleanup and insertion time are outside these timings.
The owner's default is Parakeet on GPU for its much shorter warm delay and
slightly lower observed English error count; every Whisper choice remains available.
New profiles retain their existing defaults until the user chooses another model.

Parakeet also completed eight French clips (232 words, 13 errors, 5.60% WER;
22.0 ms median), but French did not drive the default choice. The existing pinned
Whisper b5454 CUDA server crashed with native illegal-memory-access in a mixed
English/French resident sequence, below its memory ceiling. Graph-disable did not
resolve it. That route stopped; its failed evidence is retained, the completed
Whisper comparison is English-only, and historical cold French scores remain
separate. Sona releases a failed worker and preserves its existing draft.

The repaired 80.88-second Parakeet sequence completed in 173 ms and included its
final sentence. CPU fallback, repeated residency, configured expiry, Keep until
quit, queued/active cancellation, explicit unload and actual cross-family worker
exit passed. Installer tests cover cancellation, full manifests, modified-file
preservation and finite locked-partial cleanup/retry. No owner audio or global
input was used. Source regression count is 277; runtime fixture has 19 checks.

## Native scheduler compatibility repair

The official native server eagerly reserved about 1.32 GB of scheduler tensor
metadata per session for a 65,536-node capacity. Startup failed within the 3 GiB
job; a larger diagnostic budget also failed. Caller tracing identified this
owning allocation, rather than the earlier disproved zero-byte-allocation theory.
Sona keeps full graph capacity but reserves metadata lazily from actual nodes
and leaves, retaining the original conservative copy bound and checking overflow
and allocation failure. Existing buffers survive until replacement succeeds.
The repaired DLL preserves all 691 official export names/ordinals and is pinned
to `ada0590d50035510b7b4e09577ce849f7ef27ff4f43818c98b199da88839ec79`.
The installer checks both original and repaired members and all license notices.
See [patch, provenance and reproducible build](../tools/parakeet/README.md).

Local acceptance evidence: `E:/Temp/qazws/sona040-speech-final.json`, completed
per-model measurements beside it, `sona040-gpu-paired-memory.json`,
`sona040-long-tail-failure.json`, `sona040-whisper-all-clips-probe.json`,
`sona040-whisper-graphs-disabled-probe.json`, and `sona-parakeet-probe-scheduler.json`.
The negative Whisper VRAM deltas in the initial benchmark are invalid for model
attribution because its baseline still included the previous Parakeet worker;
the separate paired-memory fixture supplies the table's corrected loading deltas.

## Installed target receipt

Sona 0.4.0 was installed from qualified source
`aaeb9f2196301b46c514c5e083dcf34c23969080` with the encrypted updater; its draft
readback matched. All 35 package members and 36 CUDA runtime members matched
their receipts. Parakeet was selected and loaded by an actual `cuda:0` worker.
Only the requested Model preference changed. Amethyst, clipboard backup, 52
recognition terms, five corrections, three watched identities, 10-minute residency,
startup, tray behavior and GPU cleanup were preserved. Discord returned connected,
unpaused and Ready without owned mute/deafen changes. About/sidebar report 0.4.0;
the three compact cleanup selectors still measure 32x176 DIP.

`E:/Temp/qazws/sona040-installed-readback.json` records exact readback and the
preservation checksum; the installed settings/model screenshots were inspected.
Eight retired diagnostic/failed-build trees were removed (314673144 bytes);
accepted native source/build, original archives, benchmark/failure evidence,
every installed model and the 0.3.3 recovery installation remain. The final
documentation/screenshot commit does not change the installed product source tree.
Live personal microphone acceptance remains the owner's next Ctrl+Win exercise.
