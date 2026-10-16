using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;

internal static class TargetCaptureDiagnostic
{
    public static object Run(string output, nint expectedWindow = 0)
    {
        output = Path.GetFullPath(output);
        if (File.Exists(output)) throw new IOException("Use a fresh diagnostic receipt path.");
        var observations = new List<object>();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var insertion = new Insertion();
                var elapsed = Stopwatch.StartNew();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        var foreground = GetForegroundWindow();
                        if (expectedWindow == 0 || foreground == expectedWindow)
                        {
                            var result = insertion.CaptureWithStatus();
                            // Retain one coherent capture, never join independently
                            // sampled focused controls or hidden tab descendants.
                            if (GetForegroundWindow() == foreground && (result.ForegroundWindow == 0 || result.ForegroundWindow == foreground.ToInt64()))
                                observations.Add(new { AtMilliseconds = (int)elapsed.ElapsedMilliseconds, Capture = CaptureObservation.From("diagnostic", result) });
                        }
                    }
                    catch (Exception error) { failure = error; timer.Stop(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                    if (elapsed.Elapsed >= TimeSpan.FromSeconds(20)) { timer.Stop(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                };
                timer.Start(); Dispatcher.Run();
            }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true, Name = "Sona passive destination diagnostic" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(35))) throw new TimeoutException("Passive inspection exceeded its bound; no input was requested.");
        if (failure != null) throw new InvalidOperationException("Passive destination inspection failed.", failure);
        var report = new
        {
            SchemaVersion = 3, ExpectedWindow = expectedWindow.ToInt64(),
            AppAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Insertion).Assembly.Location))),
            Observations = observations, FieldContentsRead = false, WindowTitlesRead = false,
            MicrophoneOpened = false, GlobalInputSent = false, ClipboardTouched = false
        };
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
