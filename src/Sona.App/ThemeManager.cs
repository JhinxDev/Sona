using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Sona.Core;
using Sona.App.Windows;

namespace Sona.App;

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
            var brush = new SolidColorBrush(AccessibleColor(pair.Key, Parse(pair.Value)));
            brush.Freeze();
            app.Resources[pair.Key] = brush;
        }
        ApplyMaterialResources(false);
        foreach (Window window in app.Windows) ApplyTitleBar(window);
    }

    public static void ApplyTitleBar(Window window, bool allowNative = true)
    {
        if (!window.Dispatcher.CheckAccess()) { window.Dispatcher.Invoke(() => ApplyTitleBar(window, allowNative)); return; }
        if (!TrackedWindows.TryGetValue(window, out _))
        {
            TrackedWindows.Add(window, new object());
            window.SourceInitialized += (_, _) => ApplyTitleBar(window);
            window.SourceInitialized += (_, _) =>
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                source?.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
                {
                    if (window is MainWindow && message is 0x001A or 0x031E)
                        window.Dispatcher.BeginInvoke(() => Apply(Current.Id));
                    return 0;
                });
            };
        }
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;
        var dark = Current.IsDark ? 1 : 0;
        var material = window is MainWindow ? WindowBackdrop.Apply(window, Current.UsesGlassMaterial, allowNative) : default;
        if (window is MainWindow)
        {
            ApplyMaterialResources(material.NativeActive);
            if (Current.UsesGlassMaterial) window.Background = material.NativeActive ? Brushes.Transparent : (Brush)Application.Current.Resources["MaterialCanvas"];
            else window.ClearValue(Window.BackgroundProperty);
        }
        var caption = material.NativeActive ? -1 : ColorRef(AccessibleColor("Canvas", PaletteColor("Canvas")));
        var text = ColorRef(AccessibleColor("Ink", PaletteColor("Ink")));
        // Unsupported DWM attributes return HRESULTs and leave system defaults intact.
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        if (window is MainWindow)
        {
            var edgeColor = Mix(PaletteColor("Line"), PaletteColor("Accent"), .22);
            var edge = Current.UsesGlassMaterial && !SystemParameters.HighContrast ? ColorRef(edgeColor) : -1;
            _ = DwmSetWindowAttribute(handle, 34, ref edge, sizeof(int));
        }
    }

    private static void ApplyMaterialResources(bool native)
    {
        var resources = Application.Current.Resources;
        var highContrast = SystemParameters.HighContrast;
        var translucent = native && !highContrast;
        var canvas = PaletteColor("Canvas");
        var panel = PaletteColor("Panel");
        var inset = PaletteColor("Inset");
        var accent = PaletteColor("Accent");

        resources["MaterialCanvas"] = highContrast ? SystemColors.WindowBrush : Surface(canvas, panel, accent, .025, .08, translucent, 230);
        resources["Sidebar"] = highContrast ? SystemColors.WindowBrush : Surface(PaletteColor("Sidebar"), panel, accent, .025, .06, translucent, 246);
        resources["Panel"] = highContrast ? SystemColors.WindowBrush : Surface(panel, canvas, accent, .035, .07, translucent, 250);
        resources["Inset"] = highContrast ? SystemColors.WindowBrush : Surface(inset, canvas, accent, .018, .05, translucent, 252);
        resources["Button"] = highContrast ? SystemColors.ControlBrush : Surface(PaletteColor("Button"), panel, accent, .045, .08, translucent, 248);
        resources["Primary"] = highContrast ? SystemColors.HighlightBrush : PrimarySurface(PaletteColor("Primary"));
        resources["Selection"] = highContrast ? SystemColors.HighlightBrush : Surface(PaletteColor("Selection"), accent, accent, .10, .10, translucent, 246);
        resources["Hover"] = highContrast ? SystemColors.HighlightBrush : Surface(PaletteColor("Hover"), panel, accent, .04, .05, translucent, 244);

        var line = highContrast ? SystemColors.WindowTextColor : PaletteColor("Line");
        var edge = Mix(line, accent, highContrast ? 0 : .28);
        resources["MaterialAmbient"] = !highContrast && translucent && (RenderCapability.Tier >> 16) > 0
            ? AmbientLight(accent)
            : Brushes.Transparent;
        resources["MaterialEdge"] = highContrast ? SystemColors.WindowTextBrush : translucent
            ? EdgeGradient(line, accent)
            : new SolidColorBrush(edge);
        resources["MaterialNavEdge"] = highContrast ? SystemColors.WindowTextBrush : translucent
            ? Gradient(WithAlpha(Mix(line, accent, .72), 0xC8), WithAlpha(accent, 0xA0), WithAlpha(Mix(line, accent, .5), 0xB8), new Point(0, 0), new Point(1, 1))
            : new SolidColorBrush(accent);
        resources["MaterialNavThickness"] = new Thickness(1);
        resources["MaterialPanelRadius"] = new CornerRadius(14);
        resources["MaterialPanelEffect"] = !highContrast && (RenderCapability.Tier >> 16) > 0
            ? Shadow(accent, .08, 14, 1)
            : null;
        resources["MaterialActionEffect"] = !highContrast && (RenderCapability.Tier >> 16) > 0
            ? Shadow(accent, .12, 10, 1)
            : null;

        // Protected surfaces remain almost opaque over native backdrops. These aliases
        // also give the overlay a stable material contract without theme-specific hues.
        resources["AppIconPlate"] = Current.IsDark && !highContrast ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(32, 30, 36));
        resources["OverlaySurface"] = resources["Panel"];
        resources["OverlayEdge"] = resources["MaterialEdge"];
    }

    private static Brush Surface(Color baseColor, Color neighbor, Color accent, double accentMix, double neighborMix, bool translucent, byte opacity)
    {
        var first = Mix(baseColor, accent, accentMix);
        var middle = Mix(baseColor, neighbor, neighborMix);
        var last = Mix(baseColor, accent, accentMix * .45);
        var alpha = translucent ? opacity : byte.MaxValue;
        return Gradient(WithAlpha(first, alpha), WithAlpha(middle, alpha), WithAlpha(last, alpha), new Point(0, 0), new Point(1, 1));
    }

    private static Brush PrimarySurface(Color primary)
    {
        static Color Shade(Color color, double factor) => Color.FromRgb(
            (byte)Math.Clamp(Math.Round(color.R * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.G * factor), 0, 255),
            (byte)Math.Clamp(Math.Round(color.B * factor), 0, 255));
        return Gradient(Shade(primary, 1), Shade(primary, .94), Shade(primary, .88), new Point(0, 0), new Point(0, 1));
    }

    private static Brush EdgeGradient(Color line, Color accent) => Gradient(
        WithAlpha(Mix(line, accent, .38), 0xB5),
        WithAlpha(Mix(line, accent, .16), 0x85),
        WithAlpha(Mix(line, accent, .32), 0xA0), new Point(0, 0), new Point(.7, 1));

    private static Brush AmbientLight(Color accent)
    {
        // Static code-native illumination uses only the selected palette's accent.
        var drawing = new DrawingGroup();
        var bounds = new RectangleGeometry(new Rect(0, 0, 1, 1));
        foreach (var (alpha, center, radiusX, radiusY) in new[]
        {
            ((byte)0x24, new Point(.16, .12), .72, .62),
            ((byte)0x20, new Point(.91, .52), .68, .78),
            ((byte)0x18, new Point(.35, 1.04), .70, .62)
        })
        {
            var light = new RadialGradientBrush
            {
                Center = center, GradientOrigin = center, RadiusX = radiusX, RadiusY = radiusY
            };
            var tint = WithAlpha(accent, alpha);
            light.GradientStops.Add(new(tint, 0));
            light.GradientStops.Add(new(Color.FromArgb(0, tint.R, tint.G, tint.B), 1));
            drawing.Children.Add(new GeometryDrawing(light, null, bounds));
        }
        var brush = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
        brush.Freeze(); return brush;
    }

    private static Brush Gradient(Color first, Color middle, Color last, Point start, Point end)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        brush.GradientStops.Add(new(first, 0));
        brush.GradientStops.Add(new(middle, .48));
        brush.GradientStops.Add(new(last, 1));
        brush.Freeze(); return brush;
    }

    private static Effect Shadow(Color color, double opacity, double radius, double depth)
    {
        var effect = new DropShadowEffect { Color = color, Opacity = opacity, BlurRadius = radius, ShadowDepth = depth, RenderingBias = RenderingBias.Performance };
        effect.Freeze(); return effect;
    }

    private static Color PaletteColor(string key) => AccessibleColor(key, Parse(Current.Colors[key]));

    private static Color AccessibleColor(string key, Color paletteColor)
    {
        if (!SystemParameters.HighContrast) return paletteColor;
        return key switch
        {
            "Canvas" or "Sidebar" or "Panel" or "Inset" or "Button" or "SwitchKnob" => SystemColors.WindowColor,
            "Ink" or "Muted" or "Line" or "SwitchOff" or "ScrollThumb" or "Success" or "Warning" or "Danger" => SystemColors.WindowTextColor,
            "PrimaryInk" or "SelectedInk" => SystemColors.HighlightTextColor,
            "SwitchKnobOn" => SystemColors.HighlightColor,
            "Accent" or "Focus" or "Primary" or "Hover" or "Selection" => SystemColors.HighlightColor,
            _ => SystemColors.WindowTextColor
        };
    }

    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static Color Mix(Color left, Color right, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)Math.Clamp(Math.Round(left.R + (right.R - left.R) * amount), 0, 255),
            (byte)Math.Clamp(Math.Round(left.G + (right.G - left.G) * amount), 0, 255),
            (byte)Math.Clamp(Math.Round(left.B + (right.B - left.B) * amount), 0, 255));
    }

    private static int ColorRef(Color color)
    {
        return color.R | color.G << 8 | color.B << 16;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
