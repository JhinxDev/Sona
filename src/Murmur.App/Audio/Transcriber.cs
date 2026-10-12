using System.Diagnostics;
using System.Text.Json;
using Murmur.Core;
using Murmur.App.Windows;
namespace Murmur.App.Audio;

public sealed class Transcriber
{
    private readonly HashSet<string> retained = new(StringComparer.OrdinalIgnoreCase);
    private readonly object cleanupGate = new();
    public bool CleanupPending { get { lock (cleanupGate) return retained.Count > 0; } }
    public static string ScratchRoot => Path.Combine(Environment.GetEnvironmentVariable("MURMUR_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Murmur"), "scratch");
    public static string RuntimeRoot => Path.Combine(Environment.GetEnvironmentVariable("MURMUR_RUNTIME") ?? "E:\\DevelopmentTools\\local-dictation");
    public bool Available => File.Exists(Path.Combine(RuntimeRoot, "whisper", "Release", "whisper-cli.exe")) && File.Exists(Path.Combine(RuntimeRoot, "models", "ggml-small-q5_1.bin"));
    public async Task<string> RunAsync(byte[] wav, Preferences preferences, CancellationToken cancellation)
    {
        if (!Available) throw new IOException("The local transcription engine is missing. Run setup-runtime.ps1.");
        var work = Path.Combine(ScratchRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(work, "owner.marker"), "Murmur scratch v1", cancellation).ConfigureAwait(false);
            var audio = Path.Combine(work, "audio.wav");
            await File.WriteAllBytesAsync(audio, wav, cancellation).ConfigureAwait(false);
            var info = new ProcessStartInfo(Path.Combine(RuntimeRoot, "whisper", "Release", "whisper-cli.exe"));
            var args = new[] { "-m", Path.Combine(RuntimeRoot, "models", "ggml-small-q5_1.bin"), "-f", audio, "-l", preferences.Language, "-t", "6", "-bs", "1", "-bo", "1", "-nf", "-sns", "-ng", "-oj", "-of", Path.Combine(work, "result") };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            var prompt = string.Join(", ", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Written)).Distinct().Take(30));
            if (!string.IsNullOrEmpty(prompt)) { info.ArgumentList.Add("--prompt"); info.ArgumentList.Add(prompt); }
            using var worker = new BoundedProcess(info);
            var stdout = worker.Process.StandardOutput.ReadToEndAsync(cancellation);
            var stderr = worker.Process.StandardError.ReadToEndAsync(cancellation);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try { await worker.Process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { throw new IOException("Transcription reached the 90 second limit. Try a shorter recording."); }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            if (worker.Process.ExitCode != 0) throw new IOException("Local transcription failed. Your draft was left untouched.");
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
                if (!File.Exists(marker) || new FileInfo(marker).Length > 100 || File.ReadAllText(marker) != "Murmur scratch v1") continue;
                if (Directory.EnumerateFileSystemEntries(directory).Any(x => (File.GetAttributes(x) & FileAttributes.ReparsePoint) != 0 || Directory.Exists(x) || Path.GetFileName(x) is not ("owner.marker" or "audio.wav" or "result.json"))) continue;
                File.Delete(Path.Combine(directory, "audio.wav")); File.Delete(Path.Combine(directory, "result.json")); File.Delete(marker); Directory.Delete(directory, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Retain locked scratch instead of touching another active resource. */ }
        }
    }
}
public sealed class ScratchCleanupException(Exception cause) : IOException("Windows is still holding temporary recording files. Murmur will remain open so cleanup can be retried.", cause);
