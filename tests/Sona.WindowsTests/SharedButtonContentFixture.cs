using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Sona.App;

internal static class SharedButtonContentFixture
{
    internal static object Run(string output)
    {
        output = System.IO.Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh shared-button evidence directory.");
        Directory.CreateDirectory(output);
        var checks = new List<string>(); var failures = new List<string>();
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent();
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    ThemeManager.Apply("amethyst");
                    var root = new StackPanel { Background = (Brush)app.Resources["Panel"] };
                    var plain = new Button { Content = "Plain action", FontSize = 12, Margin = new Thickness(8) };
                    var primary = new Button { Content = "Primary action", Style = (Style)app.Resources["PrimaryButton"], Margin = new Thickness(8) };
                    var content = new StackPanel { Orientation = Orientation.Horizontal };
                    var dot = new Ellipse { Width = 7, Height = 7, Fill = (Brush)app.Resources["Accent"], Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
                    var label = new TextBlock { Text = "Whisper", FontSize = 12 };
                    content.Children.Add(dot); content.Children.Add(label);
                    var composite = new Button { Content = content, Margin = new Thickness(8) };
                    root.Children.Add(plain); root.Children.Add(primary); root.Children.Add(composite);
                    root.Measure(new Size(320, double.PositiveInfinity)); root.Arrange(new Rect(0, 0, 320, root.DesiredSize.Height)); root.UpdateLayout();
                    Check(plain.ActualHeight >= 30 && primary.ActualHeight >= 30 && Descendants(plain).OfType<TextBlock>().Any(text => text.Text == "Plain action") && Descendants(primary).OfType<TextBlock>().Any(text => text.Text == "Primary action"), "Plain and primary string buttons still render their actual readable labels");
                    Check(Descendants(composite).Contains(content) && Descendants(composite).Contains(dot) && Descendants(composite).Contains(label) && dot.ActualWidth == 7 && dot.ActualHeight == 7 && label.ActualWidth > 0 && label.ActualHeight > 0, "Button ContentPresenter renders the actual dot and Whisper label instead of a StackPanel type name");
                    Check(Descendants(composite).OfType<TextBlock>().All(text => !text.Text.Contains("System.Windows.Controls", StringComparison.Ordinal)), "Composite button exposes no framework type name as product text");
                    Check(app.StartupCallbackObserved && app.MainWindow == null, "Native presentation fixture starts no production window or services");
                    var bitmap = new RenderTargetBitmap(320, (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(System.IO.Path.Combine(output, "shared-buttons.png")); encoder.Save(stream);
                }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { app.Shutdown(); app.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
            }));
            Dispatcher.Run();
            void Check(bool value, string label) { if (value) checks.Add(label); else failures.Add(label); }
        }) { IsBackground = true, Name = "Sona shared button content fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10))) throw new TimeoutException("Shared button fixture exceeded ten seconds.");
        var report = new { Checks = checks, Failures = failures, MicrophoneOpened = false, ProductionServicesStarted = false, GlobalInputSent = false, Evidence = "Actual offscreen WPF string and UIElement button templates" };
        File.WriteAllText(System.IO.Path.Combine(output, "shared-buttons.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        if (failures.Count > 0) throw new IOException(string.Join("; ", failures));
        return report;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child;
    }
}
