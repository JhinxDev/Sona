# Owner-started Sona test

The agent does not start a live microphone or inject global key/input tests. Open the installed **Sona 0.3.2** app intentionally. Quit Spokenly/Wispr Flow while testing the shared Ctrl+Win shortcut.

1. In Settings choose **Test shortcut**. Focus an empty unsent Codex composer, hold **Ctrl + Windows** for at least 250 ms, then release. The capsule says Test shortcut/No microphone and the fixed Sona test sentence should appear once. Do not submit. Check no duplicate on a second attempt and clipboard preservation. The prepared test deliberately uses Hold even if normal recording is configured as Toggle.
2. Cancel/clear the test. Focus an empty composer, hold Ctrl+Win and speak, then release both keys. The capsule should show measured audio level, then transcription. Keep focus until the result. Review before sending. Test a short English, French and mixed technical utterance.
3. In **Hold / double-tap** mode, tap Ctrl+Win twice quickly to start hands-free, then tap once to finish. To test legacy tapping, select **Toggle** in Settings and Save preferences. Press Ctrl+Win once to start and again to finish. Esc must cancel either mode, including while modifiers are held. Alternate Ctrl+Alt+Shift+Space/V shortcuts are available only when individually registered.
4. Change focus during an intentional recording. Expect a retained Sona draft rather than insertion into the other field. Test selected microphone switch/unplug and sleep/resume when convenient; no surprise resumed recording or delayed insertion should occur.
5. Review the supplied-reference UI, minimum-size scrolling, vocabulary/snippet forms and History search/clear. Text history stays in memory unless explicitly enabled; disabling saving leaves earlier saved text until Clear history. Closing hides to tray without discarding an idle draft, and reopening restores the same text.
6. In Sona Settings, enable Discord call protection. Verify mute and restoration in an owner-started real call. Only ChatGPT/Codex need to be watched for this owner; Sona protects its own recordings directly. Hush is retired.
7. Enable clipboard backup, change focus during intentional dictation, and paste manually into an empty draft. Test update checks in Settings; until a public GitHub release exists, the correct result is that no public release is available.
8. Under Settings → Text cleanup, choose Polished and GPU / VRAM, enable cleanup
   and save. The owner configuration is enabled; fresh installations start off.
   Check the confirmed GPU memory status. Dictate a spoken correction and a
   French/English project-name sentence, then compare Latest draft with Original
   transcript and try Use original. Repeat with Casual, Lowercase and Verbatim
   when useful. These owner recordings establish subjective formatting and live
   end-to-end delay; synthetic text tests only measured the cleanup stage.

Record the installed version and observed result. Build, fixture replay and listener initialization do not prove real key/mic/Codex behavior. Keep all test drafts unsent.
