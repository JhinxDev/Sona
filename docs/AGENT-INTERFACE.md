# Sona agent interface

0.3.0 adds `theme` (eight catalog IDs), `recordingDisplay` (Panel/Capsule), and
`modelIdleMinutes` (integer 0..240, zero Keep until quit) to validated patches.
`sona-agent models load` prepares the selected model asynchronously; poll status.
`sona-agent models unload` releases it while idle without deleting its files.
Status exposes `loadedModel`, `residentProcessId`, and the selected next-use model.
MCP exposes 13 named tools, including the load/unload pair. No microphone-start,
credential or transcript-export capability is added. A bounded internal resident
worker uses a private randomized loopback route; it is not the agent API.

`sona-agent` is a Windows console binary beside Sona. A current-user-only named
pipe connects it to the running desktop app. It requires no network listener,
account or API key. All commands return `{ "ok": true, "data": ... }`, or
`{ "ok": false, "error": "..." }` with a nonzero exit status. Error messages do
not echo arbitrary user input. The service is available while Sona lives in tray.

```powershell
sona-agent status
sona-agent doctor
sona-agent settings get
sona-agent settings patch '{"language":"fr","shortcutMode":"Toggle"}'
sona-agent models list
sona-agent models info tiny
sona-agent models install tiny
sona-agent status
sona-agent models verify tiny
sona-agent settings patch '{"model":"tiny","engine":"FastCpu"}'
sona-agent models cancel
sona-agent protection status
sona-agent protection pause
sona-agent protection resume
```

Installation is an explicit network operation. It returns acceptance immediately;
`status` reports `downloadingModel`, `downloadPercent`, and `downloadOutcome`.
Existing differing files are preserved. Selecting a model requires compatible
installed model/engine. Unknown settings, duplicate keys, unavailable microphones
and busy dictation are rejected. Settings commits preserve active values if saving
fails. Protection uses the same controller as the UI. Pausing restores only owned
voice changes; resuming reconciles current watched microphone activity.

Version 0.3.1 adds `copyToClipboard` as a validated boolean. It copies a
completed dictation, including when focus changes, without pressing Enter.
It defaults off for new installations and is enabled for the owner on request.

Supported settings: `language`, `engine`, `model`, `shortcutMode`, `style`,
`device`, `autoInsert`, `copyToClipboard`, `closeToTray`, `protectionEnabled`, `silenceIncoming`,
`protectionReleaseDelayMs`, `protectionShortcut`, `watchedApps`. Credential setup, transcript/history
export and microphone capture are intentionally outside this API. A same-user
agent is trusted to make the requested local settings changes; this interface
does not isolate software already running as that Windows user.

For discovery, use `--help`, `models list` and `models info <id>`. The bounded
`request <method> [JSON]` command exposes only the same named operations. It does
not run shell commands or accept arbitrary paths/URLs. `--json` is accepted as a
compatibility flag; JSON is always the command result format.

## MCP stdio

Add the installed full path of `sona-agent.exe` to any MCP-capable client:

```json
{
  "mcpServers": {
    "sona": {
      "command": "C:\\path\\to\\sona-agent.exe",
      "args": ["--mcp"]
    }
  }
}
```

Negotiated protocol versions: 2025-11-25, 2025-06-18 and 2025-03-26. Named tools
expose status, settings get/patch, model list/info/install/verify/cancel and
protection status/pause/resume. Responses include structured JSON and compatible
text content. Logs and errors must never pollute protocol stdout. Unknown methods
and malformed/oversized messages receive JSON-RPC errors. This bounded server
implements tools and ping, not resources, sampling or remote HTTP transport.

`tools/agent-smoke.ps1` verifies the actual CLI and MCP against an isolated app
instance without a microphone, global input or actual Discord connection.
`-DownloadTiny` adds one explicit pinned download and selection pilot. Isolation
uses `MURMUR_DATA`; normal clients must omit that test-only override.
