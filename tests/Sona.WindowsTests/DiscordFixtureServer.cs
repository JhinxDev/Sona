using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using Sona.Core;

internal sealed class DiscordFixtureServer : IAsyncDisposable
{
    private readonly CancellationTokenSource stop=new(TimeSpan.FromSeconds(45));
    private readonly Task serving;
    public string Prefix { get; }="sona-settings-discord-"+Guid.NewGuid().ToString("N")+"-";
    public VoiceState State { get; set; }=new(true,false,false);
    public bool FailRestore { get; set; }
    public int Connections { get; private set; }
    public List<string> Commands { get; }=[];
    public DiscordFixtureServer()=>serving=ServeAsync();
    private async Task ServeAsync()
    {
        while(!stop.IsCancellationRequested)
        {
            using var pipe=new NamedPipeServerStream(Prefix+"0",PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            async Task<(int Opcode,JsonElement Payload)> Read()
            {
                var header=new byte[8];await pipe.ReadExactlyAsync(header,stop.Token);
                var size=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));if(size is <1 or >65536)throw new IOException("Fixture frame bound");
                var bytes=new byte[size];await pipe.ReadExactlyAsync(bytes,stop.Token);using var json=JsonDocument.Parse(bytes);return(BinaryPrimitives.ReadInt32LittleEndian(header),json.RootElement.Clone());
            }
            async Task Write(object payload)
            {
                var bytes=JsonSerializer.SerializeToUtf8Bytes(payload);var header=new byte[8];BinaryPrimitives.WriteInt32LittleEndian(header,1);BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4),bytes.Length);
                await pipe.WriteAsync(header,stop.Token);await pipe.WriteAsync(bytes,stop.Token);await pipe.FlushAsync(stop.Token);
            }
            try
            {
                await pipe.WaitForConnectionAsync(stop.Token);Connections++;
                var handshake=await Read();if(handshake.Opcode!=0)throw new IOException("Fixture handshake");await Write(new{evt="READY",data=new{}});
                while(!stop.IsCancellationRequested)
                {
                    var (_,payload)=await Read();var command=payload.GetProperty("cmd").GetString()!;var nonce=payload.GetProperty("nonce").GetString();Commands.Add(command);
                    object data=new{};
                    if(command=="GET_SELECTED_VOICE_CHANNEL")data=State.InCall?new{id="fixture-channel"}:null!;
                    else if(command=="GET_VOICE_SETTINGS")data=new{mute=State.Muted,deaf=State.Deafened};
                    else if(command=="SET_VOICE_SETTINGS")
                    {
                        var args=payload.GetProperty("args");
                        if(args.TryGetProperty("mute",out var mute)&&(!FailRestore||mute.GetBoolean()))State=State with{Muted=mute.GetBoolean()};
                        if(args.TryGetProperty("deaf",out var deaf)&&(!FailRestore||deaf.GetBoolean()))State=State with{Deafened=deaf.GetBoolean()};
                        data=new{mute=State.Muted,deaf=State.Deafened};
                    }
                    await Write(new{cmd=command,nonce,data});
                }
            }
            catch(Exception error)when(error is IOException or OperationCanceledException or ObjectDisposedException){ }
        }
    }
    public async ValueTask DisposeAsync(){stop.Cancel();await serving;stop.Dispose();}
}
