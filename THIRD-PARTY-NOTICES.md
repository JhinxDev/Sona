# Third-party notices

Sona original source is licensed under MIT and is published as an early source preview. Its original interface and code were developed independently. Installed Wispr Flow and Spokenly were owner-authorized interface references; none of their proprietary implementation or artwork is distributed here. Spokenly's separately published, openly licensed cleanup model is used with attribution below. The MIT license does not relicense the third-party components or separately downloaded model weights listed here.

- [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1): MIT, copyright Mark Heath and contributors. The license text is under `licenses/NAudio.txt` and copied into the application package.
- [whisper.cpp](https://github.com/ggml-org/whisper.cpp): MIT, copyright Georgi Gerganov and contributors. Its pinned official engine is a separate local runtime download; `licenses/whisper.cpp.txt` retains the license.
- [OpenAI Whisper](https://github.com/openai/whisper): model/code MIT, copyright OpenAI. The quantized model comes from the [ggerganov whisper.cpp model repository](https://huggingface.co/ggerganov/whisper.cpp). License retained in `licenses/Whisper.txt`.
- [NVIDIA Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3): CC-BY-4.0, NVIDIA. The official Q8_0 GGUF is a separately downloaded model. Immutable revision, file size and SHA256 are pinned in ModelCatalog and docs/PARAKEET.md. Sona executes the model locally and does not relicense its weights.
- [NeMo-Speech.cpp 0.2.0](https://github.com/NVIDIA/NeMo-Speech.cpp/releases/tag/v0.2.0): Apache-2.0, NVIDIA CORPORATION & AFFILIATES. Official Windows CPU/CUDA archives are downloaded separately with their entire bundled license/NOTICE inventory retained. CUDA dependencies remain subject to those archive notices.
- Parakeet's bundled `ggml-base.dll` scheduler compatibility repair: ggml MIT plus NVIDIA Apache-2.0 patches and Sona's MIT modification. Full licenses, modification notice and exact binary/source provenance ship in `Assets/Parakeet`; the source patch and rebuild instructions are in `tools/parakeet`. This DLL is distinct from the original upstream release member and is independently hash-pinned.
- .NET desktop runtime/SDK: Microsoft official distribution and its included notices. Sona does not modify or relicense it.
- NVIDIA CUDA 11.8/cuBLAS 11.11.3.6: separate optional local runtime from NVIDIA's official redistribution. Its full NVIDIA SDK agreement remains beside the downloaded runtime as `whisper-cuda/Release/LICENSE`; no driver or global toolkit is installed. The CUDA DLLs and model are not bundled in Sona's application package or source.
- Pillow is an optional developer tool for recreating Sona's original icon; it is not an application dependency.
- [Inter 4.1](https://github.com/rsms/inter/releases/tag/v4.1): SIL Open Font License 1.1, copyright Rasmus Andersson. Unmodified static Regular/SemiBold/Bold are embedded application resources. The full license and exact source/hash provenance ship under `Assets/Fonts`. No system font is installed.
- [Spokenly Cleanup 1](https://huggingface.co/spokenly/cleanup-1) and its [official Q8 GGUF](https://huggingface.co/spokenly/cleanup-1-gguf): Apache License 2.0. A fine-tune of [Qwen3-0.6B-Base](https://huggingface.co/Qwen/Qwen3-0.6B-Base), also Apache 2.0. The model is a separate explicit runtime download; the full license is retained in `licenses/Spokenly-Cleanup-1.txt`. Model revision, bytes and hash are pinned in `CleanupAssets` and documented in `docs/LOCAL-CLEANUP.md`.
- [llama.cpp b11583](https://github.com/ggml-org/llama.cpp/releases/tag/b11583): MIT, copyright Georgi Gerganov and contributors. The pinned CPU/CUDA runtime is downloaded separately and retains its bundled notices; `licenses/llama.cpp.txt` contains the upstream MIT license. Its Windows runtime includes `LICENSE-LLVM-OpenMP`, retained beside the runtime.
- NVIDIA CUDA 12.4/cuBLAS: optional portable cleanup runtime dependencies from the pinned official llama.cpp release assets. They remain subject to the [NVIDIA CUDA 12.4 agreement](https://docs.nvidia.com/cuda/archive/12.4.0/eula/index.html), independently of Sona's MIT source. No NVIDIA driver/global toolkit is installed, and CUDA DLLs are not bundled in the Sona app/source package.

The English/French FLEURS evaluation audio is CC-BY-4.0, credited in `docs/VERIFICATION.md`, and is not bundled in the application or source repository. Synthetic fixtures contain no owner speech.


## GitHub Octicons mark

The sidebar uses GitHub's `mark-github-16` vector path from
https://github.com/primer/octicons (retrieved October 15, 2026), under the MIT
license. Copyright (c) 2026 GitHub Inc. The license is included in
`licenses/Octicons-MIT.txt`. The mark identifies the linked repository and does
not imply GitHub endorsement. Starring is performed only by the visitor on GitHub.
