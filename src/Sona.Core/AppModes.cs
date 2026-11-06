namespace Sona.Core;

/// <summary>One ordered per-app override of the draft style and local cleanup tone.</summary>
public sealed record AppMode(string App, string Style, string Tone)
{
    public static string StyleLabel(string style) => style switch { "Verbatim" => "Verbatim", "Notes" => "Notes", _ => "Clean" };
    public static string ToneLabel(string tone) => tone switch { "casual" => "Casual", "very casual" => "Lowercase", _ => "Polished" };
}

/// <summary>Ordered app identity to mode resolution. Never reads window titles or field text.</summary>
public static class AppModes
{
    public const int Maximum = 20;
    public const int MaximumNameLength = 160;
    private static readonly string[] Styles = ["Clean", "Verbatim", "Notes"];
    private static readonly string[] Tones = ["formal", "casual", "very casual"];

    public static bool ValidName(string? app) =>
        !string.IsNullOrWhiteSpace(app) && app.Trim().Length <= MaximumNameLength && !app.Any(char.IsControl);

    public static bool ValidStyle(string? style) => style is not null && Styles.Contains(style, StringComparer.Ordinal);
    public static bool ValidTone(string? tone) => tone is not null && Tones.Contains(tone, StringComparer.Ordinal);

    /// <summary>First matching rule wins. Identities come from the captured destination app only.</summary>
    public static AppMode? Resolve(IEnumerable<AppMode>? modes, IEnumerable<string?>? identities)
    {
        if (modes == null || identities == null) return null;
        var keys = identities
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Select(identity => Normalize(identity!))
            .Where(identity => identity.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (keys.Count == 0) return null;
        foreach (var mode in modes)
        {
            if (mode == null || !ValidName(mode.App)) continue;
            var wanted = Normalize(mode.App);
            if (keys.Contains(wanted)) return mode;
            // "Codex.exe" and "Codex" address the same executable; a package identifier
            // and a friendly display name are compared only by exact normalized text.
            if (keys.Any(key => WithoutExecutableSuffix(key) == WithoutExecutableSuffix(wanted))) return mode;
        }
        return null;
    }

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static string WithoutExecutableSuffix(string value) =>
        value.EndsWith(".exe", StringComparison.Ordinal) ? value[..^4] : value;
}
