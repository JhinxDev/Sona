using System.Security.Cryptography;
using Sona.App.Windows;
using Sona.Core;

internal static class DiscordClosedChecks
{
    public static async Task<int> RunAsync()
    {
        var running=false;var count=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
        var root=Path.Combine(Path.GetTempPath(),"sona-discord-closed-"+Guid.NewGuid().ToString("N"));
        var file=Path.Combine(root,"fixture.dpapi");var vault=new ProtectedCredentials(file);
        vault.Save(new(){ClientId="123456789",ClientSecret="fixture",AccessToken="fixture",ExpiresAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+3600});
        var hash=SHA256.HashData(File.ReadAllBytes(file));using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            await using(var server=new DiscordFixtureServer())
            await using(var service=new ProtectionService(vault,server.Prefix,_=>[],()=>running))
            {
                await service.ConfigureAsync(new(){ProtectionEnabled=true,ProtectionReleaseDelayMs=0},timeout.Token);
                await service.BeginOwnRecordingAsync(timeout.Token);
                Check(!service.Connected&&!service.OwnsChanges&&server.Connections==0,"Closed Discord admits dictation without RPC or voice changes");
                Check(service.Status.Contains("dictation available"),"Closed state explains dictation remains available");
                await service.EndOwnRecordingAsync(timeout.Token);
                Check(!service.Connected&&!service.OwnsChanges,"Stopping while Discord is closed needs no transport");
                service.Start();await Task.Delay(650,timeout.Token);
                Check(server.Connections==0,"Closed Discord poll avoids reconnect failures and retry backoff");
                await service.BeginOwnRecordingAsync(timeout.Token);running=true;
                for(var i=0;i<100&&(!service.Connected||!server.State.Muted);i++)await Task.Delay(20,timeout.Token);
                Check(service.Connected&&server.State.Muted&&service.OwnsChanges,"Opening Discord during dictation reconnects and protects the retained source");
                await service.EndOwnRecordingAsync(timeout.Token);
                Check(!server.State.Muted&&!service.OwnsChanges,"Stopping after reconnect restores the acquired mute");
            }
            await using(var service=new ProtectionService(vault,"sona-unavailable-"+Guid.NewGuid().ToString("N")+"-",_=>[],()=>true))
            {
                await service.ConfigureAsync(new(){ProtectionEnabled=true},timeout.Token);var blocked=false;
                try{await service.BeginOwnRecordingAsync(timeout.Token);}catch(IOException){blocked=true;}
                Check(blocked,"Running Discord with unavailable RPC still blocks protected recording");
            }
            await using(var service=new ProtectionService(vault,"sona-unknown-",_=>[],()=>throw new IOException("Unknown process state")))
            {
                await service.ConfigureAsync(new(){ProtectionEnabled=true},timeout.Token);var blocked=false;
                try{await service.BeginOwnRecordingAsync(timeout.Token);}catch(IOException){blocked=true;}
                Check(blocked,"Unknown process state never silently bypasses protection");
            }
            var ownedServer=new DiscordFixtureServer();
            await using(var service=new ProtectionService(vault,ownedServer.Prefix,_=>[],()=>false))
            {
                try
                {
                    await service.ConnectAsync(false,timeout.Token);await service.ConfigureAsync(new(){ProtectionEnabled=true,ProtectionReleaseDelayMs=0},timeout.Token);
                    await service.BeginOwnRecordingAsync(timeout.Token);Check(service.OwnsChanges,"Pending restoration scenario owns an acknowledged fake mute");
                }
                finally{await ownedServer.DisposeAsync();}
                for(var i=0;i<100&&service.Connected;i++)await Task.Delay(10,timeout.Token);
                var blocked=false;try{await service.BeginOwnRecordingAsync(timeout.Token);}catch(IOException){blocked=true;}
                Check(blocked&&service.OwnsChanges,"Closed Discord cannot bypass an outstanding owned restoration");
            }
            Check(hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(file))),"Checks preserve saved encrypted setup");
        }
        finally{File.Delete(file);Directory.Delete(root);}
        return count;
    }

}
