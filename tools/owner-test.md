# Owner-started native test

The agent never starts a live microphone recording. Open the installed Murmur app intentionally.

1. Choose Prepare insertion test. Focus an empty, unsent Codex draft and press Ctrl+Alt+Shift+V. Expect the fixed English/French/technical sentence to appear once. Verify the chat did not submit. Press the shortcut again and verify no duplicate. The original clipboard should be unchanged.
2. Add Murmur.exe through the existing Hush Apps page before a real call test. Keep Hush's other watched apps and authentication intact. This is independent of enabling Windows startup.
3. Focus the empty Codex draft. Hold Ctrl+Alt+Space while speaking English, French, and a mixed technical sentence. Release all keys. Expect one draft insertion. Original remains available in Murmur. No Enter is sent.
4. Start intentional recording, press Esc, then wait. Expect no insertion or leftover transcription worker. Start another recording, change focus to another field, then stop. Expect the draft to remain in Murmur. Use Ctrl+Alt+Shift+V deliberately in the chosen field for retry.
5. During an intentional record, disconnect/switch the selected microphone and test sleep/resume when convenient. Expect a clear unavailable/cancelled state, no surprise resumed capture, and no automatic insertion. A friend must verify Hush prevents prompt audio from reaching the real call and releases its owned mute afterward.

Do not send the test text. Record observed failures and exact candidate version; build/fixture success is separate from these physical gates.
