# Third-party notices

Murmur is private owner source. Its original interface and code were developed independently. Installed Wispr Flow and Spokenly were owner-authorized reference targets only; none of their proprietary implementation or assets is distributed here.

- [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1): MIT, copyright Mark Heath and contributors. The license text is under `licenses/NAudio.txt` and copied into the application package.
- [whisper.cpp](https://github.com/ggml-org/whisper.cpp): MIT, copyright Georgi Gerganov and contributors. Its pinned official engine is a separate local runtime download; `licenses/whisper.cpp.txt` retains the license.
- [OpenAI Whisper](https://github.com/openai/whisper): model/code MIT, copyright OpenAI. The quantized model comes from the [ggerganov whisper.cpp model repository](https://huggingface.co/ggerganov/whisper.cpp). License retained in `licenses/Whisper.txt`.
- .NET desktop runtime/SDK: Microsoft official distribution and its included notices. Murmur does not modify or relicense it.
- Pillow is an optional developer tool for recreating Murmur's original icon; it is not an application dependency.

The French evaluation audio is credited in `docs/VERIFICATION.md` and is not bundled in the application or source repository. Synthetic fixtures contain no owner speech.
