# Owner-started Sona test

The agent does not start a live microphone or inject global key/input tests. Open **Sona 0.4.0** intentionally and record the exact version/build you use. Close competing dictation tools while testing the shared Ctrl+Win shortcut. These owner-started checks remain open for the new Parakeet integration.

1. In Settings choose **Test shortcut**. Focus an empty unsent Codex composer, hold **Ctrl + Windows** for at least 250 ms, then release. The capsule says Test shortcut/No microphone and the fixed Sona test sentence should appear once. Do not submit. Check no duplicate on a second attempt and clipboard preservation. The prepared test deliberately uses Hold even if normal recording is configured as Toggle.
2. Cancel/clear the test. Focus an empty composer, hold Ctrl+Win and speak, then release both keys. The capsule should show measured audio level, then transcription. Keep focus until the result. Review before sending. Test a short English, French and mixed technical utterance.
3. In **Hold / double-tap** mode, tap Ctrl+Win twice quickly to start hands-free, then tap once to finish. To test legacy tapping, select **Toggle** in Settings and Save preferences. Press Ctrl+Win once to start and again to finish. Esc must cancel either mode, including while modifiers are held. Alternate Ctrl+Alt+Shift+Space/V shortcuts are available only when individually registered.
4. Change focus during an intentional recording. Expect a retained Sona draft rather than insertion into the other field. Test selected microphone switch/unplug and sleep/resume when convenient; no surprise resumed recording or delayed insertion should occur.
5. Review the native UI, minimum-size scrolling, vocabulary/snippet forms and History search/clear. Text history stays in memory unless explicitly enabled; disabling saving leaves earlier saved text until Clear history. If you enable close-to-tray, closing hides the app without discarding an idle draft and reopening restores it; new profiles start with close-to-tray disabled.
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
