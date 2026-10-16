using System.Reflection;
using System.Security;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;
using Sona.Core;

internal static class QuitPreparationFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh Quit fixture output directory.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile");
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        new Preferences { Theme = "amethyst", AutoInsert = false, SoundEffects = false }.Save(Path.Combine(profile, "preferences.json"));
        var checks = new List<string>(); Exception? failure = null;
        var modelFault = Task.FromException(new UnauthorizedAccessException("Synthetic denied model file"));
        var cleanupFault = Task.FromException(new SecurityException("Synthetic denied cleanup file"));
        Task? quitOperation = null;
        bool closed = false, draftPreserved = false;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent();
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    Check(app.StartupCallbackObserved && app.MainWindow == null, "Production startup is intercepted");
                    window = new MainWindow(["--test-ui"]) { ShowActivated = false };
                    window.Show();
                    ((TextBox)window.FindName("Draft")).Text = "Synthetic draft remains available while shutdown drains preparation.";
                    window.Closed += (_, _) => { closed = true; draftPreserved = ((TextBox)window.FindName("Draft")).Text == "Synthetic draft remains available while shutdown drains preparation."; };
                    var otherField = new TextBox(); AutomationProperties.SetAutomationId(otherField, "OtherScopedField");
                    var otherWindow = new Window { Title = "Sona isolated scope fixture", Content = otherField, Width = 280, Height = 160, ShowActivated = false };
                    try
                    {
                        otherWindow.Show();
                        var firstHandle = new WindowInteropHelper(window).Handle;
                        var secondHandle = new WindowInteropHelper(otherWindow).Handle;
                        var scope = await Task.Run(() =>
                        {
                            var root = AutomationElement.FromHandle(firstHandle);
                            var field = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "Draft"));
                            return (Own: field != null && Insertion.ElementBelongsToWindow(field, firstHandle), Other: field != null && Insertion.ElementBelongsToWindow(field, secondHandle));
                        }).WaitAsync(TimeSpan.FromSeconds(5));
                        Check(scope.Own, "Actual native UIA ancestry accepts a field in its own window");
                        Check(!scope.Other, "Actual native UIA ancestry rejects another window in the same process");
                    }
                    finally { otherWindow.Close(); }
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(MainWindow).GetField("modelPreparation", flags)!.SetValue(window, modelFault);
                    typeof(MainWindow).GetField("cleanupPreparation", flags)!.SetValue(window, cleanupFault);
                    quitOperation = (Task)typeof(MainWindow).GetMethod("QuitAsync", flags)!.Invoke(window, null)!;
                    await quitOperation.WaitAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception error) { failure = error; File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); }
                finally
                {
                    _ = modelFault.Exception; _ = cleanupFault.Exception;
                    if (window?.IsVisible == true)
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(MainWindow).GetField("modelPreparation", flags)!.SetValue(window, Task.CompletedTask);
                        typeof(MainWindow).GetField("cleanupPreparation", flags)!.SetValue(window, Task.CompletedTask);
                        typeof(MainWindow).GetField("shutdownPending", flags)!.SetValue(window, false);
                        window.RequestQuit();
                    }
                    else app.Shutdown();
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Sona Quit preparation fixture" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(25))) throw new TimeoutException("The isolated Quit fixture did not finish.");
        // Application.Shutdown can stop the UI dispatcher before an awaiting
        // fixture continuation. Verify mandatory completion from this thread.
        if (failure == null)
        {
            try
            {
                Check(quitOperation?.IsCompletedSuccessfully == true && closed, "Quit completes and closes after model and cleanup preparation faults");
                Check(draftPreserved, "The editable synthetic draft remains intact through the actual Close event");
            }
            catch (Exception error) { failure = error; }
        }
        var result = new { Passed = checks.Count, Checks = checks, MicrophoneOpened = false, GlobalInputSent = false, ProductionProfileUsed = false };
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        if (failure != null) throw new InvalidOperationException("Quit preparation fixture failed.", failure);
        return result;
    }
}
