using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;

internal static class RetainedNoticeFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh retained-notice evidence directory.");
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string label) { if (value) checks.Add(label); else failures.Add(label); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    var notice = typeof(Overlay).GetMethod("UpdateRetainedNotice");
                    Check(notice != null, "Retained drafts have a dedicated compact notice mode");
                    if (notice == null) return;
                    foreach (var theme in new[] { "amethyst", "paper", "mist" })
                    foreach (var display in new[] { "Panel", "Capsule" })
                    {
                        ThemeManager.Apply(theme);
                        var overlay = new Overlay();
                        try
                        {
                            overlay.SetDisplay(display); overlay.Update("Listening", "Release to finish", 9, .03);
                            var recordingSurface = ((Border)overlay.FindName("Shell")).Background;
                            overlay.UpdateTranscript("Provisional words", "Live raw"); overlay.SetQuickCleanup("Auto", true);
                            var cancelled = 0; var reviewed = 0;
                            overlay.CancelRequested += () => cancelled++; overlay.ReviewRequested += () => reviewed++;
                            notice.Invoke(overlay, ["Draft copied", "Ctrl+V to paste."]);
                            overlay.SetReviewAction(true); overlay.SetDisplay(display);
                            var shell = (Border)overlay.FindName("Shell");
                            Layout(shell, overlay.Width);
                            Check(overlay.Width == 228 && shell.ActualHeight <= 100 && overlay.MinHeight <= 100, theme + display + " retains a compact notice footprint");
                            Check(((Grid)overlay.FindName("NoticeDisplay")).Visibility == Visibility.Visible && ((Grid)overlay.FindName("PanelDisplay")).Visibility == Visibility.Collapsed && ((Grid)overlay.FindName("CapsuleDisplay")).Visibility == Visibility.Collapsed && ((Border)overlay.FindName("TranscriptArea")).Visibility == Visibility.Collapsed, theme + display + " hides recording decorations and provisional transcript");
                            var title = (TextBlock)overlay.FindName("NoticeTitle"); var hint = (TextBlock)overlay.FindName("NoticeHint");
                            Check(title.Text == "Draft copied" && hint.Text == "Ctrl+V to paste." && title.TextAlignment == TextAlignment.Center && hint.TextAlignment == TextAlignment.Center, theme + display + " shows centered concise copy");
                            Check(ReferenceEquals(shell.Background, recordingSurface), theme + display + " preserves the recording material and opacity");
                            Check(((FrameworkElement)overlay.FindName("NoticeIcon")).Visibility == Visibility.Collapsed, theme + display + " has no placeholder app icon");
                            var titlePosition = title.TranslatePoint(new Point(), shell);
                            Check(Math.Abs(titlePosition.X + title.ActualWidth / 2 - shell.ActualWidth / 2) <= 1, theme + display + " title is centered in the shell");
                            Check(!overlay.IsVisible && !overlay.IsLoaded, theme + display + " never shows or activates native UI");
                            ((Button)overlay.FindName("NoticeReview")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Check(reviewed == 1 && cancelled == 0, theme + display + " review is an explicit action");
                            foreach (var dpi in new[] { 96, 144, 192 })
                            {
                                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(shell.ActualWidth * dpi / 96), (int)Math.Ceiling(shell.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
                                bitmap.Render(shell); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                using var stream = File.Create(Path.Combine(output, $"{theme}-{display}-{dpi}.png")); encoder.Save(stream);
                            }
                            ((Button)overlay.FindName("NoticeDismiss")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Check(cancelled == 0, theme + display + " dismissal never cancels the ready draft");
                            notice.Invoke(overlay, ["Draft pasted", "Check the chat before sending."]);
                            Check(((TextBlock)overlay.FindName("CapsuleLabel")).Text == "Draft pasted", theme + display + " preserves delivery label compatibility");
                            Layout(shell, overlay.Width);
                            Check(shell.ActualHeight <= 100 && hint.ActualHeight <= 32 && hint.ActualWidth <= shell.ActualWidth - 48, theme + display + " longer retained guidance fits without a large notice");
                            var pastedBitmap = new RenderTargetBitmap((int)Math.Ceiling(shell.ActualWidth), (int)Math.Ceiling(shell.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                            pastedBitmap.Render(shell); var pastedEncoder = new PngBitmapEncoder(); pastedEncoder.Frames.Add(BitmapFrame.Create(pastedBitmap));
                            using (var stream = File.Create(Path.Combine(output, $"{theme}-{display}-pasted.png"))) pastedEncoder.Save(stream);
                            overlay.Update("Preparing", "Getting ready"); Layout(shell, overlay.Width);
                            Check(((Grid)overlay.FindName("NoticeDisplay")).Visibility == Visibility.Collapsed && ((Grid)overlay.FindName(display + "Display")).Visibility == Visibility.Visible, theme + display + " Preparing restores the selected recording layout");
                            Check(overlay.Width == (display == "Panel" ? 320 : 228) && overlay.MinHeight == (display == "Panel" ? 144 : 66), theme + display + " restores original recording dimensions");
                            notice.Invoke(overlay, ["Draft ready", "Open to review or copy."]);
                            overlay.Update("Listening", "Release to finish", 1, .01);
                            Check(((Grid)overlay.FindName("NoticeDisplay")).Visibility == Visibility.Collapsed && ((System.Windows.Shapes.Ellipse)overlay.FindName(display + "RecordingDot")).Visibility == Visibility.Visible, theme + display + " Listening resets notice state");
                        }
                        finally { overlay.Close(); }
                    }
                    var policy = typeof(Overlay).Assembly.GetType("Sona.App.RetainedDraftNotice");
                    var select = policy?.GetMethod("Select", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    Check(select != null, "Retained notice wording has a delivery-fact policy");
                    if (select != null)
                    {
                        Verify(false, true, false, false, false, "Draft copied", "Ctrl+V to paste.");
                        Verify(false, true, true, false, false, "Check the chat", "Check before pasting again.");
                        Verify(false, false, true, false, false, "Check the chat", "Check before pasting again.");
                        Verify(false, true, true, true, false, "Draft pasted", "Check the chat before sending.");
                        Verify(false, false, false, false, false, "Draft ready", "Open to review or copy.");
                        Verify(true, false, false, false, false, "Draft ready", "Recording limit reached.");
                        void Verify(bool limit, bool recovery, bool attempted, bool confirmed, bool sent, string title, string hint)
                        {
                            var result = select.Invoke(null, [limit, recovery, attempted, confirmed, sent])!;
                            Check((string)result.GetType().GetProperty("Title")!.GetValue(result)! == title && (string)result.GetType().GetProperty("Hint")!.GetValue(result)! == hint, "Delivery facts select " + title + ": " + hint);
                        }
                    }
                }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { app.Shutdown(); }
            }));
            app.Run();
        }) { IsBackground = true, Name = "Sona offscreen retained draft notice" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Retained notice fixture exceeded its bound.");
        var report = new { Checks = checks, Failures = failures, WindowShown = false, MicrophoneOpened = false, GlobalInputSent = false, Evidence = "Offscreen WPF controls rendered at 96, 144 and 192 DPI. Physical display and owner visual acceptance remain open." };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
    private static void Layout(FrameworkElement element, double width)
    {
        element.Measure(new Size(width, double.PositiveInfinity));
        element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout();
    }
}
