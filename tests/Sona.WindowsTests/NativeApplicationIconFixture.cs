using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sona.App.Windows;

internal static class NativeApplicationIconFixture
{
    // Actual installed-app metadata, deliberately separate from colored fixture icons.
    public static async Task<object> RunAsync(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh native icon evidence directory.");
        Directory.CreateDirectory(output);
        var foreground = GetForegroundWindow();
        var applications = new List<object>();
        foreach (var name in new[] { "Discord", "ChatGPT", "msedge" })
        foreach (var process in Process.GetProcessesByName(name))
        using (process)
        {
            var window = process.MainWindowHandle;
            if (window == 0) continue;
            var path = process.MainModule?.FileName;
            if (path == null) continue;
            using var cache = new ApplicationIconCache();
            _ = cache.Get(path, window, process.Id);
            await cache.Pending.WaitAsync(TimeSpan.FromSeconds(5));
            var icon = cache.Get(path, window, process.Id);
            if (icon is not BitmapSource bitmap || !bitmap.IsFrozen) throw new IOException(name + " did not resolve to an actual frozen app bitmap.");
            var pixels = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[pixels.PixelWidth * pixels.PixelHeight * 4]; pixels.CopyPixels(bytes, pixels.PixelWidth * 4, 0);
            var visible = Enumerable.Range(0, bytes.Length / 4).Count(index => bytes[index * 4 + 3] > 0);
            if (visible == 0) throw new IOException(name + " resolved to an empty bitmap.");
            var image = Path.Combine(output, name + "-" + process.Id + ".png");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(image)) encoder.Save(stream);
            applications.Add(new { Process = name, Name = ActiveApplicationObserver.FriendlyName(name, path), process.Id, Window = window.ToInt64().ToString("X"), Executable = path, bitmap.PixelWidth, bitmap.PixelHeight, VisiblePixels = visible, Image = image });
        }
        if (GetForegroundWindow() != foreground) throw new IOException("Foreground changed while reading app icon metadata.");
        var report = new { CapturedUtc = DateTimeOffset.UtcNow, Evidence = "Actual running application HWND/process/executable icon metadata; no fixture identities", Applications = applications, TestedSonaModuleBuild = typeof(ApplicationIconCache).Assembly.ManifestModule.ModuleVersionId, ReadWindowText = false, GlobalInputSent = false, MicrophoneOpened = false, ForegroundChanged = false, BrowserWebsiteIdentityInferred = false };
        File.WriteAllText(Path.Combine(output, "native-icon-proof.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
