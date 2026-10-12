# Murmur project owner

Read C:/Users/qazws/AGENTS.md and the E:/Projects adapter. Source owner is chat 01a12f45-d73e-7191-ad2a-56a050b8dce2. Private source and new Projects/local-dictation docs belong to this owner; do not mutate Hush or shared Tool Shed/Vault Git files without coordination.

Use E:/DevelopmentTools/local-dictation for SDK, NuGet and model caches. No heavy output belongs in the synced Vault. Preserve current dictation availability, no startup by default, no microphone outside a deliberate user gesture, no automatic submission, no background transcript logging or cloud requests.

Build with the isolated SDK and --disable-build-servers -p:UseSharedCompilation=false. Set DOTNET_CLI_HOME, NUGET_PACKAGES and telemetry opt out to the documented task-owned paths. Run meaningful tests from tests/Murmur.Tests, then affected native tests. See docs/DESIGN.md and docs/IMPLEMENTATION.md for finite gates. Never claim actual Windows/Codex acceptance from a build or a replay. Keep physical owner proof open until tested.
