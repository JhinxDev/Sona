# Daily-use reliability audit, 0.4.3 and 0.4.4

This pass follows an owner-reported empty Homepage Search after dictation with
Ctrl + Windows. The recognized draft stayed available despite a visible caret.
Later bounded desktop tests identified an observable trigger: the browser search
integration uses `role=combobox`, `aria-controls` and `aria-activedescendant`.
Windows reports the selected ListItem while the DOM input retains its typing
caret. The empty field instead reports a writable ComboBox. The installed 0.4.3
capture accepts that empty field, and alternate paste inserted the entire fixed
test sentence without Enter. The destination lowercased the query; exact-case
readback was therefore unconfirmed. Synthetic Ctrl+Windows modifiers did not
change the reported ListItem; injected recording gestures are deliberately ignored.

This establishes the accessibility mismatch, not a successful physical shortcut
repair. Version 0.4.4 classifies ListItems as `SuggestionFocused` and offers manual
paste. It keeps them rejected. `ControllerFor` links alone cannot distinguish
virtual focus from actual keyboard focus on an option. A general automatic repair
needs independent current editable-focus proof plus a negative real-option-focus
test; this candidate does not provide that proof or change the browser integration.

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

Version 0.4.4 records the actual recording, Test shortcut or ready-draft capture
in memory. `sona-agent status` returns nullable `destinationCapture` without
another inspection. It contains status, type, exact HWND/process identities,
start/end focus epochs, stability, containment and a transient identity hash.
It contains no text, labels, window titles or raw runtime IDs and is not persisted.

The maintained `--capture-diagnostic <fresh-json> [decimal-HWND]` tool passively
samples that same capture for 20 seconds. Its schema 3 observations preserve one
coherent capture instead of combining independently sampled focused elements or
hidden tab descendants. A different active app or zero samples cannot qualify the
intended target. Use actual-attempt status when diagnosing the physical shortcut.

The remaining owner-started check is in [the short procedure](../tools/owner-test.md).
Automatic insertion with an active suggestion remains an explicit compatibility
gap. Copy draft and manual paste is the fallback; an empty-field alternate-paste
observation does not establish physical Ctrl+Windows or live-microphone acceptance.

Builds, simulated boundaries, file replays and personal target-app acceptance
remain separate. [Verification receipts and commands](PREVIEW-VALIDATION.md).

## October 20, responsiveness and delivery follow-up

The owner requested earlier popup feedback, quicker results where safely possible,
readable hover help, a compact pill, a quick Auto cleanup button, a longer recording
limit, queued Enter-to-send and ordinary Ctrl+V recovery. The0.4.8 implementation
and maintained synthetic qualification are documented in [RESPONSIVENESS.md](RESPONSIVENESS.md).
This does not close physical Homepage/suggestion focus or real microphone acceptance.
