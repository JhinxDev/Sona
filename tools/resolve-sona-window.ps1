if (-not ('SonaWindowResolver' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class SonaWindowResolver
{
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int count);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr FindMainWindow(int targetProcessId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, lParam) =>
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            if (pid != (uint)targetProcessId || !IsWindowVisible(hwnd))
            {
                return true;
            }
            var classBuf = new StringBuilder(256);
            GetClassName(hwnd, classBuf, classBuf.Capacity);
            var cls = classBuf.ToString();
            if (!cls.StartsWith("HwndWrapper[Sona;", StringComparison.Ordinal))
            {
                return true;
            }
            var titleBuf = new StringBuilder(256);
            GetWindowText(hwnd, titleBuf, titleBuf.Capacity);
            if (string.Equals(titleBuf.ToString(), "Sona", StringComparison.Ordinal))
            {
                found = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static bool CloseMainWindow(int targetProcessId)
    {
        var hwnd = FindMainWindow(targetProcessId);
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        return PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero);
    }
}
'@
}

function Get-SonaMainWindowHandle {
    param([Parameter(Mandatory)][int]$ProcessId)
    return [SonaWindowResolver]::FindMainWindow($ProcessId)
}

function Close-SonaMainWindow {
    param([Parameter(Mandatory)][int]$ProcessId)
    return [SonaWindowResolver]::CloseMainWindow($ProcessId)
}
