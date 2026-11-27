# Sona — Agent & Contributor Instructions

Sona is a local-first native Windows voice dictation app. Read `README.md`, `docs/DESIGN.md`, `docs/IMPLEMENTATION.md`, and `docs/PREVIEW-VALIDATION.md` before substantial changes.

## Stack & Layout

- **App & Runtime:** .NET / C# native Windows desktop app (`src/`), `tests/Sona.Tests/`, `tools/`.
- **Local Privacy:** Speech recognition and optional cleanup stay local; new profiles start without saved history, startup entries, clipboard backup, call protection, or update checks.

## Verify & Run Commands

```powershell
dotnet build --disable-build-servers -p:UseSharedCompilation=false
dotnet test tests/Sona.Tests --disable-build-servers -p:UseSharedCompilation=false
```

## Active Skills & Tools

- **.NET & Windows UI:** `dotnet-csharp` (`csharp-expert`, `csharp-refactoring`, `msbuild`, `dotnet-pinvoke`, `analyzing-dotnet-performance`, `run-tests`), `control-windows-apps`, `screenshot`.
- **Security & CI:** `security-audit`, `gh-fix-ci`, `gh-address-comments`, `humanizer`.

## Guardrails & Security

- Keep microphone recording behind an intentional user gesture. Never start a live microphone, inject global keys, submit messages, or connect calls in unattended tests.
- Preserve the owner's profile, draft, clipboard, and preferences; native UI tests use an isolated test profile.
- Keep SDKs, NuGet caches, model weights, recordings, and renders outside this repository (`SONA_RUNTIME`). Do not alter global SDKs, drivers, startup entries, or system fonts.
- Preserve upstream licenses, pinned hashes, and download boundaries. Distinguish builds, mocked tests, file replay, and live microphone/target-app acceptance.

## Release & Live App Activation

- **Live upgrade rule:** Every qualified new app version or functional update must update the installed running Sona via guarded Quit, encrypted state recovery, and verified EXE/hash readback (`docs/IMPLEMENTATION.md#live-upgrade-delivery-rule`). Docs-only commits do not require activation.
- **Releases:** Bump `<Version>` in `Directory.Build.props`, move notes under `## <version>` in `CHANGELOG.md`, update release links in `README.md` and `docs/INSTALL.md`, pass PR CI, and push an annotated `vX.Y.Z` tag so `.github/workflows/release.yml` builds the Windows x64 installer, generates `SHA256SUMS.txt`, and publishes the GitHub Latest Release.
