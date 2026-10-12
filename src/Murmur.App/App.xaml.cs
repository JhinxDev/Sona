using System.Windows;
using System.Windows.Threading;
namespace Murmur.App;
public partial class App : Application
{
    private Mutex? single;
    private bool ownsMutex;
    private EventWaitHandle? open;
    private RegisteredWaitHandle? openWait;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        single = new Mutex(true, "Local\\Murmur-" + Environment.UserName, out var created);
        ownsMutex = created;
        if (!created)
        {
            if (!e.Args.Contains("--start-hidden")) { try { using var signal = EventWaitHandle.OpenExisting("Local\\Murmur-open-" + Environment.UserName); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }
            Shutdown(); return;
        }
        global::Murmur.App.Windows.BoundedProcess.ContainApplication();
        DispatcherUnhandledException += (_, args) => { args.Handled = true; if (MainWindow is MainWindow window) window.ReportError("An unexpected error occurred. Recording was stopped; your draft was not submitted."); };
        Audio.Transcriber.CleanupAbandoned();
        var main = new MainWindow(e.Args);
        MainWindow = main;
        open = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Murmur-open-" + Environment.UserName);
        openWait = ThreadPool.RegisterWaitForSingleObject(open, (_, _) => Dispatcher.BeginInvoke(() => { main.Show(); main.WindowState = WindowState.Normal; main.Activate(); }), null, Timeout.Infinite, false);
        if (e.Args.Contains("--start-hidden")) { main.ShowActivated = false; main.Show(); main.Hide(); }
        else main.Show();
        if (e.Args.Contains("--test-ui"))
        {
            var deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(180) };
            deadline.Tick += (_, _) => { deadline.Stop(); main.RequestQuit(); }; deadline.Start();
        }
    }
    protected override void OnExit(ExitEventArgs e) { openWait?.Unregister(null); open?.Dispose(); if (ownsMutex) single?.ReleaseMutex(); single?.Dispose(); base.OnExit(e); }
}
