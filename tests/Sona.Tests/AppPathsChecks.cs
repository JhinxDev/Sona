using Sona.Core;

internal static class AppPathsChecks
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "sona-profile-compat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var legacy = Path.Combine(root, "Murmur");
        var current = Path.Combine(root, "Sona");
        var overrideRoot = Path.Combine(root, "isolated");
        try
        {
            check(AppPaths.ResolveDataRoot(root) == current, "New profiles use the Sona data directory");
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy, "preferences.json"), "{\"Theme\":\"amethyst-glass\",\"AutoInsert\":false}");
            File.WriteAllText(Path.Combine(legacy, "discord.dpapi"), "opaque fixture");
            var original = File.ReadAllBytes(Path.Combine(legacy, "preferences.json"));
            check(AppPaths.ResolveDataRoot(root) == legacy, "Earlier private profiles remain discoverable after source rename");
            Directory.CreateDirectory(Path.Combine(current, "runtime"));
            check(AppPaths.ResolveDataRoot(root) == legacy, "A new Sona runtime directory cannot hide earlier preferences or credentials");
            check(AppPaths.ResolveDataRoot(root, overrideRoot) == overrideRoot && !Directory.Exists(overrideRoot), "Explicit isolated profile wins without creating or copying data");
            check(Preferences.Load(Path.Combine(AppPaths.ResolveDataRoot(root), "preferences.json")) is { Theme: "amethyst", AutoInsert: false }, "Earlier settings deserialize under the renamed assembly");
            File.WriteAllText(Path.Combine(current, "preferences.json"), "{}");
            check(AppPaths.ResolveDataRoot(root) == current, "An established Sona profile takes precedence over the legacy profile");
            check(File.ReadAllBytes(Path.Combine(legacy, "preferences.json")).SequenceEqual(original) && File.ReadAllText(Path.Combine(legacy, "discord.dpapi")) == "opaque fixture", "Profile discovery leaves original settings and encrypted payloads untouched");
            check(AppPaths.ResolveOverride("current", "legacy") == "current" && AppPaths.ResolveOverride(" ", "legacy") == "legacy" && AppPaths.ResolveOverride(null, " ") == null, "Current overrides win while earlier aliases and blank values remain safe");
            var currentEnv = Environment.GetEnvironmentVariable(AppPaths.DataEnvironmentVariable);
            var legacyEnv = Environment.GetEnvironmentVariable(AppPaths.LegacyDataEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(AppPaths.DataEnvironmentVariable, null);
                Environment.SetEnvironmentVariable(AppPaths.LegacyDataEnvironmentVariable, overrideRoot);
                var oldPipe = AgentProtocol.PipeName;
                Environment.SetEnvironmentVariable(AppPaths.DataEnvironmentVariable, overrideRoot);
                Environment.SetEnvironmentVariable(AppPaths.LegacyDataEnvironmentVariable, null);
                check(AgentProtocol.PipeName == oldPipe && AgentProtocol.PipeName.StartsWith("Sona-Agent-"), "Current and legacy test aliases address the same scoped Sona agent pipe");
            }
            finally
            {
                Environment.SetEnvironmentVariable(AppPaths.DataEnvironmentVariable, currentEnv);
                Environment.SetEnvironmentVariable(AppPaths.LegacyDataEnvironmentVariable, legacyEnv);
            }
        }
        finally
        {
            foreach (var file in new[] { Path.Combine(legacy, "preferences.json"), Path.Combine(legacy, "discord.dpapi"), Path.Combine(current, "preferences.json") })
                if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(Path.Combine(current, "runtime"))) Directory.Delete(Path.Combine(current, "runtime"));
            if (Directory.Exists(current)) Directory.Delete(current);
            if (Directory.Exists(legacy)) Directory.Delete(legacy);
            Directory.Delete(root);
        }
    }
}
