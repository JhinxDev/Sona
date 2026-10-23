using System.Diagnostics;
using System.Text.Json;
using Sona.Core;
using Sona.App.Windows;
namespace Sona.App.Audio;

public sealed class Transcriber
{
    private readonly ResidentModel resident = new();
    public string? LoadedModel => resident.LoadedModel;
    internal ModelLoadStage LoadStage => resident.LoadStage;
    public bool ResidentCleanupPending => resident.CleanupPending;
    public int? ResidentProcessId => resident.ProcessId;
    public Task ReconcileAsync(Preferences p, CancellationToken cancellation = default) => AvailableFor(p) ? resident.WarmAsync(p, cancellation) : resident.UnloadAsync(cancellation);
    public Task UnloadAsync(CancellationToken cancellation = default) => resident.UnloadAsync(cancellation);
    public Task ExpireAsync(CancellationToken cancellation = default) => resident.ExpireAsync(cancellation);
    private readonly HashSet<string> retained = new(StringComparer.OrdinalIgnoreCase);
    private readonly object cleanupGate = new();
    public bool CleanupPending { get { lock (cleanupGate) return retained.Count > 0; } }
    public static string ScratchRoot => Path.Combine(AppPaths.DataRoot, "scratch");
    public static string RuntimeRoot => AppPaths.RuntimeOverride ?? (Directory.Exists("E:\\DevelopmentTools\\local-dictation\\whisper") ? "E:\\DevelopmentTools\\local-dictation" : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sona", "runtime"));
    public static bool CpuEngineAvailable => File.Exists(Path.Combine(RuntimeRoot, "whisper", "Release", "whisper-server.exe"));
    public static bool CudaEngineAvailable => new[] { "whisper-server.exe", "ggml-cuda.dll", "cublas64_11.dll", "cublasLt64_11.dll" }.All(name => File.Exists(Path.Combine(RuntimeRoot, "whisper-cuda", "Release", name)));
    internal static bool ParakeetUsesCuda(Preferences p) => p.Engine != "FastCpu" && ParakeetAssets.Present(true);
    internal static bool PreferParakeetCuda(Preferences p) => p.Engine != "FastCpu" && (ParakeetAssets.Present(true) || File.Exists(Path.Combine(Environment.SystemDirectory, "nvcuda.dll")));
    public static ModelStore Models => new(Path.Combine(RuntimeRoot, "models"));
    public static string SelectedModel(Preferences preferences) => preferences.Model != "auto" ? preferences.Model : preferences.Engine != "FastCpu" && CudaEngineAvailable && Models.Present("turbo") ? "turbo" : "small";
    public bool Available => ModelCatalog.All.Any(m => Models.Present(m.Id) && (m.IsParakeet ? ParakeetAssets.Present(true) || ParakeetAssets.Present(false) : ModelCatalog.NeedsCuda(m.Id) ? CudaEngineAvailable : CpuEngineAvailable || CudaEngineAvailable));
    public bool AvailableFor(Preferences preferences)
    {
        var id = SelectedModel(preferences);
        if (ModelCatalog.Get(id).IsParakeet) return Models.Present(id) && ParakeetAssets.Present(ParakeetUsesCuda(preferences));
        return Models.Present(id) && (ModelCatalog.NeedsCuda(id) ? preferences.Engine != "FastCpu" && CudaEngineAvailable : CpuEngineAvailable || preferences.Engine != "FastCpu" && CudaEngineAvailable);
    }
    public Task<string> RunAsync(byte[] wav, Preferences preferences, CancellationToken cancellation)
    {
        if (!AvailableFor(preferences)) { Array.Clear(wav); throw new IOException("The selected local transcription engine is missing. Run setup-runtime.ps1."); }
        return resident.RunAsync(wav, preferences, cancellation);
    }
    private async Task<string> RunOnceAsync(byte[] wav, Preferences preferences, CancellationToken cancellation)
    {
        if (!AvailableFor(preferences)) throw new IOException("The selected local transcription engine is missing. Run setup-runtime.ps1.");
        var modelId = SelectedModel(preferences);
        if (!await Models.VerifyAsync(modelId, cancellation).ConfigureAwait(false)) throw new IOException("Selected model integrity check failed. Your draft was left untouched.");
        var useCuda = preferences.Engine != "FastCpu" && CudaEngineAvailable;
        var work = Path.Combine(ScratchRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(work, "owner.marker"), AppPaths.ScratchMarker, cancellation).ConfigureAwait(false);
            var audio = Path.Combine(work, "audio.wav");
            await File.WriteAllBytesAsync(audio, wav, cancellation).ConfigureAwait(false);
            var info = new ProcessStartInfo(Path.Combine(RuntimeRoot, useCuda ? "whisper-cuda" : "whisper", "Release", "whisper-cli.exe"));
            var args = new[] { "-m", Models.PathFor(modelId), "-f", audio, "-l", preferences.Language, "-t", "6", "-bs", useCuda ? "5" : "1", "-bo", useCuda ? "5" : "1", "-nf", "-sns", "-oj", "-of", Path.Combine(work, "result") };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            if (!useCuda) info.ArgumentList.Add("-ng");
            var prompt = string.Join(", ", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Written)).Distinct().Take(30));
            if (!string.IsNullOrEmpty(prompt)) { info.ArgumentList.Add("--prompt"); info.ArgumentList.Add(prompt); }
            using var worker = new BoundedProcess(info, useCuda ? 3072 : 1536);
            var stdout = worker.Process.StandardOutput.ReadToEndAsync(cancellation);
            var stderr = worker.Process.StandardError.ReadToEndAsync(cancellation);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try { await worker.Process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new IOException("Transcription reached the 90 second limit. Try a shorter recording."); }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            if (worker.Process.ExitCode != 0) throw new IOException(useCuda ? "GPU transcription is unavailable. Choose Lightweight CPU in Dictation Models and try again. Your draft was left untouched." : "Local transcription failed. Your draft was left untouched.");
            if (useCuda && !(await stderr.ConfigureAwait(false)).Contains("using CUDA0 backend", StringComparison.Ordinal)) throw new IOException("The GPU engine did not use your GPU. Choose Lightweight CPU in Dictation Models or repair its local runtime.");
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(work, "result.json"), cancellation).ConfigureAwait(false));
            return string.Join("", result.RootElement.GetProperty("transcription").EnumerateArray().Select(x => x.GetProperty("text").GetString())).Trim();
        }
        finally { Array.Clear(wav); await CleanupOrRetainAsync(work).ConfigureAwait(false); }
    }
    internal async Task CleanupOrRetainAsync(string directory)
    {
        try { await CleanupScratchAsync(directory).ConfigureAwait(false); lock (cleanupGate) retained.Remove(directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { lock (cleanupGate) retained.Add(directory); throw new ScratchCleanupException(ex); }
    }
    public async Task RetryCleanupAsync()
    {
        string[] pending; lock (cleanupGate) pending = retained.ToArray();
        foreach (var directory in pending) await CleanupOrRetainAsync(directory).ConfigureAwait(false);
    }
    internal static async Task CleanupScratchAsync(string directory)
    {
        if (!Directory.Exists(directory)) return;
        var full = Path.GetFullPath(directory);
        if (!Path.GetDirectoryName(full)!.Equals(Path.GetFullPath(ScratchRoot), StringComparison.OrdinalIgnoreCase) || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full), "^[a-f0-9]{32}$")) throw new IOException("Unexpected scratch path was retained.");
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0 || Directory.EnumerateFileSystemEntries(full).Any(x => Directory.Exists(x) || (File.GetAttributes(x) & FileAttributes.ReparsePoint) != 0 || Path.GetFileName(x) is not ("owner.marker" or "audio.wav" or "result.json"))) throw new IOException("Unexpected scratch contents were retained.");
        // Remove audio before its recovery marker. Process exit may precede final file-lock release.
        foreach (var name in new[] { "audio.wav", "result.json", "owner.marker" })
        {
            var path = Path.Combine(full, name);
            for (var attempt = 0; ; attempt++)
            {
                try { File.Delete(path); break; }
                catch (IOException) when (attempt < 20) { await Task.Delay(40).ConfigureAwait(false); }
            }
        }
        Directory.Delete(full, false);
    }
    public static void CleanupAbandoned()
    {
        if (!Directory.Exists(ScratchRoot) || (File.GetAttributes(ScratchRoot) & FileAttributes.ReparsePoint) != 0) return;
        foreach (var directory in Directory.EnumerateDirectories(ScratchRoot).Take(100))
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(directory), "^[a-f0-9]{32}$")) continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                var marker = Path.Combine(directory, "owner.marker");
                if (!File.Exists(marker) || new FileInfo(marker).Length > 100 || File.ReadAllText(marker) is not (AppPaths.ScratchMarker or AppPaths.LegacyScratchMarker)) continue;
                if (Directory.EnumerateFileSystemEntries(directory).Any(x => (File.GetAttributes(x) & FileAttributes.ReparsePoint) != 0 || Directory.Exists(x) || Path.GetFileName(x) is not ("owner.marker" or "audio.wav" or "result.json"))) continue;
                File.Delete(Path.Combine(directory, "audio.wav")); File.Delete(Path.Combine(directory, "result.json")); File.Delete(marker); Directory.Delete(directory, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Retain locked scratch instead of touching another active resource. */ }
        }
    }
}
public sealed class ScratchCleanupException(Exception cause) : IOException("Windows is still holding temporary recording files. Sona will remain open so cleanup can be retried.", cause);
