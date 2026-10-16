# Current 0.4.3 design

October 16 reliability changes add specific retained-draft capture guidance and
preserve pending native settings when an agent patch conflicts. Existing themes,
layout and recording geometry remain as described below.

The dated sections below preserve design history. Where they differ, the current
October 15 implementation takes precedence: seven Settings sections, compact
304 DIP model cards grouped by family, one GitHub sidebar link, optional sound
feedback, a 272 DIP capsule and 320 DIP panel. The capsule waveform is 176 × 26
DIP and the panel waveform is 36 DIP tall. Recent transcript text is bounded;
Open draft and the provisional stage label appear only in the panel. Recording
uses a 7/8 DIP semantic red dot with a gentle 1.8-second opacity breath (static
under reduced motion/high contrast), a Recording label and neutral square Stop.
The transcript uses an Inset brush, radius 10 and padding 8 horizontal/7 vertical.
Waveform levels use a -54 to -12 dB scale, 60 ms samples and 35/95 ms attack/release.
This replaces the earlier linear mapper that saturated above .052 RMS.
The recording shell independently composites at alpha 200/255 for dark themes
and 224/255 for light themes; it respects Windows transparency-off and high
contrast. Overlay labels use Ink at full opacity, and the inset stays protected.
This supersedes the historical 232/255 native-Mica-dependent overlay note below.

The microphone label aligns to the selector row; Refresh remains beneath it.
Models uses automatic scrolling, pairs runtime status with the engine selector,
and collapses empty action feedback so it reserves no blank footer space.

Completed insertion now uses one guarded paste by default. Submission requires
explicit opt-in physical Enter intent and complete composer readback. Earlier
Unicode-only/no-submission notes describe the original implementation.
[Current behavior](PERSONALIZATION.md) · [Recording display](RECORDING-DISPLAY.md)

# Sona design

October 15, owner recording-display correction: Panel is now 320 DIP wide with
144 DIP minimum height; Capsule is 272 / 66 DIP. Both keep Stop, Cancel and
Open draft. The live transcript shows a measured two-line tail of the last two
phrases, with the complete text retained in Home. Processing gets a busy line;
measured microphone bars remain exclusive to recording. Native-glass overlay
alpha is 232/255, retaining the existing opaque/high-contrast fallbacks. The
shared settings switch now centers its 16 DIP thumb inside the track without
oversized margins. Actual Discord window-class and Codex package icons were
read back and visually inspected. Browser website identity is not inferred.
Offscreen WPF captures and focused icon checks are detailed in
[Recording display](RECORDING-DISPLAY.md); owner live recording and foreground
target-app acceptance remain open.


October 15, 0.4.1 daily-use wave: the existing Amethyst and glass palette values
are unchanged. Home adds a wrapping 12 DIP draft-state line below Latest draft:
Live raw / provisional, final raw, cleaned, or manual edits protected. Editing
freezes the editable text against later recognition and cleanup. The final cleanup
suggestion is a separate expander with an explicit Use cleaned suggestion action.
It is scoped to one session and cleared when opening another draft.

Optional live text appears below either recording display in a 140 DIP bounded
scroll area, with 14 DIP text and an 11 DIP provisional-state label. Open draft is
an explicit action that activates Home for editing; automatic overlay updates
remain nonactivating. Retained results show a 12-second review notice with the
explicit insertion shortcut. No partial external typing or animated fake stream.
Settings adds the opt-in checkbox and local snapshot explanation. About displays
the running module build ID and actual preferences location/load status. Failed
preference loads show a recovery warning and preserve the existing file.

Verified: isolated WPF normal/compact captures, native theme save/relaunch, and
synthetic recording pipeline. Real microphone, Discord insertion and owner visual
acceptance remain open. See DAILY-USE.md for behavior and proof boundaries.


October 14, final shared-glass polish: reading surfaces and controls keep crisp
foreground text without WPF drop shadows. Checked radio and switch states have
distinct geometry and color; disabled buttons use a readable neutral surface.
Settings and Models have fixed headings with inset scroll content. Related
actions appear beside their settings; a single general-preferences footer
appears only for unsaved changes. Independent idle-time changes preserve other
unsaved settings. Model and cleanup actions reflect actual residency, availability
and pending release. History exposes Open draft without automatic insertion.
The compact retained-target warning reads Draft stays in Sona; the wider Panel
and tooltip preserve the full transition. Paper is neutral daylight and Mist is
slate/seafoam. Native source and preview titles are simply Sona.

October 14, shared glass identity: all eight palettes now use the same material
structure, with surfaces, edges and restrained ambient light derived from each
palette. Amethyst uses the approved near-black colors; the former amethyst-glass
ID resolves to amethyst so saved settings survive without a duplicate menu option.
Light palettes retain dark text. Opaque fallback surfaces keep alpha at255; system
high contrast uses system brushes and removes decorative effects. Charcoal remains
the default color choice.

The recording displays follow the selected Frost layout with the darker glass
material: smaller app identity row, regular Segoe UI Variable Text/Segoe UI, ample
spacing, a measured waveform, elapsed time and stop/cancel actions. Panel width420,
minimum height216; Capsule width310, minimum height82. Real installed executable
icons are read locally with a bounded cache and a neutral fallback. No website
identity is inferred from a browser process. Live active-app observation never
retargets a recording; the original destination stays fixed, and a focus change
shows a retention warning. Physical Codex/Discord typing acceptance remains open.

Earlier design checkpoints follow for context; the shared glass identity above
supersedes the optional-material and original-Amethyst descriptions.

October 14, 0.4.0: optional Amethyst Glass uses a darker smoked violet material,
requested to match the product film's studio. The base canvas is #09070E, panels
#181320, inset #0D0B12 and edges #655775. Text remains #FAF7FF, secondary text
#C3BCD0 and accent #C4A6FF. Primary #68418E and selection #3B2856 retain white
labels. Frozen canvas gradients use #100B18 / #07060D / #0C0912; panel gradients
use #21192C / #141019 / #1B1324. Ambient alpha values are 0x24, 0x20 and 0x18,
with a dark scrim alpha of 60 and a limited action shadow at 16% opacity. Panel
radius remains 14 DIP, with a 1 DIP selected navigation edge.

Main-window Mica uses documented DWM_SYSTEMBACKDROP_TYPE on supported Windows 11
with composition and transparency. The opaque fallback and theme reset remain.
Inter, existing layout, original Amethyst and the Charcoal default are preserved.
All README captures share dark Amethyst Glass, 1234 × 867 pixels and synthetic
text. The changed source passed 279 core and 26 native checks, with actual loaded
Parakeet and GPU cleanup captures. Older Windows and physical high-contrast
behavior have source guards but were not exercised in these native captures.

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


## October 15 personalization and model browsing

Whisper and Parakeet have fixed family navigation above the model scroll area.
The green marker means selected for next use; browsing another family does not
select a model. Resident memory state remains separately labeled. Cards are
304 DIP wide and wrap into one column at the minimum window width, keeping the
action near the model rather than at the far edge of an empty full-width row.

Home offers explicit Save draft, Review corrections and Export draft actions.
Learning is a visible, removable vocabulary substitution. Generated and manually
edited text remain distinct. The finished-draft pipeline uses one paste with a
processing indicator; optional physical Enter intent is visibly separate from
ordinary Stop. No recording preview is streamed into another application's field.

October 15, Settings navigation and explicit Discord connection: Settings now uses
seven wrapping section choices (General, Audio & typing, Appearance, Text cleanup,
Privacy, Discord, About) above a shared section viewport. Hidden sections retain
all named controls and unsaved form values; only one section is visible. Navigation
wraps at the minimum window size, with vertical scrolling inside the chosen section.
About retains version/build information and Quit Sona. Existing semantic brushes,
materials and main navigation are preserved.

The Discord section uses a green connected surface with explicit Disconnect;
disconnected state exposes Connect Discord and explains that call protection is
inactive. Successful disconnect first restores owned voice changes, then saves
the manual connection choice while retaining setup and watched apps. Error text
keeps restoration/persistence failures explicit. Ten fake service checks and 23
isolated native click-path checks passed, with normal/minimum-size screenshots.
No fresh live Discord/call acceptance is claimed. See DISCORD-SETUP.md.


The sidebar footer retains the version and adds compact GitHub and Star actions.
Both open the public Sona repository only after a user click. The Star action
does not claim a star was added or perform an authenticated action. The official
Octicons mark uses the shared muted color; its MIT license ships with the app.

Sidebar correction, October 15: retain one GitHub link. The duplicate Star
action was removed because both opened the same repository page.

Owner feedback refinement: the capsule keeps its 272 DIP width, with its voice
waveform enlarged from 96 × 13 to 176 × 26 DIP. The panel gives the waveform
36 DIP of height. Measured audio, smoothing and the bounded transcript tail remain.

## History pause and deletion, 0.4.5

History has Pause history / Resume history and per-row Delete draft. The pause
choice persists and is separate from saving history across restarts. Paused
dictations do not enter recent History, including a recording still transcribing
when Resume is clicked. Existing drafts remain available; explicit Save draft
and the current editor continue to work. Clear recent uses its displayed
range/search confirmation and preserves saved drafts and the working editor.
The idle action refresh now re-enables Clear after dictation finishes.
[Behavior and regression evidence](HISTORY-CONTROLS.md).
