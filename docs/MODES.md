# Per-app modes

Sona can use a different draft style and local cleanup tone when you dictate into
a specific app. The rule is matched against the app that owns the captured
destination field. Voice capture, insertion guards, focus checks, submission and
privacy defaults are unchanged.

## Managing rules

Open **Modes**, then use **Per-app modes**:

1. Enter an app name or executable, for example `Codex`, `Discord` or
   `chrome.exe`. Matching is case-insensitive and `Codex` and `Codex.exe` address
   the same app.
2. Choose a draft style: **Clean**, **Verbatim** or **Notes**.
3. Choose a cleanup tone: **Polished**, **Casual** or **Lowercase**.
4. Add the rule, then press **Save preferences** when the unsaved bar appears.

The first matching rule wins. Unmatched apps use the default mode chosen above
the per-app list. Up to 20 rules are stored with the rest of your local
preferences. Removing a rule uses the same explicit Save.

## What a mode changes

- The draft style controls transcript formatting in the same way as the default
  mode: Clean applies vocabulary and removes explicit fillers, Verbatim keeps the
  recognized words, and Notes supports spoken paragraph commands.
- The cleanup tone is passed to the optional local cleanup model's prompt for
  that dictation. It is only used when local cleanup is enabled and the session
  is not Verbatim or a whole-utterance snippet.
- A rule applies only to the session whose destination was captured. Later
  dictation into another app uses that app's rule or the default mode.

The mode does not change recognition, insertion, Enter-to-send, clipboard
recovery or Discord protection behavior.

## Matching and privacy

Sona builds short identity strings from the captured destination process only:
process name, friendly name, executable file name and, for packaged desktop apps,
the package identity. It never reads a window title, field text or document
content for matching, and it writes no app history for this feature. Rules are
local preferences; nothing is uploaded.

Some hosts and browsers report a different process for the focused field
(for example an embedded browser control). In that case no rule may match and the
default mode is used. This is a compatibility limit, not a silent override.

## Also native-only

Per-app modes and the first-run welcome dismissal are managed in the Sona UI.
They are intentionally not part of the bounded CLI/MCP settings patch surface, so
an automation client cannot change them. The remaining validated settings keep
their existing agent controls.
