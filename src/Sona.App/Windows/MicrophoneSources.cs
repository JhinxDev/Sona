using Microsoft.Win32;
namespace Sona.App.Windows;

internal static class MicrophoneSources
{
    private const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
    public static HashSet<string> Scan(IReadOnlyList<string> watched)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suffix in new[] { "", "\\NonPackaged", "\\Packaged" })
        {
            using var root = Registry.CurrentUser.OpenSubKey(Root + suffix, false);
            if (root == null) continue;
            foreach (var name in root.GetSubKeyNames().Take(2000))
            {
                var decoded = name.Replace('#', '\\'); var leaf = Path.GetFileName(decoded);
                if (!watched.Any(app => app.Equals(name, StringComparison.OrdinalIgnoreCase) || app.Equals(leaf, StringComparison.OrdinalIgnoreCase))) continue;
                using var appKey = root.OpenSubKey(name, false); if (appKey == null) continue;
                if (Active(appKey)) result.Add(suffix + ":" + name);
                // Packaged apps can expose an additional per-device child.
                foreach (var child in appKey.GetSubKeyNames().Take(100))
                {
                    using var device = appKey.OpenSubKey(child, false); if (device != null && Active(device)) result.Add(suffix + ":" + name + ":" + child);
                }
            }
        }
        return result;
    }
    private static bool Active(RegistryKey key) => key.GetValue("LastUsedTimeStart") is long start && start > 0 && key.GetValue("LastUsedTimeStop") is long stop && stop == 0;
}
