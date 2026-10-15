using System.Diagnostics;
using NAudio.Wave;
using Sona.App.Audio;
using Sona.Core;

internal static class LiveSpeechFixture
{
    public static async Task<object> RunAsync(string file, string output)
    {
        if (File.Exists(output)) throw new IOException("Use a fresh receipt path.");
        var checks = new List<string>(); var timings = new List<double>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks.Add(label); }
        using var reader = new WaveFileReader(file);
        if (reader.WaveFormat.SampleRate != 16000 || reader.WaveFormat.BitsPerSample != 16 || reader.WaveFormat.Channels != 1 || reader.Length < 128000 || reader.Length > 3840000) throw new IOException("Use a licensed 4-120 second mono PCM fixture.");
        var pcm = new byte[reader.Length]; reader.ReadExactly(pcm);
        var capture = new FakeCapture(); using var recorder = new Recorder(_ => capture);
        var transcriber = new Transcriber(); var p = new Preferences { Model = "parakeet-v3", Engine = "Auto", Language = "en" };
        // Resolve the maintained catalog ID rather than inventing a provider alias.
        p.Model = ModelCatalog.All.Single(m => m.IsParakeet).Id;
        var lengths = new List<int>();
        try
        {
            recorder.Start(-1);
            var half = pcm.Length / 4 * 2;
            capture.Emit(pcm[..half]);
            var first = recorder.Snapshot() ?? throw new Exception("Missing first snapshot");
            var timer = Stopwatch.StartNew(); var raw = await transcriber.RunAsync(first, p, CancellationToken.None); timings.Add(timer.Elapsed.TotalMilliseconds);
            Check(!capture.Disposed && raw.Length > 0 && first.All(b => b == 0), "Actual local model returns provisional text before capture stops and clears snapshot bytes"); lengths.Add(raw.Length);
            capture.Emit(pcm[half..]);
            var second = recorder.Snapshot() ?? throw new Exception("Missing second snapshot"); timer.Restart();
            raw = await transcriber.RunAsync(second, p, CancellationToken.None); timings.Add(timer.Elapsed.TotalMilliseconds);
            Check(!capture.Disposed && raw.Length > 0 && second.All(b => b == 0), "A later real audio snapshot returns updated local text while capture stays open"); lengths.Add(raw.Length);
            var final = await recorder.StopAsync(); timer.Restart(); raw = await transcriber.RunAsync(final, p, CancellationToken.None); timings.Add(timer.Elapsed.TotalMilliseconds);
            Check(capture.Disposed && raw.Length > 0 && final.All(b => b == 0), "Final local recognition succeeds after stopping the fake capture"); lengths.Add(raw.Length);
        }
        finally { Array.Clear(pcm); await transcriber.UnloadAsync(); }
        Check(transcriber.ResidentProcessId == null && !transcriber.ResidentCleanupPending, "Task-owned real speech worker exits cleanly");
        var receipt = new { Passed = checks.Count, Checks = checks, Model = p.Model, Milliseconds = timings, TextLengths = lengths, Audio = Path.GetFileName(file), MicrophoneOpened = false, GlobalInputSent = false, ClipboardChanged = false };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, System.Text.Json.JsonSerializer.Serialize(receipt)); return receipt;
    }
}
