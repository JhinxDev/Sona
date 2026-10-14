using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Murmur.App;
using Murmur.App.Windows;
using Murmur.Core;

static class Program
{
    [DllImport("user32.dll")] static extern nint GetForegroundWindow();
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [STAThread] public static void Main(string[] args)
    {
        if (!args.Contains("--test-ui") || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MURMUR_DATA"))) throw new InvalidOperationException("A microphone-disabled test profile is required.");
        var output = args.Single(a => a.StartsWith("--material-proof=")).Split('=', 2)[1];
        var app = new App(); app.InitializeComponent();
        app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try { await VerifyAsync(app, output); }
            catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); }
            finally { if (app.MainWindow is MainWindow window) window.RequestQuit(); else app.Shutdown(2); }
        }));
        app.Run();
    }
    private static async Task VerifyAsync(App app, string output)
    {
        var window = (MainWindow)app.MainWindow;
        var handle = new WindowInteropHelper(window).Handle;
        var originalForeground = GetForegroundWindow();
        var checks = new List<string>(); var states = new List<object>();
        void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
        void Guard() { var observed = GetForegroundWindow(); Check(observed == originalForeground && observed != handle, $"Theme action preserves the existing foreground (expected {originalForeground}, observed {observed}, preview {handle})"); }
        var record = (Button)window.FindName("RecordButton");
        Check(!record.IsEnabled && !window.AllowsTransparency, "Preview disables microphone and keeps the normal non-layered WPF window");
        var combo = (ComboBox)window.FindName("ThemeChoice");
        void Choose(string id) { combo.SelectedItem = ThemeCatalog.Resolve(id); window.UpdateLayout(); Guard(); }
        Color ColorFor(ThemeDefinition theme, string key) => (Color)ColorConverter.ConvertFromString(theme.Colors[key]);
        Color Mix(Color left, Color right, double amount) => Color.FromRgb(
            (byte)Math.Clamp(Math.Round(left.R + (right.R - left.R) * amount), 0, 255),
            (byte)Math.Clamp(Math.Round(left.G + (right.G - left.G) * amount), 0, 255),
            (byte)Math.Clamp(Math.Round(left.B + (right.B - left.B) * amount), 0, 255));
        double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                var normalized = value / 255d;
                return normalized <= .04045 ? normalized / 12.92 : Math.Pow((normalized + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        double Contrast(Color foreground, Color background)
        {
            var first = Luminance(foreground); var second = Luminance(background);
            return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
        }
        bool OpaqueGlassFallback(ThemeDefinition theme)
        {
            if (SystemParameters.HighContrast)
            {
                var highContrastColors = new Dictionary<string, Color>
                {
                    ["MaterialCanvas"] = SystemColors.WindowColor, ["Sidebar"] = SystemColors.WindowColor,
                    ["Panel"] = SystemColors.WindowColor, ["Inset"] = SystemColors.WindowColor,
                    ["Button"] = SystemColors.ControlColor, ["Primary"] = SystemColors.HighlightColor,
                    ["Selection"] = SystemColors.HighlightColor, ["Hover"] = SystemColors.HighlightColor,
                    ["Ink"] = SystemColors.WindowTextColor, ["MaterialEdge"] = SystemColors.WindowTextColor,
                    ["MaterialNavEdge"] = SystemColors.WindowTextColor
                };
                return highContrastColors.All(pair => app.Resources[pair.Key] is SolidColorBrush brush && brush.Color == pair.Value) &&
                    ReferenceEquals(app.Resources["OverlaySurface"], app.Resources["Panel"]) &&
                    ReferenceEquals(app.Resources["OverlayEdge"], app.Resources["MaterialEdge"]) &&
                    app.Resources["MaterialAmbient"] is SolidColorBrush { Color.A: 0 } &&
                    app.Resources["MaterialPanelEffect"] == null && app.Resources["MaterialActionEffect"] == null;
            }
            var brushes = new[] { "MaterialCanvas", "Sidebar", "Panel", "Inset", "Button", "Primary", "Selection", "Hover" }
                .Select(key => app.Resources[key] as LinearGradientBrush).ToArray();
            var expectedPrimary = ColorFor(theme, "Primary");
            var expectedEdge = Mix(ColorFor(theme, "Line"), ColorFor(theme, "Accent"), .28);
            var ink = (app.Resources["Ink"] as SolidColorBrush)?.Color;
            var panel = app.Resources["Panel"] as LinearGradientBrush;
            return brushes.All(brush => brush != null && brush.GradientStops.All(stop => stop.Color.A == byte.MaxValue)) &&
                app.Resources["Primary"] is LinearGradientBrush primary && primary.GradientStops[0].Color == expectedPrimary &&
                app.Resources["MaterialEdge"] is SolidColorBrush edge && edge.Color == expectedEdge &&
                ReferenceEquals(app.Resources["OverlaySurface"], app.Resources["Panel"]) &&
                ReferenceEquals(app.Resources["OverlayEdge"], app.Resources["MaterialEdge"]) && ink.HasValue && panel != null &&
                panel.GradientStops.All(stop => Contrast(ink.Value, stop.Color) >= 4.5);
        }
        async Task Snapshot(string name)
        {
            await Task.Delay(220); window.UpdateLayout(); Guard();
            var readResult = DwmGetWindowAttribute(handle, 38, out var type, sizeof(int));
            var status = WindowBackdrop.StatusFor(window);
            var row = new { name, theme = ThemeManager.Current.Id, processId = Environment.ProcessId, hwnd = handle.ToInt64().ToString(), width = window.ActualWidth, height = window.ActualHeight,
                backdrop = status, dwmReadResult = readResult, dwmBackdropType = type, compositionColor = HwndSource.FromHwnd(handle)?.CompositionTarget?.BackgroundColor.ToString(),
                foreground = originalForeground.ToInt64().ToString(), microphoneEnabled = record.IsEnabled, window.AllowsTransparency, renderTier = RenderCapability.Tier >> 16,
                canvasKind = app.Resources["MaterialCanvas"].GetType().Name, ambientKind = app.Resources["MaterialAmbient"].GetType().Name, panelKind = app.Resources["Panel"].GetType().Name, panelEffect = app.Resources["MaterialPanelEffect"]?.GetType().Name };
            states.Add(row);
            File.WriteAllText(Path.Combine(output, "capture-ready.json"), JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true }));
            var acknowledgement = Path.Combine(output, name + ".captured");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!File.Exists(acknowledgement)) { deadline.Token.ThrowIfCancellationRequested(); await Task.Delay(60, deadline.Token); }
        }
        Check(ThemeCatalog.All.Count == 8 && ThemeCatalog.All.Select(theme => theme.Id).Distinct().Count() == 8 &&
            ThemeCatalog.All.All(theme => theme.UsesGlassMaterial) && !ThemeCatalog.All.Any(theme => theme.Id == "amethyst-glass") &&
            ThemeCatalog.Resolve("amethyst-glass").Id == "amethyst" && combo.Items.Count == 8,
            "Eight selectable palettes share glass materials and the former Amethyst Glass ID aliases to Amethyst without a duplicate option");
        Choose("amethyst-glass");
        var startupPreference = Preferences.Load(Path.Combine(Environment.GetEnvironmentVariable("MURMUR_DATA")!, "preferences.json"));
        Check(ThemeManager.Current.Id == "amethyst" && startupPreference.Theme == "amethyst" && ThemeManager.Current.UsesGlassMaterial &&
            (SystemParameters.HighContrast ? app.Resources["Panel"] is SolidColorBrush && app.Resources["MaterialCanvas"] is SolidColorBrush : app.Resources["Panel"] is LinearGradientBrush && app.Resources["MaterialCanvas"] is LinearGradientBrush),
            "A saved legacy Amethyst Glass ID loads as canonical Amethyst with the shared glass or accessible fallback materials");
        var initialMaterial = WindowBackdrop.StatusFor(window);
        var initialAmbientOkay = SystemParameters.HighContrast || (RenderCapability.Tier >> 16) == 0 ||
            (initialMaterial.NativeActive ? app.Resources["MaterialAmbient"] is DrawingBrush { IsFrozen: true } : app.Resources["MaterialAmbient"] is SolidColorBrush { Color.A: 0 });
        Check(initialAmbientOkay, "Native glass illumination uses a frozen static drawing only when supported; fallback has no ambient overlay");
        await Snapshot("amethyst-glass-home");
        var pages = (TabControl)window.FindName("Pages"); pages.SelectedIndex = 1; window.UpdateLayout(); Guard();
        await Snapshot("amethyst-glass-settings");
        pages.SelectedIndex = 0; window.Width = 740; window.Height = 530; window.UpdateLayout(); Guard();
        await Snapshot("amethyst-glass-compact");
        window.Width = 1000; window.Height = 700;
        ThemeManager.ApplyTitleBar(window, allowNative: false); window.UpdateLayout(); Guard();
        Check(!WindowBackdrop.StatusFor(window).NativeActive && OpaqueGlassFallback(ThemeCatalog.Resolve("amethyst")) &&
            app.Resources["MaterialAmbient"] is SolidColorBrush { Color.A: 0 },
            "Native refusal keeps Amethyst glass surfaces, edges and text palette-derived and fully opaque");
        await Snapshot("amethyst-glass-opaque-fallback");
        Choose("fjord"); ThemeManager.ApplyTitleBar(window, allowNative: false);
        Choose("amethyst-glass"); ThemeManager.ApplyTitleBar(window, allowNative: false);
        Check(app.Resources["Panel"] is LinearGradientBrush && !WindowBackdrop.StatusFor(window).NativeActive, "Switching from Fjord back to canonical Amethyst preserves shared glass materials when native Mica is refused");
        Check(app.Resources["MaterialAmbient"] is SolidColorBrush { Color.A: 0 }, "Opaque fallback clears ambient illumination");
        Check(DwmGetWindowAttribute(handle, 38, out var none, sizeof(int)) < 0 || none == 1, "Switching away confirms the documented NONE backdrop value when the API is available");
        await Snapshot("amethyst-restored-home");
        foreach (var theme in ThemeCatalog.All)
        {
            Choose(theme.Id);
            ThemeManager.ApplyTitleBar(window, allowNative: false);
            Check(OpaqueGlassFallback(theme), $"{theme.Id} uses opaque palette-derived glass with readable text when native Mica is refused");
        }
        var fjord = ThemeCatalog.Resolve("fjord");
        Choose(fjord.Id); ThemeManager.ApplyTitleBar(window, allowNative: false);
        Check(OpaqueGlassFallback(fjord) && ColorFor(fjord, "Accent") != ColorFor(ThemeCatalog.Resolve("amethyst"), "Accent") &&
            ((LinearGradientBrush)app.Resources["Primary"]).GradientStops[0].Color == ColorFor(fjord, "Primary"),
            "Fjord glass surfaces and action color follow its blue-teal palette instead of the Amethyst hue");
        Choose("paper"); ThemeManager.ApplyTitleBar(window, allowNative: false);
        Check(!ThemeManager.Current.IsDark && OpaqueGlassFallback(ThemeManager.Current), "Light Paper glass retains readable dark text and fully opaque fallback surfaces");
        await Snapshot("paper-after-glass-home");
        Choose("amethyst-glass"); await Snapshot("amethyst-glass-restored-home");
        var preferences = Preferences.Load(Path.Combine(Environment.GetEnvironmentVariable("MURMUR_DATA")!, "preferences.json"));
        Check(preferences.Theme == "amethyst" && preferences.Language == "en" && !preferences.Startup && !preferences.KeepHistory && !preferences.ProtectionEnabled && !preferences.CopyToClipboard, "Legacy glass selection persists the canonical Amethyst appearance without changing the isolated profile's other settings");
        Check(ThemeCatalog.DefaultId == "charcoal" && new Preferences().Theme == "charcoal", "Shared glass materials do not change the new-profile Charcoal default");
        File.WriteAllText(Path.Combine(output, "theme-material-receipt.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks, states, ownerProfileUsed = false, microphoneOpened = false, globalInputSent = false, clipboardWritten = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
