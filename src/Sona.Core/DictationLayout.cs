using System.Text.RegularExpressions;

namespace Sona.Core;

// Transcript/model line endings are not paragraph decisions. Only Clean folds
// prose lines; explicit structure and spoken layout commands remain intentional.
internal static class DictationLayout
{
    private static readonly Regex Structure = new(@"^\s*(?:[-*+•]\s|\d+[.)]\s|\#{1,6}\s|>|\||[\uE000]\d+[\uE001]\s*$)", RegexOptions.CultureInvariant);
    private static readonly Regex ParagraphCommand = new(@"\b(?:new paragraph|nouveau paragraphe)\b[,.]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LineCommand = new(@"\b(?:new line|nouvelle ligne)\b[,.]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SentenceEnd = new(@"(?<=[.!?]) +(?=[\p{Lu}\p{N}])", RegexOptions.CultureInvariant);
    private static readonly Regex Word = new(@"\S+", RegexOptions.CultureInvariant);
    private static readonly Regex Abbreviation = new(@"\b(?:Mr|Mrs|Ms|Dr|Prof|Sr|Jr|St|vs|etc)\.$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string Apply(string text, string style)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (style == "Clean")
        {
            var output = new List<string>();
            var prose = new List<string>();
            void Flush()
            {
                if (prose.Count == 0) return;
                output.Add(Paragraphs(string.Join(" ", prose)));
                prose.Clear();
            }
            foreach (var line in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    Flush();
                    if (output.Count > 0 && output[^1].Length != 0) output.Add("");
                }
                else if (Structure.IsMatch(line)) { Flush(); output.Add(line.TrimEnd()); }
                else prose.Add(line.Trim());
            }
            Flush();
            text = string.Join("\n", output).Trim();
        }
        // Expand after folding so an explicit single line break is never lost.
        text = ParagraphCommand.Replace(text, "\n\n");
        text = LineCommand.Replace(text, "\n");
        return Regex.Replace(text, @"[ \t]*\n[ \t]*", "\n").Trim();
    }

    private static string Paragraphs(string text)
    {
        // Long single-topic dictations get bounded reading chunks at sentence
        // boundaries, without a model rewrite or invented topic classification.
        var words = Word.Matches(text);
        if (words.Count < 150) return text;
        var paragraphs = new List<string>();
        var start = 0;
        var wordIndex = 0;
        var paragraphWordStart = 0;
        foreach (Match boundary in SentenceEnd.Matches(text))
        {
            var preceding = text[start..boundary.Index];
            while (wordIndex < words.Count && words[wordIndex].Index < boundary.Index) wordIndex++;
            if (wordIndex - paragraphWordStart < 90 || Abbreviation.IsMatch(preceding) || words.Count - wordIndex < 30) continue;
            paragraphs.Add(preceding);
            start = boundary.Index + boundary.Length;
            paragraphWordStart = wordIndex;
        }
        paragraphs.Add(text[start..]);
        return string.Join("\n\n", paragraphs);
    }
}
