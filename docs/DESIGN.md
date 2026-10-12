# Murmur

An original local Windows dictation utility for Codex drafts. Working name, private product.

Use native WPF on the installed .NET 8 desktop runtime. A purple, quiet window owns preferences and the current session. A nonactivating capsule shows intentional recording and local transcription. Hold Ctrl+Alt+Space, toggle Ctrl+Alt+Shift+Space, and cancel with Esc only while active. Existing Ctrl+Win dictation remains available.

Audio is 16 kHz mono PCM, held in memory while recording, limited to 120 seconds. The worker receives a task-specific temporary WAV, loads one quantized multilingual Whisper Small model, and exits after each request. A Windows job kills the worker on cancellation or app exit, with a 90 second deadline and a 1.5 GiB process memory ceiling. Six CPU threads avoid making GPU memory or a resident service a prerequisite. Temporary audio and worker output are deleted in the completion path. Recovery cleanup must use only this app's own temp root.

Clean removes a small explicit filler list, applies user replacements, and preserves protected code/paths/URLs. Verbatim keeps ASR words. Notes supports explicit paragraph commands. A snippet expands only when the entire utterance matches its trigger. Ambiguous self-corrections remain available to review. The original transcript and at most ten results stay in memory for the session, with no audio/transcript telemetry.

Insertion uses one batch of Unicode input only, into the same captured Windows field after all modifier keys are released. A focus-event epoch, window, process ID, and UI Automation runtime ID must all match. No Enter, Tab, clipboard overwrite, automatic retry, or focus activation is allowed. Line breaks and long drafts require an explicit copy until a safe multiline path is qualified. Partial OS input acceptance prevents automatic retry. Test actual Codex behavior before claiming acceptance.

The default is entirely local. No cloud endpoint, key store, sign-in, startup entry, audio feedback, or model download runs automatically. Supported OpenAI API transcription and eligible Sign in with ChatGPT text processing are separate optional adapters. Plan-covered speech eligibility is unverified; do not use another app's OAuth token or undocumented endpoints.

Hush stays owned by its existing project. Its installed 0.4.0 receipt and clean source are current; July Vault claims are stale. The new executable must be added through Hush's Apps UI and verified during an owner-started real microphone/call test. No credentials or historical Hush candidates are touched.

Acceptance requires a warning-free build, meaningful state/text regressions, synthetic/public licensed speech fixtures, measured CPU/idle/peak memory and worker exit, real native UI readback, actual Codex insertion/no submission, cancellation/focus/duplicate/device/sleep checks, owner-started microphone proof, source/license provenance, and private Git convergence. Technical proof and owner creative/physical acceptance stay distinct.
