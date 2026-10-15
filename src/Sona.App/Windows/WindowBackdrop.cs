using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace Sona.App.Windows;

public readonly record struct WindowMaterialStatus(bool Requested, bool Supported, bool NativeActive,
    string Mode, int SetResult, int FrameResult, int ReadResult, int BackdropType);

/// <summary>Documented Windows 11 Mica, with fail-opaque behavior and explicit teardown.</summary>
public static class WindowBackdrop
{
    private sealed class State(Color compositionColor)
    {
        public Color OriginalCompositionColor { get; } = compositionColor;
        public bool Touched;
        public WindowMaterialStatus Status;
    }
    private static readonly ConditionalWeakTable<Window, State> States = new();
    private const uint SystemBackdrop = 38;
    private const int None = 1, MainWindow = 2;
    public static WindowMaterialStatus StatusFor(Window window) => States.TryGetValue(window, out var state) ? state.Status : default;

    public static WindowMaterialStatus Apply(Window window, bool requested, bool allowNative = true)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(handle);
        if (handle == 0 || source?.CompositionTarget == null) return new(requested, false, false, "Waiting for native window", 0, 0, 0, 0);
        var state = States.GetValue(window, _ => new State(source.CompositionTarget.BackgroundColor));
        var supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        var composition = DwmIsCompositionEnabled(out var enabled) >= 0 && enabled;
        var allow = requested && allowNative && supported && composition && !SystemParameters.HighContrast && TransparencyEnabled();
        int set = 0, frame = 0, read = 0, actual = 0;
        if (allow)
        {
            int type = MainWindow;
            set = DwmSetWindowAttribute(handle, SystemBackdrop, ref type, sizeof(int));
            var margins = new Margins(-1, -1, -1, -1);
            frame = set >= 0 ? DwmExtendFrameIntoClientArea(handle, ref margins) : set;
            read = DwmGetWindowAttribute(handle, SystemBackdrop, out actual, sizeof(int));
            state.Touched = true;
            if (set >= 0 && frame >= 0 && read >= 0 && actual == MainWindow)
            {
                // No layered/per-pixel Window transparency. DWM owns the backdrop.
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
                return state.Status = new(requested, supported, true, "Native Mica with palette-derived glass", set, frame, read, actual);
            }
        }
        if (state.Touched)
        {
            int type = None;
            set = DwmSetWindowAttribute(handle, SystemBackdrop, ref type, sizeof(int));
            var margins = new Margins(0, 0, 0, 0);
            frame = DwmExtendFrameIntoClientArea(handle, ref margins);
            source.CompositionTarget.BackgroundColor = state.OriginalCompositionColor;
            read = DwmGetWindowAttribute(handle, SystemBackdrop, out actual, sizeof(int));
            state.Touched = set < 0 || frame < 0 || read < 0 || actual != None;
        }
        else if (supported)
        {
            read = DwmGetWindowAttribute(handle, SystemBackdrop, out actual, sizeof(int));
        }
        return state.Status = new(requested, supported, false,
            state.Touched ? "Opaque surface; native backdrop reset needs retry" : requested ? "Opaque glass material fallback" : "Opaque palette", set, frame, read, actual);
    }
    internal static bool TransparencyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("EnableTransparency") is not int value || value != 0;
        }
        catch (System.Security.SecurityException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }
    [StructLayout(LayoutKind.Sequential)] private readonly struct Margins(int left, int right, int top, int bottom)
    { public readonly int Left = left, Right = right, Top = top, Bottom = bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);
    [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
}
