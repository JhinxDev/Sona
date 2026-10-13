# Sona design

Owner direction, October 12, 2026: reject the working name Murmur, choose Sona, and use six supplied Spokenly screenshots as the concrete interface reference. The earlier purple/hero/tab prototype was rejected. This document governs the current independent WPF implementation.

The reference has a quiet charcoal canvas/sidebar, a single blue selected navigation row, small line icons, 20 px page titles, 14 px body text, generous page margins and flat grouped settings separated by rules. Use these proportions and hierarchy. The sidebar contains Home, Settings, Modes, Dictation Models, Transcribe File, History, Vocabulary and Snippets. Account/subscription/provider ratings are absent because Sona has no such live services. Reference source/branding/assets are not copied.

Home shows one recording status surface and an editable latest draft, with original text collapsed below it. History has its own searchable page with dated results and a truthful empty state. Settings uses Interface, Audio, Behavior, Privacy and About groups. Settings, model choice, mode choice, word/snippet forms and every displayed primary action have a real implementation. Fixed current behavior such as Dark appearance and sound Off is displayed as text rather than an inactive switch.

The native window is 1000 by 700 DIP, minimum 740 by 530. Each long page scrolls; Home can scroll when secondary details expand at the minimum. Segoe UI and Segoe MDL2 Assets supply native text/line icons. Keyboard focus uses a visible border. Blue primary buttons use a darker fill for readable white text. The existing original waveform icon is retained under the new Sona identity.

The nonactivating 310 by 60 DIP capsule shows a microphone icon, live measured level, elapsed time, stop and cancel. It positions relative to the captured target's monitor. Preview explicitly says no microphone and closes in five seconds. It uses NOACTIVATE and WM_MOUSEACTIVATE handling; source/preview proof does not replace physical target-focus acceptance.

Default Ctrl+Win Hold has a 200 ms dwell and ends on release. Explicit Toggle starts/stops on a press; a revocable admission token survives normal release but cancellation/reset revokes it, including during target inspection. Keyboard processing is on a dedicated hook/message-loop thread. Only current down-state and current gesture tokens are held, never typed text or general key history. Legacy shortcuts fail independently. A competitor warning appears visibly rather than remaining only in the tray window.

Local recording is 16 kHz mono, at most 120 seconds. Workers have a 90-second deadline, CPU 1.5 GiB and Turbo CUDA 3 GiB host job ceilings. GPU device allocation is separate from host job memory and is sampled as device-wide evidence. Each worker exits after its utterance. Default history is memory-only; explicitly saved text has a 200-record/16 MiB load limit, no audio, atomic replacement, and malformed existing files are preserved.

Insertion retains the original captured field identity, focus epoch/window/process/runtime ID and released modifier requirements. It uses Unicode input without Enter, Tab, clipboard overwrite or automatic retry. Focus drift preserves the draft. Multiline/large text uses explicit copy. Owner-started physical key/microphone, Codex delivery, Hush call and creative acceptance are distinct required proof.
