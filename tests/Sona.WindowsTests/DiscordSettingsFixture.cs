using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Windows;
using Sona.Core;

internal static class DiscordSettingsFixture
{
    public static object Run(string output)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output))throw new IOException("Use a new isolated evidence directory.");Directory.CreateDirectory(output);
        var profile=Path.Combine(output,"profile");var preferencesPath=Path.Combine(profile,"preferences.json");var vaultPath=Path.Combine(profile,"fixture-discord.dpapi");
        new Preferences{Theme="amethyst",ProtectionEnabled=true,DiscordDisconnected=true,AutoInsert=false,KeepHistory=false,CloseToTray=false,WatchedApps=["fixture.exe"]}.Save(preferencesPath);
        var vault=new ProtectedCredentials(vaultPath);vault.Save(new(){ClientId="123456789",ClientSecret="fixture-only-secret",AccessToken="fixture-only-access",ExpiresAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600});var vaultHash=SHA256.HashData(File.ReadAllBytes(vaultPath));
        var prior=Environment.GetEnvironmentVariable("SONA_DATA");Environment.SetEnvironmentVariable("SONA_DATA",profile);
        var checks=new List<string>();Exception? failure=null;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);File.WriteAllLines(Path.Combine(output,"progress.txt"),checks);}
        var thread=new Thread(()=>
        {
            var app=new ThemeResourceTestApplication();app.InitializeComponent();
            app.Exit+=(_,_)=>Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>
            {
                MainWindow? window=null;ProtectionService? service=null;DiscordFixtureServer? server=null;
                try
                {
                    server=new DiscordFixtureServer();service=new(vault,server.Prefix,_=>[]);
                    await service.ConfigureAsync(Preferences.Load(preferencesPath),CancellationToken.None);service.Start();
                    window=new MainWindow(["--test-ui"]);window.ShowActivated=false;window.Show();window.AttachProtectionForTests(service);
                    T Field<T>(string name)=>(T)window.FindName(name);
                    async Task Wait(Func<bool> predicate,string label)
                    {
                        var until=DateTime.UtcNow.AddSeconds(8);while(!predicate()){if(DateTime.UtcNow>until)throw new TimeoutException(label);await Task.Delay(20);}window.UpdateLayout();
                    }
                    void Capture(string name)
                    {
                        window.UpdateLayout();var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name));png.Save(file);
                    }
                    async Task Select(string section)
                    {
                        var nav=Field<RadioButton>("Settings"+section+"Nav");var provider=(ISelectionItemProvider)new RadioButtonAutomationPeer(nav).GetPattern(PatternInterface.SelectionItem)!;provider.Select();
                        await Wait(()=>Field<FrameworkElement>("Settings"+section+"Section").Visibility==Visibility.Visible,"Section "+section);
                    }
                    void Invoke(string name)=>((IInvokeProvider)new ButtonAutomationPeer(Field<Button>(name)).GetPattern(PatternInterface.Invoke)!).Invoke();
                    Field<TabControl>("Pages").SelectedIndex=1;await Task.Delay(50);
                    var sections=new[]{"General","Audio","Appearance","Cleanup","Privacy","Discord","About"};
                    foreach(var section in sections)
                    {
                        await Select(section);
                        Check(sections.Count(item=>Field<FrameworkElement>("Settings"+item+"Section").Visibility==Visibility.Visible)==1,"Only "+section+" settings are shown after semantic navigation");
                        Check(Field<ScrollViewer>("SettingsScroll").HorizontalOffset==0,"No horizontal settings scroll in "+section);
                        Capture("section-"+section.ToLowerInvariant()+".png");
                    }
                    await Select("Discord");await Task.Delay(600);
                    Check(server.Connections==0&&!service.Connected&&Field<TextBlock>("HushStatus").Text=="Not connected","Saved disconnected choice prevents automatic fake connection on launch");
                    Invoke("ConnectDiscordButton");await Wait(()=>service.Connected&&!Preferences.Load(preferencesPath).DiscordDisconnected,"Connect action");await Task.Delay(30);
                    Check(Field<TextBlock>("HushStatus").Text=="Connected"&&Field<Border>("DiscordConnectionBadge").Background is SolidColorBrush{Color:{G:0x65}},"Native connected status has the green connection surface");
                    Check(Field<Button>("ConnectDiscordButton").Visibility==Visibility.Collapsed&&Field<Button>("DisconnectDiscordButton").IsEnabled,"Connected state exposes explicit Disconnect action");Capture("discord-connected.png");
                    Invoke("DisconnectDiscordButton");await Wait(()=>!service.Connected&&Preferences.Load(preferencesPath).DiscordDisconnected,"Disconnect action");
                    var connections=server.Connections;await Task.Delay(600);
                    Check(Field<TextBlock>("DiscordConnectionDetail").Text.Contains("inactive"),"Disconnected status clearly explains that call protection is inactive");
                    await using(var restarted=new ProtectionService(vault,server.Prefix,_=>[]))
                    {
                        await restarted.ConfigureAsync(Preferences.Load(preferencesPath),CancellationToken.None);restarted.Start();await Task.Delay(600);
                        Check(server.Connections==connections&&!restarted.Connected,"Fresh service honors persisted Disconnect without reconnecting");
                    }
                    Check(vaultHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(vaultPath))),"Connect and Disconnect preserve encrypted fixture setup");
                    Check(Preferences.Load(preferencesPath).WatchedApps.SequenceEqual(new[]{"fixture.exe"})&&Preferences.Load(preferencesPath).ProtectionEnabled,"Connection actions preserve watched apps and enabled protection preference");Capture("discord-disconnected.png");
                    window.Width=740;window.Height=530;await Select("Audio");await Task.Delay(30);
                    Check(Field<ScrollViewer>("SettingsScroll").ScrollableWidth==0,"Audio and typing fit the minimum window without horizontal scrolling");Capture("audio-compact.png");
                    await Select("Discord");Capture("discord-compact.png");
                    Check(Field<RadioButton>("SettingsAboutNav").ActualHeight>=36,"Settings section navigation retains usable control height");
                }
                catch(Exception error){failure=error;File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());}
                finally
                {
                    if(service!=null){try{await service.RestoreForQuitAsync(CancellationToken.None);await service.DisposeAsync();}catch(Exception error){failure??=error;}}
                    if(window!=null){typeof(MainWindow).GetField("protection",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,null);window.Close();}
                    if(server!=null)await server.DisposeAsync();app.Shutdown();
                }
            }));app.Run();
        }){IsBackground=true};
        try{thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!thread.Join(TimeSpan.FromSeconds(30)))throw new TimeoutException("Settings fixture exceeded 30 seconds.");if(failure!=null)throw new Exception("Discord/settings native fixture failed",failure);}
        finally{Environment.SetEnvironmentVariable("SONA_DATA",prior);}
        var report=new{Passed=checks.Count,Checks=checks,Output=output,LiveDiscordConnected=false,OwnerCredentialsRead=false,MicrophoneOpened=false,GlobalInputSent=false};File.WriteAllText(Path.Combine(output,"report.json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return report;
    }
}
