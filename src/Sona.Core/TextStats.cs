namespace Sona.Core;

/// <summary>Small local text statistics. No text is stored or transmitted.</summary>
public static class TextStats
{
    /// <summary>Counts letter/digit runs; apostrophes, hyphens and underscores join a word.</summary>
    public static int WordCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var count = 0;
        var inWord = false;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord) { count++; inWord = true; }
            }
            else if (inWord && character is '\'' or '\u2019' or '-' or '_' or '.' &&
                     index + 1 < text.Length && char.IsLetterOrDigit(text[index + 1]))
            {
                // Keep the current word open across "don't", "state-of-the-art",
                // "foo_bar" and decimal or dotted identifiers.
            }
            else inWord = false;
        }
        return count;
    }

    public static string WordLabel(string? text)
    {
        var words = WordCount(text);
        return words == 1 ? "1 word" : words.ToString(System.Globalization.CultureInfo.InvariantCulture) + " words";
    }
}
