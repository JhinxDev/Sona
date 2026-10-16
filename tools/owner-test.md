# Owner-started Sona test

## Short 0.4.4 Homepage check

Read the version/build in Settings → About first. This candidate explains the
suggestion-focus limitation; automatic insertion through active suggestions is
still unsupported. Preserve your current draft.

1. If Windows reports a suggestion or list item, use **Copy draft**, return to
   the Search input and paste manually. Keep it unsent.
2. To compare the empty-input path without microphone capture, use Settings →
   Audio & typing → **Test shortcut**. Return to empty Homepage Search, hold
   physical Ctrl+Windows for at least 250 ms and release. Do not press Enter.
3. The read-only `sona-agent status` now includes `destinationCapture` from the
   actual attempt. An agent can inspect it after the attempt without asking you
   to keep focus in Search for a timed capture. No draft contents need sharing.

The observed complete alternate paste into an empty field is distinct from this
still-pending physical shortcut check. Do not treat synthetic modifier injection
as a recording test; Sona ignores injected recording gestures.

## Short 0.4.3 Homepage check

Record the version/build from About first. Keep the existing retained draft.

1. Focus an empty Search field on Homepage and use Ctrl+Alt+Shift+V to retry
   the ready draft. This shortcut inserts once and does not request Enter.
   Review the result without submitting. If Sona withholds insertion, report
   its specific status and keep the draft.
2. For a synthetic check without microphone capture, prepare Settings → Audio & typing → Test
   shortcut, then return to empty Homepage Search and hold Ctrl+Win for at
   least 250 ms before releasing. Expect one fixed test sentence, no Enter.
3. In a separate attempt, change field or app after the test starts. The draft
   must remain in Sona rather than reaching the new field.

The agent does not press these keys. Actual voice and call checks below remain
separate, deliberate owner gestures.

## Short 0.4.2 acceptance run

Use the coordinated private candidate. Settings → About shows the exact build
and existing profile path. These are owner-started checks; automated fixtures do
not record your microphone, change a real Discord call or send test messages.

1. In an empty unsent composer, dictate a longer paragraph and stop normally.
   Expect a compact processing display and one complete paste. Change focus in a
   separate attempt: the draft must stay in Sona. Confirm Discord's app icon and
   the quiet display outside a text field.
2. Enable Enter to finish and send under Audio & typing and save. First use a
   disposable text-editor document: double-tap Ctrl+Win, speak, then press Enter.
   Expect the completed draft followed by one Enter when readback is supported.
   Repeat using configured Toggle. Missing/ambiguous readback must withhold Enter
   and tell you to review manually. Only use a real chat send for a message you
   deliberately intend to send; there is no automatic test submission.
3. Save an edited draft, approve a short correction, and reopen it from History.
   Verify original/generated/edited text and the removable Vocabulary rule.
   With disposable recent entries, preview a date-range clear and cancel once,
   then confirm. Saved drafts and the current editable text must remain.
4. Review the compact model cards, green selected-family marker, Settings sections,
   toggle geometry and recording tail. Switch to Parakeet's page without selecting
   its model: the selected family must remain unchanged.
5. When using a real Discord call intentionally, verify Connected, Disconnect
   restoration and later Connect. Restart once while disconnected to confirm it
   stays disconnected. A successful fake transport test is not real-call acceptance.

6. In General, try Sound effects on and off. Listen for a soft start cue before
   capture, a ready cue after processing, and a distinct cue after an intentional
   Enter submission. Confirm the sidebar GitHub link opens Sona’s repo.

Report the first failed step and visible status. No private draft or recording
needs to be shared. Existing theme persistence, cancellation and microphone
acceptance checks below remain available for targeted follow-up.

## Extended checks

The agent does not start a live microphone or inject global key/input tests. Open **Sona 0.4.2** intentionally and record the exact version/build you use. Close competing dictation tools while testing the shared Ctrl+Win shortcut. These owner-started checks remain open for the new Parakeet integration.

1. In Settings choose **Test shortcut**. Focus an empty unsent Codex composer, hold **Ctrl + Windows** for at least 250 ms, then release. The capsule says Test shortcut/No microphone and the fixed Sona test sentence should appear once. Do not submit. Check no duplicate on a second attempt and clipboard preservation. The prepared test deliberately uses Hold even if normal recording is configured as Toggle.
2. Cancel/clear the test. Focus an empty composer, hold Ctrl+Win and speak, then release both keys. The capsule should show measured audio level, then transcription. Keep focus until the result. Review before sending. Test a short English, French and mixed technical utterance.

When the popup appears, confirm that it names the original app and uses its
installed icon where available. Codex should read Codex; Discord should read
Discord. Browsers identify the browser process rather than guessing the website.
Change field or app during a recording and check that the original target stays
fixed and the display says the draft will stay in Sona. Test both Panel and Capsule
under a dark and a light theme. These are owner-started checks, not fixture claims.

3. In **Hold / double-tap** mode, tap Ctrl+Win twice quickly to start hands-free, then tap once to finish. To test legacy tapping, select **Toggle** in Settings and Save preferences. Press Ctrl+Win once to start and again to finish. Esc must cancel either mode, including while modifiers are held. Alternate Ctrl+Alt+Shift+Space/V shortcuts are available only when individually registered.
4. Change focus during an intentional recording. Expect a retained Sona draft rather than insertion into the other field. Test selected microphone switch/unplug and sleep/resume when convenient; no surprise resumed recording or delayed insertion should occur.
5. Review the native UI, minimum-size scrolling, vocabulary/snippet forms and History search/clear. Text history stays in memory unless explicitly enabled; disabling saving leaves earlier saved text until Clear history. If you enable close-to-tray, closing hides the app without discarding an idle draft and reopening restores it; new profiles start with close-to-tray enabled.
6. Start Discord, complete your own application connection setup and enable call protection in Sona Settings. Verify mute and restoration in an owner-started real call. Sona protects its own recordings directly; add external apps to the watched list only if you want their dictation protected. Disconnected status is expected when Discord is closed.
7. Enable clipboard backup, change focus during intentional dictation, and paste manually into an empty draft. Test update checks in Settings; until a public GitHub release exists, the correct result is that no public release is available.
8. Under Settings → Text cleanup, choose Polished and GPU / VRAM, enable cleanup
   and save. Fresh installations start with cleanup disabled.
   Check the confirmed GPU memory status. Dictate a spoken correction and a
   French/English project-name sentence, then compare Latest draft with Original
   transcript and try Use original. Repeat with Casual, Lowercase and Verbatim
   when useful. These owner recordings establish subjective formatting and live
   end-to-end delay; synthetic text tests only measured the cleanup stage.

Record the installed version and observed result. Build, fixture replay and listener initialization do not prove real key/mic/Codex behavior. Keep all test drafts unsent.
