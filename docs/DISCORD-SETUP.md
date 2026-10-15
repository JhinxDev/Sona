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
