# Sona and other local dictation tools

Comparison checked October 13, 2026 against the projects' own documentation.
These are documented capabilities, not a hands-on performance or accuracy test.
Availability and features can change.

Sona's reason to exist is a focused Windows workflow: native settings and
editable drafts, cautious insertion, Discord call protection, and a bounded
CLI/MCP management surface. Offline transcription and agent integrations are
shared ideas in this space. There is no evidence here that Sona is faster,
more accurate, or the only subscription-free option.
Discord is Sona's only implemented call-protection adapter; support for other
call platforms remains future work.

| Area | Sona private preview | Handy | OpenWhispr |
| --- | --- | --- | --- |
| Desktop platforms | Windows x64, native WPF | Windows, macOS and Linux | Windows, macOS and Linux, Electron |
| Speech engines | Six Whisper profiles through whisper.cpp | Whisper and Parakeet | Multiple local engines including Whisper and Parakeet; cloud/BYOK options |
| Main workflow | Focused-field dictation with an editable retained draft; no automatic submission | Shortcut recording and insertion into a text field | Dictation alongside notes, meetings and voice-assistant workflows |
| Automation | Current-user pipe plus JSON CLI and MCP stdio for settings/models/protection; no microphone-start API | Running-instance CLI flags for recording and cancellation | Public API and MCP for notes and transcriptions |
| Account/cloud scope | No sign-in, cloud transcription or sync implemented | Documents offline transcription | Offers local processing and optional cloud services |
| Distribution | Private preview; no published binary yet | Public downloads | Public downloads |

Handy's [official README](https://github.com/cjpais/Handy#readme) documents
offline transcription, configurable recording shortcuts, Whisper/Parakeet
selection, cross-platform releases and command-line flags. Its
[official website](https://handy.computer/) emphasizes a simple local
speech-to-text workflow. It is a useful choice to investigate when that
cross-platform offline scope matches your needs.

OpenWhispr's [official README](https://github.com/OpenWhispr/openwhispr#readme)
documents local and cloud engines, notes, meeting transcription, voice-assistant
features, and API/MCP access. Its [official website](https://openwhispr.com/)
also describes on-device dictation and bringing your own API keys. Investigate
it when you want a broader workspace or cloud-provider choice. This comparison
does not audit its account tiers, cloud charges or each engine's hardware support.

The cited overview pages were not a comprehensive audit of either project's
Discord behavior or insertion safety. Absence from this table is not evidence
that an alternative lacks a feature. Sona's call-protection implementation and
actual-target acceptance are described in its [README](../README.md) and
[verification record](VERIFICATION.md).

Sona uses its own UI, copy and vector artwork. This document paraphrases the
linked primary sources and does not reuse their logos, screenshots or product
assets. Framework differences are architectural descriptions, not measured
claims about memory use, latency or accessibility.
