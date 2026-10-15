using System.Buffers.Binary;
using System.Security.Cryptography;
using Sona.App.Audio;

internal static class SoundFeedbackChecks
{
    public static async Task<int> RunAsync()
    {
        var passed=0;void Check(bool value,string label){if(!value)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
        var hashes=new HashSet<string>();
        foreach(var cue in Enum.GetValues<FeedbackCue>())
        {
            var wav=SoundFeedback.Synthesize(cue);var data=wav.AsSpan(44).ToArray();var samples=new short[data.Length/2];for(var i=0;i<samples.Length;i++)samples[i]=BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(i*2));
            Check(System.Text.Encoding.ASCII.GetString(wav,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(wav,8,4)=="WAVE"&&BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40))==data.Length&&BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4))==wav.Length-8,"Valid in-memory PCM WAV container for "+cue);
            Check(BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20))==1&&BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22))==1&&BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24))==24000&&BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34))==16&&samples.Length is >0 and <=6000,"Mono 24kHz 16-bit cue is at most 250ms for "+cue);
            var peak=samples.Max(x=>Math.Abs((int)x));var edge=samples.Take(96).Concat(samples.TakeLast(96)).Average(x=>Math.Abs((int)x));
            Check(peak is >100 and <=1803&&samples[0]==0&&samples[^1]==0&&edge<peak*.15,"Conservative peak and soft attack/release for "+cue);
            hashes.Add(Convert.ToHexString(SHA256.HashData(wav)));
        }
        Check(hashes.Count==4,"All four original cue waveforms are distinct");
        var calls=0;var background=false;
        using(var feedback=new SoundFeedback(_=>{Interlocked.Increment(ref calls);background=Thread.CurrentThread.IsThreadPoolThread;}))
        {
            await feedback.PlayAsync(FeedbackCue.Ready);
            Check(calls==1&&background,"Injected playback is called on a background thread");
            using var canceled=new CancellationTokenSource();canceled.Cancel();await feedback.PlayAsync(FeedbackCue.Start,canceled.Token);
            Check(calls==1,"Already canceled sound is silently suppressed");
            await feedback.PlayAsync((FeedbackCue)99);Check(calls==1,"Unknown cue remains nonfatal and silent");
            feedback.Dispose();await feedback.PlayAsync(FeedbackCue.Open);Check(calls==1,"Disposed feedback suppresses future cues");
        }
        using(var failed=new SoundFeedback(_=>throw new InvalidOperationException("Synthetic playback failure")))
        {await failed.PlayAsync(FeedbackCue.Sent);Check(true,"Playback failure cannot fail the dictation task");}
        using(var release=new ManualResetEventSlim())
        {
            var began=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var order=new List<int>();
            using var feedback=new SoundFeedback(wav=>{lock(order)order.Add(wav.Length);if(order.Count==1){began.TrySetResult();if(!release.Wait(TimeSpan.FromSeconds(4)))throw new TimeoutException("Fixture release");}});
            var open=feedback.PlayAsync(FeedbackCue.Open);await began.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await feedback.PlayAsync(FeedbackCue.Ready);await feedback.PlayAsync(FeedbackCue.Sent);var start=feedback.PlayAsync(FeedbackCue.Start);await Task.Delay(25);
            Check(order.Count==1&&!start.IsCompleted,"Start waits for an active cue while other overlapping cues are skipped");
            release.Set();await Task.WhenAll(open,start).WaitAsync(TimeSpan.FromSeconds(2));
            Check(order.Count==2&&order[1]==SoundFeedback.Synthesize(FeedbackCue.Start).Length,"Awaited Start finishes its own playback after the earlier cue");
        }
        using(var release=new ManualResetEventSlim())
        {
            var began=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var count=0;
            using var feedback=new SoundFeedback(_=>{Interlocked.Increment(ref count);began.TrySetResult();release.Wait(TimeSpan.FromSeconds(4));});
            var open=feedback.PlayAsync(FeedbackCue.Open);await began.Task.WaitAsync(TimeSpan.FromSeconds(2));
            using var cancel=new CancellationTokenSource();var start=feedback.PlayAsync(FeedbackCue.Start,cancel.Token);cancel.Cancel();await start.WaitAsync(TimeSpan.FromSeconds(2));
            Check(count==1,"Canceling a waiting Start never schedules its playback");
            var pending=feedback.PlayAsync(FeedbackCue.Start);feedback.Dispose();await pending.WaitAsync(TimeSpan.FromSeconds(2));release.Set();await open.WaitAsync(TimeSpan.FromSeconds(2));
            Check(count==1,"Disposal suppresses waiting Start and all subsequent playback");
        }
        return passed;
    }
}
