using System.Text;
using System.Text.Json;
using Sona.Core;
internal static class SpeechProtocolChecks
{
    public static void Run(Action<bool, string> check)
    {
        byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
        bool Rejected(string json) { try { SpeechProtocol.Text(Bytes(json)); return false; } catch(Exception ex) when(ex is IOException or JsonException){ return true; } }
        var parakeet = ModelCatalog.Get("parakeet-v3");
        check(parakeet.IsParakeet && parakeet.Provider == "NVIDIA" && parakeet.License == "CC-BY-4.0" && parakeet.DownloadUrl == "https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3/resolve/541d1f99c6b0c3cd0b11a95167540bb8edefd82b/parakeet-tdt-0.6b-v3.q8_0.gguf", "Parakeet catalog pins real NVIDIA attribution and provider-specific artifact");
        check(ModelCatalog.All.Where(m=>!m.IsParakeet).Select(m=>m.Id).SequenceEqual(new[]{"tiny","base","small","medium","turbo","large-v3"}), "Adding Parakeet preserves all six Whisper choices");
        using(var patch=JsonDocument.Parse("{\"model\":\"parakeet-v3\"}"))
        {
            var prior=new Preferences{Model="turbo",Theme="amethyst",CopyToClipboard=true};var next=AgentSettings.Apply(prior,patch.RootElement);
            check(next.Model=="parakeet-v3"&&prior.Model=="turbo"&&next.Theme=="amethyst"&&next.CopyToClipboard, "Agent Parakeet selection preserves theme and clipboard preferences without mutating the active profile");
            next.Normalize();check(next.Model=="parakeet-v3", "Parakeet selection survives preference normalization");
        }
        check(SpeechProtocol.Text(Bytes("{\"text\":\" Hello. \",\"duration\":1}")) == "Hello.", "Bounded speech reply reads text without depending on provider-specific optional fields");
        check(Rejected("{}")&&Rejected("{\"text\":123}")&&Rejected("[]")&&Rejected("{broken"), "Malformed speech replies cannot become drafts");
        check(Rejected(JsonSerializer.Serialize(new{text=new string('x',24001)})), "Speech text has a finite output bound");
        var oversized=false;try{SpeechProtocol.Text(new byte[SpeechProtocol.MaximumBytes+1]);}catch(IOException){oversized=true;}
        check(oversized,"Oversized speech responses reject before parsing");
        var gpu=Bytes("{\"ready\":true,\"device\":\"cuda:0\",\"capabilities\":[\"asr\"]}");
        var cpu=Bytes("{\"ready\":true,\"device\":\"cpu\",\"capabilities\":[\"asr\"]}");
        check(SpeechProtocol.ParakeetReady(gpu,true)&&!SpeechProtocol.ParakeetReady(cpu,true),"GPU readiness rejects CPU fallback");
        check(SpeechProtocol.ParakeetReady(cpu,false)&&!SpeechProtocol.ParakeetReady(gpu,false),"Explicit CPU readiness rejects an unintended GPU backend");
        check(!SpeechProtocol.ParakeetReady(Bytes("{\"ready\":false,\"device\":\"CUDA0\",\"capabilities\":[\"transcription\"]}"),true)&&!SpeechProtocol.ParakeetReady(Bytes("{\"ready\":true,\"device\":\"CUDA0\",\"capabilities\":[\"speech\"]}"),true),"Speech readiness requires a loaded transcription capability");
        var inventory=Bytes("{\"data\":[{\"id\":\""+SpeechProtocol.ParakeetNativeId+"\",\"capability\":\"transcription\"}]}");
        check(SpeechProtocol.ParakeetIdentity(Bytes("{\"version\":\"0.2.0\"}"),inventory)&&!SpeechProtocol.ParakeetIdentity(Bytes("{\"version\":\"0.1.0\"}"),inventory),"Parakeet identity pins its compatible runtime version");
        check(!SpeechProtocol.ParakeetIdentity(Bytes("{\"version\":\"0.2.0\"}"),Bytes("{\"data\":[{\"id\":\"another-model\",\"capability\":\"transcription\"}]}")),"Parakeet readiness cannot silently serve a different loaded model");
    }
}
