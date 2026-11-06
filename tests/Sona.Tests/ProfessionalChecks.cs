using Sona.Core;

internal static class ProfessionalChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Per-app mode resolution: friendly name, executable and package identities,
        // first-match ordering, case folding and safe misses.
        check(AppModes.Resolve([new AppMode("Codex", "Verbatim", "casual")], ["codex", "Codex.exe"]) != null, "Per-app mode matches the friendly app name case-insensitively");
        check(AppModes.Resolve([new AppMode("codex.exe", "Notes", "formal")], ["Codex"]) is { Style: "Notes" }, "Per-app mode treats Codex.exe and Codex as the same app");
        check(AppModes.Resolve([new AppMode("Discord", "Notes", "casual"), new AppMode("discord.exe", "Clean", "formal")], ["discord"]) is { Style: "Notes" }, "First matching per-app rule wins");
        check(AppModes.Resolve([new AppMode("Codex", "Verbatim", "casual")], ["chrome", "Discord"]) == null, "Unmatched apps keep the default mode");
        check(AppModes.Resolve([new AppMode("Codex", "Verbatim", "casual")], []) == null && AppModes.Resolve(null, ["codex"]) == null, "Missing identities or rules resolve to the default mode");
        check(AppModes.Resolve([new AppMode("OpenAI.ChatGPT_2p2nqsd0c76g0", "Clean", "formal")], ["openai.chatgpt_2p2nqsd0c76g0"]) != null, "Per-app mode matches a packaged app identity");
        check(!AppModes.ValidName(" ") && !AppModes.ValidName(new string('a', 161)) && !AppModes.ValidName("code\nx"), "Per-app rule names are bounded and control characters are rejected");
        check(AppModes.ValidStyle("Verbatim") && !AppModes.ValidStyle("Bogus") && AppModes.ValidTone("very casual") && !AppModes.ValidTone("loud"), "Per-app rules accept only the supported styles and tones");

        var normalized = new Preferences
        {
            AppModes =
            [
                null!, new AppMode(" ", "Clean", "formal"), new AppMode("Codex", "Bogus", "formal"),
                new AppMode("codex", "Notes", "casual"), new AppMode("Discord", "Notes", "nonsense")
            ]
        };
        normalized.Normalize();
        check(normalized.AppModes.Count == 2 && normalized.AppModes[0] == new AppMode("Codex", "Clean", "formal"), "Saved per-app rules normalize invalid styles, tones and duplicate names");
        check(normalized.AppModes[1] == new AppMode("Discord", "Notes", "formal"), "Invalid cleanup tone falls back to the polished default");

        var longList = new Preferences { AppModes = Enumerable.Range(0, 30).Select(index => new AppMode("App" + index, "Clean", "formal")).ToList() };
        longList.Normalize();
        check(longList.AppModes.Count == AppModes.Maximum, "Saved per-app rules stay within the documented bound");

        var roundTripPath = Path.Combine(Path.GetTempPath(), "sona-modes-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var stored = new Preferences { OnboardingDismissed = true, AppModes = [new AppMode("Codex", "Verbatim", "casual")] };
            stored.Save(roundTripPath);
            var loaded = Preferences.Load(roundTripPath);
            check(loaded.AppModes.Single() == new AppMode("Codex", "Verbatim", "casual") && loaded.OnboardingDismissed, "Per-app modes and welcome dismissal persist across relaunch");
        }
        finally { File.Delete(roundTripPath); }

        // Onboarding state: welcome only for a genuinely new profile; setup guidance
        // whenever the selected model is unusable.
        var fresh = Onboarding.Evaluate(PreferencesLoadStatus.Missing, false, false);
        check(fresh is { ShowWelcome: true, ShowSetup: true, ModelReady: false }, "A new profile with no ready model shows welcome and setup guidance");
        check(!Onboarding.Evaluate(PreferencesLoadStatus.Missing, false, true).ShowSetup, "A new profile with a ready model shows no setup callout");
        check(!Onboarding.Evaluate(PreferencesLoadStatus.Missing, true, false).ShowWelcome, "Dismissed welcome stays dismissed on a new profile");
        check(!Onboarding.Evaluate(PreferencesLoadStatus.Loaded, false, true).ShowWelcome, "Existing profiles never see the first-run welcome card");
        check(Onboarding.Evaluate(PreferencesLoadStatus.Loaded, true, false) is { ShowWelcome: false, ShowSetup: true }, "A loaded profile with a missing model still shows setup guidance");

        // Word count: bounded, punctuation-safe and stable across identities.
        check(TextStats.WordCount("Hello world") == 2 && TextStats.WordCount("  ") == 0 && TextStats.WordCount(null) == 0, "Draft word count handles ordinary and empty text");
        check(TextStats.WordCount("Don't stop") == 2 && TextStats.WordCount("state-of-the-art") == 1, "Draft word count keeps contracted and hyphenated words together");
        check(TextStats.WordCount("foo_bar = 42") == 2 && TextStats.WordCount("version 2.5") == 2, "Draft word count handles identifiers and decimals");
        check(TextStats.WordLabel("") == "0 words" && TextStats.WordLabel("one") == "1 word", "Draft word count labels singular and plural correctly");

        // Durable atomic replacement used by preferences, history and credentials.
        var atomicPath = Path.Combine(Path.GetTempPath(), "sona-atomic-" + Guid.NewGuid().ToString("N"), "state.json");
        try
        {
            AtomicFile.WriteAllText(atomicPath, "first");
            AtomicFile.WriteAllText(atomicPath, "second");
            check(File.ReadAllText(atomicPath) == "second" && Directory.GetFiles(Path.GetDirectoryName(atomicPath)!, "*.tmp").Length == 0, "Atomic replacement commits the new content and leaves no temporary file");
            var rejected = false;
            try { AtomicFile.WriteAllText(atomicPath, "third", () => throw new IOException("fixture rejection")); }
            catch (IOException) { rejected = true; }
            check(rejected && File.ReadAllText(atomicPath) == "second" && Directory.GetFiles(Path.GetDirectoryName(atomicPath)!, "*.tmp").Length == 0, "Rejected atomic replacement preserves the committed file and cleans its candidate");
        }
        finally { Directory.Delete(Path.GetDirectoryName(atomicPath)!, true); }
    }
}
