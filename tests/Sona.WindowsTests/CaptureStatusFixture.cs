using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;
using Sona.Core;

internal static class CaptureStatusFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh capture fixture path.");
        Directory.CreateDirectory(output);
        var previousData = Environment.GetEnvironmentVariable("SONA_DATA");
        var previousTransfer = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        Environment.SetEnvironmentVariable("SONA_DATA", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
        new Preferences { SoundEffects = false, AutoInsert = false }.Save(Path.Combine(output, "profile", "preferences.json"));
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string label) { if (value) checks.Add(label); else failures.Add(label); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    var captures = 0; DraftTarget? target = new(101, 31001, [5, 6, 7], 4);
                    using var insertion = new Insertion(() => { captures++; return target; }, () => false, _ => throw new Exception("No input expected"));
                    window = new MainWindow(["--test-ui"], insertion);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    async Task<JsonElement> Status()
                    {
                        using var document = JsonDocument.Parse("{}");
                        var reply = await (Task<AgentReply>)typeof(MainWindow).GetMethod("HandleAgentOnUiAsync", flags)!.Invoke(window, [new AgentRequest("status", document.RootElement), CancellationToken.None])!;
                        return JsonSerializer.SerializeToElement(reply.Data, AgentProtocol.Json);
                    }
                    var initial = await Status();
                    Check(initial.TryGetProperty("destinationCapture", out var empty) && empty.ValueKind == JsonValueKind.Null, "Status begins without an invented capture");
                    Check(captures == 0, "Reading status does not inspect or change system focus");
                    var captureMethod = typeof(MainWindow).GetMethod("CaptureDestination", flags);
                    Check(captureMethod != null, "Actual gesture capture has one shared observation boundary");
                    if (captureMethod != null)
                    {
                        var result = (TargetCaptureResult)captureMethod.Invoke(window, ["test-shortcut"])!;
                        Check(result.Target == target && captures == 1, "The gesture records its actual capture once");
                        var ready = await Status(); var observed = ready.GetProperty("destinationCapture");
                        Check(observed.GetProperty("attempt").GetString() == "test-shortcut" && observed.GetProperty("status").GetString() == "Ready", "Agent status reports the actual attempt and result");
                        Check(observed.GetProperty("runtimeIdentityHash").GetString()?.Length == 64, "Status exports only a transient identity hash");
                        target = null; captureMethod.Invoke(window, ["ready-draft"]);
                        var rejected = (await Status()).GetProperty("destinationCapture");
                        Check(rejected.GetProperty("status").GetString() == "NoFocusedControl" && rejected.GetProperty("attempt").GetString() == "ready-draft" && rejected.GetProperty("runtimeIdentityHash").ValueKind == JsonValueKind.Null, "A failed later attempt replaces stale successful metadata");
                        Check(captures == 2, "Repeated status reads never perform extra captures");
                        Check(!rejected.TryGetProperty("text", out _) && !rejected.TryGetProperty("name", out _) && !rejected.TryGetProperty("title", out _) && !rejected.TryGetProperty("runtimeId", out _), "Capture status contains no field contents, labels, titles or raw runtime IDs");
                    }
                    Check(!window.IsVisible && !window.IsLoaded && new[] { "recorder", "hotkeys", "activeApplication", "agentServer" }.All(name => typeof(MainWindow).GetField(name, flags)!.GetValue(window) == null), "The fixture leaves the window unshown without microphone, hotkeys, focus observer or server");
                }
                catch (Exception error) { failures.Add(error.ToString()); }
                finally { window?.RequestQuit(); app.Shutdown(); }
            }));
            app.Run();
        }) { IsBackground = true, Name = "Sona isolated capture status fixture" };
        try
        {
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("Capture status fixture exceeded its bound.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SONA_DATA", previousData);
            Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", previousTransfer);
        }
        var report = new { Version = AppVersion.Number, BuildId = AppVersion.BuildId, AppAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(MainWindow).Assembly.Location))), Checks = checks, Failures = failures, WindowShown = false, MicrophoneOpened = false, GlobalInputSent = false, ProductionProfileUsed = false };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
}
