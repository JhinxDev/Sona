using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class DiscordClosedStartFixture
{
    public static object Run(string output)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Use a fresh fixture output");Directory.CreateDirectory(output);
        var profile=Path.Combine(output,"profile");Environment.SetEnvironmentVariable("SONA_DATA",profile);
        new Preferences{Theme="amethyst",Model="tiny",ProtectionEnabled=true,AutoInsert=false,CopyToClipboard=false,KeepHistory=false,LiveTranscription=false,SoundEffects=false}.Save(Path.Combine(profile,"preferences.json"));
        var checks=new List<string>();Exception? failure=null;
        void Check(bool ok,string text){if(!ok)throw new IOException(text);checks.Add(text);}
        var thread=new Thread(()=>
        {
            var app=new ThemeResourceTestApplication();app.InitializeComponent();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            app.Exit+=(_,_)=>Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>
            {
                MainWindow? window=null;
                try
                {
                    Check(app.StartupCallbackObserved&&app.MainWindow==null,"Production startup is intercepted");
                    window=new MainWindow(["--test-ui"]);window.OverlayForTests.SuppressNativeDisplayForTests=true;
                    var capture=new FakeCapture();window.RecorderForTests=()=>new Recorder(_=>capture);
                    var protection=new ProtectionService(new ProtectedCredentials(Path.Combine(profile,"unused.dpapi")),"sona-closed-native-",_=>[],()=>false);
                    await protection.ConfigureAsync(new(){ProtectionEnabled=true},CancellationToken.None);window.AttachProtectionForTests(protection);
                    Check(window.PreparationForTests==null,"Actual protection preparation path is exercised");
                    Check(await window.BeginForTestsAsync(),"Recording start succeeds with enabled protection and Discord closed");
                    Check(window.SessionForTests==SessionState.Recording&&capture.Started,"Real MainWindow reaches Recording using only fake capture");
                    Check(((TextBlock)window.OverlayForTests.FindName("CapsuleLabel")).Text=="Recording","Recording overlay receives the active state");
                    window.CancelForTests();for(var i=0;i<100&&!capture.Disposed;i++)await Task.Delay(10);Check(capture.Disposed&&window.SessionForTests==SessionState.Cancelled,"Cancellation releases fake capture cleanly");
                    Check(!protection.Connected&&!protection.OwnsChanges,"No Discord connection or voice change occurs");
                    Check(!File.Exists(Path.Combine(profile,"unused.dpapi")),"Closed path does not read or require Discord credentials");
                }
                catch(Exception ex){failure=ex;}
                finally{if(window!=null)window.RequestQuit();else app.Shutdown();}
            }));Dispatcher.Run();
        }){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(25)))throw new TimeoutException("Closed Discord native fixture timed out");
        var report=new{Checks=checks,Failure=failure?.ToString(),MicrophoneOpened=false,GlobalInput=false,ProductionProfileTouched=false};File.WriteAllText(Path.Combine(output,"proof.json"),System.Text.Json.JsonSerializer.Serialize(report));
        if(failure!=null)throw new IOException("Native closed Discord start failed",failure);return report;
    }
}
