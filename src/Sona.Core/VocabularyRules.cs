namespace Sona.Core;

public sealed record VocabularyRules(List<string> Terms, List<Replacement> Replacements)
{
    public static VocabularyRules Parse(string source)
    {
        var terms = new List<string>(); var replacements = new List<Replacement>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in source.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var line = raw.Trim(); var equals = line.IndexOf('=');
            var key = (equals < 0 ? line : line[..equals]).Trim(); var value = equals < 0 ? key : line[(equals + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) throw new FormatException("Add an exact term, or use spoken phrase = exact spelling.");
            if (key.Length > 120 || value.Length > 120) throw new FormatException("Vocabulary terms and spellings must fit in 120 characters.");
            if (!seen.Add(key)) throw new FormatException("Each vocabulary term or spoken correction must be unique.");
            if (equals < 0) terms.Add(key); else replacements.Add(new(key, value));
            if (seen.Count > 200) throw new FormatException("Keep at most 200 vocabulary entries.");
        }
        return new(terms, replacements);
    }
}
