using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace Murmur.App.Windows;

public sealed class Hotkeys : IDisposable
{
    private readonly nint handle;
    private readonly HwndSource source;
    private readonly Action<bool> begin;
    private readonly Action finish;
    private readonly Action cancel;
    private readonly Action insert;
    private readonly System.Windows.Threading.DispatcherTimer release;
    private bool holding;
    public Hotkeys(nint handle, Action<bool> begin, Action finish, Action cancel, Action insert)
    {
        this.handle = handle; this.begin = begin; this.finish = finish; this.cancel = cancel; this.insert = insert;
        source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
        // MOD_NOREPEAT. Hold and toggle use distinct chords, preserving existing Ctrl+Win dictation.
        if (!RegisterHotKey(handle, 1, 0x4003, 0x20)) throw new IOException("Ctrl+Alt+Space is in use by another app. Close the competing hotkey owner and retry.");
        if (!RegisterHotKey(handle, 2, 0x4007, 0x20)) { UnregisterHotKey(handle, 1); throw new IOException("Ctrl+Alt+Shift+Space is in use by another app."); }
        if (!RegisterHotKey(handle, 4, 0x4007, 0x56)) { UnregisterHotKey(handle, 1); UnregisterHotKey(handle, 2); throw new IOException("Ctrl+Alt+Shift+V is in use by another app."); }
        release = new() { Interval = TimeSpan.FromMilliseconds(30) };
        release.Tick += (_, _) => { if (holding && ((GetAsyncKeyState(0x20) & 0x8000) == 0 || (GetAsyncKeyState(0x11) & 0x8000) == 0 || (GetAsyncKeyState(0x12) & 0x8000) == 0)) { holding = false; release.Stop(); finish(); } };
    }
    private nint Hook(nint hwnd, int msg, nint wparam, nint lparam, ref bool handled)
    {
        if (msg == 0x0312)
        {
            handled = true;
            if (wparam == 1) { holding = true; release.Start(); begin(true); }
            if (wparam == 2) begin(false);
            if (wparam == 4) insert();
        }
        return 0;
    }
    public void Recording(bool active) { if (!active) { holding = false; release.Stop(); } }
    public void Dispose() { release.Stop(); source.RemoveHook(Hook); for (var i = 1; i <= 4; i++) UnregisterHotKey(handle, i); }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
