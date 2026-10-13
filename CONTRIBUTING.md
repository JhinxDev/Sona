# Contributing to Sona

This is an early Windows app. Feature suggestions, reproducible bugs and small
focused contributions are welcome through GitHub issues and pull requests.

For a bug, include the Sona version, Windows version, selected model, CPU/CUDA
choice and a brief reproduction. Say whether it happened during live dictation,
file transcription, cleanup or Discord protection. Share synthetic sample text
or audio where possible. Keep tokens, private transcripts and account/profile
files out of reports.

Use the [README](README.md) to provision a separate runtime and build. Keep
weights, downloads, recordings, NuGet caches and generated renders outside Git.
Respect the app's original design in [docs/DESIGN.md](docs/DESIGN.md) and preserve
upstream notices for runtime/dependency changes.

After a source change, run the relevant core checks and affected native checks.
Explain what was tested and what still needs a real microphone or target app.
Unattended tests must use an isolated profile, never activate the owner's
microphone or send a message. [Preview validation](docs/PREVIEW-VALIDATION.md)
lists known gaps; a build or fixture replay does not close those gaps.

Keep pull requests focused and describe the resulting behavior. Contributions
to original Sona source are made under its [MIT license](LICENSE). Separately
downloaded engines, model weights and bundled third-party components retain
their own licenses, as listed in [the notices](THIRD-PARTY-NOTICES.md).
