using Microsoft.Win32;
namespace Murmur.App.Windows;
public static class Startup
{
    public static void Set(bool enabled)
    {
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new IOException("Windows startup preferences were unavailable.");
        if (enabled) run.SetValue("Murmur", "\"" + Environment.ProcessPath + "\" --start-hidden");
        else run.DeleteValue("Murmur", false);
    }
}
