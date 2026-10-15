using System.Reflection;
using System.Security.Cryptography;
using Sona.App.Windows;
using Sona.Core;

internal static class DiscordDisconnectChecks
{
    public static async Task<int> RunAsync()
    {
        var disconnect=typeof(ProtectionService).GetMethod("DisconnectAsync",BindingFlags.Instance|BindingFlags.Public);
        if(disconnect==null)throw new Exception("Explicit Discord disconnect must exist before the connection is torn down.");
        var checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
        var root=Path.Combine(Path.GetTempPath(),"sona-disconnect-"+Guid.NewGuid().ToString("N"));var file=Path.Combine(root,"fixture.dpapi");
        var vault=new ProtectedCredentials(file);vault.Save(new(){ClientId="123456789",ClientSecret="fixture-secret",AccessToken="fixture-token",ExpiresAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600});var before=SHA256.HashData(File.ReadAllBytes(file));
        async Task Disconnect(ProtectionService service,CancellationToken token)=>await(Task)disconnect.Invoke(service,[token])!;
        try
        {
            await using var server=new DiscordFixtureServer();var sources=new HashSet<string>{"fixture.exe"};
            await using var service=new ProtectionService(vault,server.Prefix,_=>new(sources));using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await service.ConnectAsync(false,timeout.Token);await service.ConfigureAsync(new(){ProtectionEnabled=true,SilenceIncoming=true,WatchedApps=["fixture.exe"],ProtectionReleaseDelayMs=0},timeout.Token);await service.BeginOwnRecordingAsync(timeout.Token);await service.EndOwnRecordingAsync(timeout.Token);service.Start();
            Check(server.State.Muted&&server.State.Deafened&&service.OwnsChanges,"Fake watched app owns acknowledged mute and deafen");
            await Disconnect(service,timeout.Token);
            Check(!service.Connected&&!service.OwnsChanges&&!server.State.Muted&&!server.State.Deafened,"Disconnect restores owned changes before closing the fake connection");
            var count=server.Connections;await Task.Delay(1100,timeout.Token);Check(server.Connections==count&&!service.Connected,"Explicit disconnect suppresses watcher-driven automatic reconnect");
            await service.BeginOwnRecordingAsync(timeout.Token);
            Check(server.Connections==count&&!service.Connected&&!server.State.Muted&&!server.State.Deafened,"Intentional disconnect allows dictation without reconnecting or muting Discord");
            Check(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(file))),"Disconnect preserves protected setup bytes");
            await service.ConnectAsync(false,timeout.Token);Check(service.Connected&&service.OwnsChanges&&server.State.Muted&&server.State.Deafened,"Explicit reconnect resumes the retained watched-app configuration");
            server.FailRestore=true;var rejected=false;try{await Disconnect(service,timeout.Token);}catch(IOException){rejected=true;}catch(TargetInvocationException error)when(error.InnerException is IOException){rejected=true;}
            Check(rejected&&service.Connected&&service.OwnsChanges,"Failed restoration retains the connection and ownership for retry");
            var blocked=false;try{await service.BeginOwnRecordingAsync(timeout.Token);}catch(IOException){blocked=true;}
            Check(blocked&&service.OwnsChanges,"Pending owned restoration cannot silently bypass protection for a new recording");
            server.FailRestore=false;await Disconnect(service,timeout.Token);Check(!service.Connected&&!service.OwnsChanges,"Retry finishes restoration and disconnect");
            sources.Clear();server.State=new(true,true,false);await service.ConnectAsync(false,timeout.Token);await service.BeginOwnRecordingAsync(timeout.Token);await Disconnect(service,timeout.Token);
            Check(server.State.Muted&&!server.State.Deafened,"Disconnect preserves the user's preexisting manual mute");
        }
        finally{if(File.Exists(file))File.Delete(file);if(Directory.Exists(root))Directory.Delete(root);}
        return checks;
    }
}
