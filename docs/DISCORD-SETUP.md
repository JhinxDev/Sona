# Discord connection and call protection

Open **Settings → Discord** to connect, disconnect, or change call protection.
The connection status is green only when Sona has an authenticated connection.
Connection state and the saved **Mute Discord while dictating** preference are
separate: a disconnected Sona continues dictation with call protection inactive.

**Disconnect** first restores mute/deafen changes owned by Sona, then closes the
connection. A successful choice is saved until an explicit **Connect Discord**,
including after restarting Sona. Credentials, watched apps, the enabled protection
preference and manual mute are retained. If restoration fails, Sona keeps the
connection and ownership for retry; it does not report a successful disconnect.
Pending owned restoration prevents a new protected recording until resolved.
If saving the choice fails after a successful disconnect, the UI explains that
restart may reconnect and offers a retry. The runtime stays disconnected.

## Immediate switches, 0.4.7

**Mute Discord while dictating** and **Also silence incoming Discord audio**
apply and save directly. A nearby message reports applying, the saved policy,
or a failed change. With incoming silencing Off, Sona keeps microphone protection
and releases only deafen changes it owns. Preexisting manual mute/deafen is
preserved. Watched apps, release delay and shortcut still use **Save protection**;
switch actions preserve pending advanced and general edits.

Incoming-audio changes apply during an existing watched/Sona recording. Ordinary
setting changes preserve an explicit pause. A disabled master still tracks Sona's
recording lifecycle so enabling protection uses current sources and an ended
source cannot cause a phantom mute. Failed acquisition/restoration remains
retryable; a manual override during an awaited retry stops subsequent commands.
Failed preference persistence restores the prior switch and runtime policy,
including paused state without intermediate acquisition. Failed rollback retains
ownership and explicit retry guidance. Hush import remains review-then-save.

The maintained build runs an isolated native switch fixture through actual WPF
controls, a private synthetic profile and a uniquely named fake Discord pipe:

```powershell
.\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.exe --protection-controls-fixture <fresh-evidence-directory>
```

Verification: Core362 checks, native switches19, default Windows49, fake
Disconnect10, closed-Discord11, Settings23 and agent pending-settings88 passed.
Normal/minimum-size synthetic UI captures were inspected. Source review caught
three additional import/rollback/awaited-override cases; each was reproduced and
fixed with an affected regression. These checks neither connect the owner's
Discord nor open a microphone. Actual owner-call acceptance remains separate.

## Retained setup missing after a desktop handoff

A packaged Codex terminal and Explorer can see different physical AppData
folders at the same logical path. On October 18, the installed 0.4.6 app had
preferences in its desktop profile but no encrypted Discord setup there. The
retained setup in the caller's redirected profile still authenticated and read
voice state. Restoring those exact encrypted bytes to the active desktop profile
made the actual Connect action and a Disconnect/Connect cycle succeed.

`tools/start-sona.ps1 -FilePath <owned-installed-Sona.exe> -CheckProfileOnly`
performs a read-only check through the protected Explorer bootstrap without
launching Sona. If the caller has a saved Discord setup but the desktop profile
cannot see one, it fails with a bounded setup-missing reason. `tools/upgrade.ps1`
runs this check before capturing drafts or quitting the current app. This checks
file presence, not authorization validity or real-call behavior.

For a verified split, run `tools/restore-discord-setup.ps1` from the destination
app's desktop context with explicit `-SourceProfileDirectory` and
`-DestinationProfileDirectory`. The destination must already contain physical
`preferences.json`. The tool validates current-user DPAPI, copies the existing
encrypted file atomically, verifies its encrypted hash, preserves the source,
and refuses a different existing destination. It rejects redirected destinations
and never returns credential plaintext. Select the retained Sona setup; Hush
migration remains the separate explicit action described below.

The recovery and preflight fixtures use synthetic profiles:

```powershell
.\tools\discord-setup-restore-checks.ps1 -OutputDirectory <fresh-evidence-directory>
.\tools\desktop-profile-checks.ps1 -OutputRoot <fresh-sona-upgrade-rehearsal-and-32-hex-directory>
```

Eight recovery checks passed under PowerShell 7 and Windows PowerShell 5.1.
Eight profile checks passed, including missing setup, read-only desktop visibility
and preservation of the running owner instance. App binaries remain 0.4.6;
this is a tooling and active-profile repair. No microphone, message submission
or call was started, and call protection remained off during live verification.

## Application credentials and consent

Sona's current adapter uses Discord's local desktop RPC connection with OAuth2
application credentials and user consent. It does not run a bot, use a bot token,
request the `bot` scope, or invite a bot into a server. This follows the inspected
`DiscordVoiceTransport` implementation. Discord documents developer application
registration and the client ID/client secret for OAuth2.
[Official OAuth2 documentation](https://docs.discord.com/developers/topics/oauth2).

Discord requires authorization before RPC commands and documents an in-client
consent prompt and authorization-code exchange. Its RPC documentation also
describes approved application/tester access. Current OAuth2 documentation marks
`rpc` and `rpc.voice.write` as approved-partner scopes, so creating an arbitrary
new application is not proof that Discord will grant this capability.
[RPC authentication and authorization](https://docs.discord.com/developers/topics/rpc#authenticating),
[OAuth2 scope requirements](https://docs.discord.com/developers/topics/oauth2#oauth2-scopes).

Sona ships no shared application credentials. Keep an existing authorized setup
if it is working. For your own eligible application, expand **Set up your own
Discord application**, enter its application ID and client secret, then choose
**Save protected setup**. The adapter currently requests `rpc` and
`rpc.voice.write`; its authorization-code exchange uses the redirect URI
`http://127.0.0.1`. Configure that exact URI for the application. Open Discord's
desktop app, select **Connect Discord**, and approve its prompt when shown.
The local credential file uses current-user Windows encryption. The UI clears
the credential fields after saving; connection actions preserve the saved file.

The optional **Migrate from Hush** action imports only when explicitly invoked.
Pause and quit Hush before connecting Sona so two applications do not control
the same voice state. Migration never deletes the legacy setup automatically.

Speech and cleanup remain local. Discord authorization/refresh contacts Discord's
HTTPS token endpoint; voice control uses the desktop app's local named pipe.
No new hosted service, bot process, subscription or background server was added.

## Settings sections

Settings has seven wrapping navigation choices: General, Audio & typing,
Appearance, Text cleanup, Privacy, Discord and About. Only the selected section
is shown, and scrolling belongs to that section. Existing named controls and
save actions are retained. **Quit Sona** and the version readback are under
**About**. At the minimum 740 × 530 DIP size, navigation wraps and content scrolls
vertically without horizontal scrolling.

## Verification, October 15, 2026

The isolated source build passed ten fake named-pipe service checks and 23 native
Settings/Discord click-path checks. They exercised owned-state restoration,
failed restoration/retry, manual-mute preservation, credential byte preservation,
watcher retention, intentional disconnected dictation, saved disconnection in a
fresh service, green connection status, all seven sections and minimum-size
layout. The native fixture uses WPF automation peers and the real UI handlers.
It never connects to the owner's Discord, reads owner credentials, starts a
microphone or injects global input.

Owner-machine screenshots and native receipt:
`E:/Temp/qazws/sona-personalization/discord-settings-qualified/report.json`.
`discord-connected.png`, `discord-disconnected.png`, `audio-compact.png`,
`discord-compact.png` and `section-*.png` were captured from the isolated test
window. The connected and compact captures were visually inspected. These
prove mock behavior and native layout, not fresh authorization eligibility or
live-call acceptance for the installed candidate.

Reproduce with the README's isolated SDK environment:

```powershell
& $sonaDotnet build .\tests\Sona.WindowsTests -c Release --disable-build-servers -p:UseSharedCompilation=false
& $sonaDotnet .\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.dll --discord-disconnect-checks
& $sonaDotnet .\tests\Sona.WindowsTests\bin\Release\net8.0-windows\Sona.WindowsTests.dll --discord-settings-fixture C:\absolute\new-evidence-directory
```

Use a fresh evidence path. The fixture creates its own synthetic profile and
encrypted fixture setup, and closes the test window and fake pipe afterward.


## Dictating while Discord is closed

Enabled call protection no longer requires the desktop Discord app to be open
before Sona can record. When no Discord, DiscordPTB, DiscordCanary or
DiscordDevelopment process is running, dictation starts without an RPC connection
or credential read. Protection remains enabled; its polling loop can reconnect
and protect the retained recording source if Discord opens during dictation.
It cannot promise instantaneous protection during an app launch or connection
failure, so the connection status remains meaningful.

If Discord is running but its connection/authorization is unavailable, protected
recording still waits for a working connection or an explicit user decision to
pause/disconnect protection. Unknown process state or pending owned restoration
never silently bypasses the guard. The no-IPC error now describes an unavailable
connection instead of assuming the process is absent.

The defect was reproduced as “Discord is not running” at the real protection
admission boundary. Eleven maintained closed/reopen/blocked-state checks, ten
existing disconnect checks and eight isolated native start/cancel checks pass.
The native fixture reaches Recording through the actual MainWindow preparation
path using fake capture only; no live microphone, physical shortcut, message or
real Discord call is exercised by these checks.
