using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Murmur.Core;
using Murmur.App.Windows;

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
        var caption = material.NativeActive ? -1 : ColorRef(Current.Colors["Canvas"]);
        var text = ColorRef(Current.Colors["Ink"]);
        // Unsupported DWM attributes return HRESULTs and leave system defaults intact.
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
        if (window is MainWindow)
        {
            var edge = Current.UsesGlassMaterial ? ColorRef("#75628E") : -1;
            _ = DwmSetWindowAttribute(handle, 34, ref edge, sizeof(int));
        }
    }

    private static void ApplyMaterialResources(bool native)
    {
        var resources = Application.Current.Resources;
        var glass = Current.UsesGlassMaterial;
        resources["MaterialCanvas"] = resources["Canvas"];
        resources["MaterialAmbient"] = Brushes.Transparent;
        resources["MaterialEdge"] = resources["Line"];
        resources["MaterialNavEdge"] = Brushes.Transparent;
        resources["MaterialNavThickness"] = new Thickness(0);
        resources["MaterialPanelRadius"] = new CornerRadius(10);
        resources["MaterialPanelEffect"] = null;
        resources["MaterialActionEffect"] = null;
        if (!glass) return;
        var opaque = !native || SystemParameters.HighContrast;
        if (!SystemParameters.HighContrast && (RenderCapability.Tier >> 16) > 0)
            resources["MaterialAmbient"] = AmbientLight();
        resources["MaterialCanvas"] = Gradient(opaque ? "#FF332043" : "#BC332043", opaque ? "#FF191321" : "#C9191321", opaque ? "#FF272035" : "#B8272035", new Point(0, 0), new Point(1, 1));
        resources["Sidebar"] = Gradient("#F02A2138", "#ED1D1728", "#F0322148", new Point(0, 0), new Point(.5, 1));
        resources["Panel"] = Gradient(opaque ? "#FF3F3152" : "#B83F3152", opaque ? "#FF292034" : "#C4292034", opaque ? "#FF342440" : "#B8342440", new Point(0, 0), new Point(.75, 1));
        resources["Inset"] = Gradient("#F4251D32", "#F41C1728", "#F42D213D", new Point(0, 0), new Point(1, 1));
        resources["Button"] = Gradient("#F53B2C51", "#EF291F3B", "#F0342749", new Point(0, 0), new Point(0, 1));
        resources["Primary"] = Gradient("#FF8757C2", "#FF6B419C", "#FF563178", new Point(0, 0), new Point(0, 1));
        resources["Selection"] = Gradient("#ED885BC4", "#DF583781", "#E8462D68", new Point(0, 0), new Point(1, 1));
        resources["Hover"] = Gradient("#E14B365F", "#DD342843", "#D13E2C52", new Point(0, 0), new Point(1, 1));
        resources["MaterialEdge"] = Gradient("#BCEAD7FF", "#647E6998", "#886C4D94", new Point(0, 0), new Point(.7, 1));
        resources["MaterialNavEdge"] = Gradient("#CED9B8FF", "#9D9C6CDE", "#7B704CA0", new Point(0, 0), new Point(1, 1));
        resources["MaterialNavThickness"] = new Thickness(1);
        resources["MaterialPanelRadius"] = new CornerRadius(14);
        if (!SystemParameters.HighContrast && (RenderCapability.Tier >> 16) > 0)
        {
            resources["MaterialActionEffect"] = Shadow("#9B63E5", .25, 12, 0);
        }
    }
    private static Brush AmbientLight()
    {
        // Static code-native illumination: broad fields, never a copied wallpaper or animation.
        var drawing = new DrawingGroup();
        var bounds = new RectangleGeometry(new Rect(0, 0, 1, 1));
        foreach (var (color, center, radiusX, radiusY) in new[]
        {
            ("#987F50CE", new Point(.16, .12), .72, .62),
            ("#88725CBA", new Point(.91, .52), .68, .78),
            ("#606A427D", new Point(.35, 1.04), .70, .62)
        })
        {
            var light = new RadialGradientBrush
            {
                Center = center, GradientOrigin = center, RadiusX = radiusX, RadiusY = radiusY
            };
            var tint = (Color)ColorConverter.ConvertFromString(color);
            light.GradientStops.Add(new(tint, 0));
            light.GradientStops.Add(new(Color.FromArgb(0, tint.R, tint.G, tint.B), 1));
            drawing.Children.Add(new GeometryDrawing(light, null, bounds));
        }
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(32, 20, 13, 30)), null, bounds));
        var brush = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
        brush.Freeze(); return brush;
    }
    private static Brush Gradient(string first, string middle, string last, Point start, Point end)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(first), 0));
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(middle), .48));
        brush.GradientStops.Add(new((Color)ColorConverter.ConvertFromString(last), 1));
        brush.Freeze(); return brush;
    }
    private static Effect Shadow(string color, double opacity, double radius, double depth)
    {
        var effect = new DropShadowEffect { Color = (Color)ColorConverter.ConvertFromString(color), Opacity = opacity, BlurRadius = radius, ShadowDepth = depth, RenderingBias = RenderingBias.Performance };
        effect.Freeze(); return effect;
    }

    private static int ColorRef(string value)
    {
        var color = (Color)ColorConverter.ConvertFromString(value);
        return color.R | color.G << 8 | color.B << 16;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);
}
