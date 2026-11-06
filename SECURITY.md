# Security policy

## Reporting a vulnerability

Report security issues privately to the repository owner through GitHub's
private vulnerability reporting on
https://github.com/JhinxDev/Sona (Security tab, "Report a vulnerability").
Do not open a public issue for a vulnerability, and do not include private
transcripts, recordings, credentials, Discord setup values or other personal data
in a report.

Include the Sona version, Windows version, a short description of the issue and
the smallest reproduction you can share. Redact any personal text.

## Scope

Sona is a local-first Windows application. In scope: local data handling
(drafts, history, preferences, encrypted Discord setup), insertion and clipboard
safety, update checks, model and runtime download verification, the current-user
CLI/MCP pipe, and the guarded installer and upgrade scripts.

Out of scope: third-party models, engines, drivers and runtimes distributed
under their own licenses; Discord platform behavior; and any behavior that
requires access to the reporter's own profile data.

No Sona account, cloud speech service or telemetry exists, so account takeover
and server-side data exposure are not part of this application's surface.

## Supported versions

Only the current preview version is maintained. There is no published binary or
long-term support branch yet; build from reviewed source at the current head.
