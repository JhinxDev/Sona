using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Sona.App;

/// <summary>A small display-only tail; the complete transcript stays in the draft.</summary>
internal static class RecentTranscriptTail
{
    internal static string Fit(string text, double width, Typeface typeface, double fontSize, Brush foreground, double pixelsPerDip)
    {
        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        if (normalized.Length == 0) return "";
        var starts = new List<int> { 0 };
        for (var index = 1; index < normalized.Length; index++)
            if (normalized[index] == ' ' && normalized[index - 1] is '.' or '!' or '?' or ';') starts.Add(index + 1);
        var start = starts.Count > 2 ? starts[^2] : 0;
        start = Math.Max(start, normalized.Length - 240);
        if (start > 0 && normalized[start - 1] != ' ')
        {
            var nextWord = normalized.IndexOf(' ', start);
            if (nextWord >= 0) start = nextWord + 1;
        }
        while (start < normalized.Length)
        {
            if (char.IsLowSurrogate(normalized[start])) { start++; continue; }
            var tail = (start == 0 ? "" : "… ") + normalized[start..];
            var measured = new FormattedText(tail, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize, foreground, pixelsPerDip)
            { MaxTextWidth = Math.Max(1, width), LineHeight = 17 };
            if (measured.Height <= 34.1 || normalized.Length - start <= 1) return tail;
            var nextWord = normalized.IndexOf(' ', start);
            start = nextWord >= 0 ? nextWord + 1 : start + 1;
        }
        return "";
    }
}
