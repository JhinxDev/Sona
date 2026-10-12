using System.Text.RegularExpressions;
namespace Murmur.Core;

public static partial class TextPipeline
{
    // Formatting acts outside literal tokens. It does not invent or paraphrase words.
    private static readonly Regex Protected = new(@"`[^`]*`|https?://\S+|\S*[/\\]\S+|\b[\w]+[._][\w.]+\b|\b[A-Za-z_]+\([^)]*\)", RegexOptions.CultureInvariant);
    private static readonly Regex Filler = new(@"(?<![\w])(?:um+|uh+|erm|euh+)(?![\w])[, ]*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static string Apply(string transcript, Preferences settings)
    {
        if (settings.Style == "Verbatim") return transcript.Trim();
        var literal = new List<string>();
        var text = Protected.Replace(transcript, m => { literal.Add(m.Value); return $"\uE000{literal.Count - 1}\uE001"; });
        // A snippet expands only when the whole utterance matches an explicit trigger.
        var snippet = settings.Snippets.FirstOrDefault(s => s.Trigger.Trim().Equals(text.Trim().TrimEnd('.', '!', '?'), StringComparison.OrdinalIgnoreCase));
        if (snippet != null) return snippet.Expansion;
        text = Filler.Replace(text, "");
        foreach (var item in settings.Vocabulary.OrderByDescending(x => x.Spoken.Length))
        {
            if (string.IsNullOrWhiteSpace(item.Spoken)) continue;
            text = Regex.Replace(text, @"(?<![\w])" + Regex.Escape(item.Spoken) + @"(?![\w])", _ => item.Written, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\s+([,.;:!?])", "$1").Trim(' ', ',');
        if (settings.Style == "Notes")
            text = Regex.Replace(text, @"\b(?:new paragraph|nouveau paragraphe)\b[,.]?", "\n\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        // Explicit correction syntax only. Ambiguous natural self-corrections remain visible.
        var correction = Regex.Match(text, @"^(.*?)\b(?:correction|corrige)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (correction.Success) text = correction.Groups[2].Value.Trim();
        for (var i = 0; i < literal.Count; i++) text = text.Replace($"\uE000{i}\uE001", literal[i], StringComparison.Ordinal);
        return text.Trim();
    }
}
