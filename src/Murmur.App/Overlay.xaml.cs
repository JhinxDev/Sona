using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Murmur.App;
public partial class Overlay : Window
{
    public Overlay()
    {
        InitializeComponent();
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Bottom - Height - 26;
        SourceInitialized += (_, _) => { var hwnd = new WindowInteropHelper(this).Handle; SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000080); };
    }
    public void Update(string label, string hint, double seconds = 0) { Label.Text = label; Hint.Text = hint; Time.Text = TimeSpan.FromSeconds(seconds).ToString(@"m\:ss"); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
