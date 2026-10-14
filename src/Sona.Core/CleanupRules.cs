using System.Text.RegularExpressions;

namespace Sona.Core;

/// <summary>Bounded preservation checks for optional, untrusted model cleanup.</summary>
public static class CleanupRules
{
    public const int MaximumInputLength = 4000;
    public const int MaximumPromptLength = 6000;
    public const int MaximumOutputLength = 6000;
    private static readonly Regex Literal = new(@"`[^`]*`|https?://\S+|(?<!\S)\S*[/\\]\S+|\b[\w]+[._][\w.]+\b|\b[A-Za-z_]+\([^)]*\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    internal static readonly Regex Tags = new(@"\[TERM_\d+\]", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Negations = new(@"(?<![\w])(?:not|never|without|cannot|can't|can’t|don't|don’t|doesn't|doesn’t|won't|won’t|shouldn't|shouldn’t|sans|pas|jamais)(?![\w])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Leakage = new(@"(?:^|\n)\s*(?:app|tone|text|out)\s*:|<\||\|>|</?(?:think|thinking)>|(?:^|\n)\s*(?:Here is (?:the|your) (?:cleaned|corrected|formatted)|Sure[,!]|As an AI)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Instruction = new(@"\b(?:ignore|disregard)\s+(?:(?:all|the)\s+)?(?:previous|above|prior|system)\s+instructions\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static ProtectedCleanup? Prepare(string text, Preferences settings, string app)
    {
        try { return PrepareCore(text, settings, app); }
        catch (RegexMatchTimeoutException) { return null; }
    }

    private static ProtectedCleanup? PrepareCore(string text, Preferences settings, string app)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputLength || InvalidCharacters(text) || Reserved(text) || text.Contains("<|", StringComparison.Ordinal)) return null;
        if (app == null || app.Length > 160 || app.Any(char.IsControl) || Reserved(app) || app.Contains("<|", StringComparison.Ordinal)) return null;
        if (settings.CleanupTone is not ("formal" or "casual" or "very casual")) return null;
        var spans = Literal.Matches(text).Cast<Match>().Select(m => (Start: m.Index, Length: m.Length)).ToList();
        var terms = settings.Terms.Concat(settings.Vocabulary.Select(v => v.Written)).Append("cloud")
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length <= 120).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var term in terms)
        {
            var pattern = @"(?<![\w])" + Regex.Escape(term) + @"(?![\w])";
            foreach (Match match in Regex.Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                spans.Add((match.Index, match.Length));
        }
        // Prefer the widest span at a shared start, then skip any overlap.
        var originals = new List<string>();
        var protectedText = new System.Text.StringBuilder();
        var cursor = 0;
        foreach (var span in spans.OrderBy(s => s.Start).ThenByDescending(s => s.Length))
        {
            if (span.Start < cursor) continue;
            protectedText.Append(text, cursor, span.Start - cursor);
            protectedText.Append($"[TERM_{originals.Count}]");
            originals.Add(text.Substring(span.Start, span.Length));
            cursor = span.Start + span.Length;
        }
        protectedText.Append(text, cursor, text.Length - cursor);
        var prompt = $"app: {app}\ntone: {settings.CleanupTone}\ntext: {protectedText}\nout:\n";
        if (prompt.Length > MaximumPromptLength) return null;
        return new ProtectedCleanup(prompt, text, originals.ToArray());
    }

    internal static bool Reserved(string value) => value.Contains("[TERM", StringComparison.OrdinalIgnoreCase);
    internal static bool InvalidCharacters(string value) => value.Any(c => (char.IsControl(c) && c is not ('\r' or '\n' or '\t')) || c is >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069');
    internal static bool Leaks(string value) => Leakage.IsMatch(value);
    internal static bool PreservesLiteralInstruction(string original, string restored) => !Instruction.IsMatch(original) || Instruction.IsMatch(restored);
    internal static bool PreservesNegation(string original, string restored)
    {
        // Narrow safeguard, not a claim of general semantic validation. Discourse
        // "no" is excluded so an explicit Tuesday/no/Wednesday correction can work.
        var required = Negations.Matches(original).Cast<Match>().GroupBy(m => m.Value.Replace('’', '\'').ToLowerInvariant());
        var present = Negations.Matches(restored).Cast<Match>().GroupBy(m => m.Value.Replace('’', '\'').ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count());
        return required.All(g => present.TryGetValue(g.Key, out var count) && count >= g.Count());
    }
}

public sealed class ProtectedCleanup
{
    private readonly string original;
    private readonly string[] originals;
    public string Prompt { get; }
    internal ProtectedCleanup(string prompt, string original, string[] originals)
    { Prompt = prompt; this.original = original; this.originals = originals; }

    public bool TryRestore(string output, out string restored)
    {
        try { return TryRestoreCore(output, out restored); }
        catch (RegexMatchTimeoutException) { restored = string.Empty; return false; }
    }

    private bool TryRestoreCore(string output, out string restored)
    {
        restored = string.Empty;
        if (string.IsNullOrWhiteSpace(output) || output.Length > CleanupRules.MaximumOutputLength || CleanupRules.InvalidCharacters(output) || CleanupRules.Leaks(output)) return false;
        var tags = CleanupRules.Tags.Matches(output);
        if (tags.Count != originals.Length) return false;
        for (var i = 0; i < tags.Count; i++) if (tags[i].Value != $"[TERM_{i}]") return false;
        var index = 0;
        var candidate = CleanupRules.Tags.Replace(output, _ => originals[index++]).Trim();
        if (candidate.Length == 0 || candidate.Length > CleanupRules.MaximumOutputLength || CleanupRules.Reserved(candidate) || !CleanupRules.PreservesNegation(original, candidate) || !CleanupRules.PreservesLiteralInstruction(original, candidate)) return false;
        restored = candidate;
        return true;
    }
}
