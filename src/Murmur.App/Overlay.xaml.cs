using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Shapes;
namespace Murmur.App;
public partial class Overlay : Window
{
    public event Action? StopRequested;
    public event Action? CancelRequested;
    public Overlay()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000080);
            HwndSource.FromHwnd(hwnd).AddHook(PreventActivation);
        };
    }
    private nint PreventActivation(nint hwnd, int message, nint wparam, nint lparam, ref bool handled) { if (message == 0x21) { handled = true; return 3; } return 0; }
    public void ShowFor(nint target)
    {
        var screen = System.Windows.Forms.Screen.FromHandle(target == 0 ? GetForegroundWindow() : target);
        Show();
        void Position()
        {
            if (!IsVisible) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (!GetWindowRect(hwnd, out var rect)) throw new IOException("The voice bar could not be positioned.");
            var x = screen.WorkingArea.Left + (screen.WorkingArea.Width - (rect.Right - rect.Left)) / 2;
            var y = screen.WorkingArea.Bottom - (rect.Bottom - rect.Top) - 24;
            if (!SetWindowPos(hwnd, (nint)(-1), x, y, 0, 0, 0x0011)) throw new IOException("The voice bar could not be shown safely.");
        }
        Position();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, Position);
    }
    public void Update(string label, string hint, double seconds = 0, double level = 0)
    {
        Label.Text = label;
        Time.Text = label == "Listening" ? TimeSpan.FromSeconds(seconds).ToString(@"m\:ss") : hint;
        StopControl.Visibility = label == "Listening" ? Visibility.Visible : Visibility.Hidden;
        var weights = new[] { .45, .8, 1, .72, .5 }; var amplitude = double.IsFinite(level) ? Math.Clamp(level * 20, 0, 1) : 0;
        for (var i = 0; i < Wave.Children.Count; i++) ((Rectangle)Wave.Children[i]).Height = 6 + 19 * amplitude * weights[i];
    }
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
