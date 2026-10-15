using System.Text.RegularExpressions;
namespace Sona.Core;

// Learn only from a short explicit replacement, not an arbitrary rewrite.
public static class CorrectionLearning
{
    private static readonly Regex Word = new(@"^[\p{L}][\p{L}\p{M}'’ -]{0,59}$", RegexOptions.CultureInvariant);
    public static IReadOnlyList<Replacement> Suggest(string generated, string edited)
    {
        if (generated.Length > 6000 || edited.Length > 6000 || generated == edited) return [];
        var before = Regex.Matches(generated, @"\S+").Select(x => x.Value).ToArray();
        var after = Regex.Matches(edited, @"\S+").Select(x => x.Value).ToArray();
        int start = 0, endBefore = before.Length, endAfter = after.Length;
        while (start < endBefore && start < endAfter && before[start] == after[start]) start++;
        while (endBefore > start && endAfter > start && before[endBefore - 1] == after[endAfter - 1]) { endBefore--; endAfter--; }
        if (endBefore - start is < 1 or > 3 || endAfter - start is < 1 or > 3) return [];
        var heard = string.Join(" ", before[start..endBefore]).Trim('.', ',', '!', '?', ':', ';', '"');
        var written = string.Join(" ", after[start..endAfter]).Trim('.', ',', '!', '?', ':', ';', '"');
        if (heard == written || !Word.IsMatch(heard) || !Word.IsMatch(written)) return [];
        return [new(heard, written)];
    }
}
