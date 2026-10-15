using System.Windows;
using System.Windows.Threading;
namespace Sona.App;
public partial class App : Application
{
    private Mutex? single;
    private bool ownsMutex;
    private EventWaitHandle? open;
    private RegisteredWaitHandle? openWait;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var test = e.Args.Contains("--test-ui");
        if (test || e.Args.Contains("--software-rendering"))
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var testData = Core.AppPaths.DataOverride;
        if (test && string.IsNullOrWhiteSpace(testData)) { Shutdown(2); return; }
        var testScope = test ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(testData!))))[..16] : "";
        var mutexName = test ? "Local\\Sona-ui-" + testScope : Core.AppPaths.ProductionMutexName;
        var openName = test ? mutexName + "-open" : Core.AppPaths.ProductionOpenName;
        single = new Mutex(true, mutexName, out var created);
        ownsMutex = created;
        if (!created)
        {
            if (!e.Args.Contains("--start-hidden")) { try { using var signal = EventWaitHandle.OpenExisting(openName); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
            Shutdown(); return;
        }
        global::Sona.App.Windows.BoundedProcess.ContainApplication();
        DispatcherUnhandledException += (_, args) => { args.Handled = true; if (MainWindow is MainWindow window) window.ReportError("An unexpected error occurred. Recording was stopped; your draft was not submitted."); };
        Audio.Transcriber.CleanupAbandoned();
        var main = new MainWindow(e.Args);
        MainWindow = main;
        open = new EventWaitHandle(false, EventResetMode.AutoReset, openName);
        openWait = ThreadPool.RegisterWaitForSingleObject(open, (_, _) => Dispatcher.BeginInvoke(() => { main.Show(); main.WindowState = WindowState.Normal; main.Activate(); }), null, Timeout.Infinite, false);
        if (e.Args.Contains("--start-hidden") && main.PreferencesAvailable) { main.ShowActivated = false; main.Show(); main.Hide(); }
        else main.Show();
        if (e.Args.Contains("--test-ui"))
        {
            var deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(180) };
            deadline.Tick += (_, _) => { deadline.Stop(); main.RequestQuit(); }; deadline.Start();
        }
    }
    protected override void OnExit(ExitEventArgs e) { openWait?.Unregister(null); open?.Dispose(); if (ownsMutex) single?.ReleaseMutex(); single?.Dispose(); base.OnExit(e); }
}
