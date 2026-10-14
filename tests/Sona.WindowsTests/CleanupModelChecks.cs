using System.Diagnostics;
using System.Text;
using Sona.App.Audio;
using Sona.Core;

internal static class CleanupModelChecks
{
    public static void Pure(Action<bool, string> check)
    {
        bool Rejected(string json) { try { CleanupModel.ParseResponse(Encoding.UTF8.GetBytes(json)); return false; } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException) { return true; } }
        check(CleanupModel.ParseResponse(Encoding.UTF8.GetBytes("{\"content\":\" Clean text. \",\"stopped_limit\":false,\"truncated\":false}")) == "Clean text.", "Cleanup response accepts finite completed text");
        check(CleanupModel.ParseResponse(Encoding.UTF8.GetBytes("{\"content\":\" Clean text. \",\"stop_type\":\"eos\",\"stop\":true,\"truncated\":false}")) == "Clean text.", "Modern llama.cpp EOS response proves a complete cleanup result");
        foreach (var proof in new[] { "\"stop_type\":\"limit\",\"stop\":true,\"truncated\":false", "\"stop_type\":\"word\",\"stop\":true,\"truncated\":false", "\"stop_type\":\"eos\",\"stop\":false,\"truncated\":false", "\"stop_type\":\"eos\",\"stop\":true", "\"stop_type\":\"eos\",\"truncated\":true", "\"stop_type\":\"eos\",\"stop\":true,\"truncated\":false,\"stopped_limit\":true" })
            check(Rejected("{\"content\":\"unsafe\"," + proof + "}"), "Modern cleanup rejects token limits, unsupported stops, missing proof and contradictory completion flags");
        check(Rejected("{\"content\":\"cut\",\"stopped_limit\":true}") && Rejected("{\"content\":\"cut\",\"stopped_limit\":false,\"truncated\":true}"), "Token-limited output and truncated input cannot become a cleaned draft");
        check(Rejected("{\"content\":\"unknown\"}") && Rejected("{\"content\":\"\",\"stopped_limit\":false}") && Rejected("{\"content\":24,\"stopped_limit\":false}"), "Missing completion proof and empty or malformed content fail closed");
        check(Rejected(System.Text.Json.JsonSerializer.Serialize(new { content = new string('x', 6001), stopped_limit = false })), "Cleanup text remains within 6000 characters");
        check(CleanupModel.CudaProof("load_tensors: offloaded 29/29 layers to GPU\nload_tensors: CUDA0 model buffer size = 611 MiB") == (true, true), "CUDA proof requires model layers and a CUDA model buffer");
        check(CleanupModel.CudaProof("found CUDA device\noffloaded 0/29 layers to GPU") == (false, false), "Installed CUDA or zero offload cannot advertise GPU use");
        check(CleanupModel.ParseModelVramMiB("load_tensors: CUDA0 model buffer size = 608.25 MiB") == 608.25, "GPU model VRAM uses the invariant CUDA0 model-buffer measurement");
        check(CleanupModel.ParseModelVramMiB("CPU_Mapped model buffer size = 608.25 MiB") == null && CleanupModel.ParseModelVramMiB("CUDA0 model buffer size = 0 MiB") == null && CleanupModel.ParseModelVramMiB("CUDA0 model buffer size = -1 MiB") == null, "CPU buffers and invalid measurements never become model VRAM");
        var root = Path.Combine(Path.GetTempPath(), "sona-cleanup-path-test");
        foreach (var path in new[] { "../escape", "C:\\escape", "file:stream", "\\\\server\\share" })
        {
            var rejected = false; try { CleanupAssets.ContainedPath(root, path); } catch (IOException) { rejected = true; }
            check(rejected, "Cleanup runtime paths stay inside their owned staging directory");
        }
    }

    // Called only by root's explicit fixture gate with verified local assets.
    // No fixtures, networking or worker launches occur merely by loading tests.
    public static async Task ResidentAsync(Preferences configured, string prompt, Action<bool, string> check, CancellationToken token)
    {
        var clock = new CleanupClock(); var model = new CleanupModel(clock);
        try
        {
            await model.WarmAsync(configured, token);
            var first = model.ProcessId; check(first.HasValue && !model.CleanupPending, "Cleanup warm owns one confirmed resident worker");
            check(model.LoadedBackend == "GPU" ? model.ModelVramMiB > 0 : model.ModelVramMiB == null, "Cleanup exposes model VRAM only for its confirmed GPU backend");
            await model.WarmAsync(configured, token); check(first == model.ProcessId, "Unchanged cleanup warm reuses its worker");
            var text = await model.RunAsync(prompt, configured, token); check(text.Length is > 0 and <= 6000 && first == model.ProcessId, "Cleanup requests reuse the bounded resident worker");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel(); var rejected = false;
                try { await model.RunAsync(prompt, configured, cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
                check(rejected && first == model.ProcessId, "Cancelled gate admission leaves the existing worker untouched");
            }
            using (var inflight = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                var pending = model.RunAsync(prompt, configured, inflight.Token);
                inflight.Cancel(); var rejected = false;
                try { await pending; } catch (OperationCanceledException) { rejected = true; }
                check(rejected && model.ProcessId == null && !model.CleanupPending && Exited(first!.Value), "In-flight cleanup cancellation confirms its worker exits");
            }
            var retained = Copy(configured); retained.CleanupIdleMinutes = 0;
            await model.WarmAsync(retained, token); first = model.ProcessId;
            clock.Advance(TimeSpan.FromDays(1)); await model.ExpireAsync(token);
            check(first == model.ProcessId, "Zero cleanup idle timeout retains the model until explicit release");
            var expiring = Copy(configured); expiring.CleanupIdleMinutes = 1;
            await model.ReconcileAsync(expiring, token);
            clock.Advance(TimeSpan.FromMinutes(2)); await model.ExpireAsync(token);
            check(model.ProcessId == null && model.LoadedBackend == null && model.ModelVramMiB == null && !model.CleanupPending && Exited(first!.Value), "Cleanup idle expiry confirms worker exit and releases ownership");
            await model.WarmAsync(configured, token); var second = model.ProcessId;
            var disabled = Copy(configured); disabled.CleanupEnabled = false;
            await model.ReconcileAsync(disabled, token);
            check(model.ProcessId == null && Exited(second!.Value), "Disabling cleanup unloads its owned worker");
            if (configured.CleanupEngine == "Auto" && CleanupAssets.Available("Cpu"))
            {
                await model.WarmAsync(configured, token); var gpu = model.ProcessId;
                var cpu = Copy(configured); cpu.CleanupEngine = "Cpu";
                await model.ReconcileAsync(cpu, token);
                check(model.LoadedBackend == "CPU" && model.ProcessId != gpu && Exited(gpu!.Value), "CPU selection confirms prior worker exit before replacement");
            }
        }
        finally { await model.UnloadAsync(); }
    }
    private static bool Exited(int id)
    {
        try { using var process = Process.GetProcessById(id); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }
    private static Preferences Copy(Preferences p) => new() { CleanupEnabled = p.CleanupEnabled, CleanupEngine = p.CleanupEngine, CleanupIdleMinutes = p.CleanupIdleMinutes, CleanupTone = p.CleanupTone };
    private sealed class CleanupClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
