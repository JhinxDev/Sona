using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;

internal static class OverlayVisualFixture
{
    public static object Run(string output)
    {
        output = System.IO.Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh overlay evidence directory.");
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var failures = new List<string>(); var captures = new List<object>();
        Exception? failure = null;
        var foreground = GetForegroundWindow();
        var thread = new Thread(() =>
        {
            ThemeResourceTestApplication? app = null;
            try
            {
                app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    try
                    {
                        Check(app.StartupCallbackObserved && app.MainWindow == null, "Resource fixture intercepts production startup before services, profile and microphone initialization");
                        foreach (var theme in new[] { "amethyst", "paper" })
                        {
                            ThemeManager.Apply(theme);
                            CheckToggles(theme);
                        }
                        ThemeManager.Apply("amethyst");
                        typeof(ThemeManager).GetMethod("ApplyMaterialResources", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [true]);
                        Check(app.Resources["OverlaySurface"] is LinearGradientBrush overlayMaterial && overlayMaterial.GradientStops.All(stop => stop.Color.A == 232), "Controlled native-glass resource branch makes only the overlay slightly translucent");
                        foreach (var display in new[] { "Panel", "Capsule" })
                        {
                            var overlay = new Overlay();
                            try
                            {
                                Check(((TextBlock)overlay.FindName("PanelTarget")).Text == "" && ((TextBlock)overlay.FindName("CapsuleTarget")).Text == "", display + " starts with a quiet identity before any app metadata update");
                                overlay.SetDisplay(display);
                                var untargeted = new RecordingDestinationContext();
                                untargeted.Observe(new((nint)1, 10, "Discord", 0)); untargeted.Begin(null, "No focused text field");
                                Check(untargeted.Destination == null && untargeted.Label == "Discord", display + " can identify the active app outside an editable field without inventing an insertion target");
                                overlay.UpdateTarget("No focused text field"); overlay.UpdateAppIcon(ApplicationIconCache.DefaultIcon); Layout((Border)overlay.FindName("Shell"), overlay.Width);
                                var neutral = (TextBlock)overlay.FindName(display == "Panel" ? "PanelTarget" : "CapsuleTarget");
                                Check(neutral.Text == "" && neutral.Visibility == Visibility.Collapsed && ReferenceEquals(overlay.ActiveAppIcon, ApplicationIconCache.DefaultIcon), display + " unknown or non-editable destination is visually quiet with a neutral icon");
                                Capture((Border)overlay.FindName("Shell"), display.ToLowerInvariant() + "-neutral.png", overlay.Width);
                                overlay.UpdateTarget("Discord");
                                foreach (var process in System.Diagnostics.Process.GetProcessesByName("Discord"))
                                using (process)
                                {
                                    if (process.MainWindowHandle == 0 || process.MainModule?.FileName is not string executable) continue;
                                    using var icons = new ApplicationIconCache(); _ = icons.Get(executable, process.MainWindowHandle, process.Id);
                                    if (!icons.Pending.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Actual Discord icon exceeded five seconds.");
                                    overlay.UpdateAppIcon(icons.Get(executable, process.MainWindowHandle, process.Id)); break;
                                }
                                overlay.Update("Listening", "Release the shortcut to finish", 18, .02);
                                overlay.UpdateTranscript("", "");
                                var shell = (Border)overlay.FindName("Shell");
                                Layout(shell, overlay.Width);
                                Check(overlay.Width <= (display == "Panel" ? 330 : 280), display + " uses the requested smaller footprint");
                                Check(shell.ActualHeight <= (display == "Panel" ? 170 : 84), display + " remains compact without a live transcript");
                                Capture(shell, display.ToLowerInvariant() + "-idle.png", overlay.Width);
                                var oldText = string.Join(' ', Enumerable.Repeat("Earlier phrase that should leave the recording display.", 30));
                                var recent = "Keep this recent phrase. The latest words stay visible.";
                                overlay.UpdateTranscript(oldText + " " + recent, "Live raw · provisional"); Layout(shell, overlay.Width);
                                var live = (TextBlock)overlay.FindName("LiveTranscript");
                                Check(live.Text.EndsWith("The latest words stay visible.", StringComparison.Ordinal) && !live.Text.Contains("Earlier phrase", StringComparison.Ordinal), display + " displays only recent phrases and preserves the latest words");
                                Check(live.ActualHeight <= 36 && live.Text.Length <= 240, display + " live tail is bounded to two readable lines");
                                Check(Descendants(shell).OfType<ScrollViewer>().All(scroll => scroll.Visibility == Visibility.Collapsed), display + " live transcript needs no scroll bar");
                                var review = (Button)overlay.FindName("ReviewDraft");
                                Check(review.ActualHeight >= 26 && review.Visibility == Visibility.Visible, display + " preserves an explicit Open draft action");
                                Capture(shell, display.ToLowerInvariant() + "-live.png", overlay.Width);
                                overlay.Update("Preparing your draft", "Transcribing locally"); Layout(shell, overlay.Width);
                                var busyName = display == "Panel" ? "PanelBusy" : "CapsuleBusy";
                                Check(overlay.FindName(busyName) is ProgressBar { Visibility: Visibility.Visible }, display + " gives processing a truthful busy indicator");
                                Capture(shell, display.ToLowerInvariant() + "-busy.png", overlay.Width);
                                var before = shell.ActualHeight;
                                overlay.UpdateTranscript(oldText + " " + oldText + " " + recent, "Live raw · provisional"); Layout(shell, overlay.Width);
                                Check(Math.Abs(shell.ActualHeight - before) < .5, display + " does not grow as recognition text accumulates");
                            }
                            finally { overlay.Close(); }
                        }
                        Check(GetForegroundWindow() == foreground, "Offscreen WPF layout and captures preserve the user's foreground window");
                    }
                    catch (Exception error) { failure = error; }
                    finally { app.Shutdown(); app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                }));
                Dispatcher.Run();
            }
            catch (Exception error) { failure = error; }
            void Check(bool condition, string label) { if (condition) checks.Add(label); else failures.Add(label); }
            void Layout(FrameworkElement element, double width)
            {
                element.Measure(new Size(width, double.PositiveInfinity));
                element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout();
            }
            void Capture(FrameworkElement element, string name, double width)
            {
                Layout(element, width);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual(); using (var context = visual.RenderOpen()) context.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight)); bitmap.Render(visual); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var stream = File.Create(System.IO.Path.Combine(output, name))) encoder.Save(stream);
                captures.Add(new { Image = name, Width = element.ActualWidth, Height = element.ActualHeight });
            }
            void CheckToggles(string theme)
            {
                var root = new StackPanel { Background = (Brush)app!.Resources["Panel"] };
                foreach (var state in new[] { "off", "on", "disabled-off", "disabled-on" })
                {
                    var toggle = new CheckBox { Style = (Style)app.Resources[typeof(CheckBox)], Content = state, IsChecked = state.EndsWith("on", StringComparison.Ordinal), IsEnabled = !state.StartsWith("disabled", StringComparison.Ordinal), MinHeight = 32 };
                    root.Children.Add(toggle); Layout(root, 310); toggle.ApplyTemplate(); Layout(root, 310);
                    var knob = toggle.Template.FindName("Knob", toggle) as Ellipse;
                    var surface = toggle.Template.FindName("Switch", toggle) as Border;
                    if (knob == null || surface == null) { failures.Add(theme + " " + state + " switch template is missing"); continue; }
                    var position = knob.TranslatePoint(new Point(0, 0), surface);
                    Check(Math.Abs(knob.ActualHeight - knob.ActualWidth) < .01 && knob.Height + knob.Margin.Top + knob.Margin.Bottom <= surface.ActualHeight - surface.BorderThickness.Top - surface.BorderThickness.Bottom + .01 && position.Y >= surface.BorderThickness.Top && position.Y + knob.ActualHeight <= surface.ActualHeight - surface.BorderThickness.Bottom + .01, theme + " " + state + " toggle retains a full unclipped circular thumb at the minimum row size");
                }
                Capture(root, "toggles-" + theme + ".png", 310);
            }
        }) { IsBackground = true, Name = "Sona isolated overlay and toggle layout" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("Overlay visual fixture exceeded twenty seconds.");
        if (failure != null) failures.Add(failure.ToString());
        var report = new { Checks = checks, Failures = failures, Captures = captures, Evidence = "Actual WPF controls rendered offscreen at 96 DPI; no desktop screenshot or microphone/foreground acceptance", MicrophoneOpened = false, GlobalInputSent = false, ProductionServicesStarted = false };
        File.WriteAllText(System.IO.Path.Combine(output, "overlay-proof.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count > 0) throw new IOException("Overlay visual fixture: " + string.Join("; ", failures));
        return report;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
