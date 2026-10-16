using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using Sona.App.Windows;

internal static class TargetCaptureDiagnostic
{
    public static object Run(string output)
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
                string? last = null;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        var result = insertion.CaptureWithStatus();
                        var key = JsonSerializer.Serialize(new { result.Status, result.ControlTypeId, result.ProviderProcessId, result.WindowProcessId, insertion.FocusEpoch });
                        if (key != last)
                        {
                            observations.Add(new { AtMilliseconds = (int)elapsed.ElapsedMilliseconds, Status = result.Status.ToString(), result.ControlTypeId, result.ProviderProcessId, result.WindowProcessId, FocusEpoch = insertion.FocusEpoch, Captured = result.Target != null });
                            last = key;
                        }
                    }
                    catch (Exception error) { failure = error; timer.Stop(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                    if (elapsed.Elapsed >= TimeSpan.FromSeconds(30)) { timer.Stop(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                };
                timer.Start(); Dispatcher.Run();
            }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true, Name = "Sona passive destination diagnostic" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(40))) throw new TimeoutException("The passive diagnostic did not finish; no text input was requested.");
        if (failure != null) throw new InvalidOperationException("Passive destination inspection failed.", failure);
        var result = new
        {
            SchemaVersion = 1,
            AppAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Insertion).Assembly.Location))),
            Observations = observations,
            FieldContentsRead = false, WindowTitlesRead = false,
            MicrophoneOpened = false, GlobalInputSent = false, ClipboardTouched = false
        };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }
}
