using System.Text;
using System.Text.Json;
using Murmur.Core;

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
    }
}
