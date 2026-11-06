using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class ProfessionalPolishFixture
{
    /// <summary>Fresh profile: welcome card, word count, per-app mode management, session resolution.</summary>
    public static object RunFresh(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Fresh evidence directory required.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile");
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        var checks = new List<string>(); Exception? failure = null;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); File.WriteAllLines(Path.Combine(output, "progress.txt"), checks); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown; app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    window = new MainWindow(["--test-ui"]) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, Width = 1000, Height = 700 };
                    window.Show();
                    await Task.Delay(150); window.UpdateLayout();
                    object Field(string name) => window.FindName(name);
                    object? Invoke(string name, params object?[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);
                    void Click(string name, object? sender = null) => Invoke(name, sender ?? window, new RoutedEventArgs());
                    void Capture(string name)
                    {
                        window.UpdateLayout(); var root = (FrameworkElement)window.Content;
                        const double scale = 1.25;
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale), (int)Math.Ceiling(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                        bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
                    }
                    var pages = (TabControl)Field("Pages");
                    var welcome = (FrameworkElement)Field("WelcomeCard");
                    ((ComboBox)Field("ThemeChoice")).SelectedItem = ThemeCatalog.Resolve("amethyst");
                    await Task.Delay(120); window.UpdateLayout();
                    var setup = (FrameworkElement)Field("SetupCallout");
                    var modelReady = new Transcriber().AvailableFor(new Preferences());
                    Check(welcome.Visibility == Visibility.Visible, "Fresh profile shows the welcome card on Home");
                    Check(setup.Visibility == Visibility.Collapsed, "Welcome card replaces the separate setup callout");
                    var status = ((TextBlock)Field("WelcomeModelStatus")).Text;
                    Check(modelReady == status.Contains("ready on this PC", StringComparison.Ordinal), "Welcome card reports the actual local model state");
                    var welcomeAction = (string)((Button)Field("WelcomeModelsButton")).Content;
                    Check(modelReady ? welcomeAction.Contains("Review", StringComparison.Ordinal) : welcomeAction.Contains("Choose", StringComparison.Ordinal), "Welcome action matches the model state");
                    Capture("welcome");
                    Click("OpenModels_Click");
                    Check(ReferenceEquals(pages.SelectedItem, Field("ModelsTabItem")), "Welcome action opens Dictation Models");
                    Click("OpenSettings_Click");
                    Check(ReferenceEquals(pages.SelectedItem, Field("SettingsTabItem")), "Welcome action opens Settings");
                    pages.SelectedIndex = 0;
                    var draft = (TextBox)Field("Draft");
                    draft.Text = "Hello world. Don't stop.";
                    window.UpdateLayout();
                    Check(((TextBlock)Field("DraftStats")).Text == "4 words", "Home shows a live draft word count");
                    draft.Clear();
                    Check(((TextBlock)Field("DraftStats")).Text == "0 words", "Clearing the draft resets its word count");
                    Click("DismissWelcome_Click");
                    var dismissed = Preferences.Load(Path.Combine(profile, "preferences.json"));
                    Check(dismissed.OnboardingDismissed && welcome.Visibility == Visibility.Collapsed, "Dismissing the welcome card hides it and persists the choice");
                    var modesTab = pages.Items.OfType<TabItem>().Single(tab => Equals(tab.Header, "Modes"));
                    pages.SelectedItem = modesTab; window.UpdateLayout();
                    var appName = Process.GetCurrentProcess().ProcessName;
                    ((TextBox)Field("AppModeApp")).Text = appName;
                    ((ComboBox)Field("AppModeStyle")).SelectedIndex = 1;
                    ((ComboBox)Field("AppModeTone")).SelectedIndex = 1;
                    Click("AddAppMode_Click");
                    Check(((ItemsControl)Field("AppModeList")).Items.Count == 1, "Modes page lists an added per-app rule");
                    Invoke("RefreshSettingsActions");
                    Check(((FrameworkElement)Field("PreferencesSaveBar")).Visibility == Visibility.Visible, "Adding a per-app rule raises the unsaved preferences bar");
                    Capture("modes");
                    Click("Preferences_Click");
                    var stored = Preferences.Load(Path.Combine(profile, "preferences.json"));
                    Check(stored.AppModes.Count == 1 && stored.AppModes[0].Style == "Verbatim" && stored.AppModes[0].Tone == "casual", "Save preferences commits the per-app rule with its style and tone");
                    Check(((FrameworkElement)Field("PreferencesSaveBar")).Visibility == Visibility.Collapsed, "Saving a per-app rule clears the unsaved bar");
                    Invoke("ResolveSessionMode", new DraftTarget(0, Environment.ProcessId, [1, 2], 0));
                    var mode = typeof(MainWindow).GetField("sessionMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                    var delivery = (Preferences?)typeof(MainWindow).GetField("sessionDeliveryPreferences", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
                    Check(mode != null && delivery is { Style: "Verbatim", CleanupTone: "casual" }, "Captured process identity resolves its per-app delivery preferences");
                    Check(delivery is not null && delivery.Style == "Verbatim" && delivery.CleanupTone == "casual", "Resolved per-app mode changes only the session style and tone");
                    Invoke("ResetSessionMode");
                    Check(typeof(MainWindow).GetField("sessionMode", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) == null, "Session mode resets after the dictation context ends");
                    Check(((FrameworkElement)Field("SetupCallout")).Visibility == Visibility.Collapsed, "Ready model shows no setup callout after welcome dismissal");
                }
                catch (Exception ex) { failure = ex; File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); }
                finally { if (window != null) window.RequestQuit(); else app.Shutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40))) throw new TimeoutException("Professional polish UI did not exit");
        if (failure != null) throw new InvalidOperationException("Professional polish fixture failed", failure);
        return new { Passed = checks.Count, Checks = checks, MicrophoneOpened = false, GlobalInputSent = false };
    }

    /// <summary>Loaded profile with a missing runtime: setup callout instead of welcome, Record gated.</summary>
    public static object RunSetup(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Fresh evidence directory required.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile");
        new Preferences { SoundEffects = false, AutoInsert = false }.Save(Path.Combine(profile, "preferences.json"));
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        var missingRuntime = Path.Combine(output, "missing-runtime");
        Directory.CreateDirectory(missingRuntime);
        var priorRuntime = Environment.GetEnvironmentVariable("SONA_RUNTIME");
        Environment.SetEnvironmentVariable("SONA_RUNTIME", missingRuntime);
        var checks = new List<string>(); Exception? failure = null;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks.Add(label); File.WriteAllLines(Path.Combine(output, "progress.txt"), checks); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown; app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    window = new MainWindow(["--test-ui"]) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, Width = 1000, Height = 700 };
                    window.Show();
                    await Task.Delay(150); window.UpdateLayout();
                    object Field(string name) => window.FindName(name);
                    var pages = (TabControl)Field("Pages");
                    var welcome = (FrameworkElement)Field("WelcomeCard");
                    ((ComboBox)Field("ThemeChoice")).SelectedItem = ThemeCatalog.Resolve("amethyst");
                    await Task.Delay(120); window.UpdateLayout();
                    var setup = (FrameworkElement)Field("SetupCallout");
                    ((ComboBox)Field("ThemeChoice")).SelectedItem = ThemeCatalog.Resolve("amethyst");
                    await Task.Delay(120); window.UpdateLayout();
                    Check(welcome.Visibility == Visibility.Collapsed, "Existing profile never sees the first-run welcome card");
                    Check(setup.Visibility == Visibility.Visible, "Missing local runtime shows setup guidance on Home");
                    Check(((TextBlock)Field("SetupCalloutDetail")).Text.Contains("runtime is missing", StringComparison.Ordinal), "Setup guidance names the missing local runtime honestly");
                    window.UpdateLayout();
                    var root = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(Path.Combine(output, "setup-callout.png"))) png.Save(stream);
                    var setupBorder = (Border)setup;
                    var openModels = ((Grid)setupBorder.Child).Children.Cast<UIElement>().OfType<Button>().Single();
                    typeof(MainWindow).GetMethod("OpenModels_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [openModels, new RoutedEventArgs()]);
                    Check(ReferenceEquals(pages.SelectedItem, Field("ModelsTabItem")), "Setup guidance opens Dictation Models");
                }
                catch (Exception ex) { failure = ex; File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); }
                finally { if (window != null) window.RequestQuit(); else app.Shutdown(); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40))) throw new TimeoutException("Sona setup UI did not exit");
        Environment.SetEnvironmentVariable("SONA_RUNTIME", priorRuntime);
        if (failure != null) throw new InvalidOperationException("Sona setup fixture failed", failure);
        return new { Passed = checks.Count, Checks = checks, MissingRuntime = missingRuntime };
    }
}
