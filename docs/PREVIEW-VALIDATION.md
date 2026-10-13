# Preview validation

Sona 0.4.0 is an early source preview. The implementation was independently
reviewed on October 13, 2026. Publishing the source does not turn fixture results
into personal microphone or target-app acceptance.

## What the evidence covers

- 277 core checks and 47 Windows integration checks passed in the retained
  qualification receipts. The Windows set includes fake Discord behavior; it
  does not open a physical microphone or inject the global shortcut.
- 19 native speech checks cover verified CPU/CUDA assets, model lifecycle and a
  restored 80.88-second synthetic long-tail sentinel. Exact chunk coverage is
  separate from recognition quality across natural speech boundaries.
- 19 agent-interface checks cover the JSON CLI and its 18 MCP tools. Normal and
  compact model UI checks passed; prior theme/cleanup checks were reused where
  their implementation stayed unchanged.
- Fresh trailer/screenshots use the unchanged 0.4.0 application assembly, a
  separate demo profile and synthetic text. The supplied generated narration was
  transcribed through the real local-model path. No live microphone, clipboard
  insertion or Discord call was recorded. The demo's disconnected Discord state
  is expected when Discord is closed.

The public README screenshots show real native controls and actual Parakeet /
cleanup residency. They are demonstrations, not an accuracy or latency promise.

## Open checks and known limits

1. Personal microphone, accent, focus/clipboard handling and insertion into the
   intended text field still need an owner-started test of the new Parakeet path.
   Follow [the owner test](../tools/owner-test.md); leave test messages unsubmitted.
2. The pinned Whisper b5454 CUDA engine crashed in a mixed English/French
   resident sequence. Disabling CUDA graphs did not resolve that recorded case.
   This is a known runtime issue, not a claim that every Whisper request fails.
3. Automatic model integrity hashing happens before the worker's 60-second
   startup deadline. A bounded verification deadline and a focused regression
   are appropriate follow-up work; a slow-disk stall was not observed here.
4. The cancellation fixture uses an already-cancelled token. Actual queued
   cancellation and forced readiness/request timeout expiry need stronger native
   receipts. Source deadlines already exist.
5. Natural continuous speech across the independent long-recording chunks needs
   a licensed 60–120-second fixture with per-boundary omissions/duplications
   before changing chunking or selecting models from the small benchmark alone.

See [verification](VERIFICATION.md), [Parakeet](PARAKEET.md) and
[cleanup](LOCAL-CLEANUP.md) for retained methodology and runtime provenance.
There is no published binary release yet. Contributions and feature suggestions
are welcome; the main README links to the current build instructions.
