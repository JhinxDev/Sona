# Cleanup model evaluation, October 15, 2026

Retain **Spokenly Cleanup 1 Q8** as Sona's local cleanup model. Two larger
Qwen3.5-based alternatives ran successfully on the owner's RTX 4080 SUPER, but
both introduced meaning changes and took about 2.6 to 2.9 times longer on short
requests. Ochre improved one repeated French correction; that isolated gain does
not justify its regressions in mixed language and long text. This is the best
tested choice for this bounded English/French dictation fixture, not a claim
about every model or speaker. No production weights, model setting or installed
application were changed by this evaluation.

## Pinned candidates and sources

All three weight files have Apache-2.0 model licenses. The candidates are external
downloads, with their notices retained beside the private evaluation evidence;
none is bundled with Sona. Spokenly's official GGUF card still points to the same
October 13 revision when checked on October 15. Its small dictation fine-tune
explicitly includes French. [Official model card](https://huggingface.co/spokenly/cleanup-1-gguf).

| Model | Exact repository revision | File | Bytes | SHA256 |
| --- | --- | --- | ---: | --- |
| Spokenly Cleanup 1 | `79936c95e5c0ea9162ae7a435e64cb3127020e36` | `cleanup-1-q8_0.gguf` | 639442784 | `66509e0fe1578d2ac4b0773c79a6c83e8d6f218618dfe11e4532beacd999b6e8` |
| Ochre Refine 4B v4 | `5787a89e6e6f8fe182359a36f138faeebf47a536` | `ochre-refine-4b-v4-Q4_K_M.gguf` | 2708804480 | `baed73b8c2db25121abcc36a38efdade18020243d8250455b0621b8af5d0c485` |
| Qwen3.5-4B, Unsloth Q6_K conversion | `e87f176479d0855a907a41277aca2f8ee7a09523` | `Qwen3.5-4B-Q6_K.gguf` | 3525956768 | `fdedd781c9ce676ab66b018ca247ff78e8a33c98098a822c1e2d5075e7718f66` |

Ochre is a task-specific fine-tune. Its author recommends raw ChatML completion
with an empty thinking seed and provides its own evaluation. That author-reported
result is separate from Sona's measurements below. The published prompt was
read at source revision `bee87ad5ac4600a4dd6c56814b4b187c1c3a1259`; the local
trial added only Sona's placeholder-preservation instruction.
[Model card](https://huggingface.co/polonuim210/ochre-refine-4b),
[prompt source](https://github.com/jordan-gibbs/ochre/blob/bee87ad5ac4600a4dd6c56814b4b187c1c3a1259/crates/ochre-refine/src/prompts.rs).

The other candidate is the general Qwen3.5-4B post-trained model, using Unsloth's
Q6_K conversion and an independently written conservative cleanup prompt. Its
general multilingual capability is a reason to test it, not cleanup acceptance.
The official base revision is `851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a`.
[Conversion](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF),
[official base and license](https://huggingface.co/Qwen/Qwen3.5-4B/tree/851bf6e806efd8d0a36b00ddf55e13ccb7b8cd0a).

## Measured scope

Windows x64, Ryzen 9 7900X, RTX 4080 SUPER 16 GB, driver 617.42. About 11 GB of
GPU memory was free at initial inspection. All runs reused Sona's existing
official llama.cpp b11583 CUDA 12.4 runtime, commit `988015788`. No SDK, driver,
global runtime, live microphone, owner transcript, foreground input or live
Sona preferences were used or changed.

Thirty fixed synthetic cases cover English, French, mixed technical language,
Codex/Discord and project names, literal code/path/URL preservation, numbers,
questions and instruction-like dictation, false starts, self-corrections, clean
text, Montréal wording, and two long paragraphs. The corpus contains written
expectations for human assessment; it is not an automatic semantic quality score.
`Sona.CleanupEvaluation` prepares protected prompts and replays every output
through the actual `CleanupProcessor`, `CleanupRules`, and `TextPipeline`.

Each model used one resident worker and three sequential greedy requests per
case, no prompt-cache reuse, six CPU threads, 4096 context tokens, at most 2500
input tokens and 1536 output tokens, and a 20-second request timeout. All 90
outputs per model were deterministic and untruncated. Every measured worker
exited; each finite supervisor reported zero remaining job processes. Model
hashing happened before the process readiness timer. The filesystem cache was
warm. Timings cover the raw /completion request. They exclude prompt preparation,
tokenization, preservation replay, speech recognition and insertion.

| Measurement | Spokenly Q8 | Ochre v4 Q4_K_M | Qwen3.5-4B Q6_K |
| --- | ---: | ---: | ---: |
| Median of 28 short-case medians | 75.87 ms | 200.26 ms | 222.94 ms |
| Short-case median range | 33.29-124.85 ms | 127.84-344.09 ms | 124.58-344.76 ms |
| English long paragraph | 437.01 ms | 1113.84 ms | 1187.77 ms |
| French long paragraph | 412.33 ms | 1002.09 ms | 1325.64 ms |
| Process ready, cache warm | 993.96 ms | 2149.32 ms | 2261.16 ms |
| CUDA model-weight buffer | 604.15 MiB | 2572.86 MiB | 3352.15 MiB |
| Peak host working set | 1254.52 MiB | 3222.75 MiB | 4064.98 MiB |
| Cases accepted by preservation checks | 29/30 | 28/30 | 29/30 |

Preservation acceptance is not semantic correctness. Additional CUDA buffers and
other applications consume memory beyond the weight buffer. The experiment did
not measure CPU-only candidate speed, game contention, disk-cold startup,
near-4000-character input, live microphone quality, or owner acceptance.

## Material findings

- Both larger models changed the long English paragraph's relationship between
  focus and clipboard fallback. They attached the focus-change condition to
  model residency instead. The preservation checks accepted that meaning change.
  Spokenly retained the relationship in this case.
- General Qwen reversed the explicit Tuesday-to-Wednesday and Alice-to-Ben
  corrections, returning Tuesday rather than Wednesday and Alice rather than
  Ben. It also left several false starts and French fillers intact. These are
  decisive cleanup regressions despite successful model execution.
- Ochre translated the English clause of mixed French/English dictation into
  French; Qwen translated both clauses into English. The existing negation
  safeguard rejected both outputs and kept the original.
- Ochre invented Chrome for the uncertain `codecks`. Its output also lost a
  protected repeated name, so Sona rejected that result. An uncertain name
  should instead be supplied through recognition hints or an explicit correction.
- Ochre resolved the French repeated correction to Thursday at 9. Spokenly and
  general Qwen left all alternatives in place. This remains a baseline limitation.
- Spokenly mishandled punctuation around a requested literal instruction,
  turning its scope into separate actions. The current checks accepted it. The
  original remains necessary for review; this study does not establish complete
  semantic protection for the retained model.
- Spokenly and Ochre both lost a repeated protected-name placeholder in the
  correction fixture, producing a preserved original rather than a polished
  result. General Qwen kept both placeholders but left the correction unresolved.
- Spokenly retained `pis` in the Montréal example; both larger models replaced
  it with `et` while changing additional phrasing.

No candidate passed a stronger preservation/quality tradeoff than the existing
model. No replacement selector, new download default or broader production
rewrite was added. [Current setup and safeguards](LOCAL-CLEANUP.md) remain the
production contract.

## Reproduce without downloads

The maintained sources are `tools/fixtures/cleanup-evaluation.json`,
`tests/Sona.CleanupEvaluation`, `tools/cleanup_evaluation.py` and
`tools/test_cleanup_evaluation.py`. The runner requires Python 3.11+ on Windows
x64, exact caller-supplied absolute paths, a model digest and the existing runtime
integrity manifest. It verifies every manifest file, refuses differing files and
existing receipts, caps each worker process at 8 GiB, admits the hidden supervisor
while suspended to its own kill-on-close Windows job, and writes a separate
`.job.json` cleanup receipt. It makes only authenticated loopback inference calls.

Set the isolated SDK environment as in the README. Supply your own already
verified runtime, model and fresh evidence directory:

```powershell
$sonaSdk = 'C:\absolute\runtime\sdk\dotnet.exe'
$sonaPython = 'C:\absolute\python\python.exe'
$sonaEvidence = 'C:\absolute\new-evidence-directory'
$sonaCleanupRuntime = 'C:\absolute\cleanup\runtime-cuda'
$sonaCleanupModel = 'C:\absolute\cleanup\cleanup-1-q8_0.gguf'
$sonaCorpus = (Resolve-Path '.\tools\fixtures\cleanup-evaluation.json').Path
$sonaPrepared = Join-Path $sonaEvidence 'prepared.json'
$sonaMeasured = Join-Path $sonaEvidence 'measured.json'
$sonaRestored = Join-Path $sonaEvidence 'restored.json'

& $sonaSdk run --project .\tests\Sona.CleanupEvaluation -c Release --disable-build-servers -p:UseSharedCompilation=false -- prepare $sonaCorpus $sonaPrepared
& $sonaPython .\tools\cleanup_evaluation.py --runtime-dir $sonaCleanupRuntime --runtime-manifest (Join-Path $sonaCleanupRuntime 'integrity.json') --model $sonaCleanupModel --model-sha256 66509e0fe1578d2ac4b0773c79a6c83e8d6f218618dfe11e4532beacd999b6e8 --model-id spokenly/cleanup-1-gguf --model-revision 79936c95e5c0ea9162ae7a435e64cb3127020e36 --backend GPU --prompt-format plain --prepared-cases $sonaPrepared --output $sonaMeasured
& $sonaSdk run --project .\tests\Sona.CleanupEvaluation -c Release --disable-build-servers -p:UseSharedCompilation=false -- restore $sonaMeasured $sonaRestored
& $sonaPython .\tools\test_cleanup_evaluation.py
```

The helper never starts the Sona UI. Model alternatives use `--prompt-format
chatml --system-prompt C:\absolute\prompt.txt`, with a separately reviewed prompt
file and fresh output paths. The raw prompt ends with an empty thinking seed;
there is no thinking generation or general chat API. Read every output against
the corpus expectation and original input before deciding to promote a model.

## Evidence and verification

Private owner-machine receipts are under
`E:/Temp/qazws/sona-personalization/cleanup`. The initial actual comparison used
the isolated `benchmark.py` and native prototype, with the same 30 input strings.
The maintained runner was then separately executed against the actual baseline;
every initial completion was replayed through the maintained native helper.
Historical raw measurements remain unchanged.

| Initial measurement | SHA256 |
| --- | --- |
| `spokenly-results.json` | `90bdda90a9e858987a89ceb36b801ecca3f27695e89a06d8b5d9982ab88ea216` |
| `ochre-v4-results.json` | `d50e7df5e735541496f1b39aed371c8c939c20a040ad6c0e67a0d9969e021909` |
| `qwen35-4b-q6-results.json` | `1d605209cc44394562392a1ae1ae6d98899e757634c8e780f51a6e56032662ac` |

`*-utf8-all-variants-restored.json` contains all three outputs per case replayed through
Sona.Core. `qualified-spokenly-results.json` and its `.job.json` qualify the final
maintained runner against the installed assets. `downloads.json` pins candidate
download identities; the baseline file was reused, not overwritten. The original
comparison began from source `825538530c5d4e4293a6dea3a3d398023e8d219d`.
The maintained fixture SHA256 is
`ac4f8a9bec3bcab5781b3c6aa5141d9e141f893de47365b1f74064615bc33814`.

Focused verification: the separate .NET helper built and prepared 30 cases;
all 270 original completions replayed through Sona.Core; the maintained baseline
run completed 30 cases with three repeats and zero residual owned workers;
nine evaluator boundary checks passed, including a forced worker-and-descendant
timeout. No production model code changed, so this work did not repeat the
unmodified microphone/UI/production cleanup acceptance suites.
