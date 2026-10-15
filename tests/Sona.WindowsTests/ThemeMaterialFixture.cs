using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Shapes;
using Path = System.IO.Path;
using Sona.App;
using Sona.Core;

internal sealed record ThemeMaterialFixtureResult(
    bool StartupCallbackObserved,
    bool ProductionStartupSuppressed,
    bool HighContrast,
    string[] Checks,
    string[] Failures);

internal sealed record PresentationFixtureResult(
    bool StartupCallbackObserved,
    bool ProductionStartupSuppressed,
    bool AppResourcesLoaded,
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

    public static PresentationFixtureResult RunPresentation()
    {
        var checks = new List<string>();
        var failures = new List<string>();
        Exception? threadFailure = null;
        var startupObserved = false;
        var productionStartupSuppressed = false;
        var appResourcesLoaded = false;
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
                            && application.Resources["SettingGroup"] is Style
                            && application.Resources["PrimaryButton"] is Style
                            && application.Resources[typeof(TabItem)] is Style
                            && application.Resources[typeof(RadioButton)] is Style;
                        if (!startupObserved) failures.Add("Suppressed application startup callback was not observed");
                        else checks.Add("Production startup callback was observed and intercepted");
                        if (!productionStartupSuppressed) failures.Add("Production startup created a window");
                        else checks.Add("No production window was created");
                        if (!appResourcesLoaded) failures.Add("App.xaml did not load the actual presentation styles and resources");
                        else checks.Add("App.xaml presentation styles and resources loaded");

                        if (startupObserved && productionStartupSuppressed && appResourcesLoaded)
                        {
                            ThemeManager.Apply(ThemeCatalog.DefaultId);
                            var root = new StackPanel();
                            var groupText = new TextBlock { Text = "Readable settings" };
                            var settingGroup = new Border
                            {
                                Style = (Style)application.Resources["SettingGroup"],
                                Child = groupText
                            };
                            var buttonText = new TextBlock { Text = "Continue" };
                            var button = new Button
                            {
                                Style = (Style)application.Resources["PrimaryButton"],
                                Content = buttonText
                            };
                            var tab = new TabItem
                            {
                                Style = (Style)application.Resources[typeof(TabItem)],
                                Header = "Settings",
                                Tag = "S",
                                IsSelected = true
                            };
                            var radio = new RadioButton
                            {
                                Style = (Style)application.Resources[typeof(RadioButton)],
                                Content = "Panel",
                                IsChecked = false
                            };
                            root.Children.Add(settingGroup);
                            root.Children.Add(button);
                            root.Children.Add(tab);
                            root.Children.Add(radio);
                            root.Measure(new Size(640, 480));
                            root.Arrange(new Rect(0, 0, 640, 480));
                            root.UpdateLayout();
                            settingGroup.ApplyTemplate();
                            button.ApplyTemplate();
                            tab.ApplyTemplate();
                            radio.ApplyTemplate();

                            CheckNoVisualEffects(settingGroup, "SettingGroup", checks, failures);
                            CheckNoVisualEffects(button, "PrimaryButton", checks, failures);
                            CheckSelectedTab(tab, application, checks, failures);
                            CheckRadioAffordance(radio, root, application, checks, failures);
                            CheckIsolatedSettings(checks, failures);
                            CheckDisabledButtons(root, application, checks, failures);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add("Presentation fixture dispatcher callback: " + ex.GetType().Name + ": " + ex.Message);
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
        }) { IsBackground = true, Name = "Sona presentation fixture" };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Presentation fixture exceeded ten seconds");
        if (threadFailure != null)
            failures.Add(threadFailure.GetType().Name + ": " + threadFailure.Message);
        return new PresentationFixtureResult(startupObserved, productionStartupSuppressed, appResourcesLoaded, checks.ToArray(), failures.ToArray());
    }

    private static void CheckDisabledButtons(Panel root, Application application, List<string> checks, List<string> failures)
    {
        foreach (var theme in ThemeCatalog.All)
        {
            ThemeManager.Apply(theme.Id);
            var button = new Button { Style = (Style)application.Resources["PrimaryButton"], Content = "Record", IsEnabled = false };
            root.Children.Add(button); root.Measure(new Size(640, 480)); root.Arrange(new Rect(0, 0, 640, 480));
            root.UpdateLayout(); button.ApplyTemplate();
            if (button.Opacity == 1 && Describe(button.Background) == Describe(application.Resources["Inset"])
                && Describe(button.Foreground) == Describe(application.Resources["Muted"])
                && button.Cursor == System.Windows.Input.Cursors.Arrow)
                checks.Add(theme.Id + ": disabled primary action retains readable neutral styling without whole-control dimming");
            else failures.Add(theme.Id + ": disabled primary action state: opacity=" + button.Opacity + "; background=" + Describe(button.Background) + "; expected=" + Describe(application.Resources["Inset"]) + "; foreground=" + Describe(button.Foreground) + "; cursor=" + button.Cursor);
            CheckTextContrast(application, new[] { "Inset" }, "Muted", theme.Id + ": disabled action label", checks, failures);
            root.Children.Remove(button);
        }
        ThemeManager.Apply(ThemeCatalog.DefaultId);
    }

    private static void CheckNoVisualEffects(FrameworkElement root, string label, List<string> checks, List<string> failures)
    {
        var effected = VisualDescendants(root)
            .OfType<UIElement>()
            .Where(element => element.Effect != null)
            .Select(element => element.GetType().Name)
            .ToArray();
        if (effected.Length == 0)
            checks.Add(label + " and its rendered descendants have no WPF Effect");
        else
            failures.Add(label + " applies WPF Effect to rendered foreground content: " + string.Join(", ", effected));
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        yield return root;
        if (root is not Visual) yield break;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
            foreach (var child in VisualDescendants(VisualTreeHelper.GetChild(root, index)))
                yield return child;
    }

    private static void CheckSelectedTab(TabItem tab, Application application, List<string> checks, List<string> failures)
    {
        var nav = tab.Template?.FindName("Nav", tab) as Border;
        if (tab.IsSelected && nav != null)
            CheckNoVisualEffects(tab, "Selected tab", checks, failures);
        else
            failures.Add("Selected tab template did not create its Nav surface");

        var expected = application.Resources["SelectedInk"] as SolidColorBrush;
        var actual = tab.Foreground as SolidColorBrush;
        var textColors = VisualDescendants(tab).OfType<TextBlock>().Select(text => text.Foreground as SolidColorBrush).ToArray();
        if (expected != null && actual?.Color == expected.Color && textColors.Length >= 2 && textColors.All(color => color?.Color == expected.Color))
            checks.Add("Selected tab and rendered labels resolve to the selected foreground color");
        else
            failures.Add("Selected tab foreground did not reach both rendered labels with the selected palette color");
    }

    private static void CheckRadioAffordance(RadioButton radio, StackPanel root, Application application, List<string> checks, List<string> failures)
    {
        var dot = radio.Template?.FindName("SelectionDot", radio) as FrameworkElement;
        var indicator = radio.Template?.FindName("Indicator", radio) as Border;
        var panel = application.Resources["Panel"];
        if (dot?.Visibility == Visibility.Collapsed && panel != null && ReferenceEquals(indicator?.Background, panel))
            checks.Add("Unchecked display option renders an empty ring against the panel");
        else
            failures.Add("Unchecked display option did not render the unselected radio affordance");

        radio.IsChecked = true;
        root.UpdateLayout();
        var accent = application.Resources["Accent"] as SolidColorBrush;
        var selectedInk = application.Resources["SelectedInk"] as SolidColorBrush;
        if (dot?.Visibility == Visibility.Visible && ReferenceEquals(dot is Ellipse ellipse ? ellipse.Fill : null, panel)
            && accent != null && (indicator?.Background as SolidColorBrush)?.Color == accent.Color
            && selectedInk != null && (radio.Foreground as SolidColorBrush)?.Color == selectedInk.Color)
            checks.Add("Checked display option renders a visible accent dot and selected foreground");
        else
            failures.Add("Checked display option did not visibly differ with the selected accent treatment");
    }

    private static void CheckIsolatedSettings(List<string> checks, List<string> failures)
    {
        var dataRoot = Path.Combine(Path.GetTempPath(), "sona-presentation-fixture-" + Guid.NewGuid().ToString("N"));
        var preferencesPath = Path.Combine(dataRoot, "preferences.json");
        var priorData = Environment.GetEnvironmentVariable("SONA_DATA");
        var priorTransfer = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        MainWindow? window = null;
        void Proof(bool condition, string name)
        {
            if (condition) checks.Add(name);
            else failures.Add(name);
        }

        try
        {
            var saved = new Preferences
            {
                AutoInsert = false,
                Startup = true,
                ProtectionEnabled = false,
                SilenceIncoming = false,
                ProtectionReleaseDelayMs = 321,
                ProtectionShortcut = "ctrl+shift+h",
                WatchedApps = ["fixture.exe"],
                ModelIdleMinutes = 10,
                Theme = ThemeCatalog.DefaultId
            };
            saved.Save(preferencesPath);
            Environment.SetEnvironmentVariable("SONA_DATA", dataRoot);
            // The constructor consumes this process-only transfer variable even in test mode.
            // Clear it for the isolated window and restore it without inspecting its contents.
            Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
            var app = Application.Current ?? throw new InvalidOperationException("Isolated settings fixture requires the intercepted WPF application");
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window = new MainWindow(["--test-ui"]);

            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var transcriber = typeof(MainWindow).GetField("transcriber", flags)?.GetValue(window) as Sona.App.Audio.Transcriber;
            var noRuntimeServices = !window.IsVisible && !window.IsLoaded
                && typeof(MainWindow).GetField("recorder", flags)?.GetValue(window) is null
                && typeof(MainWindow).GetField("protection", flags)?.GetValue(window) is null
                && typeof(MainWindow).GetField("credentialVault", flags)?.GetValue(window) is null
                && typeof(MainWindow).GetField("agentServer", flags)?.GetValue(window) is null
                && typeof(MainWindow).GetField("hotkeys", flags)?.GetValue(window) is null
                && typeof(MainWindow).GetField("activeApplication", flags)?.GetValue(window) is null
                && transcriber != null && transcriber.ResidentProcessId is null && transcriber.LoadedModel is null;
            Proof(noRuntimeServices, "Isolated --test-ui settings window stayed unshown with no recorder, resident worker, protection service, credential vault, agent server, shortcut hook, or active-app observer");
            Proof(window.PreferencesSaveBar.Visibility == Visibility.Collapsed, "Fresh isolated preferences begin with the general Save bar hidden");

            window.ProtectionEnabled.IsChecked = true;
            window.SilenceIncoming.IsChecked = true;
            window.WatchedApps.Text = "fixture-only.exe\nsecond-fixture.exe";
            window.ProtectionDelay.Text = "876";
            window.ProtectionShortcutInput.Text = "ctrl+alt+q";
            var afterProtectionEdits = Preferences.Load(preferencesPath);
            Proof(window.PreferencesSaveBar.Visibility == Visibility.Collapsed
                && afterProtectionEdits.ProtectionEnabled == saved.ProtectionEnabled
                && afterProtectionEdits.SilenceIncoming == saved.SilenceIncoming
                && afterProtectionEdits.ProtectionReleaseDelayMs == saved.ProtectionReleaseDelayMs
                && afterProtectionEdits.ProtectionShortcut == saved.ProtectionShortcut
                && afterProtectionEdits.WatchedApps.SequenceEqual(saved.WatchedApps),
                "Protection-only form edits neither show the general Save bar nor write protected settings");

            window.AutoInsert.IsChecked = true;
            var editShowsSave = window.PreferencesSaveBar.Visibility == Visibility.Visible;
            window.AutoInsert.IsChecked = false;
            var revertHidesSave = window.PreferencesSaveBar.Visibility == Visibility.Collapsed;
            Proof(editShowsSave && revertHidesSave, "A general preference edit shows the sticky Save bar and reverting it hides the bar");

            window.AutoInsert.IsChecked = true;
            var idleChoice = window.ModelIdleChoice.Items.OfType<ComboBoxItem>().FirstOrDefault(item => (string?)item.Tag == "5");
            if (idleChoice == null)
            {
                failures.Add("Five-minute model idle choice was not available in the isolated preferences window");
            }
            else
            {
                window.ModelIdleChoice.SelectedItem = idleChoice;
                var afterIdleSave = Preferences.Load(preferencesPath);
                Proof(afterIdleSave.ModelIdleMinutes == 5 && afterIdleSave.AutoInsert == saved.AutoInsert
                    && afterIdleSave.Startup == saved.Startup
                    && afterIdleSave.ProtectionEnabled == saved.ProtectionEnabled
                    && afterIdleSave.SilenceIncoming == saved.SilenceIncoming
                    && afterIdleSave.ProtectionReleaseDelayMs == saved.ProtectionReleaseDelayMs
                    && afterIdleSave.ProtectionShortcut == saved.ProtectionShortcut
                    && afterIdleSave.WatchedApps.SequenceEqual(saved.WatchedApps),
                    "Model idle choice persists only its committed value while retaining saved general, startup, and protection preferences");
                Proof(window.AutoInsert.IsChecked == true && window.PreferencesSaveBar.Visibility == Visibility.Visible,
                    "Idle-only persistence leaves the unrelated unsaved general form choice and sticky Save bar intact");
            }
        }
        catch (Exception ex)
        {
            failures.Add("Isolated settings fixture: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            try
            {
                if (window != null)
                {
                    var quitting = typeof(MainWindow).GetField("quitting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (quitting == null) throw new MissingFieldException(typeof(MainWindow).FullName, "quitting");
                    quitting.SetValue(window, true);
                    window.Close();
                }
            }
            catch (Exception ex)
            {
                failures.Add("Closing isolated settings window: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SONA_DATA", priorData);
                Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", priorTransfer);
                try { if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { failures.Add("Removing isolated settings fixture data: " + ex.GetType().Name + ": " + ex.Message); }
            }
        }
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
            var allowOverlay = !highContrast && Sona.App.Windows.WindowBackdrop.TransparencyEnabled();
            var expectedAlpha = theme.IsDark ? 200 : 224;
            var overlayMatches = (allowOverlay
                ? overlaySurface is LinearGradientBrush glass && glass.GradientStops.All(stop => stop.Color.A == expectedAlpha)
                : ReferenceEquals(overlaySurface, application.Resources["Panel"]) || highContrast && ReferenceEquals(overlaySurface, SystemColors.WindowBrush))
                && ReferenceEquals(overlayEdge, edge);
            if (overlayMatches) checks.Add(prefix + "overlay honors independent transparency preference and theme edge");
            else failures.Add(prefix + "overlay transparency contract failed");
            if (ReferenceEquals(ThemeManager.CreateOverlaySurface(false, false), application.Resources["Panel"])
                && ReferenceEquals(ThemeManager.CreateOverlaySurface(true, true), SystemColors.WindowBrush))
                checks.Add(prefix + "disabled transparency and high contrast retain opaque fallback");
            else failures.Add(prefix + "recording fallback failed");
            if (!highContrast)
            {
                var recording = (LinearGradientBrush)ThemeManager.CreateOverlaySurface(true, false);
                foreach (var backdrop in new[] { Colors.Black, Colors.White })
                {
                    var ink = ((SolidColorBrush)application.Resources["Ink"]).Color;
                    var accent = ((SolidColorBrush)application.Resources["Accent"]).Color;
                    var backgrounds = recording.GradientStops.Select(stop => Composite(stop.Color, backdrop)).ToArray();
                    if (backgrounds.All(bg => Contrast(ink, bg) >= 4.5 && Contrast(accent, bg) >= 3))
                        checks.Add(prefix + "recording text and waveform meet contrast over " + backdrop);
                    else failures.Add(prefix + "recording contrast failed over " + backdrop);
                }
            }

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

    private static Color Composite(Color surface, Color background)
    {
        var alpha = surface.A / 255d;
        byte Blend(byte front, byte back) => (byte)Math.Round(front * alpha + back * (1 - alpha));
        return Color.FromRgb(Blend(surface.R, background.R), Blend(surface.G, background.G), Blend(surface.B, background.B));
    }

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
