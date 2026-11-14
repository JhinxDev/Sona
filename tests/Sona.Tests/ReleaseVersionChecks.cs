using System.Text;
using System.Text.Json;
using Sona.Core;

internal static class ReleaseVersionChecks
{
    public static void Run(Action<bool, string> check)
    {
        byte[] Metadata(string tag = "v0.4.0", string? url = null, bool draft = false, bool prerelease = false) =>
            JsonSerializer.SerializeToUtf8Bytes(new { tag_name = tag, html_url = url ?? "https://github.com/JhinxDev/Sona/releases/tag/" + tag, draft, prerelease });
        bool Rejected(byte[] metadata) { try { ReleaseVersion.Parse(metadata); return false; } catch (Exception ex) when (ex is FormatException or JsonException) { return true; } }
        var release = ReleaseVersion.Parse(Metadata());
        check(ReleaseVersion.IsNewer(release, "0.3.0") && !ReleaseVersion.IsNewer(release, "0.4.0") && !ReleaseVersion.IsNewer(release, "0.5.0"), "Release comparison handles newer, same and older versions");
        check(!ReleaseVersion.IsNewer(release, "unknown"), "Unknown installed version never invents update availability");
        foreach (var tag in new[] { "0.4.0", "v0.4.0-beta", "v0.4", "v00.4.0", "v0.4.0+build", "v2147483648.0.0", "v0.4.0/elsewhere", "v0.4.0\n" })
            check(Rejected(Metadata(tag)), "Release parser rejects noncanonical stable tag " + tag);
        foreach (var url in new[] { "http://github.com/JhinxDev/Sona/releases/tag/v0.4.0", "https://github.com.evil.test/JhinxDev/Sona/releases/tag/v0.4.0", "https://github.com/Other/Sona/releases/tag/v0.4.0", "https://github.com/JhinxDev/Sona/releases/tag/v0.4.0?redirect=1", "https://github.com/JhinxDev/Sona/releases/tag/v0.3.0" })
            check(Rejected(Metadata(url: url)), "Release parser rejects unrelated or altered release links");
        check(Rejected(Metadata(draft: true)) && Rejected(Metadata(prerelease: true)), "Draft and prerelease metadata cannot advertise a stable update");
        check(Rejected(Encoding.UTF8.GetBytes("{}")) && Rejected(Encoding.UTF8.GetBytes("[]")) && Rejected(Encoding.UTF8.GetBytes("{broken")), "Malformed or missing release metadata is rejected");
        check(Rejected(new byte[ReleaseVersion.MaximumBytes + 1]), "Release parser enforces bounded response bytes");
        object Published(string tag, bool preview = true, bool draft = false, bool uploaded = true, string? download = null) => new
        {
            tag_name = tag, html_url = "https://github.com/JhinxDev/Sona/releases/tag/" + tag, draft, prerelease = preview,
            assets = new[] { new { name = "Sona-Setup-" + tag[1..] + ".exe", state = uploaded ? "uploaded" : "new", size = 123,
                browser_download_url = download ?? "https://github.com/JhinxDev/Sona/releases/download/" + tag + "/Sona-Setup-" + tag[1..] + ".exe" } }
        };
        var list = JsonSerializer.SerializeToUtf8Bytes(new[] { Published("v0.5.6"), Published("v0.6.0"), Published("v0.4.0", preview: false), Published("v0.9.0", draft: true), Published("v0.8.0", uploaded: false) });
        var preview = ReleaseVersion.LatestPublished(list, includePreviews: true);
        check(preview?.Version == new Version(0, 6, 0) && preview.Preview, "Preview installs find the newest published preview installer regardless of list order");
        check(ReleaseVersion.LatestPublished(list, includePreviews: false)?.Version == new Version(0, 4, 0), "Stable installs exclude previews, drafts and incomplete uploads");
        check(ReleaseVersion.LatestPublished("[]"u8.ToArray(), true) == null, "An empty public release list has no update");
        var unsafeList = JsonSerializer.SerializeToUtf8Bytes(new[] { Published("v0.7.0", download: "https://example.com/file.exe") });
        check(ReleaseVersion.LatestPublished(unsafeList, true) == null, "An installer link outside the exact Sona release is rejected");
    }
}
