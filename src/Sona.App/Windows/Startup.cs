using Microsoft.Win32;
using System.Text.Json;
namespace Sona.App.Windows;
public static class Startup
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static bool Owned(string command, string product)
    {
        var suffix = "\" --start-hidden";
        if (!command.StartsWith('"') || !command.EndsWith(suffix, StringComparison.Ordinal)) return false;
        var executable = command[1..^suffix.Length];
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", product) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(executable).StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable)) return false;
        var receipt = Path.Combine(Path.GetDirectoryName(executable)!, "installation.json");
        if (!File.Exists(receipt) || new FileInfo(receipt).Length > 256_000) return false;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(receipt));
            var installed = json.RootElement;
            return installed.GetProperty("Path").GetString() == executable && installed.GetProperty("SHA256").GetString() == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(executable))).ToLowerInvariant();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException) { return false; }
    }
    private static string InstalledRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Sona") + Path.DirectorySeparatorChar;
    // Startup registration belongs to an installed build only. A source/debug build
    // must never repoint the user's Run entry at a temporary executable.
    private static string? InstalledExecutable()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        var full = Path.GetFullPath(path);
        return full.StartsWith(InstalledRoot, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
    public static void MigrateLegacy(bool enabled)
    {
        using var read = Registry.CurrentUser.OpenSubKey(RunPath);
        var desired = InstalledExecutable() is { } executable ? "\"" + executable + "\" --start-hidden" : null;
        var legacyOwned = read?.GetValue("Murmur") is string legacy && Owned(legacy, "Murmur");
        var sonaOwned = read?.GetValue("Sona") is string sona && Owned(sona, "Sona") && (desired == null || sona != desired);
        if (desired != null && (legacyOwned || sonaOwned)) Set(enabled);
    }
    public static void Set(bool enabled)
    {
        var executable = InstalledExecutable()
            ?? throw new IOException("Launch at startup is available from the installed Sona build, not a source build. Install Sona, then enable this there.");
        using var run = Registry.CurrentUser.OpenSubKey(RunPath, true) ?? throw new IOException("Windows startup preferences were unavailable.");
        var desired = "\"" + executable + "\" --start-hidden";
        if (run.GetValue("Sona") is string existing && existing != desired && !Owned(existing, "Sona")) throw new IOException("An unrelated Sona startup entry was preserved.");
        if (enabled) run.SetValue("Sona", desired); else run.DeleteValue("Sona", false);
        if (run.GetValue("Murmur") is string legacy && Owned(legacy, "Murmur")) run.DeleteValue("Murmur", false);
    }
}
