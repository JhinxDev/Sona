using System.Text;
namespace Murmur.Core;

public static class SnippetRules
{
    private static string Encode(string text) => text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("=", "\\=");
    private static string Decode(string text)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 == text.Length) { result.Append(text[i]); continue; }
            var escaped = text[++i];
            result.Append(escaped switch { 'n' => "\n", 'r' => "\r", '\\' => "\\", '=' => "=", _ => "\\" + escaped });
        }
        return result.ToString();
    }
    public static string Format(IEnumerable<Snippet> snippets) => string.Join("\n", snippets.Select(x => Encode(x.Trigger) + " = " + Encode(x.Expansion)));
    public static List<Snippet> Parse(string text)
    {
        var result = new List<Snippet>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var separator = -1;
            for (var i = 0; i < line.Length; i++) { if (line[i] == '\\' && i + 1 < line.Length) { i++; continue; } if (line[i] == '=') { separator = i; break; } }
            if (separator < 1) throw new FormatException("Use trigger = expanded text on each line.");
            var trigger = Decode(line[..separator].Trim()); var encoded = line[(separator + 1)..];
            if (encoded.StartsWith(' ')) encoded = encoded[1..];
            var expansion = Decode(encoded);
            if (string.IsNullOrWhiteSpace(trigger) || trigger.Length > 120 || string.IsNullOrWhiteSpace(expansion) || expansion.Length > 4000) throw new FormatException("Use a trigger up to 120 characters and expanded text up to 4000.");
            if (!seen.Add(trigger)) throw new FormatException("Each snippet trigger must be unique.");
            result.Add(new(trigger, expansion));
            if (result.Count > 200) throw new FormatException("Keep at most 200 snippets.");
        }
        return result;
    }
}
