using System.Buffers.Binary;
using System.Media;

namespace Sona.App.Audio;

internal enum FeedbackCue { Open, Start, Ready, Sent }

/// <summary>Small original local cues. Feedback can never fail a dictation.</summary>
internal sealed class SoundFeedback(Action<byte[]>? playback = null) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1,1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly object sync = new();
    private SoundPlayer? activePlayer;
    private bool disposed;

    public async Task PlayAsync(FeedbackCue cue,CancellationToken cancellation=default)
    {
        if(Volatile.Read(ref disposed)||cancellation.IsCancellationRequested||!Enum.IsDefined(cue))return;
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,shutdown.Token);
        var admitted=false;
        try
        {
            // Start is a microphone barrier: it waits for the earlier cue and
            // then completes its own playback. Other overlaps are never queued.
            if(cue==FeedbackCue.Start){await gate.WaitAsync(lifetime.Token).ConfigureAwait(false);admitted=true;}
            else if(!(admitted=gate.Wait(0)))return;
            if(Volatile.Read(ref disposed)||lifetime.IsCancellationRequested)return;
            await Task.Run(()=>
            {
                if(Volatile.Read(ref disposed)||lifetime.IsCancellationRequested)return;
                var wav=Synthesize(cue);
                if(playback!=null)playback(wav);else PlayNative(wav,lifetime.Token);
            }).ConfigureAwait(false);
        }
        catch(Exception)
        {
            // Optional feedback is deliberately isolated from draft/capture
            // errors, including missing or changing Windows audio devices.
        }
        finally{if(admitted)gate.Release();}
    }

    private void PlayNative(byte[] wav,CancellationToken cancellation)
    {
        using var input=new MemoryStream(wav,false);using var player=new SoundPlayer(input){LoadTimeout=500};
        lock(sync){if(disposed||cancellation.IsCancellationRequested)return;activePlayer=player;}
        using var cancel=cancellation.Register(()=>Stop(player));
        try{if(!cancellation.IsCancellationRequested)player.PlaySync();}
        finally{lock(sync){if(ReferenceEquals(activePlayer,player))activePlayer=null;}}
    }
    private void Stop(SoundPlayer? expected=null)
    {
        SoundPlayer? player;
        lock(sync)
        {
            if(activePlayer==null||expected!=null&&!ReferenceEquals(activePlayer,expected))return;
            player=activePlayer;
        }
        // Never hold the ownership lock across a Windows audio call.
        try{player.Stop();}catch(Exception){ }
    }
    public void Dispose()
    {
        lock(sync){if(disposed)return;Volatile.Write(ref disposed,true);}
        shutdown.Cancel();Stop();
        // Gate/token storage remains valid for in-flight completions. Neither
        // object creates a native wait handle in this implementation.
    }

    internal static byte[] Synthesize(FeedbackCue cue)
    {
        const int rate=24000;
        var duration=cue switch{FeedbackCue.Open=>.180,FeedbackCue.Start=>.100,FeedbackCue.Ready=>.220,FeedbackCue.Sent=>.180,_=>throw new ArgumentOutOfRangeException(nameof(cue))};
        var count=(int)Math.Round(duration*rate);var wav=new byte[44+count*2];var header=wav.AsSpan();
        "RIFF"u8.CopyTo(header);BinaryPrimitives.WriteInt32LittleEndian(header[4..],wav.Length-8);"WAVEfmt "u8.CopyTo(header[8..]);BinaryPrimitives.WriteInt32LittleEndian(header[16..],16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..],1);BinaryPrimitives.WriteInt16LittleEndian(header[22..],1);BinaryPrimitives.WriteInt32LittleEndian(header[24..],rate);BinaryPrimitives.WriteInt32LittleEndian(header[28..],rate*2);BinaryPrimitives.WriteInt16LittleEndian(header[32..],2);BinaryPrimitives.WriteInt16LittleEndian(header[34..],16);"data"u8.CopyTo(header[36..]);BinaryPrimitives.WriteInt32LittleEndian(header[40..],count*2);
        static double Smooth(double value){value=Math.Clamp(value,0,1);return value*value*(3-2*value);}
        static double Tone(double time,double start,double length,double frequency)
        {
            var local=time-start;if(local<0||local>=length)return 0;
            var envelope=Smooth(local/.008)*Smooth((length-local)/.035)*Math.Exp(-local*7);
            return envelope*(.88*Math.Sin(2*Math.PI*frequency*local)+.12*Math.Sin(2*Math.PI*frequency*2*local));
        }
        for(var index=1;index<count-1;index++)
        {
            var time=index/(double)rate;
            var signal=cue switch
            {
                FeedbackCue.Open=>.62*Tone(time,0,.180,523.25)+.38*Tone(time,0,.180,783.99),
                FeedbackCue.Start=>Tone(time,0,.100,830.61),
                FeedbackCue.Ready=>.70*Tone(time,0,.135,659.25)+.70*Tone(time,.085,.135,880),
                FeedbackCue.Sent=>.70*Tone(time,0,.115,987.77)+.70*Tone(time,.065,.115,739.99),
                _=>0
            };
            // -26 dBFS peak ceiling, with no limiter distortion in the designed
            // waveforms. System/device volume remains the user's own choice.
            var sample=(short)Math.Round(Math.Clamp(signal,-1,1)*.05*short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(44+index*2),sample);
        }
        return wav;
    }
}
