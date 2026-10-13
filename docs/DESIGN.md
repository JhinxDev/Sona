# Sona design

October 13, 0.4.0: the existing model rows add NVIDIA Parakeet v3, supported
CPU/CUDA choices, provider-specific license/runtime metadata and measured English
results. All six Whisper choices remain. Active selection and loaded state remain
separate. Download/Use installs the needed pinned Parakeet engine explicitly;
Cancel / retry download covers retained model/runtime partials. Layout, original
provider waveform badges, theme persistence and compact cleanup controls retain
the accepted 0.3.3 design.

October 13, 0.3.3: the three TEXT CLEANUP selectors use the named SettingChoice
style: 176 DIP wide, exactly 32 DIP tall, vertically centered in 48 DIP rows,
with aligned right edges and 8 DIP above and below each control. The compact
GPU / VRAM label has a tooltip explaining NVIDIA CUDA and the CPU alternative.
Appearance changes commit only the chosen theme immediately. A failed write
restores the prior selection and palette and reports the error; unrelated
unsaved form choices still require Save preferences. General Save commits its
candidate before updating active settings, so failed form edits cannot leak
into a later theme save. Native layout/persistence proof is in VERIFICATION.md.

October 13, 0.3.2: TEXT CLEANUP is a grouped Settings section using existing
semantic brushes, Inter typography, row spacing and wrapping controls. It offers
an explicit enable switch, three formatting choices, GPU/VRAM or CPU/RAM,
separate idle residency and setup/load/unload/cancel-retry actions. Memory status
only labels GPU use after CUDA model-layer and VRAM-buffer proof, and reports
model-weight MiB separately from total memory. Its accessible name follows the
current text with a polite live-region hint. New installations default off;
the owner requested GPU processing and warm residency. Native layout is checked
at 1000x700 and 740x530 DIP without microphone capture.

October 13, 0.3.1: model rows use 20 DIP margins, a 60 DIP leading column
and an original 44 DIP rounded vector waveform badge. The provider remains
explicit text, not a borrowed official logo. Both recording displays identify
the captured destination app without reading window titles or following pointer
hover. Hold mode also recognizes two quick Ctrl+Win chords for hands-free;
the next chord finishes. Clipboard backup is explicit and defaults off for new
users. GitHub checks are opt-in, use unauthenticated public metadata only, and
show an in-app Home notice and Settings release link without automatic install.


October 13 owner additions: retain the accepted layout, add eight selectable
original palettes (Charcoal, Amethyst, Fjord, Mocha, OLED; Paper, Linen, Mist),
and bundle unmodified Inter 4.1 Regular/SemiBold/Bold under SIL OFL. Semantic
brushes update controls and native title bars live. The monochrome SVG source
is an original five-bar mark; WPF uses matching vector geometry tinted by the
theme, while Windows uses a neutral high-resolution multi-size ICO. There is
one running-assembly version source for the About/sidebar labels and agent status.

Recording display is user-selectable Panel or Capsule. Both use 28 historical
measured RMS samples with eased attack/release; silence and no-microphone previews
stay flat. No random waveform masquerades as live input. Each preserves the
nonactivating target-monitor positioning and accessible stop/cancel actions.

The owner's speed preference supersedes the older exit-after-each-utterance
design below: exactly one contained Whisper or Parakeet worker holds the selected
model until idle expiry, explicit unload, switching or quit. Default expiry is
10 minutes; zero means Keep until quit. A randomized capability route on loopback
is internal transport, with proxies disabled; the public agent surface remains
a current-user pipe. Audio is sent in memory and worker output is drained without
transcript logging. Cancellation/timeout releases the worker. Unconfirmed release
retains ownership, blocks replacement and is retried, never labeled unloaded.

The model page has compact grouped rows with an Active dot/badge for next use,
separate resident state, actual download sizes and only measured benchmark notes.
Selectable families are OpenAI Whisper through whisper.cpp and NVIDIA Parakeet
through NeMo-Speech.cpp. Other companies require a real runtime integration
before appearing as supported choices. Provider typography is original rather
than an official logo.

Owner direction, October 12, 2026: reject the working name Murmur, choose Sona, and use six supplied Spokenly screenshots as the concrete interface reference. The earlier purple/hero/tab prototype was rejected. This document governs the current independent WPF implementation.

The reference has a quiet charcoal canvas/sidebar, a single blue selected navigation row, small line icons, 20 px page titles, 14 px body text, generous page margins and flat grouped settings separated by rules. Use these proportions and hierarchy. The sidebar contains Home, Settings, Modes, Dictation Models, Transcribe File, History, Vocabulary and Snippets. Account/subscription/provider ratings are absent because Sona has no such live services. Reference source/branding/assets are not copied.

Home shows one recording status surface and an editable latest draft, with original text collapsed below it. History has its own searchable page with dated results and a truthful empty state. Settings uses Interface, Audio, Behavior, Privacy and About groups. Settings, model choice, mode choice, word/snippet forms and every displayed primary action have a real implementation. Fixed current behavior such as Dark appearance and sound Off is displayed as text rather than an inactive switch.

The native window is 1000 by 700 DIP, minimum 740 by 530. Each long page scrolls; Home can scroll when secondary details expand at the minimum. Bundled Inter supplies text; Segoe MDL2 Assets supplies native line icons. Keyboard focus uses a visible border. Primary fills preserve readable white labels in every theme. The original five-bar Sona mark uses crisp theme-aware vector geometry.

The nonactivating Capsule is 310 by at least 72 DIP; the Panel is 420 by at least 184 DIP. Both show microphone level history, elapsed time, stop and cancel, and position relative to the captured target's monitor. Preview explicitly says no microphone and closes in five seconds. Both use NOACTIVATE and WM_MOUSEACTIVATE handling; source/preview proof does not replace physical target-focus acceptance.

Default Ctrl+Win Hold has a 200 ms dwell and ends on release. Explicit Toggle starts/stops on a press; a revocable admission token survives normal release but cancellation/reset revokes it, including during target inspection. Keyboard processing is on a dedicated hook/message-loop thread. Only current down-state and current gesture tokens are held, never typed text or general key history. Legacy shortcuts fail independently. A competitor warning appears visibly rather than remaining only in the tray window.

Local recording is 16 kHz mono, at most 120 seconds. Each transcription request has a 90-second deadline; resident workers retain CPU 1.5 GiB and Turbo CUDA 3 GiB host job ceilings. GPU device allocation is separate from host job memory. The selected model stays resident until configured inactivity, switching, explicit unload or quit. Default history is memory-only; explicitly saved text has a 200-record/16 MiB load limit, no audio, atomic replacement, and malformed existing files are preserved.

Insertion retains the original captured field identity, focus epoch/window/process/runtime ID and released modifier requirements. It uses Unicode input without Enter, Tab, clipboard overwrite or automatic retry. Focus drift preserves the draft. Multiline/large text uses explicit copy. Owner-started physical key/microphone, Codex delivery, Hush call and creative acceptance are distinct required proof.
