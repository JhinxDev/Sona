using System.Text.RegularExpressions;
namespace Sona.Core;

// Auto is deliberately narrow: it bypasses only short, already formatted text.
// Uncertainty stays on the model path; basic literal-preserving rules still run.
public static class CleanupPolicy
{
    private static readonly Regex Words = new(@"[\p{L}\p{N}_]+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    private static readonly Regex RepairCue = new(@"\b(?:um+|uh+|erm|euh+|actually|correction|corrige|sorry|plutôt|en fait|i mean|no wait|non attends)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
    public static bool SkipModel(string original, Preferences p)
    {
        if (p.CleanupMode != "Auto" || p.CleanupTone != "formal" || p.Style != "Clean") return false;
        var text = original.Trim();
        if (text.Length is < 2 or > 180 || !char.IsUpper(text[0]) || text[^1] is not ('.' or '!' or '?') ||
            text[..^1].IndexOfAny(['.', '!', '?', '\r', '\n', ';', ':', '[', ']', '{', '}', '<', '>']) >= 0) return false;
        try
        {
            if (RepairCue.IsMatch(text)) return false;
            var words = Words.Matches(text);
            if (words.Count is 0 or > 28) return false;
            for (var i=1;i<words.Count;i++)
                if (words[i].Value.Equals(words[i-1].Value, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
