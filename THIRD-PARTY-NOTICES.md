# Third-party notices

Sona original source is licensed under MIT; the repository currently remains a private preview. Its original interface and code were developed independently. Installed Wispr Flow and Spokenly were owner-authorized reference targets only; none of their proprietary implementation or assets is distributed here.

- [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1): MIT, copyright Mark Heath and contributors. The license text is under `licenses/NAudio.txt` and copied into the application package.
- [whisper.cpp](https://github.com/ggml-org/whisper.cpp): MIT, copyright Georgi Gerganov and contributors. Its pinned official engine is a separate local runtime download; `licenses/whisper.cpp.txt` retains the license.
- [OpenAI Whisper](https://github.com/openai/whisper): model/code MIT, copyright OpenAI. The quantized model comes from the [ggerganov whisper.cpp model repository](https://huggingface.co/ggerganov/whisper.cpp). License retained in `licenses/Whisper.txt`.
- .NET desktop runtime/SDK: Microsoft official distribution and its included notices. Sona does not modify or relicense it.
- NVIDIA CUDA 11.8/cuBLAS 11.11.3.6: separate optional private local runtime from NVIDIA's official redistribution. Its full NVIDIA SDK agreement remains beside the downloaded runtime as `whisper-cuda/Release/LICENSE`; no driver or global toolkit is installed. The CUDA DLLs and model are not bundled in Sona's application package or source.
- Pillow is an optional developer tool for recreating Sona's original icon; it is not an application dependency.
- [Inter 4.1](https://github.com/rsms/inter/releases/tag/v4.1): SIL Open Font License 1.1, copyright Rasmus Andersson. Unmodified static Regular/SemiBold/Bold are embedded application resources. The full license and exact source/hash provenance ship under `Assets/Fonts`. No system font is installed.

The French evaluation audio is credited in `docs/VERIFICATION.md` and is not bundled in the application or source repository. Synthetic fixtures contain no owner speech.
