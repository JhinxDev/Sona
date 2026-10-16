# Daily-use reliability audit, 0.4.3

This pass follows an owner-reported empty Homepage Search after dictation with
Ctrl + Windows. The recognized draft stayed available. A later screenshot shows
a caret in Search. The exact shortcut-time capture failure has not been reproduced;
the compatibility and diagnostic changes below do not establish its cause.

## Repairs

| Area | Finding and resulting behavior | Evidence |
| --- | --- | --- |
| Target capture | A UI Automation provider can belong to a different process from the foreground window. Capture now proves membership in that exact window through bounded UIA ancestry, tracks provider and window owner separately, and supports a combobox only when it explicitly reports a writable value. Password, disabled, read-only, unrelated-window and changed-focus targets remain blocked. | Metadata policy checks, actual two-window UIA check, insertion and completed-paste regressions. |
| Recovery guidance | A generic missing-field message concealed different capture failures. The retained-draft status now distinguishes tracking, focus, read-only/unsupported control and provider failures without reading or logging field contents. | Shared capture policy and native draft workflow checks. |
| Speech startup | Integrity/runtime verification previously sat outside the protected startup deadline and error normalization. It now shares the bounded startup operation; expected access failures become actionable errors, owner cancellation stays cancellation, and audio/gate cleanup still runs. | Fault, cancellation, queued request and deadline regressions using fake boundaries. |
| Quit | An already-faulted preparation task could escape after disabling the window. Quit now observes finished faults/cancellation and continues resource cleanup; still-pending operations keep the bounded retry path. | Actual isolated MainWindow closes after preparation faults with its synthetic draft intact. |
| Saved history | Saving could overwrite a history file damaged after startup. Save now revalidates the existing file immediately before replacement, including invalid persisted records, and preserves rejected bytes. | Corrupt/null/wrong-shape/oversized/invalid-record and valid-update regressions; native saved-draft/reopen workflows. |
| Settings | An agent patch could replace unsaved choices in the native form. Patches now reject pending edits with save/revert guidance, including edits made while protection configuration is awaiting; rejected transactions restore committed protection. Status exposes only an `unsavedSettings` boolean. | Actual native general/protection controls, persisted bytes and clean-patch recovery; local CLI/MCP smoke. |

Microsoft's [hosted UIA provider issue](https://github.com/microsoft/winappCli/issues/691)
documents the provider/window process distinction. It is general compatibility
evidence, not a diagnosis of this owner's Homepage failure. Capture requires the
exact window's UIA ancestry rather than shared process identity.

## Scope and reuse

| Area | Disposition |
| --- | --- |
| Shortcuts and recording | Existing hold/double-tap/toggle and cancellation behavior retained. Automated checks initialize the listener and use fake capture; physical keys and microphone remain owner-started. |
| Drafts, cleanup and insertion | Manual edits, original/generated separation, cancellation and same-field paste guards checked. Local engines, model choices and cleanup defaults retained. |
| History and personalization | Save/reopen, retention-off and damaged-file preservation checked. Existing theme and appearance behavior retained. |
| Discord | Existing fake-transport/restoration regressions reused in the Windows suite. No real call was connected or changed. Unsaved protection settings now participate in the patch guard. |
| Interface and accessibility | Existing palette, keyboard and compact-layout implementation retained; native affected layout checked. No new visual redesign or screen-reader acceptance claimed. |
| Resources and performance | Startup verification is now inside the deadline. Worker containment and cleanup regressions checked. Existing recognition/cleanup benchmarks reused because engines, pins, inference paths and chunking did not change. This pass makes no new latency/WER/GPU claim. |
| Installation and launch | Existing desktop-owned launcher and guarded upgrade retained. Package qualification uses an isolated profile. Source, package, installed version and running version must be recorded separately. |
| Agent interface | Same current-user pipe and bounded CLI/MCP methods retained. No microphone, transcript-export or submission endpoint added. |

## Diagnostic and owner check

`Sona.WindowsTests --capture-diagnostic <fresh-json>` passively samples the actual
capture code for 30 seconds. It records status, control type, process identities,
focus epoch and test-assembly hash; no text, window titles, microphone or input.
Use it only for a bounded, intentional reproduction. An observation of another
active app does not qualify Homepage.

The decisive remaining check is an owner-started attempt in empty Homepage
Search on the exact candidate. [The short procedure](../tools/owner-test.md)
uses a retained draft or the existing microphone-free Test shortcut first, with
no Enter submission. Preserve the original draft if insertion is withheld.

Builds, simulated boundaries, file replays and personal target-app acceptance
remain separate. [Verification receipts and commands](PREVIEW-VALIDATION.md).
