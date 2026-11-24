using System.Diagnostics;
using System.Text.Json;
using Sona.Core;
using Sona.App.Windows;
namespace Sona.App.Audio;

public sealed class Transcriber
{
    private readonly ResidentModel resident = new();
    public string? LoadedModel => resident.LoadedModel;
    public double? LastLoadSeconds => resident.LastLoadSeconds;
    public void UpdateModelIdle(int minutes) => resident.UpdateIdleMinutes(minutes);
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
