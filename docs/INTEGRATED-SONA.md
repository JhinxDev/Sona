# Integrated Sona

Owner accepted the Sona 0.1.1 interface. Preserve its sidebar, grouped settings,
voice capsule and unsent insertion behavior while extending the existing app.

## Local first

No account is required for dictation, history, vocabulary, downloadable models or
agent settings access. Keep transcripts and recordings on the user's machine.
Model downloads are explicit user or agent operations with pinned size and SHA256,
progress, cancellation and no replacement of differing existing files.

Model catalog: six multilingual Whisper models, pinned whisper.cpp conversion
revision 5359861c739e955e79d9a303bcbc70fb988958b1. Download sizes describe files,
not runtime RAM or VRAM. Only Small CPU and Turbo CUDA have Sona measurements:
16 licensed FLEURS clips, eight per language. Display WER and median seconds with
sample size and hardware/method context, not invented rankings. Other models
remain explicitly unmeasured until tested. Medium and Large may exceed the
bounded worker allocation and must report failure without damaging the draft.

## Local text cleanup research

The October 13 [local cleanup pilot](CLEANUP-RESEARCH.md) measures an openly
licensed small language model on synthetic English/French/technical text.
It is separate from installed 0.3.1 behavior. Consult its latency, fidelity
limitations, protected-term pilot and implementation boundary before extending
the production text pipeline.

## Agent integration

An installed `sona-agent` console binary will use a current-user-only local named
pipe to the running desktop app. Settings changes go through a bounded whitelist,
busy checks and the same persistence used by the UI. JSON success/error envelopes
and MCP stdio expose the same operations. No HTTP listener, sign-in, transcript
export or microphone-start endpoint is needed. Never return credentials through
status, settings or errors. Agents may select/download models deliberately.

## Hush fusion and retirement

Port mic watching and Discord mute/deafen ownership into Sona rather than running
a hidden second Hush process. Reconcile overlapping recording apps, acquire only
changes acknowledged by Discord, release only owned state, preserve manual mute,
serialize reconnect/pause/delayed release/quit, and protect before Sona opens its
own microphone. Runtime credential migration stays private and uses Windows
protection; agents do not read credentials. Only retire matching Hush installation,
startup and shortcuts after bounded restoration and qualified replacement. Do not
blindly unmute Discord or delete source/history to make the desktop look clean.

## Optional Google sync, separate from identity

Google sign-in authenticates a person; it does not itself store Sona settings.
The recommended future optional integration is desktop OAuth with PKCE and an
external system browser, requesting only Drive's non-sensitive `drive.appdata`
scope. The private per-app `appDataFolder` can store explicitly opted-in settings,
vocabulary and snippets without a Sona account/email database. Google handles the
identity and storage account. The user can disconnect or delete that app data.

No audio, transcripts, credentials, local paths or device IDs belong in sync.
Default is off. Use versioned export schemas, bounded merge/conflict review,
encrypted local refresh-token storage, and explicit connect/disconnect controls.
Google OAuth client registration and consent configuration are necessary before
shipping this optional feature. Do not deploy a backend or upload data merely
because an account option was discussed. Implement portable export/import first.

Primary references:

- https://github.com/openai/whisper
- https://huggingface.co/ggerganov/whisper.cpp/tree/5359861c739e955e79d9a303bcbc70fb988958b1
- https://developers.google.com/workspace/drive/api/guides/appdata
- https://developers.google.com/identity/protocols/oauth2/native-app
- https://modelcontextprotocol.io/specification/2025-11-25/server/tools
- https://docs.discord.com/developers/topics/rpc

## Release proof

Tests and source preparation are not installed behavior or physical acceptance.
Qualify the combined candidate, safe Hush restoration, installed hashes, private
Git convergence, native UI and owner Codex/call behavior separately. Eventual
open-source publication requires a sanitized release and an explicit publication
decision; private repository history is not a public distribution artifact.
