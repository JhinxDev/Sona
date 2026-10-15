# Recording display and active app icons

October 15, 2026 owner repair. The Panel uses 320 DIP width and a 144 DIP
minimum height; Capsule uses 272 and 66 DIP. Both retain app identity, elapsed
time, measured microphone levels, Stop, Cancel and an explicit Open draft action.
Native glass gives the display surface alpha 232/255; opaque and high-contrast
fallbacks retain their readable surfaces. Main-window surfaces are unaffected.

The identity row can show the active app outside an editable field while keeping
the insertion target null. Unknown/no-field states start with a quiet neutral
glyph and no negative label; raw descriptions remain in accessible identity
metadata and tooltips. Home keeps the accurate text-field eligibility warning.

Live text shows at most two recent phrases, measured to fit two 17 DIP lines.
It has no scroll bar and does not grow with the complete transcript. Home and
Open draft retain the complete text. Transcribing, Polishing and Preparing your
draft show a busy line without manufacturing microphone levels or a percentage.
When Windows disables client-area animation, the busy line stays static.

The shared settings toggle has a 16 DIP circular thumb centered with 2 DIP
margins inside the 22 DIP track. The former 3 DIP margins exceeded the track's
20 DIP content height and could clip the bottom of the circle.

The shared Button style uses its native ContentPresenter for both plain labels
and composed dot/label content. A focused native fixture prevents framework type
names from replacing model-family buttons, while checking plain/primary labels.

## Icon discovery

The installed package logo is preferred for WindowsApps applications, preserving
the Codex identity even when the executable and window still expose an older
mark. Other apps use the supplied window or window-class icon, then executable
resources. WM_GETICON requests have a 60 ms timeout; HWND/process ownership is
checked before copying a foreign icon. Only the copy is destroyed. No window
title, chat text, browser URL, DOM or network favicon lookup is used.

The cache keeps at most 32 frozen images and one extraction in flight. Window
and process IDs form part of the key so separate windows and reused HWNDs do
not inherit another process's icon. A missing icon uses the neutral fallback.
Browser identity remains the browser unless its native window supplies an icon;
this is not a website-favicon integration or proof of individual tab identity.

The Discord defect was an executable extraction success check. On the actual
Discord, Codex and Edge installations, ExtractIconEx returned 2 while supplying
valid small and large icon handles. The old `!= 1` test discarded those handles.
Codex happened to recover through its package logo; Discord had no package
fallback. Extraction now accepts valid handles after a nonzero, non-error result.

## Focused verification

After building the native test runner using the README runtime settings:

```powershell
& $sonaDotnet run --project .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false -- --application-icon-fixture
& $sonaDotnet run --project .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false -- --native-app-icons "$env:TEMP\sona-app-icons-proof"
& $sonaDotnet run --project .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false -- --overlay-fixture "$env:TEMP\sona-overlay-proof"
```

Use fresh output directories. The first command has 17 maintained icon checks,
including a required bitmap from Sona.exe's compiled multi-size ICO. That exact
input returns a generic DrawingImage on the frozen original assembly. The
colored cache/recording fixtures are separate from real installed-app evidence.

The native metadata fixture inspected the actual Discord HWND and resolved a
40 by 40 bitmap with 1,415 nontransparent pixels. Codex resolved its package
bitmap and Edge resolved its native app icon. Each PNG was visually inspected.
The receipt identifies the tested Sona module, process/HWND and executable; the
already running production Sona instance was preserved.

The offscreen WPF fixture covers smaller Panel/Capsule layouts, bounded recent
text, Open draft, busy state, constant height under longer input and eight toggle
states across Amethyst/Paper. It captures the actual controls at 96 DPI without
starting production services, opening a microphone or changing foreground. Its
translucency check deliberately selects the native-glass resource branch; this
does not establish a live DWM/device appearance. Physical foreground recording,
actual target-app insertion and owner visual acceptance remain separate.

See [Microsoft's WM_GETICON contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-geticon).

Owner refinement: Capsule omits the Open draft button, leaving recording controls
and the recent transcript. Review remains available in the main Sona window;
Panel retains its review action.
