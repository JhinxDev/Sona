using System.Text.Json;
using Sona.Core;

internal static class PreferenceRecoveryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "sona-preference-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "preferences.json");
        bool SaveBlocked(Preferences value)
        {
            try { value.Save(path); return false; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
        }
        try
        {
            var missing = Preferences.LoadWithStatus(path);
            check(missing.Status == PreferencesLoadStatus.Missing && missing.CanSave && !missing.Value.LiveTranscription,
                "A genuinely new profile allows saving and starts without live transcription");
            var saved = new Preferences { Theme = "amethyst", Language = "fr", LiveTranscription = true };
            saved.Save(path);
            var loaded = Preferences.LoadWithStatus(path);
            check(loaded.Status == PreferencesLoadStatus.Loaded && loaded.CanSave && loaded.Value.Theme == "amethyst" && loaded.Value.LiveTranscription,
                "Saved Amethyst and optional live transcription survive reload");
            var original = File.ReadAllBytes(path);
            using (var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var locked = Preferences.LoadWithStatus(path);
                check(locked.Status == PreferencesLoadStatus.Unavailable && !locked.CanSave && !string.IsNullOrWhiteSpace(locked.Message),
                    "An unreadable existing profile is distinct from a new profile and has an actionable warning");
                var clone = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(locked.Value))!;
                check(SaveBlocked(clone), "Cloned defaults cannot overwrite a profile whose load failed");
            }
            check(SaveBlocked(new Preferences()) && File.ReadAllBytes(path).SequenceEqual(original),
                "Releasing a temporary read lock alone cannot allow fallback defaults to replace saved settings");
            loaded = Preferences.LoadWithStatus(path);
            check(loaded.Status == PreferencesLoadStatus.Loaded && loaded.Value.Theme == "amethyst",
                "An explicit successful reload recovers the original saved theme");
            loaded.Value.Save(path);
            foreach (var invalid in new[] { "{broken", "null", "[]", "{\"LiveTranscription\":\"yes\"}" })
            {
                File.WriteAllText(path, invalid);
                var broken = Preferences.LoadWithStatus(path);
                check(broken.Status == PreferencesLoadStatus.Unavailable && SaveBlocked(broken.Value) && File.ReadAllText(path) == invalid,
                    "Invalid existing preference data remains intact and cannot be overwritten by defaults");
                File.WriteAllBytes(path, original);
                _ = Preferences.LoadWithStatus(path);
            }
            File.WriteAllText(path, "{damaged after startup");
            check(SaveBlocked(loaded.Value) && File.ReadAllText(path) == "{damaged after startup",
                "Saving detects a profile damaged after startup and preserves the damaged source for recovery");
            File.WriteAllBytes(path, original);
            loaded = Preferences.LoadWithStatus(path);
            using (var held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                check(SaveBlocked(loaded.Value), "A replacement-denying file lock fails without replacing saved preferences");
            check(File.ReadAllBytes(path).SequenceEqual(original) && Directory.GetFiles(root, "*.tmp").Length == 0,
                "A failed replacement preserves preferences and removes only its temporary candidate");
            loaded.Value.Save(path);
            check(Preferences.Load(path).Theme == "amethyst", "A failed replacement can be retried after its lock releases");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}