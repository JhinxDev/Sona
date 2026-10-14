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
            var edge = Current.UsesGlassMaterial ? ColorRef("#493B5A") : -1;
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
        resources["MaterialCanvas"] = Gradient(opaque ? "#FF100B18" : "#E8100B18", opaque ? "#FF07060D" : "#EB07060D", opaque ? "#FF0C0912" : "#E80C0912", new Point(0, 0), new Point(1, 1));
        resources["Sidebar"] = Gradient("#F50F0C16", "#F50B0911", "#F5130E1D", new Point(0, 0), new Point(.5, 1));
        resources["Panel"] = Gradient(opaque ? "#FF21192C" : "#DC21192C", opaque ? "#FF141019" : "#E4141019", opaque ? "#FF1B1324" : "#DE1B1324", new Point(0, 0), new Point(.75, 1));
        resources["Inset"] = Gradient("#F8100C17", "#F809080E", "#F8130E1B", new Point(0, 0), new Point(1, 1));
        resources["Button"] = Gradient("#F5251B32", "#F015101E", "#F01C1426", new Point(0, 0), new Point(0, 1));
        resources["Primary"] = Gradient("#FF704B98", "#FF553575", "#FF3E2657", new Point(0, 0), new Point(0, 1));
        resources["Selection"] = Gradient("#F05F3F88", "#EA39264F", "#EC2A1E3B", new Point(0, 0), new Point(1, 1));
        resources["Hover"] = Gradient("#EB2D203D", "#EB1D1528", "#EB241A31", new Point(0, 0), new Point(1, 1));
        resources["MaterialEdge"] = Gradient("#90BBA4D2", "#50594A6D", "#704F3A68", new Point(0, 0), new Point(.7, 1));
        resources["MaterialNavEdge"] = Gradient("#ABA98DCB", "#80694D8B", "#60543C70", new Point(0, 0), new Point(1, 1));
        resources["MaterialNavThickness"] = new Thickness(1);
        resources["MaterialPanelRadius"] = new CornerRadius(14);
        if (!SystemParameters.HighContrast && (RenderCapability.Tier >> 16) > 0)
        {
            resources["MaterialActionEffect"] = Shadow("#8858BD", .16, 10, 0);
        }
    }
    private static Brush AmbientLight()
    {
        // Static code-native illumination: broad fields, never a copied wallpaper or animation.
        var drawing = new DrawingGroup();
        var bounds = new RectangleGeometry(new Rect(0, 0, 1, 1));
        foreach (var (color, center, radiusX, radiusY) in new[]
        {
            ("#24674389", new Point(.16, .12), .72, .62),
            ("#205A497E", new Point(.91, .52), .68, .78),
            ("#1851305D", new Point(.35, 1.04), .70, .62)
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
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(60, 6, 5, 11)), null, bounds));
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
