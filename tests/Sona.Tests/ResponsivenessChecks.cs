using System.Buffers.Binary;
using System.Text.Json;
using Sona.Core;

internal static class ResponsivenessChecks
{
    public static void Run(Action<bool,string> check)
    {
        var intentType=typeof(Preferences).Assembly.GetType("Sona.Core.PendingSubmission");
        check(intentType!=null,"Submission intent has a bounded thread-safe owner");
        var intent=Activator.CreateInstance(intentType!)!;
        object? Call(string name,params object[] args)=>intentType!.GetMethod(name)!.Invoke(intent,args);
        Call("Begin",true);check((bool)Call("TryRequest")! && (bool)Call("Complete")! && !(bool)Call("Complete")!,"Queued Enter is consumed exactly once at finalization");
        check(!(bool)Call("TryRequest")!,"Late Enter cannot attach to a completed operation");
        Call("Begin",false);check(!(bool)Call("TryRequest")! && !(bool)Call("Complete")!,"Forced-limit completion cannot acquire send intent");
        var gesture=new HoldGesture(); var key=typeof(HoldGesture).GetMethod("Key")!;
        var queued=(HoldChange)key.Invoke(gesture,[13,true,100L,false,false,true,true])!;
        check(queued.Action.ToString()=="QueueSend" && queued.Suppress,"Bare Enter can queue send during pending transcription");
        check(gesture.Key(13,false,101).Suppress,"Queued Enter key-up is drained");
        check(gesture.Key(13,true,102,finishOnEnter:true).Suppress,"Repeated pending Enter never reaches the old composer text");
        var toggleStop=new HoldGesture();toggleStop.Key(0xA2,true,0,toggle:true);var admitted=toggleStop.Key(0x5B,true,1,toggle:true);
        check(admitted.Action==HoldAction.Begin && toggleStop.ConsumeToggle(admitted.Generation),"Toggle setup consumes its real admission token");toggleStop.Key(0x5B,false,202,toggle:true);toggleStop.Key(0xA2,false,203,toggle:true);
        var afterPill=(HoldChange)key.Invoke(toggleStop,[13,true,204L,false,true,true,true])!;
        check(afterPill.Action.ToString()=="QueueSend" && afterPill.Suppress,"Enter after pill Stop queues a result despite retained toggle gesture state");
                var shifted=new HoldGesture();shifted.Key(0xA0,true,0);
        var newline=(HoldChange)key.Invoke(shifted,[13,true,1L,false,false,true,true])!;
        check(!newline.Suppress && newline.Action==HoldAction.None,"Shift+Enter remains a normal newline while text is pending");
                var p = JsonSerializer.Deserialize<Preferences>("{\"CleanupEnabled\":true,\"CleanupMode\":\"Auto\"}")!;
        var calls = 0;
        CleanupOutput Run(string text) => CleanupProcessor.RunAsync(text, p, "Editor", (_,_) => { calls++; return Task.FromResult(text); }, CancellationToken.None).GetAwaiter().GetResult();
        var simple = Run("Please open the draft.");
        check(calls == 0 && simple.Text == "Please open the draft." && !simple.Applied && simple.Outcome == "already-clean", "Auto cleanup bypasses inference for a short clean sentence");
        foreach (var text in new[] { "please open the draft", "Um, please open the draft.", "Please please open the draft.", "Use the blue one, actually use the red one.", "First sentence. Second sentence.", new string('a',181) + "." })
        { var before = calls; Run(text); check(calls == before + 1, "Uncertain Auto text still reaches model: " + text[..Math.Min(36,text.Length)]); }
        check(Run("Bonjour, ouvrez le brouillon.").Outcome == "already-clean", "Auto accepts a short punctuated French sentence");
        p.CleanupTone = "very casual"; var previous=calls; Run("Please open the draft."); check(calls==previous+1, "Explicit lowercase styling still runs cleanup");
        p.CleanupTone="formal";
        var always=AgentSettings.Apply(p,JsonSerializer.SerializeToElement(new {cleanupMode="Always"}));
        check(JsonSerializer.SerializeToElement(AgentSettings.View(always), AgentProtocol.Json).GetProperty("cleanupMode").GetString()=="Always", "Cleanup mode round trips through validated agent settings");
        var invalid=false; try { AgentSettings.Apply(p,JsonSerializer.SerializeToElement(new{cleanupMode="guess"})); } catch(ArgumentException) { invalid=true; }
        check(invalid, "Unknown cleanup modes are rejected");
        var normalized=JsonSerializer.Deserialize<Preferences>("{\"CleanupMode\":null}")!; normalized.Normalize();
        check(JsonSerializer.SerializeToElement(normalized).GetProperty("CleanupMode").GetString()=="Always", "Legacy and invalid modes preserve always-clean behavior");
        p.CleanupEnabled=false; previous=calls; Run("please open the draft"); check(calls==previous,"Disabled cleanup never invokes a model");
        p.CleanupEnabled=true; p.Style="Verbatim"; previous=calls; Run("Um, please."); check(calls==previous,"Verbatim still preserves speech without model cleanup");
        var pcmBytes=32000*300; var wav=new byte[44+pcmBytes];
        "RIFF"u8.CopyTo(wav); "WAVEfmt "u8.CopyTo(wav.AsSpan(8)); "data"u8.CopyTo(wav.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4),wav.Length-8); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16),16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20),1); BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22),1);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24),16000); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28),32000);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32),2); BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34),16); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40),pcmBytes);
        var supported=true; IReadOnlyList<SpeechChunk> parts=[]; try { parts=SpeechAudio.Plan(wav); } catch(IOException) { supported=false; }
        check(supported && parts.Sum(x=>x.Bytes)==pcmBytes && parts.All(x=>x.Bytes<=25*32000), "Five-minute PCM reaches all bounded speech requests without losing the tail");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24),15999); BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28),31998);
        var overLimit=false; try { SpeechAudio.Plan(wav); } catch(IOException) { overLimit=true; }
        check(overLimit,"Audio exceeding five minutes remains bounded"); Array.Clear(wav);
    }
}
