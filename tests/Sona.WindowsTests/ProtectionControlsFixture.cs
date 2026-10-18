using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sona.App;
using Sona.App.Windows;
using Sona.Core;

internal static class ProtectionControlsFixture
{
    public static object Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh isolated fixture directory.");
        var profile = Path.Combine(output, "profile"); Directory.CreateDirectory(profile);
        var path = Path.Combine(profile, "preferences.json");
        new Preferences { Theme="amethyst", SoundEffects=false, ProtectionEnabled=true, SilenceIncoming=true,
            ProtectionReleaseDelayMs=0, WatchedApps=["fixture.exe"], AutoInsert=false, KeepHistory=false, CloseToTray=false }.Save(path);
        var vaultPath = Path.Combine(profile, "fixture-discord.dpapi"); var vault = new ProtectedCredentials(vaultPath);
        vault.Save(new() { ClientId="123456789", ClientSecret="fixture-only", AccessToken="fixture-only", ExpiresAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600 });
        var vaultHash = SHA256.HashData(File.ReadAllBytes(vaultPath));
        var prior = Environment.GetEnvironmentVariable("SONA_DATA"); Environment.SetEnvironmentVariable("SONA_DATA", profile);
        var checks = new List<string>(); Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window=null; ProtectionService? service=null; DiscordFixtureServer? server=null;
                try
                {
                    var active = true; server = new DiscordFixtureServer();
                    service = new(vault, server.Prefix, names => active && names.Contains("fixture.exe") ? ["fixture.exe"] : []);
                    await service.ConfigureAsync(Preferences.Load(path), default); await service.ConnectAsync(false, default);
                    var initialHash = SHA256.HashData(File.ReadAllBytes(path)); var initialCommands = server.Commands.Count;
                    window = new MainWindow(["--test-ui"]); window.Title="Sona protection test (isolated)"; window.ShowActivated=false; window.Show(); window.AttachProtectionForTests(service);
                    T Field<T>(string name) => (T)window.FindName(name);
                    void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); File.WriteAllLines(Path.Combine(output,"progress.txt"),checks); }
                    async Task Wait(Func<bool> condition, string label)
                    {
                        var until=DateTime.UtcNow.AddSeconds(5);
                        while(!condition()) { if(DateTime.UtcNow>=until) throw new TimeoutException(label); await Task.Delay(20); }
                    }
                    bool Busy() => (bool)typeof(MainWindow).GetField("preferencesApplying",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
                    async Task Toggle(string name)
                    {
                        await Wait(() => Field<CheckBox>(name).IsEnabled && !Busy(), "Toggle ready: "+name);
                        ((IToggleProvider)new CheckBoxAutomationPeer(Field<CheckBox>(name)).GetPattern(PatternInterface.Toggle)!).Toggle();
                    }
                    async Task Saved(bool incoming) => await Wait(() => !Busy() && Preferences.Load(path).SilenceIncoming==incoming, "Incoming switch applies and saves without a separate Save action");
                    Field<TabControl>("Pages").SelectedIndex=1;
                    Field<RadioButton>("SettingsDiscordNav").IsChecked=true;
                    Check(SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(initialHash) && server.Commands.Count==initialCommands, "Initializing controls neither saves nor changes fake Discord");
                    Check(server.State.Muted && server.State.Deafened, "Initial fake watched source is muted and deafened");
                    using(var imported=JsonDocument.Parse("{\"focus_deafen\":false}"))
                        typeof(MainWindow).GetMethod("ApplyHushSettings",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[imported.RootElement]);
                    await Wait(()=>!Busy(),"Imported fields remain a review step");
                    Check(server.State.Muted && server.State.Deafened && SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(initialHash) && Field<CheckBox>("SilenceIncoming").IsChecked==false,
                        "Populating imported settings keeps them pending without applying or saving voice policy");
                    typeof(MainWindow).GetMethod("RefreshProtectionFields",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    Field<CheckBox>("SoundEffects").IsChecked=true;
                    Field<TextBox>("WatchedApps").Text="pending-advanced.exe"; Field<TextBox>("ProtectionDelay").Text="pending";
                    await Toggle("SilenceIncoming"); await Saved(false);
                    Check(server.State.Muted && !server.State.Deafened, "Incoming Off immediately removes owned deafen and retains microphone mute");
                    Check(Field<TextBox>("WatchedApps").Text=="pending-advanced.exe" && Field<TextBox>("ProtectionDelay").Text=="pending"
                        && !Preferences.Load(path).SoundEffects && Field<CheckBox>("SoundEffects").IsChecked==true
                        && Preferences.Load(path).WatchedApps.SequenceEqual(new[]{"fixture.exe"}) && Preferences.Load(path).ProtectionReleaseDelayMs==0,
                        "Switch saves preserve unsaved advanced fields and do not commit them");
                    Check(Field<TextBlock>("ProtectionToggleResult").Text.Contains("Microphone only"), "The switch shows the applied mute-only policy nearby");
                    await Toggle("SilenceIncoming"); await Saved(true);
                    Check(server.State.Muted && server.State.Deafened, "Incoming On applies to an already active fake recording without polling");
                    var beforeFailure = File.ReadAllBytes(path);
                    using(var held = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        await Toggle("SilenceIncoming");
                        await Wait(() => !Busy() && Field<TextBlock>("ProtectionToggleResult").Text.Contains("not saved"), "Persistence failure is reported");
                    }
                    Check(File.ReadAllBytes(path).SequenceEqual(beforeFailure) && Field<CheckBox>("SilenceIncoming").IsChecked==true
                        && server.State.Muted && server.State.Deafened, "Failed persistence restores checkbox and active policy while preserving file bytes");
                    server.FailRestore=true; await Toggle("SilenceIncoming");
                    await Wait(() => !Busy() && Field<TextBlock>("ProtectionToggleResult").Text.Contains("not saved"), "Rejected deafen restoration is reported");
                    Check(Field<CheckBox>("SilenceIncoming").IsChecked==true && Preferences.Load(path).SilenceIncoming && service.OwnsChanges,
                        "Unacknowledged restoration cannot be reported as saved or lose ownership");
                    server.FailRestore=false; await Toggle("SilenceIncoming"); await Saved(false);
                    Check(server.State.Muted && !server.State.Deafened, "A later incoming-Off retry succeeds after restoration failure");
                    active=false; await service.EndOwnRecordingAsync(default);
                    server.State=new(true,true,true); active=true; await service.BeginOwnRecordingAsync(default);
                    await Toggle("SilenceIncoming"); await Saved(true); await Toggle("SilenceIncoming"); await Saved(false);
                    active=false; await service.EndOwnRecordingAsync(default);
                    Check(server.State.Muted && server.State.Deafened && !service.OwnsChanges, "Switch changes and completion preserve preexisting manual mute/deafen");
                    server.State=new(true,false,false); await service.PauseAsync(true,default); active=true;
                    var pausedWrites=server.Commands.Count(command=>command=="SET_VOICE_SETTINGS");
                    using(var held = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        await Toggle("ProtectionEnabled");
                        await Wait(()=>!Busy()&&Field<TextBlock>("ProtectionToggleResult").Text.Contains("not saved"),"Paused master-Off persistence failure");
                    }
                    Check(Preferences.Load(path).ProtectionEnabled && service.Paused && !server.State.Muted && !server.State.Deafened
                        && server.Commands.Count(command=>command=="SET_VOICE_SETTINGS")==pausedWrites,
                        "Failed master-Off save restores an explicit pause without transient voice acquisition");
                    var pausedCommands=server.Commands.Count; await Toggle("SilenceIncoming"); await Saved(true);
                    Check(service.Paused && !server.State.Muted && !server.State.Deafened && server.Commands.Count==pausedCommands,
                        "Changing incoming policy preserves an explicit pause without acquiring voice changes");
                    await Toggle("SilenceIncoming"); await Saved(false); active=false; await service.PauseAsync(false,default);
                    await Toggle("ProtectionEnabled"); await Wait(()=>!Busy()&&!Preferences.Load(path).ProtectionEnabled,"Master Off saved");
                    await service.BeginOwnRecordingAsync(default);
                    await Toggle("ProtectionEnabled"); await Wait(()=>!Busy()&&Preferences.Load(path).ProtectionEnabled,"Master On saved");
                    Check(server.State.Muted && !server.State.Deafened, "Master On protects a Sona recording that began while master was Off");
                    await service.EndOwnRecordingAsync(default);
                    Check(!server.State.Muted && !server.State.Deafened, "Ending the mute-only source restores the fake microphone");
                    active=true; await service.BeginOwnRecordingAsync(default);
                    await Toggle("ProtectionEnabled"); await Wait(()=>!Busy()&&!Preferences.Load(path).ProtectionEnabled,"Disable active source");
                    active=false; await service.EndOwnRecordingAsync(default);
                    var noSourceCommands=server.Commands.Count;
                    await Toggle("ProtectionEnabled"); await Wait(()=>!Busy()&&Preferences.Load(path).ProtectionEnabled,"Enable with no source");
                    Check(!server.State.Muted && !server.State.Deafened && server.Commands.Count==noSourceCommands,
                        "Re-enabling refreshes ended sources before unpausing and never creates phantom mute");
                    var processing=typeof(MainWindow).GetField("processing",BindingFlags.Instance|BindingFlags.NonPublic)!;
                    processing.SetValue(window,true);
                    typeof(MainWindow).GetMethod("RefreshSettingsActions",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    Check(!Field<CheckBox>("ProtectionEnabled").IsEnabled && !Field<CheckBox>("SilenceIncoming").IsEnabled,
                        "Both switches are disabled during conflicting dictation/settings operations");
                    processing.SetValue(window,false);
                    void Capture(string name)
                    {
                        window.UpdateLayout();
                        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
                        var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream=File.Create(Path.Combine(output,name)); png.Save(stream);
                    }
                    typeof(MainWindow).GetMethod("RefreshSettingsActions",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                    Capture("protection-normal.png"); window.Width=740; window.Height=530; window.UpdateLayout(); Capture("protection-compact.png");
                    Check(vaultHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(vaultPath))), "All switch tests preserve encrypted fixture setup");
                    Check(Preferences.Load(path).ProtectionEnabled && !Preferences.Load(path).SilenceIncoming, "Final saved preference is master On with incoming silencing Off");
                }
                catch(Exception error) { failure=error; File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString()); }
                finally
                {
                    if(service!=null) { try { await service.RestoreForQuitAsync(default); await service.DisposeAsync(); } catch(Exception error){failure??=error;} }
                    if(window!=null) { typeof(MainWindow).GetField("protection",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,null); window.Close(); }
                    if(server!=null) await server.DisposeAsync(); app.Shutdown();
                }
            }));
            app.Run();
        }) { IsBackground=true };
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); if(!thread.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException("Protection fixture exceeded 90 seconds."); }
        finally { Environment.SetEnvironmentVariable("SONA_DATA",prior); }
        var report=new { Passed=checks.Count, Checks=checks, Failure=failure?.Message, LiveDiscordConnected=false, OwnerCredentialsRead=false, MicrophoneOpened=false, GlobalInputSent=false };
        File.WriteAllText(Path.Combine(output,"report.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        if(failure!=null) throw new Exception("Isolated protection controls failed: "+failure.Message,failure);
        return report;
    }
}
