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
    public static void MigrateLegacy(bool enabled)
    {
        using var read = Registry.CurrentUser.OpenSubKey(RunPath);
        var desired = "\"" + Environment.ProcessPath + "\" --start-hidden";
        if (read?.GetValue("Murmur") is string legacy && Owned(legacy, "Murmur") || read?.GetValue("Sona") is string sona && sona != desired && Owned(sona, "Sona")) Set(enabled);
    }
    public static void Set(bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunPath, true) ?? throw new IOException("Windows startup preferences were unavailable.");
        var desired = "\"" + Environment.ProcessPath + "\" --start-hidden";
        if (run.GetValue("Sona") is string existing && existing != desired && !Owned(existing, "Sona")) throw new IOException("An unrelated Sona startup entry was preserved.");
        if (enabled) run.SetValue("Sona", desired); else run.DeleteValue("Sona", false);
        if (run.GetValue("Murmur") is string legacy && Owned(legacy, "Murmur")) run.DeleteValue("Murmur", false);
    }
}
