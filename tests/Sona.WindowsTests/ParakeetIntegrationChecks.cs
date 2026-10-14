using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sona.App.Audio;
using Sona.Core;
using NAudio.Wave;

internal static class ParakeetIntegrationChecks
{
    private sealed record Case(string Name, string Text, string Language);
    private sealed record Row(string Model, string Case, int Repeat, double AudioSeconds, double Milliseconds,
        int ReferenceWords, int Errors, string Actual, string AudioSha256);
    private sealed record Measurement(string Model, double LoadMilliseconds, double PeakCommitMiB,
        double PeakWorkingSetMiB, int DeviceBeforeMiB, int DeviceLoadedMiB, int DevicePeakMiB, List<Row> Rows);

    public static async Task RunAsync(string corpus, string output, string cudaArchive, string cpuArchive)
    {
        if (File.Exists(output)) throw new IOException("Preserve existing qualification evidence.");
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var token = lifetime.Token;
        var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new IOException(name); checks.Add(name); Console.WriteLine("PASS " + name); }
        await ParakeetAssets.FromVerifiedArchiveAsync(true, cudaArchive, null, token);
        await ParakeetAssets.FromVerifiedArchiveAsync(false, cpuArchive, null, token);
        Check(await ParakeetAssets.VerifyAsync(true, token) && await ParakeetAssets.VerifyAsync(false, token), "Official CPU/CUDA archives and bundled compatibility repair verify through production installation");
        await InstallerChecksAsync(cpuArchive, output, Check, token);

        var cases = JsonSerializer.Deserialize<Case[]>(await File.ReadAllTextAsync(Path.Combine(corpus, "cases.json"), token)) ?? throw new IOException("Missing corpus.");
        if (cases.Length != 16 || cases.Any(c => !Regex.IsMatch(c.Name, "^(en_us|fr_fr)-[0-9]{2}$"))) throw new IOException("Use the frozen sixteen-clip FLEURS corpus.");
        var ordered = cases.OrderBy(c => c.Name.StartsWith("en_us", StringComparison.Ordinal) ? 0 : 1).ThenBy(c => c.Name).ToArray();
        var clock = new SpeechClock();
        var resident = new ResidentModel(clock);
        var preferences = new Preferences { Model = "parakeet-v3", Engine = "Auto", Language = "auto", ModelIdleMinutes = 1, Terms = ["Codex", "Jhinx", "GitHub", "Nextcloud", "PowerShell"] };
        var measurements = new List<Measurement>();
        try
        {
            foreach (var model in new[] { "parakeet-v3", "turbo" })
            {
                var savedMeasurement = output + "." + model + ".json";
                if (File.Exists(savedMeasurement))
                {
                    var saved = JsonSerializer.Deserialize<Measurement>(await File.ReadAllTextAsync(savedMeasurement, token)) ?? throw new IOException("Invalid prior measurement.");
                    if (saved.Model != model || saved.Rows.Count != (model == "turbo" ? 24 : 32) || saved.Rows.Any(r => r.AudioSha256 != Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(corpus, r.Case + ".wav")))))) throw new IOException("Prior measurement corpus differs.");
                    measurements.Add(saved); Check(true, "Reuse unchanged completed " + model + " corpus measurement after a lifecycle harness repair"); continue;
                }
                var prior = resident.ProcessId;
                preferences.Model = model;
                var before = await DeviceMemoryAsync(token);
                var timer = Stopwatch.StartNew(); await resident.WarmAsync(preferences, token); timer.Stop();
                var pid = resident.ProcessId ?? throw new IOException("No resident worker.");
                Check(resident.LoadedModel == model && (prior == null || prior != pid && Exited(prior.Value)), "Switching to " + model + " confirms the previous worker has exited");
                var loaded = await DeviceMemoryAsync(token); var peakDevice = loaded;
                var rows = new List<Row>();
                // The pinned Whisper CUDA server failed a mixed-language sequence
                // with native illegal-memory-access, outside its memory ceiling.
                // Keep its comparison English-only; retain that failed evidence.
                foreach (var sample in model == "turbo" ? ordered.Where(c => c.Name.StartsWith("en_us", StringComparison.Ordinal)) : ordered)
                {
                    var repeats = sample.Name.StartsWith("en_us", StringComparison.Ordinal) ? 3 : 1;
                    for (var repeat = 0; repeat < repeats; repeat++)
                    {
                        var audio = await File.ReadAllBytesAsync(Path.Combine(corpus, sample.Name + ".wav"), token);
                        var hash = Convert.ToHexString(SHA256.HashData(audio));
                        double duration; using (var wav = new WaveFileReader(new MemoryStream(audio, false))) duration = wav.TotalTime.TotalSeconds;
                        var elapsed = Stopwatch.StartNew(); var text = await resident.RunAsync(audio, preferences, token); elapsed.Stop();
                        if (text.Length == 0 || audio.Any(b => b != 0) || resident.ProcessId != pid) throw new IOException("Fixture recognition, audio clearing or resident reuse failed.");
                        var reference = Words(sample.Text); var errors = Errors(reference, Words(text));
                        rows.Add(new(model, sample.Name, repeat, duration, elapsed.Elapsed.TotalMilliseconds, reference.Length, errors, text, hash));
                        Console.WriteLine("Recognized " + model + " " + sample.Name + " repeat " + repeat);
                    }
                    peakDevice = Math.Max(peakDevice, await DeviceMemoryAsync(token));
                }
                using var process = Process.GetProcessById(pid); process.Refresh();
                measurements.Add(new(model, timer.Elapsed.TotalMilliseconds, process.PeakPagedMemorySize64 / 1048576d,
                    process.PeakWorkingSet64 / 1048576d, before, loaded, peakDevice, rows));
                await File.WriteAllTextAsync(output + "." + model + ".json", JsonSerializer.Serialize(measurements[^1], new JsonSerializerOptions { WriteIndented = true }), token);
                Check(rows.Count == (model == "turbo" ? 24 : 32) && resident.ProcessId == pid, model + " reuses one contained worker for all qualified requests and clears every audio input");
            }

            // The installed default decision uses English. French stays a secondary result.
            preferences.Model = "parakeet-v3"; await resident.WarmAsync(preferences, token);
            var idlePid = resident.ProcessId!.Value;
            clock.Advance(59); await resident.ExpireAsync(token); Check(resident.ProcessId == idlePid, "Parakeet stays resident before the configured idle deadline");
            clock.Advance(2); await resident.ExpireAsync(token); Check(resident.ProcessId == null && Exited(idlePid), "Parakeet idle expiry confirms native worker exit");
            preferences.ModelIdleMinutes = 0; await resident.WarmAsync(preferences, token); var keepPid = resident.ProcessId!.Value;
            clock.Advance(3600); await resident.ExpireAsync(token); Check(resident.ProcessId == keepPid, "Keep until quit retains Parakeet in memory");

            var longAudio = LongAudio(corpus, ordered.Where(c => c.Name.StartsWith("en_us", StringComparison.Ordinal)).ToArray());
            double longSeconds; using (var wav = new WaveFileReader(new MemoryStream(longAudio, false))) longSeconds = wav.TotalTime.TotalSeconds;
            var longTimer = Stopwatch.StartNew(); var longText = await resident.RunAsync(longAudio, preferences, token); longTimer.Stop();
            await File.WriteAllTextAsync(output + ".long.json", JsonSerializer.Serialize(new { Seconds = longSeconds, Cleared = longAudio.All(b => b == 0), Text = longText, Milliseconds = longTimer.Elapsed.TotalMilliseconds }), token);
            Check(longSeconds > 60 && longAudio.All(b => b == 0) && longText.Contains("expressed", StringComparison.OrdinalIgnoreCase), "A real English recording sequence over one minute reaches its final sentence and clears audio");

            using var queuedCancellation = new CancellationTokenSource(); queuedCancellation.Cancel();
            var queued = await File.ReadAllBytesAsync(Path.Combine(corpus, ordered[0].Name + ".wav"), token);
            try { await resident.RunAsync(queued, preferences, queuedCancellation.Token); throw new IOException("Queued cancellation was ignored."); } catch (OperationCanceledException) { }
            Check(queued.All(b => b == 0) && resident.ProcessId == keepPid, "Queued cancellation clears audio and retains an idle resident model");

            preferences.Engine = "FastCpu"; await resident.WarmAsync(preferences, token);
            var cpuPid = resident.ProcessId!.Value; Check(cpuPid != keepPid && Exited(keepPid), "Explicit CPU/RAM selection replaces the GPU worker through production readiness checks");
            var cpuAudio = await File.ReadAllBytesAsync(Path.Combine(corpus, ordered[0].Name + ".wav"), token);
            var cpuTimer = Stopwatch.StartNew(); var cpuText = await resident.RunAsync(cpuAudio, preferences, token); cpuTimer.Stop();
            Check(cpuText.Length > 0 && cpuAudio.All(b => b == 0) && resident.ProcessId == cpuPid, "Parakeet CPU fallback transcribes and clears a licensed recording");
            var inFlightAudio = LongAudio(corpus, ordered.Where(c => c.Name.StartsWith("en_us", StringComparison.Ordinal)).ToArray());
            using var inFlightCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));
            try { await resident.RunAsync(inFlightAudio, preferences, inFlightCancellation.Token); throw new IOException("In-flight cancellation was ignored."); } catch (OperationCanceledException) { }
            Check(inFlightAudio.All(b => b == 0) && resident.ProcessId == null && resident.LoadedModel == null && !resident.CleanupPending && Exited(cpuPid), "In-flight CPU cancellation clears audio and confirms contained native worker exit");
            await resident.WarmAsync(preferences, token); var lastPid = resident.ProcessId!.Value;
            await resident.UnloadAsync(token); Check(resident.ProcessId == null && Exited(lastPid), "Explicit unload confirms Parakeet worker exit");

            var summary = measurements.Select(m => new {
                m.Model, m.LoadMilliseconds, m.PeakCommitMiB, m.PeakWorkingSetMiB,
                DeviceLoadedDeltaMiB = m.Model == "turbo" ? (int?)null : m.DeviceLoadedMiB - m.DeviceBeforeMiB,
                DevicePeakDeltaMiB = m.Model == "turbo" ? (int?)null : m.DevicePeakMiB - m.DeviceBeforeMiB,
                Languages = m.Rows.GroupBy(r => r.Case.StartsWith("en_us", StringComparison.Ordinal) ? "English" : "French").Select(g => {
                    var originals = g.Where(r => r.Repeat == 0).ToArray(); var times = g.Select(r => r.Milliseconds).Order().ToArray();
                    return new { Language = g.Key, DistinctClips = originals.Length, TimedRequests = times.Length,
                        ReferenceWords = originals.Sum(r => r.ReferenceWords), Errors = originals.Sum(r => r.Errors),
                        WordErrorRate = 100d * originals.Sum(r => r.Errors) / originals.Sum(r => r.ReferenceWords),
                        MedianWarmMilliseconds = (times[(times.Length - 1) / 2] + times[times.Length / 2]) / 2 };
                }).ToArray()
            }).ToArray();
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new {
                Passed = checks.Count, Checks = checks, Summary = summary, Measurements = measurements,
                LongEnglish = new { Seconds = longSeconds, Milliseconds = longTimer.Elapsed.TotalMilliseconds, CompletedFinalSentence = true },
                CpuEnglishMilliseconds = cpuTimer.Elapsed.TotalMilliseconds,
                Corpus = "google/fleurs 70bb2e84b976b7e960aa89f1c648e09c59f894dd, CC-BY-4.0, eight distinct original test clips per language in archive order",
                Scoring = "Strict lowercase Unicode word edit distance; punctuation ignored; numeric spelling differences count. WER uses one pass per distinct clip. English timing uses three requests per clip.",
                ComparisonLimitation = "Whisper b5454 CUDA failed a mixed English/French resident sequence with a native illegal-memory-access. Disabling CUDA graphs did not resolve it. The completed Whisper comparison is English-only; historical cold French scores are separate evidence. Parakeet runs both languages.",
                DeviceMemory = "Device-wide before/load/after-clip samples; other workloads are included; not per-process VRAM attribution.",
                MicrophoneOpened = false, OwnerTextUsed = false, GlobalInputSent = false,
                SourceAssemblyVersion = typeof(Transcriber).Assembly.GetName().Version?.ToString()
            }, new JsonSerializerOptions { WriteIndented = true }), token);
            Console.WriteLine(JsonSerializer.Serialize(new { Output = output, Passed = checks.Count, Summary = summary }));
        }
        finally { await resident.UnloadAsync(); }
    }

    public static async Task MemoryAsync(string output)
    {
        if (File.Exists(output)) throw new IOException("Preserve prior memory qualification.");
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = lifetime.Token; var resident = new ResidentModel(); var rows = new List<object>();
        var preferences = new Preferences { Engine = "Auto", ModelIdleMinutes = 10 };
        try
        {
            foreach (var id in new[] { "parakeet-v3", "turbo" })
            {
                var baseline = await DeviceMemoryAsync(token); preferences.Model = id;
                await resident.WarmAsync(preferences, token); var pid = resident.ProcessId!.Value;
                var loaded = await DeviceMemoryAsync(token);
                using var process = Process.GetProcessById(pid); process.Refresh();
                rows.Add(new { Model = id, DeviceBaselineMiB = baseline, DeviceLoadedMiB = loaded,
                    DeviceLoadedDeltaMiB = loaded - baseline, HostPeakCommitMiB = process.PeakPagedMemorySize64 / 1048576d });
                await resident.UnloadAsync(token); if (!Exited(pid)) throw new IOException("GPU worker exit was not confirmed.");
            }
            preferences.Model = "turbo"; await resident.WarmAsync(preferences, token); var whisper = resident.ProcessId!.Value;
            preferences.Model = "parakeet-v3"; await resident.WarmAsync(preferences, token); var parakeet = resident.ProcessId!.Value;
            if (whisper == parakeet || !Exited(whisper) || resident.LoadedModel != "parakeet-v3") throw new IOException("Cross-family replacement did not confirm worker release.");
            await resident.UnloadAsync(token); if (!Exited(parakeet)) throw new IOException("Final worker did not exit.");
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Rows = rows, CrossFamilySwitchConfirmed = true,
                Measurement = "Device-wide paired samples with the previous test worker fully unloaded before each baseline; other workloads remain included. Not per-process VRAM attribution.",
                MicrophoneOpened = false, WorkersExited = true }, new JsonSerializerOptions { WriteIndented = true }), token);
            Console.WriteLine("PASS paired GPU loading measurements and actual cross-family worker replacement");
        }
        finally { await resident.UnloadAsync(); }
    }

    private static async Task InstallerChecksAsync(string archive, string output, Action<bool, string> check, CancellationToken token)
    {
        var prior = Environment.GetEnvironmentVariable("SONA_RUNTIME");
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "sona-runtime-fixture-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("SONA_RUNTIME", root);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await ParakeetAssets.FromVerifiedArchiveAsync(false, archive, null, cancelled.Token); throw new IOException("Cancelled install ran."); } catch (OperationCanceledException) { }
            check(!Directory.Exists(ParakeetAssets.RuntimePath(false)), "Cancelled runtime setup leaves no installed target");
            await ParakeetAssets.FromVerifiedArchiveAsync(false, archive, null, token);
            check(await ParakeetAssets.VerifyAsync(false, token), "CPU runtime staged installation verifies every member and the required compatibility DLL");
            var notice = Path.Combine(ParakeetAssets.RuntimePath(false), "licenses", "NOTICE");
            await File.AppendAllTextAsync(notice, "\nSona fixture mismatch\n", token); var modified = await File.ReadAllBytesAsync(notice, token);
            check(!await ParakeetAssets.VerifyAsync(false, token), "Runtime member tampering fails integrity verification");
            try { await ParakeetAssets.FromVerifiedArchiveAsync(false, archive, null, token); throw new InvalidOperationException("Existing altered runtime was overwritten."); } catch (IOException) { }
            check(modified.SequenceEqual(await File.ReadAllBytesAsync(notice, token)), "Retry preserves an existing altered runtime without overwriting it");
            var partial = Path.Combine(ParakeetAssets.Root, ".download-" + Guid.NewGuid().ToString("N"));
            var cleanup = typeof(ParakeetAssets).GetMethod("DeleteOwnedPartialAsync", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            using (var held = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                try { await (Task)cleanup.Invoke(null, [partial])!; throw new InvalidOperationException("Locked partial cleanup passed."); } catch (IOException) { }
                check(ParakeetAssets.CleanupPending && File.Exists(partial), "Locked partial cleanup retains its exact owned path for retry");
            }
            await ParakeetAssets.RetryCleanupAsync(); check(!ParakeetAssets.CleanupPending && !File.Exists(partial), "Retry releases a previously locked partial after the handle closes");
            check(!Directory.EnumerateFileSystemEntries(ParakeetAssets.Root).Any(p => Path.GetFileName(p).StartsWith(".", StringComparison.Ordinal)), "Completed and cancelled setup leave no download or staging partials");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SONA_RUNTIME", prior);
            var parent = Path.GetDirectoryName(Path.GetFullPath(output))!;
            if (!Path.GetDirectoryName(Path.GetFullPath(root))!.Equals(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("sona-runtime-fixture-", StringComparison.Ordinal)) throw new IOException("Fixture cleanup scope differs.");
            if (Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories).Any(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)) Directory.Delete(root, true);
        }
    }
    private static bool Exited(int pid) { try { using var p = Process.GetProcessById(pid); return p.HasExited; } catch (ArgumentException) { return true; } }
    private static string[] Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"\w+").Select(m => m.Value).ToArray();
    private static int Errors(string[] reference, string[] actual)
    {
        var row = Enumerable.Range(0, actual.Length + 1).ToArray();
        for (var i = 1; i <= reference.Length; i++) { var next = new int[actual.Length + 1]; next[0] = i;
            for (var j = 1; j <= actual.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, row[j] + 1), row[j - 1] + (reference[i - 1] == actual[j - 1] ? 0 : 1)); row = next; }
        return row[^1];
    }
    private static byte[] LongAudio(string corpus, Case[] english)
    {
        using var output = new MemoryStream();
        using (var writer = new WaveFileWriter(output, new WaveFormat(16000, 16, 1)))
        {
            double firstSeconds; using (var first = new WaveFileReader(Path.Combine(corpus, english[0].Name + ".wav"))) firstSeconds = first.TotalTime.TotalSeconds;
            var names = Enumerable.Repeat(english[0].Name, (int)Math.Ceiling(70 / firstSeconds)).Append(english[^1].Name); var buffer = new byte[32768];
            try { foreach (var name in names) { using var reader = new WaveFileReader(Path.Combine(corpus, name + ".wav")); int read; while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) writer.Write(buffer, 0, read); } }
            finally { Array.Clear(buffer); }
        }
        var result = output.ToArray(); if (output.TryGetBuffer(out var retained)) Array.Clear(retained.Array!); return result;
    }
    private static async Task<int> DeviceMemoryAsync(CancellationToken token)
    {
        var info = new ProcessStartInfo("nvidia-smi.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--query-gpu=memory.used", "--format=csv,noheader,nounits", "-i", "0" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new IOException("Device memory monitor failed.");
        try { var text = process.StandardOutput.ReadToEndAsync(token); var errors = process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(3), token); await errors;
            if (process.ExitCode != 0 || !int.TryParse((await text).Trim(), out var used)) throw new IOException("Device memory monitor returned invalid data."); return used; }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
    }
    private sealed class SpeechClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Advance(int seconds) => timestamp += seconds * TimeSpan.TicksPerSecond;
    }
}
