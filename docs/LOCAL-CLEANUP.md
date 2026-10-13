# Local text cleanup

Sona 0.3.2 optionally runs Spokenly Cleanup 1 after Whisper. It cleans up
punctuation, filler words, repeated phrases and spoken corrections while keeping
the original transcript available in Home. It runs locally without an account,
API key or cloud inference. New installations start with this feature off.

In Settings → Text cleanup, choose GPU / VRAM for NVIDIA CUDA or CPU / RAM,
then Set up selected backend. Setup explicitly downloads the model and the
pinned portable runtime. Enable cleanup, choose a formatting style, and Save
preferences. Polished uses formal punctuation; Casual keeps a conversational
style; Lowercase uses the model's very casual style. Verbatim and exact snippets
bypass the model. Voice dictation and local WAV transcription share this stage.

GPU preference uses an installed CUDA runtime first. With only the CPU runtime
installed it uses CPU; a CUDA failure does not silently switch engines. Memory
status confirms actual CUDA model layers and a GPU model buffer before showing
GPU use. GPU processing still uses system RAM for the process, mapped files and
host buffers. Displayed VRAM is model weights, not total GPU allocation.

Cleanup has its own 10-minute default idle timeout. Choose 1, 5, 10 or 30 minutes,
or Keep until quit. Load/Unload are explicit actions. A backend change, disabling
cleanup, Verbatim, cancellation and quit release its contained worker. Downloads
are kept for reuse. When an idle model unloads, the next dictation loads it again.

## Preservation and limits

Project vocabulary, backtick code, URLs, paths and identifiers are replaced with
protected placeholders. All placeholders must survive exactly once and in order
before restoration. Bounds and narrow negation/instruction checks reject prompt
leakage, missing terms and some meaning changes. Rejected text keeps the original
wording; runtime failure uses basic cleanup. Esc cancellation cannot commit a
late result. Home always retains the original transcript and Use original.

These checks are conservative rather than comprehensive semantic verification.
The earlier CPU study observed a mixed-language `cloud` → `Claude` change;
`cloud` is now protected, and the production synthetic case preserved it. This
does not establish that all names, numbers or meaning will always be correct.

Input is bounded to 4000 characters and 2500 prompt tokens, with a 4096-token
context and 1536-token output reserve. Output is capped at 6000 characters.
Each request has a 20-second deadline; startup has a 45-second deadline. CPU
and GPU workers have finite job memory limits and remain contained by Sona.
No transcript is exposed by the agent interface or written to runtime logs.

## Qualified hardware fixture, October 13, 2026

One run of the production cleanup pipeline on synthetic text, Ryzen 9 7900X and
RTX 4080 Super. Eleven short cases covered fillers, spoken correction, negation,
project names, numbers, code/paths, French, mixed language, instruction-like text
and a question. All were accepted with protected content intact. Casual and
Lowercase were also exercised. This is cleanup time only, excluding microphone
recording, Whisper transcription and final insertion.

| Measurement | Observed |
| --- | --- |
| Short GPU cleanup | 41–98 ms |
| 134-word GPU paragraph | 373 ms |
| GPU preparation including integrity checks | 2.27 s, filesystem cache warm |
| GPU model-weight buffer | 604.15 MiB |
| GPU worker peak host working set | 1115.65 MiB |
| One explicit CPU comparison sentence | 214 ms |

Resident reuse, in-flight/queued cancellation, idle expiry, Keep until quit,
disable and GPU-to-CPU release passed the same actual-runtime fixture. No
microphone, owner transcript or global input was used. Personal speech quality
and live end-to-end delay remain owner acceptance checks. The earlier repeated
CPU study and its failures are preserved in [CLEANUP-RESEARCH.md](CLEANUP-RESEARCH.md).

## Pinned assets and agent controls

- Model: Spokenly Cleanup 1 Q8, official GGUF revision
  `79936c95e5c0ea9162ae7a435e64cb3127020e36`, 639442784 bytes,
  SHA256 `66509e0fe1578d2ac4b0773c79a6c83e8d6f218618dfe11e4532beacd999b6e8`.
- Runtime: official llama.cpp `b11583`, Windows x64 CPU or CUDA 12.4, with
  pinned archive hashes and verified file manifests. No driver/global toolkit.
- Licenses: model/Qwen3 Apache 2.0, llama.cpp MIT, runtime notices retained,
  and NVIDIA's separate CUDA terms. See [third-party notices](../THIRD-PARTY-NOTICES.md).

```powershell
sona-agent settings patch '{"cleanupEnabled":true,"cleanupEngine":"Auto","cleanupTone":"formal","cleanupIdleMinutes":10}'
sona-agent cleanup install
sona-agent cleanup status
sona-agent cleanup load
sona-agent cleanup unload
sona-agent cleanup cancel
```

CLI and MCP change the same bounded settings as the UI. Setup is explicit and
cancellable; differing existing files are preserved. Cancel while idle retries
retained setup cleanup. [Agent interface](AGENT-INTERFACE.md).
