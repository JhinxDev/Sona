# Installing Sona on Windows

Sona has a per-user Windows x64 installer. It does not need administrator rights,
and it does not need a separate .NET installation: the app is self-contained and
the CLI ships beside it. The installer is part of the early, unsigned preview.

## Download

[Download Sona-Setup-0.5.3.exe](https://github.com/JhinxDev/Sona/releases/download/v0.5.3/Sona-Setup-0.5.3.exe)
(52.6 MiB) from the [v0.5.3 release](https://github.com/JhinxDev/Sona/releases/tag/v0.5.3).
This Windows x64 installer is an unsigned pre-release. Its
[published SHA-256 checksum](https://github.com/JhinxDev/Sona/releases/download/v0.5.3/SHA256SUMS.txt)
is `c0997819f9ced1dd1b01b73a1e95a96cdc7b8b1b459178bbab11d575137d466f`.

## Requirements

- Windows 11 x64 (verified). Windows 10 x64 is not formally verified yet.
- A working microphone for live dictation.
- Internet access on first setup to download the speech model you choose.
- About 2 GB free disk after a model download (the app and bundled engine are
  roughly 185 MB installed, plus the model weights you download and the optional
  cleanup model).

## Install

1. Run `Sona-Setup-<version>.exe`. The preview is unsigned, so Windows SmartScreen
   may show "Windows protected your PC". Use **More info → Run anyway** only when
   the file came from a source you trust and its SHA256 matches the published
   checksum for that build.
2. Choose whether to add a desktop shortcut. The **Whisper CPU speech engine**
   component is checked by default and installs the pinned whisper.cpp engine to
   `%LOCALAPPDATA%\Sona\runtime\whisper\Release`. You can untick it and use
   Parakeet instead, which downloads its own runtime inside Sona.
3. Launch Sona, open **Dictation Models**, press **Download** on the model you
   want, then **Use model**. Whisper CPU models run on the bundled engine;
   Parakeet and the optional text-polish model download their runtimes in-app.
4. Pick your microphone in **Settings**, then hold `Ctrl + Win` and speak. The
   first dictation warms the model if it is not already loaded.

## Where things live

| Item | Location |
| --- | --- |
| Installed app and CLI | `%LOCALAPPDATA%\Programs\Sona\app` |
| Profile, preferences, draft, history | `%LOCALAPPDATA%\Sona` (new profiles) or `%LOCALAPPDATA%\Murmur` (legacy profiles) |
| Runtime root (`SONA_RUNTIME` fallback) | `%LOCALAPPDATA%\Sona\runtime` |
| Whisper CPU engine | `%LOCALAPPDATA%\Sona\runtime\whisper\Release` |
| Downloaded models | `%LOCALAPPDATA%\Sona\runtime\models` |

Sona keeps speech recognition, live preview, cleanup, drafts and optional history
on your PC. It has no account, cloud speech, sync or analytics. See the network
table in the [README](../README.md#privacy-and-network-calls).

## Uninstall and updates

Uninstall from **Settings → Apps → Installed apps → Sona**, or run
`unins000.exe` in the install folder. The uninstaller removes the application and
its Start Menu shortcuts. It deliberately leaves your profile, models and the
engine under `%LOCALAPPDATA%\Sona` (and any legacy `%LOCALAPPDATA%\Murmur`
profile) so a reinstall or upgrade keeps your setup. Delete those folders
yourself to remove every trace.

To update, quit Sona and run a newer installer. It upgrades in place and keeps
your settings and downloaded models. Sona itself checks for updates only when you
ask it to; nothing downloads or installs automatically.

## The CLI

`sona-agent.exe` is installed next to `Sona.exe`. It talks to the running app over
a current-user-only named pipe and also supports `--mcp` over stdio. Add the
install folder to your `PATH` if you want to call it by name. See the
[agent interface](../docs/AGENT-INTERFACE.md).

## Optional CUDA

The installer includes the pinned CPU engine. NVIDIA CUDA acceleration is still a
developer setup step (the engine and CUDA runtime are large and version-pinned):
run the source checkout's `tools/setup-cuda-runtime.ps1` against
`%LOCALAPPDATA%\Sona\runtime`. CPU dictation works without CUDA.

## Troubleshooting

- **Record is disabled and Home asks for a model:** open Dictation Models and
  download a model, then press **Use model**.
- **Whisper says the resident engine is missing:** reinstall with the Whisper CPU
  engine component checked, or use Parakeet.
- **Antivirus quarantines a worker:** allow `Sona.exe` and the Sona workers under
  the runtime root. Sona does not disable security software.
- **Launch at startup is unavailable:** startup registration only works from an
  installed build under `%LOCALAPPDATA%\Programs\Sona`.
- **The app window is blank on a remote desktop:** launch
  `Sona.exe --software-rendering`, or set the same option in Settings.
- **A preview build bugs out:** quit Sona, keep your draft and preferences, then
  reinstall the previous installer or a newer one.

## macOS

There is no macOS installer. Sona's native code is Windows-only: the interface is
WPF, and capture, hotkeys, insertion, microphone ownership, Discord IPC, the
worker containment and the model runtimes all use Windows APIs. A macOS version
would be a separate native app, not a repackage of this installer. The current
source is not cross-platform.

## Building the installer

The maintained build is reproducible from a source checkout on Windows x64 with
the pinned SDK and Inno Setup 7:

```powershell
pwsh -File .\tools\installer\build-installer.ps1 -Rehearsal
```

`-Rehearsal` builds an isolated test installer, installs it into a temporary
folder, verifies the receipt, engine pins, shortcut, installed app start and
uninstall behavior, uninstalls it, and only then builds the real
`artifacts\installer\Sona-Setup-<version>.exe`. Build metadata and hashes are
written to `artifacts\installer\installer-build-info.json`; the pinning and
receipt contract live in `installer/Sona.iss` and
[`tools/installer/build-installer.ps1`](../tools/installer/build-installer.ps1).
