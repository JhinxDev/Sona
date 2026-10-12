# Murmur

Private native Windows dictation for Codex. Hold **Ctrl+Alt+Space**, speak, then release to make an unsent draft. **Ctrl+Alt+Shift+Space** toggles recording. **Esc** cancels recording, transcription or pending insertion, including while modifiers are held.

The first candidate uses local multilingual Whisper Small, an editable original/cleaned draft, technical vocabulary and explicit snippets. Models run only for transcription and exit afterward. No account, cloud requests, analytics, automatic microphone capture, notification sound or automatic message submission.

## Use

1. Open Murmur from the Windows Start menu. Recording starts only from its button or your shortcut.
2. Focus an editable Codex composer, hold Ctrl+Alt+Space and speak. Keep focus in that field until the result arrives. Focus changes keep the transcript in Murmur.
3. Review your draft before sending. To insert a ready draft manually, focus its destination and press Ctrl+Alt+Shift+V. Already inserted drafts cannot be inserted again automatically.

Vocabulary accepts exact terms on their own lines, or `spoken phrase = exact spelling` replacements. Snippets use `trigger = expansion` and expand only when the entire utterance matches. Clean makes conservative explicit edits; Verbatim preserves words; Notes supports explicit paragraph commands. Natural self-corrections remain visible for review. Multiline drafts require explicit copy.

Closing keeps shortcuts available in the tray. Use tray **Quit** to stop Murmur. Windows startup is opt in. Add `Murmur.exe` through Hush's Apps page if microphone ducking is wanted, then verify an owner-started call.

## Build and install

Run `tools/setup-runtime.ps1 -BuildTools` to obtain pinned, hash-checked official engine/model/SDK files in `E:/DevelopmentTools/local-dictation`. This explicit setup downloads about 181 MiB of model data plus the engine and optional SDK; the app never downloads automatically. The installed .NET 8 desktop runtime is required.

Run `tools/build.ps1` for the build, Core and Windows lifetime checks, and dependency audit. `tools/native-smoke.ps1 -ReplayFile <fixture.wav>` checks the real app's accessible controls, saved vocabulary/relaunch and active-worker quit without a microphone or global input. Close Murmur before that isolated test. `tools/prepare-release.ps1` creates a versioned package with source identity and file hashes. `tools/install.ps1` verifies that package and installs only its versioned files and Start-menu shortcut.

## Current proof and limits

The initial candidate passed 24 Core, 17 Windows and 10 native smoke checks. Short English fixtures and one short licensed French phrase matched their expected words; English transcription took about 3.2 seconds and a worker used about 367 MiB on the owner's Ryzen 9 7900X. These are narrow fixture measurements. Personal French/mixed-language speech, noise, actual Codex insertion, global shortcut races, physical device/sleep behavior and Hush calls require the [owner exercise](tools/owner-test.md). The initial interface still needs owner creative acceptance.

Insertion uses a checked Windows focus target and Unicode input. Windows input is global, so focus inspection cannot atomically bind delivery to one field. OS acceptance does not prove the destination retained every character. Keep your draft available and verify actual Codex behavior. The clipboard is untouched unless **Copy draft** is deliberately used.

Recordings stay in memory until local transcription needs a temporary WAV. Audio/result files are deleted before the recovery marker. Cleanup failure keeps the app open with a visible warning and permits Quit retry. Raw and cleaned transcripts stay in session memory only. See [verification](docs/VERIFICATION.md), [reference analysis](docs/REFERENCE-BEHAVIOR.md) and [third-party notices](THIRD-PARTY-NOTICES.md).

Optional cloud speech and local AI rewriting are unimplemented. ChatGPT subscription-covered text inference does not establish eligibility for speech transcription in another app. Any later adapter must use its own supported authentication and explicit cost consent.
