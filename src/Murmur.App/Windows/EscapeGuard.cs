using System.Runtime.InteropServices;
namespace Murmur.App.Windows;

public sealed class EscapeGuard : IDisposable
{
    private readonly HookCallback callback;
    private readonly Action cancelImmediately;
    private nint hook;
    public EscapeGuard(Action cancelImmediately) { this.cancelImmediately = cancelImmediately; callback = OnKey; }
    public void Arm(bool active)
    {
        if (!active) { if (hook != 0) { UnhookWindowsHookEx(hook); hook = 0; } return; }
        if (hook != 0) return;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == 0) throw new IOException("Windows could not enable cancellation. Recording was not started.");
    }
    private nint OnKey(int code, nint message, nint data)
    {
        // Active-only, Esc-only. No key history, text, or modifier state is recorded.
        if (code >= 0 && (message == 0x100 || message == 0x104) && Marshal.ReadInt32(data) == 0x1B) { cancelImmediately(); return 1; }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose() => Arm(false);
    private delegate nint HookCallback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
