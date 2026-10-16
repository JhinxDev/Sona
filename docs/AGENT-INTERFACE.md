# Sona agent interface

0.4.3 adds `unsavedSettings` to status. `settings.patch` rejects while the native
general or Discord-protection form already has unsaved edits, before validation
or side effects. Edits made while protection configuration is awaiting also
reject the patch before saving or refreshing the form, restoring the committed
protection configuration. Save or revert those edits in Sona, then retry.
Status returns only the boolean, not the pending values. Clean patches retain
the existing validated commit behavior.

0.4.2 adds `pasteCompletedDraft` (default true) and `enterToSend` (default false)
boolean settings, plus `soundEffects` (default true). Enter-to-send still requires the owner's physical Enter gesture
during an admitted hands-free recording and verified insertion into the same field.
The agent API cannot start a recording or submit a draft. `liveTranscription` is
also a validated boolean (default false).

0.3.2 adds optional local cleanup controls: `cleanupEnabled` (boolean),
`cleanupTone` (`formal`, `casual`, `very casual`), `cleanupEngine` (`Auto`, `Cpu`),
and `cleanupIdleMinutes` (integer 0..240, zero Keep until quit). Defaults are
off, formal, GPU preferred and 10 minutes. `Auto` prefers the installed CUDA
runtime; with only a CPU runtime installed, it uses CPU. A failed CUDA worker
does not silently fall back to CPU. Choose `Cpu` explicitly when needed.

0.3.0 added `theme` (eight catalog IDs), `recordingDisplay` (Panel/Capsule), and
`modelIdleMinutes` (integer 0..240, zero Keep until quit) to validated patches.
`sona-agent models load` prepares the selected model asynchronously; poll status.
`sona-agent models unload` releases it while idle without deleting its files.
Status exposes `loadedModel`, `residentProcessId`, and the selected next-use model.
MCP exposes 18 named tools, including separate speech and cleanup management.
No microphone-start,
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
sona-agent cleanup status
sona-agent settings patch '{"cleanupEnabled":true,"cleanupEngine":"Auto","cleanupTone":"formal","cleanupIdleMinutes":10}'
sona-agent cleanup install
sona-agent cleanup load
sona-agent cleanup unload
sona-agent cleanup cancel
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

Cleanup installation downloads the pinned model and selected portable runtime;
poll `cleanup status` for `installing`, `percent` and `outcome`. Status also
reports the confirmed resident `backend`, `processId`, `modelVramMiB` (GPU model
weights only), worker release state and retained setup cleanup state. Cancel
stops an active setup, or retries retained partial cleanup while idle. Load
requires cleanup to be enabled; unload preserves downloaded files. Disabled
cleanup and Verbatim release its worker. There is no transcript cleanup/export
endpoint. [Local cleanup details](LOCAL-CLEANUP.md).

Version 0.3.1 adds `copyToClipboard` as a validated boolean. It copies a
completed dictation, including when focus changes, without pressing Enter.
It defaults off for new installations and is enabled for the owner on request.

Supported settings: `language`, `engine`, `model`, `modelIdleMinutes`, `theme`,
`recordingDisplay`, `shortcutMode`, `style`, `cleanupEnabled`, `cleanupTone`,
`cleanupEngine`, `cleanupIdleMinutes`,
`device`, `soundEffects`, `liveTranscription`, `pasteCompletedDraft`, `enterToSend`, `autoInsert`, `copyToClipboard`, `closeToTray`, `protectionEnabled`, `silenceIncoming`,
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
expose status, settings get/patch, model list/info/install/verify/cancel/load/unload,
cleanup status/install/cancel/load/unload and protection status/pause/resume.
Responses include structured JSON and compatible
text content. Logs and errors must never pollute protocol stdout. Unknown methods
and malformed/oversized messages receive JSON-RPC errors. This bounded server
implements tools and ping, not resources, sampling or remote HTTP transport.

`tools/agent-smoke.ps1` verifies the actual CLI and MCP against an isolated app
instance without a microphone, global input or actual Discord connection.
`-DownloadTiny` adds one explicit pinned download and selection pilot. Isolation
uses `SONA_DATA`; normal clients must omit that test-only override.

The settings interface also accepts `liveTranscription` (boolean, default false).
Status includes the running module `buildId` and `preferencesLoad` result. These
contain no transcript or filesystem path. Live text remains local to the UI.
