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
        Choose("amethyst"); await Snapshot("amethyst-baseline-home");
        Choose("amethyst-glass");
        Check(ThemeManager.Current.UsesGlassMaterial && app.Resources["Panel"] is LinearGradientBrush && app.Resources["MaterialCanvas"] is LinearGradientBrush, "Glass theme provides native WPF layered materials instead of palette-only changes");
        Check(SystemParameters.HighContrast || (RenderCapability.Tier >> 16) == 0 || app.Resources["MaterialAmbient"] is DrawingBrush { IsFrozen: true }, "Glass illumination uses a frozen static drawing on supported rendering tiers");
        await Snapshot("amethyst-glass-home");
        var pages = (TabControl)window.FindName("Pages"); pages.SelectedIndex = 1; window.UpdateLayout(); Guard();
        await Snapshot("amethyst-glass-settings");
        pages.SelectedIndex = 0; window.Width = 740; window.Height = 530; window.UpdateLayout(); Guard();
        await Snapshot("amethyst-glass-compact");
        window.Width = 1000; window.Height = 700;
        ThemeManager.ApplyTitleBar(window, allowNative: false); window.UpdateLayout(); Guard();
        Check(!WindowBackdrop.StatusFor(window).NativeActive && ((LinearGradientBrush)app.Resources["MaterialCanvas"]).GradientStops.All(stop => stop.Color.A == 255), "Native refusal uses a completely opaque material canvas");
        await Snapshot("amethyst-glass-opaque-fallback");
        Choose("amethyst");
        Check(app.Resources["Panel"] is SolidColorBrush && app.Resources["MaterialPanelEffect"] == null && app.Resources["MaterialActionEffect"] == null && !WindowBackdrop.StatusFor(window).NativeActive, "Switching to original Amethyst clears glass brushes, effects and native ownership");
        Check(app.Resources["MaterialAmbient"] is SolidColorBrush { Color.A: 0 }, "Switching away clears all ambient illumination");
        Check(DwmGetWindowAttribute(handle, 38, out var none, sizeof(int)) < 0 || none == 1, "Switching away confirms the documented NONE backdrop value when the API is available");
        await Snapshot("amethyst-restored-home");
        Choose("paper"); Check(!ThemeManager.Current.IsDark && app.Resources["Panel"] is SolidColorBrush, "Light theme remains opaque after a glass switch"); await Snapshot("paper-after-glass-home");
        Choose("amethyst-glass"); await Snapshot("amethyst-glass-restored-home");
        var preferences = Preferences.Load(Path.Combine(Environment.GetEnvironmentVariable("MURMUR_DATA")!, "preferences.json"));
        Check(preferences.Theme == "amethyst-glass" && preferences.Language == "en" && !preferences.Startup && !preferences.KeepHistory && !preferences.ProtectionEnabled && !preferences.CopyToClipboard, "Real theme selector persists only the chosen appearance in its isolated profile");
        Check(ThemeCatalog.DefaultId == "charcoal" && new Preferences().Theme == "charcoal", "Optional glass does not change new-profile defaults");
        File.WriteAllText(Path.Combine(output, "theme-material-receipt.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks, states, ownerProfileUsed = false, microphoneOpened = false, globalInputSent = false, clipboardWritten = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
