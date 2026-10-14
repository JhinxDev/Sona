using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sona.Core;

public sealed record PublicRelease(Version Version, Uri Url);

public static class ReleaseVersion
{
    public const int MaximumBytes = 64 * 1024;
    public static PublicRelease Parse(ReadOnlyMemory<byte> json)
    {
        if (json.Length > MaximumBytes) throw new FormatException("Release response is too large.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var release = document.RootElement;
        if (release.ValueKind != JsonValueKind.Object ||
            !release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
            !release.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False ||
            !release.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String ||
            !release.TryGetProperty("html_url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String)
            throw new FormatException("Release metadata is not a stable public release.");
        var tag = tagElement.GetString()!;
        if (tag.Length > 64 || !Regex.IsMatch(tag, @"\Av(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant) ||
            !Version.TryParse(tag[1..], out var version))
            throw new FormatException("Release version is invalid.");
        var expected = "https://github.com/JhinxDev/Sona/releases/tag/" + tag;
        if (!string.Equals(urlElement.GetString(), expected, StringComparison.Ordinal))
            throw new FormatException("Release link is outside the Sona release page.");
        return new PublicRelease(version, new Uri(expected));
    }

    public static bool IsNewer(PublicRelease release, string installedVersion) =>
        Version.TryParse(installedVersion, out var installed) && release.Version > installed;
}
