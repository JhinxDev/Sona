using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Murmur.App;
using Murmur.Core;

internal sealed record ThemeMaterialFixtureResult(
    bool StartupCallbackObserved,
    bool ProductionStartupSuppressed,
    bool HighContrast,
    string[] Checks,
    string[] Failures);

internal sealed record ThemeStartupFixtureResult(
    bool StartupCallbackObserved,
    bool ProductionStartupSuppressed,
    bool AppResourcesLoaded,
    string[] Failures);

internal static class ThemeMaterialFixture
{
    private static readonly string[] SurfaceKeys =
    [
        "MaterialCanvas", "Sidebar", "Panel", "Inset", "Button", "Primary", "Selection", "Hover"
    ];

    public static ThemeStartupFixtureResult RunStartupInterception()
    {
        Exception? threadFailure = null;
        var startupObserved = false;
        var productionStartupSuppressed = false;
        var appResourcesLoaded = false;
        var failures = new List<string>();
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var application = new ThemeResourceTestApplication();
                application.InitializeComponent();
                dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        startupObserved = application.StartupCallbackObserved;
                        productionStartupSuppressed = application.MainWindow is null && application.Windows.Count == 0;
                        appResourcesLoaded = application.Resources["SonaMark"] is Geometry
                            && application.Resources["MaterialEdge"] is not null;
                        if (!startupObserved) failures.Add("Suppressed application startup callback was not observed");
                        if (!productionStartupSuppressed) failures.Add("Production startup created a window");
                        if (!appResourcesLoaded) failures.Add("App.xaml styles/resources did not load in the same-assembly test subclass");
                    }
                    catch (Exception ex)
                    {
                        failures.Add("Startup fixture dispatcher callback: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        application.Shutdown();
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }));
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        }) { IsBackground = true, Name = "Sona intercepted startup fixture" };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Theme startup interception fixture exceeded ten seconds");
        if (threadFailure != null)
            failures.Add(threadFailure.GetType().Name + ": " + threadFailure.Message);
        return new ThemeStartupFixtureResult(startupObserved, productionStartupSuppressed, appResourcesLoaded, failures.ToArray());
    }

    public static ThemeMaterialFixtureResult RunIsolated()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        Exception? threadFailure = null;
        var startupObserved = false;
        var productionStartupSuppressed = false;
        var highContrast = false;

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                var application = new Application();
                application.Startup += (_, _) => startupObserved = true;
                dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        highContrast = SystemParameters.HighContrast;
                        if (startupObserved) checks.Add("Plain WPF startup event was observed before the resource checks");
                        else failures.Add("Plain WPF startup event was not observed before the resource checks");

                        productionStartupSuppressed = application.MainWindow is null && application.Windows.Count == 0;
                        if (productionStartupSuppressed) checks.Add("Resource-only application has no production window or service startup");
                        else failures.Add("Resource-only application created a window before resource checks");

                        failures.AddRange(CheckResources(application, checks, highContrast));
                    }
                    catch (Exception ex)
                    {
                        failures.Add("Fixture dispatcher callback: " + ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        application.Shutdown();
                        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
                    }
                }));
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                threadFailure = ex;
            }
        }) { IsBackground = true, Name = "Sona theme resource fixture" };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Theme resource fixture exceeded ten seconds");
        if (threadFailure != null)
            throw new InvalidOperationException("Theme resource fixture thread failed", threadFailure);

        return new ThemeMaterialFixtureResult(startupObserved, productionStartupSuppressed, highContrast, checks.ToArray(), failures.ToArray());
    }

    public static IReadOnlyList<string> CheckResources(Application application, List<string>? checks = null, bool? highContrastOverride = null)
    {
        checks ??= [];
        var failures = new List<string>();
        var highContrast = highContrastOverride ?? SystemParameters.HighContrast;
        var themes = ThemeCatalog.All;
        var edgeColors = new HashSet<Color>();
        if (themes.Count != 8)
            failures.Add($"Catalog contains {themes.Count} themes; expected eight");
        else
            checks.Add("Catalog exposes all eight themes");

        foreach (var theme in themes)
        {
            ThemeManager.Apply(theme.Id);
            var prefix = theme.Id + ": ";
            var edge = application.Resources["MaterialEdge"];
            var overlaySurface = application.Resources["OverlaySurface"];
            var overlayEdge = application.Resources["OverlayEdge"];
            var aliasesMatch = ReferenceEquals(overlaySurface, application.Resources["Panel"])
                && ReferenceEquals(overlayEdge, edge);
            if (aliasesMatch) checks.Add(prefix + "overlay aliases point to the selected panel and edge resources");
            else failures.Add(prefix + "overlay aliases do not point to Panel/MaterialEdge");

            if (theme.UsesGlassMaterial)
                checks.Add(prefix + "theme retains its glass-material selection");
            else
                failures.Add(prefix + "theme does not select the shared glass material");

            if (highContrast)
            {
                CheckHighContrast(application, theme.Id, checks, failures);
                continue;
            }

            var expectedPrimary = Parse(theme.Colors["Primary"]);
            var primary = application.Resources["Primary"] as LinearGradientBrush;
            if (primary?.GradientStops.FirstOrDefault()?.Color == expectedPrimary)
                checks.Add(prefix + "primary material begins with this palette's primary color");
            else
                failures.Add(prefix + $"Primary first stop did not equal palette color {expectedPrimary}; resource={Describe(application.Resources["Primary"])}");

            if (edge is SolidColorBrush edgeBrush && edgeBrush.Color.A == byte.MaxValue)
            {
                edgeColors.Add(edgeBrush.Color);
                checks.Add(prefix + "fallback edge is a solid opaque color");
            }
            else
                failures.Add(prefix + $"MaterialEdge is not an opaque solid fallback; resource={Describe(edge)}");

            var badSurfaces = new List<string>();
            foreach (var key in SurfaceKeys)
            {
                if (application.Resources[key] is not LinearGradientBrush brush || brush.GradientStops.Count < 2)
                {
                    badSurfaces.Add(key + " (missing gradient)");
                    continue;
                }
                var translucentStops = brush.GradientStops.Where(stop => stop.Color.A != byte.MaxValue).ToArray();
                if (translucentStops.Length > 0)
                    badSurfaces.Add(key + $" ({translucentStops.Length} translucent stop(s))");
            }
            if (badSurfaces.Count == 0)
                checks.Add(prefix + "all eight protected/material surfaces are gradients with fully opaque stops");
            else
                failures.Add(prefix + "opaque fallback surface predicate failed: " + string.Join(", ", badSurfaces));

            CheckTextContrast(application, theme.Id, new[] { "MaterialCanvas", "Panel", "Inset", "Button" }, "Ink", "primary text", checks, failures);
            CheckTextContrast(application, new[] { "Sidebar", "Panel" }, "Muted", "secondary text", checks, failures);
            CheckTextContrast(application, new[] { "Primary" }, "PrimaryInk", "primary action text", checks, failures);
            CheckTextContrast(application, new[] { "Selection" }, "SelectedInk", "selected text", checks, failures);
        }

        if (!highContrast && edgeColors.Count > 1)
            checks.Add($"Palette selection changes the opaque fallback edge across {edgeColors.Count} distinct palette colors");
        else if (!highContrast)
            failures.Add("Opaque fallback edge did not vary across palette selection");

        return failures;
    }

    private static void CheckHighContrast(Application application, string themeId, List<string> checks, List<string> failures)
    {
        var prefix = themeId + ": high contrast: ";
        foreach (var key in SurfaceKeys)
        {
            var expected = key switch
            {
                "Button" => SystemColors.ControlColor,
                "Primary" or "Selection" or "Hover" => SystemColors.HighlightColor,
                _ => SystemColors.WindowColor
            };
            if (application.Resources[key] is SolidColorBrush brush && brush.Color == expected && brush.Color.A == byte.MaxValue)
                checks.Add(prefix + key + " uses its opaque system color");
            else
                failures.Add(prefix + $"{key} did not use opaque system color {expected}; resource={Describe(application.Resources[key])}");
        }

        var edgeExpected = SystemColors.WindowTextColor;
        if (application.Resources["MaterialEdge"] is SolidColorBrush edge && edge.Color == edgeExpected && edge.Color.A == byte.MaxValue)
            checks.Add(prefix + "material edge uses opaque WindowTextColor");
        else
            failures.Add(prefix + "material edge did not use opaque WindowTextColor");

        foreach (var key in new[] { "PrimaryInk", "SelectedInk" })
        {
            if (application.Resources[key] is SolidColorBrush brush && brush.Color == SystemColors.HighlightTextColor)
                checks.Add(prefix + key + " uses HighlightTextColor");
            else
                failures.Add(prefix + key + " did not use HighlightTextColor");
        }

        foreach (var key in new[] { "Ink", "Muted" })
        {
            if (application.Resources[key] is SolidColorBrush brush && brush.Color == SystemColors.WindowTextColor)
                checks.Add(prefix + key + " uses WindowTextColor");
            else
                failures.Add(prefix + key + " did not use WindowTextColor");
        }
    }

    private static void CheckTextContrast(Application application, string themeId, string[] surfaceKeys, string textKey, string label, List<string> checks, List<string> failures)
        => CheckTextContrast(application, surfaceKeys, textKey, themeId + ": " + label, checks, failures);

    private static void CheckTextContrast(Application application, string[] surfaceKeys, string textKey, string label, List<string> checks, List<string> failures)
    {
        if (application.Resources[textKey] is not SolidColorBrush textBrush)
        {
            failures.Add(label + " did not resolve to a solid text color");
            return;
        }

        var failingEndpoints = new List<string>();
        foreach (var surfaceKey in surfaceKeys)
        {
            if (application.Resources[surfaceKey] is not LinearGradientBrush surface)
            {
                failingEndpoints.Add(surfaceKey + " (not a gradient)");
                continue;
            }
            foreach (var (stop, index) in surface.GradientStops.Select((stop, index) => (stop, index)))
            {
                if (Contrast(textBrush.Color, stop.Color) < 4.5)
                    failingEndpoints.Add($"{surfaceKey}[{index}]={stop.Color}");
            }
        }

        if (failingEndpoints.Count == 0)
            checks.Add(label + " meets 4.5:1 contrast at every material endpoint");
        else
            failures.Add(label + " falls below 4.5:1 contrast at " + string.Join(", ", failingEndpoints));
    }

    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);

    private static double Contrast(Color foreground, Color background)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                var s = value / 255d;
                return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }

    private static string Describe(object? value) => value switch
    {
        SolidColorBrush brush => $"SolidColorBrush({brush.Color})",
        LinearGradientBrush brush => $"LinearGradientBrush({string.Join(";", brush.GradientStops.Select(stop => stop.Color))})",
        null => "null",
        _ => value.GetType().Name
    };
}
