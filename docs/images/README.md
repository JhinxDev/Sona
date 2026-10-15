# README image provenance

The original `sona-home-042.png`, `sona-models-042.png`,
`sona-settings-042.png` and `sona-capsule-042.png` images were captured October 15, 2026 from Sona 0.4.2,
build `aa8b429e480b`, source `05f838c87641507d7eb7f81e755559de7bd3627f`.
They render the actual WPF controls offscreen in isolated demonstration profiles,
using the Amethyst palette and synthetic draft text. They are not UI mockups.
Main-window images show its client area, not the operating system title bar.
The capsule fixture supplies synthetic microphone levels and demonstration text.

No production profile, live microphone, real message or call was used. Startup
controls are deliberately disabled in the isolated Settings preview. Model
installation and selection labels are the state of the capture machine, not a
promise about a new installation. Offscreen rendering does not qualify native
DWM transparency on every Windows system.

The unversioned earlier images and `sona-trailer.jpg` are retained historical
assets. The linked published trailer depicts 0.4.0; it is not a 0.4.2 demonstration.

## SHA-256

- `sona-home-042.png`: `8f73aae2db673f349b1e84fec336a9632165f3c5778b4421ddc9bf2b83e9c601`
- `sona-models-042.png`: `a176c58e30059f14f32d711294661bdc572e9580752aefa84b620d394906f7ee`
- `sona-settings-042.png`: `c06d1aa21701b776a6e17cb4d4dbcf53921162bbcda7e295096d65f4c1dd19a2`
- `sona-capsule-042.png`: `0a56f0a880a088a90eafc1ad587500b1eb2420183a643040e9ed9c473ad3ca94`


## Refined glass capsule

`sona-capsule-glass-042.png` is a 5x native WPF render (1360 × 800 pixels;
272 × 160 DIP fixed canvas with transparent spare height). It uses 0.4.2 build
`5cd0afbf41b5`, source `7f43257df581e35ce309bec7d0ed657148f774f9`, with
neutral Stop, a red recording dot and an independently translucent surface.
Image SHA-256: `abab466e91fa5653237cf2b670975f3e302cf587fe595755f97f20ddcb176902`.

The frame replays measured PCM levels and real Whisper Turbo transcript events
from an accepted prerecorded “Let's review the design tomorrow” clip through
the production waveform mapping and animation step. The original recognition
receipt used source `f4d7432194c307b59922b1d108180675f21c9c74`; the only
subsequent app changes affect material brushes, overlay label colors and the
Windows transparency preference accessor. Recognition, recording, live polling
and waveform code are unchanged. This is a measured-event UI replay, not a new
recognition run, live microphone capture or external-app delivery claim. The
Demo app label and neutral icon deliberately avoid implying a real destination.
