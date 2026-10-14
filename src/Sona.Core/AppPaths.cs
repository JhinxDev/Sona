namespace Sona.Core;

/// <summary>Current Sona paths, with narrow compatibility for existing private profiles.</summary>
public static class AppPaths
{
    public const string DataEnvironmentVariable = "SONA_DATA";
    public const string LegacyDataEnvironmentVariable = "MURMUR_DATA";
    public const string ScratchMarker = "Sona scratch v1";
    public const string LegacyScratchMarker = "Murmur scratch v1";

    public static string? ResolveOverride(string? current, string? legacy) =>
        !string.IsNullOrWhiteSpace(current) ? current : !string.IsNullOrWhiteSpace(legacy) ? legacy : null;

    public static string? DataOverride => ResolveOverride(
        Environment.GetEnvironmentVariable(DataEnvironmentVariable),
        Environment.GetEnvironmentVariable(LegacyDataEnvironmentVariable));

    public static string DataRoot => ResolveDataRoot(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataOverride);

    public static string ResolveDataRoot(string localAppData, string? dataOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(dataOverride)) return dataOverride;
        var current = Path.Combine(localAppData, "Sona");
        var legacy = Path.Combine(localAppData, "Murmur");
        // A runtime-only Sona directory must not hide the owner's earlier preferences,
        // encrypted credentials, history or draft. Reuse the profile without copying it.
        return File.Exists(Path.Combine(current, "preferences.json")) || !Directory.Exists(legacy) ? current : legacy;
    }

    public static string? RuntimeOverride => ResolveOverride(
        Environment.GetEnvironmentVariable("SONA_RUNTIME"), Environment.GetEnvironmentVariable("MURMUR_RUNTIME"));

    // Retain the production gate/open signal so an already installed older Sona
    // cannot run a second global shortcut listener against the same private profile.
    public static string ProductionMutexName => "Local\\Murmur-" + Environment.UserName;
    public static string ProductionOpenName => "Local\\Murmur-open-" + Environment.UserName;
}
