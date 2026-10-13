using System.Windows.Interop;
using System.Runtime.InteropServices;
namespace Murmur.App.Windows;

internal sealed class ProtectionHotkey : IDisposable
{
    private readonly HwndSource source;
    private readonly IntPtr handle;
    private readonly Action pause;
    private bool registered;
    public ProtectionHotkey(IntPtr handle, string shortcut, Action pause)
    {
        this.handle = handle; this.pause = pause; source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
        if (Murmur.Core.ProtectionShortcut.TryParse(shortcut, out var modifiers, out var key)) registered = RegisterHotKey(handle, 6, modifiers, key);
    }
    public bool Registered => registered;
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x0312 && wparam == 6) { handled = true; pause(); }
        return IntPtr.Zero;
    }
    public void Dispose() { source.RemoveHook(Hook); if (registered) UnregisterHotKey(handle, 6); registered = false; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
