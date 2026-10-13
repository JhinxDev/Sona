# Working on Sona

Sona is an early native Windows dictation app. Read README.md, docs/DESIGN.md,
docs/IMPLEMENTATION.md and docs/PREVIEW-VALIDATION.md before substantial changes.
Respect the active user's model choices, working changes and explicit scope.

- Keep microphone recording behind an intentional user gesture. Never start a
  live microphone, inject global keys, submit a message or connect a call as part
  of an unattended test.
- Preserve the owner's profile, draft, clipboard and enabled preferences. Native
  UI tests use a separate test profile; fixture replay does not prove live input.
- Speech and optional cleanup stay local. New profiles start without saved
  history, startup, clipboard backup, cleanup, call protection or update checks.
- Keep SDKs, NuGet caches, model weights, recordings and renders outside this
  repository. Use the documented SONA_RUNTIME and isolated SDK settings. Do not
  change a user's global SDK, driver, startup registration or font installation.
- Build with --disable-build-servers -p:UseSharedCompilation=false. Run the
  relevant checks from tests/Murmur.Tests and affected native tools after source
  changes. A documentation-only change does not need an unchanged full replay.
- Preserve upstream licenses and notices, pinned sizes/hashes and local download
  boundaries. Review dependency changes and every scoped Git diff before syncing.
- Distinguish builds, fake-system tests, file replay and real microphone/target-app
  acceptance. Keep missing personal or physical-device proof explicit.
- Stage only owned paths and preserve concurrent work. Public source does not
  authorize uploading videos, publishing binary releases or messaging others.
