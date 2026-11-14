using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sona.Core;

public sealed record PublicRelease(Version Version, Uri Url, bool Preview = false);

public static class ReleaseVersion
{
    public const int MaximumBytes = 64 * 1024;
    public static PublicRelease Parse(ReadOnlyMemory<byte> json)
    {
        if (json.Length > MaximumBytes) throw new FormatException("Release response is too large.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        return ParseMetadata(document.RootElement, includePreviews: false);
    }
    public static PublicRelease? LatestPublished(ReadOnlyMemory<byte> json, bool includePreviews)
    {
        if (json.Length > MaximumBytes) throw new FormatException("Release response is too large.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        if (document.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Release list is invalid.");
        PublicRelease? latest = null;
        foreach (var metadata in document.RootElement.EnumerateArray())
        {
            PublicRelease release;
            try { release = ParseMetadata(metadata, includePreviews); }
            catch (FormatException) { continue; }
            // A source tag without an uploaded installer is not an app update.
            var name = "Sona-Setup-" + release.Version.ToString(3) + ".exe";
            var download = "https://github.com/JhinxDev/Sona/releases/download/v" + release.Version.ToString(3) + "/" + name;
            if (!metadata.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array ||
                !assets.EnumerateArray().Any(asset => asset.ValueKind == JsonValueKind.Object &&
                    asset.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == name &&
                    asset.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() == "uploaded" &&
                    asset.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var length) && length > 0 &&
                    asset.TryGetProperty("browser_download_url", out var url) && url.ValueKind == JsonValueKind.String && url.GetString() == download)) continue;
            if (latest == null || release.Version > latest.Version) latest = release;
        }
        return latest;
    }
    private static PublicRelease ParseMetadata(JsonElement release, bool includePreviews)
    {
        if (release.ValueKind != JsonValueKind.Object ||
            !release.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
            !release.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind is not (JsonValueKind.False or JsonValueKind.True) ||
            prerelease.ValueKind == JsonValueKind.True && !includePreviews ||
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
        return new PublicRelease(version, new Uri(expected), prerelease.ValueKind == JsonValueKind.True);
    }

    public static bool IsNewer(PublicRelease release, string installedVersion) =>
        Version.TryParse(installedVersion, out var installed) && release.Version > installed;
}
