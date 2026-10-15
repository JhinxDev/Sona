using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sona.App;

public partial class Overlay : Window
{
    private static readonly DependencyPropertyKey ActiveAppIconPropertyKey = DependencyProperty.RegisterReadOnly(nameof(ActiveAppIcon), typeof(ImageSource), typeof(Overlay), new PropertyMetadata(Windows.ApplicationIconCache.DefaultIcon));
    public static readonly DependencyProperty ActiveAppIconProperty = ActiveAppIconPropertyKey.DependencyProperty;
    public ImageSource ActiveAppIcon => (ImageSource)GetValue(ActiveAppIconProperty);
    private ImageSource appIcon = Windows.ApplicationIconCache.DefaultIcon;
    private bool quietIdentity = true;
    public void UpdateAppIcon(ImageSource icon) { appIcon = icon; SetValue(ActiveAppIconPropertyKey, quietIdentity ? Windows.ApplicationIconCache.DefaultIcon : appIcon); }
    private const int SampleCount = 28;
    private readonly double[] samples = new double[SampleCount];
    private readonly double[] displayed = new double[SampleCount];
    private readonly Rectangle[] panelBars = new Rectangle[SampleCount];
    private readonly Rectangle[] capsuleBars = new Rectangle[SampleCount];
    private readonly DispatcherTimer animation;
    private readonly Stopwatch frameClock = new();
    private System.Windows.Forms.Screen? targetScreen;
    private HwndSource? source;
    private bool listening;
    private bool hasSamples;
    private string display = "Panel";
    private string transcriptText = "";

    public event Action? StopRequested;
    public event Action? CancelRequested;

    public Overlay()
    {
        InitializeComponent();
        for (var i = 0; i < SampleCount; i++)
        {
            panelBars[i] = MakeBar(4, 2);
            capsuleBars[i] = MakeBar(2, 1);
            PanelWave.Children.Add(panelBars[i]);
            CapsuleWave.Children.Add(capsuleBars[i]);
        }
        animation = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        animation.Tick += Animate;
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000080);
            source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(PreventActivation);
        };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { frameClock.Restart(); animation.Start(); }
            else { animation.Stop(); frameClock.Reset(); ResetWaveform(); }
        };
        SizeChanged += (_, _) => { RefreshTranscriptTail(); Position(); };
        Closed += (_, _) => { animation.Stop(); animation.Tick -= Animate; source?.RemoveHook(PreventActivation); };
    }

    private static Rectangle MakeBar(double width, double margin)
    {
        var bar = new Rectangle { Width = width, Height = 3, RadiusX = width / 2, RadiusY = width / 2, Margin = new Thickness(margin, 0, margin, 0), VerticalAlignment = VerticalAlignment.Center };
        bar.SetResourceReference(Shape.FillProperty, "Accent");
        return bar;
    }

    public void SetDisplay(string id)
    {
        display = string.Equals(id, "Capsule", StringComparison.OrdinalIgnoreCase) ? "Capsule" : "Panel";
        var panel = display == "Panel";
        PanelDisplay.Visibility = panel ? Visibility.Visible : Visibility.Collapsed;
        CapsuleDisplay.Visibility = panel ? Visibility.Collapsed : Visibility.Visible;
        ReviewDraft.Visibility = panel ? Visibility.Visible : Visibility.Collapsed;
        TranscriptStage.Visibility = panel && !string.IsNullOrEmpty(TranscriptStage.Text) ? Visibility.Visible : Visibility.Collapsed;
        LiveTranscript.Margin = new Thickness(0, panel ? 5 : 0, 0, 0);
        Width = panel ? 320 : 272;
        MinHeight = panel ? 144 : 66;
        Shell.Padding = new Thickness(panel ? 10 : 8);
        Shell.CornerRadius = new CornerRadius(panel ? 18 : 22);
        RefreshTranscriptTail();
        if (IsVisible) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Position);
    }

    public void UpdateTarget(string name)
    {
        var label = name.StartsWith("Active app: ", StringComparison.Ordinal) ? name["Active app: ".Length..] : name;
        quietIdentity = string.IsNullOrWhiteSpace(label) || label is "No focused text field" or "No active app" or "Unknown app" or "Captured app";
        PanelTarget.Text = quietIdentity ? "" : label;
        CapsuleTarget.Text = quietIdentity ? "" : label.StartsWith("Draft stays in Sona · ", StringComparison.Ordinal) ? "Draft stays in Sona" : label;
        PanelTarget.Visibility = CapsuleTarget.Visibility = quietIdentity ? Visibility.Collapsed : Visibility.Visible;
        PanelTarget.ToolTip = CapsuleTarget.ToolTip = PanelAppImage.ToolTip = CapsuleAppImage.ToolTip = name;
        System.Windows.Automation.AutomationProperties.SetName(PanelAppImage, name);
        System.Windows.Automation.AutomationProperties.SetName(CapsuleAppImage, name);
        SetValue(ActiveAppIconPropertyKey, quietIdentity ? Windows.ApplicationIconCache.DefaultIcon : appIcon);
    }

    public void UpdateModel(string name)
    {
        ModelName.Text = string.IsNullOrWhiteSpace(name) ? "On-device dictation" : name;
        ModelName.ToolTip = ModelName.Text;
    }

    private nint PreventActivation(nint hwnd, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x21) { handled = true; return 3; }
        return 0;
    }

    public void ShowFor(nint target)
    {
        targetScreen = System.Windows.Forms.Screen.FromHandle(target == 0 ? GetForegroundWindow() : target);
        Show();
        Position();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Position);
    }

    private void Position()
    {
        if (!IsVisible || targetScreen is null) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (!GetWindowRect(hwnd, out var rect)) throw new IOException("The recording display could not be positioned.");
        var area = targetScreen.WorkingArea;
        var x = area.Left + Math.Max(0, (area.Width - (rect.Right - rect.Left)) / 2);
        var y = Math.Max(area.Top, area.Bottom - (rect.Bottom - rect.Top) - 24);
        if (!SetWindowPos(hwnd, (nint)(-1), x, y, 0, 0, 0x0011)) throw new IOException("The recording display could not be shown safely.");
    }

    public void Update(string label, string hint, double seconds = 0, double level = 0)
    {
        var wasListening = listening;
        listening = label == "Listening";
        PanelLabel.Text = CapsuleLabel.Text = label;
        var busy = label is "Preparing your draft" or "Transcribing" or "Polishing" or "Preparing";
        PanelBusy.Visibility = CapsuleBusy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        PanelBusy.IsIndeterminate = CapsuleBusy.IsIndeterminate = busy && SystemParameters.ClientAreaAnimation;
        PanelBusy.Value = CapsuleBusy.Value = 0;
        var busyBackground = busy && !SystemParameters.ClientAreaAnimation ? "Accent" : "Inset";
        PanelBusy.SetResourceReference(System.Windows.Controls.ProgressBar.BackgroundProperty, busyBackground);
        CapsuleBusy.SetResourceReference(System.Windows.Controls.ProgressBar.BackgroundProperty, busyBackground);
        PanelWave.Visibility = CapsuleWave.Visibility = busy ? Visibility.Hidden : Visibility.Visible;
        var elapsed = TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Clamp(seconds, 0, 120) : 0).ToString(@"m\:ss");
        PanelTime.Text = listening ? elapsed : "";
        CapsuleTime.Text = listening ? elapsed : "";
        PanelHint.Text = hint;
        CapsuleHint.Text = hint;
        CapsuleHint.Visibility = listening || string.IsNullOrWhiteSpace(hint) ? Visibility.Collapsed : Visibility.Visible;
        Shell.ToolTip = string.IsNullOrWhiteSpace(hint) ? label : $"{label}. {hint}";
        System.Windows.Automation.AutomationProperties.SetName(Shell, listening ? $"Sona listening, {elapsed}. {hint}" : $"Sona {label}. {hint}");
        PanelStop.Visibility = CapsuleStop.Visibility = listening ? Visibility.Visible : Visibility.Hidden;
        if (listening)
        {
            if (!wasListening) ResetWaveform();
            // Each point is one actual recorder RMS sample. No idle or preview signal is synthesized.
            Array.Copy(samples, 1, samples, 0, SampleCount - 1);
            samples[^1] = double.IsFinite(level) ? Math.Clamp((level - .002) * 20, 0, 1) : 0;
            hasSamples = true;
        }
        else ResetWaveform();
    }

    private void Animate(object? sender, EventArgs e)
    {
        var dt = Math.Clamp(frameClock.Elapsed.TotalSeconds, .001, .1);
        frameClock.Restart();
        if (!listening || !hasSamples) return;
        for (var i = 0; i < SampleCount; i++)
        {
            var target = samples[i];
            var duration = target > displayed[i] ? .065 : .17;
            displayed[i] += (target - displayed[i]) * (1 - Math.Exp(-dt / duration));
            if (displayed[i] < .001) displayed[i] = 0;
            panelBars[i].Height = 3 + displayed[i] * 29;
            capsuleBars[i].Height = 3 + displayed[i] * 19;
        }
    }

    private void ResetWaveform()
    {
        Array.Clear(samples);
        Array.Clear(displayed);
        hasSamples = false;
        for (var i = 0; i < SampleCount; i++) panelBars[i].Height = capsuleBars[i].Height = 3;
    }

    public event Action? ReviewRequested;
    public void UpdateTranscript(string text, string stage)
    {
        transcriptText = text; TranscriptStage.Text = stage; TranscriptStage.ToolTip = stage;
        TranscriptStage.Visibility = display == "Panel" && !string.IsNullOrEmpty(stage) ? Visibility.Visible : Visibility.Collapsed;
        LiveTranscript.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        RefreshTranscriptTail();
        TranscriptArea.Visibility = string.IsNullOrEmpty(stage) ? Visibility.Collapsed : Visibility.Visible;
    }
    private void RefreshTranscriptTail()
    {
        var width = Math.Max(1, Width - Shell.Padding.Left - Shell.Padding.Right - Shell.Margin.Left - Shell.Margin.Right - 4);
        var typeface = new Typeface(LiveTranscript.FontFamily, LiveTranscript.FontStyle, LiveTranscript.FontWeight, LiveTranscript.FontStretch);
        LiveTranscript.Text = RecentTranscriptTail.Fit(transcriptText, width, typeface, LiveTranscript.FontSize, LiveTranscript.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
    public void SetReviewAction(bool visible) => TranscriptArea.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    private void Review_Click(object sender, RoutedEventArgs e) => ReviewRequested?.Invoke();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
