# Sona

Private Windows dictation for unsent Codex drafts. Hold **Ctrl + Windows** while speaking and release to finish. In Settings, choose **Toggle** to press the same shortcut once to start and again to finish. **Esc** cancels. The microphone opens only after your intentional recording action.

Sona 0.1.1 replaces the working name Murmur. Its dark sidebar, blue selection and grouped settings follow the owner's supplied Spokenly screenshots. Home owns the current draft; Settings, Modes, Dictation Models, Transcribe File, History, Vocabulary and Snippets have dedicated pages. The compact voice capsule appears during recording/transcription without activating its window. Closing the main window keeps Sona in the tray by default; tray Quit performs cleanup and exits.

## Use

1. Open **Sona** from Windows Start. Focus an editable Codex composer before recording.
2. Hold Ctrl + Windows for at least 200 ms, speak, then release both keys. Keep the original field focused until the draft is ready. For Toggle, select that mode and Save preferences first.
3. Review the inserted text before sending. Sona sends no Enter or Tab. If the field changed, the transcript stays in Sona. Copy draft is an explicit action; a ready draft can also use Ctrl+Alt+Shift+V in its intended field.

Quit Spokenly or Wispr Flow before using the shared shortcut. A visible capsule explains a detected competitor conflict. Failure to reserve an alternate shortcut does not disable the independent Ctrl+Win listener. Settings reports listener initialization. Actual physical key/input behavior still needs the [owner test](tools/owner-test.md).

Clean applies conservative explicit fillers, vocabulary and exact whole-utterance snippets. Verbatim retains recognized words. Notes supports spoken paragraph commands. Vocabulary has separate recognition terms and heard-as corrections; snippets have add/edit/delete forms and lossless bulk import. Original text remains available. Natural self-correction and personal mixed-language accuracy are unproven.

History searches original and cleaned text, has a 200-entry cap and stays in session memory by default. **Keep text history on this device** explicitly enables local text storage. Disabling saving leaves earlier saved records until Clear history is used. Clear history removes the exact history file and preserves the current draft. Audio is never retained as history.

Transcribe File accepts 16 kHz, 16-bit mono PCM WAVs up to two minutes and 4 MB. It never inserts automatically. All local engines unload after each utterance. GPU Auto uses installed Whisper Large v3 Turbo CUDA; explicit Lightweight CPU uses Whisper Small. The app downloads nothing automatically and has no account, cloud requests, analytics, sound effects or automatic submission.

## Build and install

`tools/setup-runtime.ps1 -BuildTools` explicitly obtains hash-checked engine/model/SDK files in `E:/DevelopmentTools/local-dictation`. `tools/setup-cuda-runtime.ps1` explicitly prepares the separately pinned official CUDA engine, NVIDIA cuBLAS runtime and Turbo model. The .NET 8 desktop runtime is required. No driver, service or global PATH changes are made.

`tools/build.ps1` builds and runs Core/Windows lifetime checks plus the NuGet advisory query. `tools/native-smoke.ps1` uses isolated data, disabled microphone/hotkeys and a 180-second test lifetime; it can run alongside an owner instance. Optional `-ReplayFile` uses a licensed fixture. `tools/prepare-release.ps1` requires a reviewed clean source commit and makes a source-identified package with hashes. `tools/install.ps1` requires both Sona and legacy Murmur to be stopped, verifies files, creates Sona's Start shortcut, and removes only a receipt/hash-verified legacy shortcut. Recovery versions and existing preferences stay intact.

Private source: https://github.com/JhinxDev/local-dictation, branch `codex/local-dictation`. Internal source/namespace and data directory names retain Murmur for compatibility. Startup stays opt in; only receipt-verified application-owned legacy Run entries can migrate. Add **Sona.exe** through Hush's existing Apps page before an owner-started real call test.

## Proof and limits

The Sona build has 113 Core checks and 19 Windows checks. Native UI coverage verifies forms, settings, history search/clear/relaunch, no-mic capsule preview and harmless prepared insertion text. The licensed 8 English/8 French FLEURS corpus, measured before the shell rename with unchanged ASR settings, gave Turbo CUDA 9.91% French and 5.77% English word error rates, median 0.9025/0.81 seconds respectively. These are finite public-corpus measurements, not personal speech acceptance.

Windows global input cannot atomically bind focus checking to a single field; OS acceptance alone does not prove retained destination text. Actual Codex insertion, physical Hold/Toggle/Esc, bilingual microphone speech, focus/device/sleep behavior, Hush calls and owner visual acceptance remain open. Optional cloud speech and AI rewriting are unimplemented. See [verification](docs/VERIFICATION.md), [design](docs/DESIGN.md), [reference findings](docs/REFERENCE-BEHAVIOR.md) and [third-party notices](THIRD-PARTY-NOTICES.md).
