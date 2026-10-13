using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Murmur.Core;

namespace Murmur.App;

/// <summary>Applies semantic brushes on the UI thread without recreating the window or disturbing focus.</summary>
public static class ThemeManager
{
    private static readonly ConditionalWeakTable<Window, object> TrackedWindows = new();
    public static ThemeDefinition Current { get; private set; } = ThemeCatalog.Resolve(ThemeCatalog.DefaultId);

    public static void Apply(string id)
    {
        var app = Application.Current ?? throw new InvalidOperationException("Theme changes require the running application.");
        if (!app.Dispatcher.CheckAccess()) { app.Dispatcher.Invoke(() => Apply(id)); return; }
        Current = ThemeCatalog.Resolve(id);
        foreach (var pair in Current.Colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(pair.Value));
            brush.Freeze();
            app.Resources[pair.Key] = brush;
        }
        foreach (Window window in app.Windows) ApplyTitleBar(window);
    }

    public static void ApplyTitleBar(Window window)
    {
        if (!window.Dispatcher.CheckAccess()) { window.Dispatcher.Invoke(() => ApplyTitleBar(window)); return; }
        if (!TrackedWindows.TryGetValue(window, out _))
        {
            TrackedWindows.Add(window, new object());
            window.SourceInitialized += (_, _) => ApplyTitleBar(window);
        }
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;
        var dark = Current.IsDark ? 1 : 0;
        var caption = ColorRef(Current.Colors["Canvas"]);
        var text = ColorRef(Current.Colors["Ink"]);
        // Unsupported DWM attributes return HRESULTs and leave system defaults intact.
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
    }

    private static int ColorRef(string value)
    {
        var color = (Color)ColorConverter.ConvertFromString(value);
        return color.R | color.G << 8 | color.B << 16;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
