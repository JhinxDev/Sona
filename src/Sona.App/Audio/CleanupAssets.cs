using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Sona.App.Audio;

internal static class CleanupAssets
{
    internal const long ModelBytes = 639442784;
    internal const string ModelHash = "66509e0fe1578d2ac4b0773c79a6c83e8d6f218618dfe11e4532beacd999b6e8";
    private const string Revision = "79936c95e5c0ea9162ae7a435e64cb3127020e36";
    private const string Build = "b11583";
    private const string ManifestName = "integrity.json";
    private static readonly SemaphoreSlim installGate = new(1, 1);
    private static readonly HashSet<string> retainedPartials = new(StringComparer.OrdinalIgnoreCase);
    public static bool CleanupPending { get { lock (retainedPartials) return retainedPartials.Count > 0; } }
    public static string Root => Path.Combine(Transcriber.RuntimeRoot, "cleanup");
    public static string ModelPath => Path.Combine(Root, "cleanup-1-q8_0.gguf");
    private sealed record Asset(string Name, long Bytes, string Sha256)
    {
        public string Url => "https://github.com/ggml-org/llama.cpp/releases/download/" + Build + "/" + Name;
    }
    private static readonly Asset Cpu = new("llama-b11583-bin-win-cpu-x64.zip", 19509591, "2bceb716d9d93b950c008c9d81701650965c33e957de9e1d4da325c35c41abfe");
    private static readonly Asset Cuda = new("llama-b11583-bin-win-cuda-12.4-x64.zip", 265226766, "db709bfc5ee1a8eb34e03cf6cb5f936a985aaf8cd7b15af967e137c4a7d0191b");
    private static readonly Asset Cudart = new("cudart-llama-bin-win-cuda-12.4-x64.zip", 391443627, "8c79a9b226de4b3cacfd1f83d24f962d0773be79f1e7b75c6af4ded7e32ae1d6");
    private sealed record Manifest(string Runtime, string ModelRevision, string Backend, string[] Archives, FileEntry[] Files);
    private sealed record FileEntry(string Path, long Bytes, string Sha256);
    private static Asset[] Archives(string backend) => backend == "GPU" ? [Cuda, Cudart] : [Cpu];
    public static string RuntimePath(string backend) => Path.Combine(Root, backend switch { "GPU" => "runtime-cuda", "CPU" => "runtime-cpu", _ => throw new ArgumentException("Unknown cleanup backend.") });
    public static string ResolveBackend(string engine) => engine switch
    {
        "Cpu" => "CPU",
        "Auto" => File.Exists(Path.Combine(RuntimePath("GPU"), "llama-server.exe")) ? "GPU" : "CPU",
        _ => throw new ArgumentException("Unknown cleanup engine.")
    };
    public static bool Present(string engine) => File.Exists(ModelPath) && new FileInfo(ModelPath).Length == ModelBytes &&
        File.Exists(Path.Combine(RuntimePath(ResolveBackend(engine)), "llama-server.exe"));
    public static bool Available(string engine) => Present(engine) && File.Exists(Path.Combine(RuntimePath(ResolveBackend(engine)), ManifestName));
    public static async Task RetryCleanupAsync()
    {
        string[] paths; lock (retainedPartials) paths = retainedPartials.ToArray();
        foreach (var path in paths) await DeleteOwnedPartialAsync(path).ConfigureAwait(false);
    }

    public static async Task<bool> VerifyAsync(string engine, CancellationToken token = default)
    {
        if (!Available(engine)) return false;
        CheckPlainPath(Root); CheckPlainPath(ModelPath);
        if (!await HashMatchesAsync(ModelPath, ModelBytes, ModelHash, token).ConfigureAwait(false)) return false;
        return await VerifyRuntimeAsync(ResolveBackend(engine), token).ConfigureAwait(false);
    }
    private static async Task<bool> VerifyRuntimeAsync(string backend, CancellationToken token)
    {
        var directory = RuntimePath(backend); CheckPlainPath(directory);
        var path = Path.Combine(directory, ManifestName); CheckPlainPath(path);
        if (new FileInfo(path).Length > 256 * 1024) return false;
        Manifest? manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllBytesAsync(path, token).ConfigureAwait(false)); }
        catch (JsonException) { return false; }
        if (manifest == null || manifest.Runtime != Build || manifest.ModelRevision != Revision || manifest.Backend != backend ||
            manifest.Archives == null || !manifest.Archives.SequenceEqual(Archives(backend).Select(a => a.Sha256)) || manifest.Files == null || manifest.Files.Length is < 1 or > 512) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (file == null || string.IsNullOrEmpty(file.Path) || !names.Add(file.Path) || file.Bytes < 0 || file.Sha256 is not { Length: 64 }) return false;
            var target = ContainedPath(directory, file.Path); CheckPlainPath(target);
            if (!await HashMatchesAsync(target, file.Bytes, file.Sha256, token).ConfigureAwait(false)) return false;
        }
        var actual = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(directory, p)).Where(p => p != ManifestName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return names.SetEquals(actual) && names.Contains("llama-server.exe");
    }

    // Only for explicitly provisioned, archive-hash-verified runtime files. The
    // installer calls this on its private staging directory before publication.
    public static Task WriteManifestAsync(string backend, CancellationToken token = default) => WriteManifestAtAsync(RuntimePath(backend), backend, token);
    private static async Task WriteManifestAtAsync(string directory, string backend, CancellationToken token)
    {
        CheckPlainPath(directory);
        var entries = new List<FileEntry>();
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested(); CheckPlainPath(path);
            var name = Path.GetRelativePath(directory, path); if (name == ManifestName) continue;
            if (entries.Count >= 512) throw new IOException("Cleanup runtime has too many files.");
            entries.Add(new(name, new FileInfo(path).Length, await HashAsync(path, token).ConfigureAwait(false)));
        }
        if (!entries.Any(e => e.Path.Equals("llama-server.exe", StringComparison.OrdinalIgnoreCase))) throw new IOException("Cleanup server is missing.");
        var manifest = new Manifest(Build, Revision, backend, Archives(backend).Select(a => a.Sha256).ToArray(), entries.ToArray());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var target = Path.Combine(directory, ManifestName);
        if (File.Exists(target)) throw new IOException("An existing cleanup integrity manifest was preserved.");
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await output.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    public static async Task InstallAsync(string engine, IProgress<int>? progress, CancellationToken token)
    {
        if (engine is not ("Auto" or "Cpu")) throw new ArgumentException("Unknown cleanup engine.");
        if (!await installGate.WaitAsync(0, token).ConfigureAwait(false)) throw new IOException("A cleanup download is already running.");
        var owned = new List<string>();
        try
        {
            if (CleanupPending) throw new IOException("A partial cleanup download is still releasing. Retry download cleanup first.");
            Directory.CreateDirectory(Root); CheckPlainPath(Root);
            // GPU/VRAM is the owner's preferred installation. CPU/RAM remains
            // an explicit download choice for hardware without NVIDIA CUDA.
            var backend = engine == "Auto" ? "GPU" : "CPU";
            if (File.Exists(ModelPath) && !await HashMatchesAsync(ModelPath, ModelBytes, ModelHash, token).ConfigureAwait(false))
                throw new IOException("The existing cleanup model differs from the pinned model and was preserved.");
            var runtime = RuntimePath(backend);
            var runtimeReady = Directory.Exists(runtime) && File.Exists(Path.Combine(runtime, ManifestName)) && await VerifyRuntimeAsync(backend, token).ConfigureAwait(false);
            if (Directory.Exists(runtime) && !runtimeReady) throw new IOException("The existing cleanup runtime differs or has no integrity manifest and was preserved.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(30));
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            if (!File.Exists(ModelPath))
            {
                var partial = Path.Combine(Root, ".download-" + Guid.NewGuid().ToString("N")); owned.Add(partial);
                await DownloadAsync(client, "https://huggingface.co/spokenly/cleanup-1-gguf/resolve/" + Revision + "/cleanup-1-q8_0.gguf", ModelBytes, ModelHash, partial, progress, deadline.Token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); File.Move(partial, ModelPath, false); owned.Remove(partial);
            }
            if (!runtimeReady)
            {
                var stage = Path.Combine(Root, ".runtime-" + Guid.NewGuid().ToString("N")); owned.Add(stage); Directory.CreateDirectory(stage);
                foreach (var asset in Archives(backend))
                {
                    var zip = Path.Combine(Root, ".download-" + Guid.NewGuid().ToString("N")); owned.Add(zip);
                    await DownloadAsync(client, asset.Url, asset.Bytes, asset.Sha256, zip, progress, deadline.Token).ConfigureAwait(false);
                    await ExtractAsync(zip, stage, deadline.Token).ConfigureAwait(false);
                    File.Delete(zip); owned.Remove(zip);
                }
                await WriteManifestAtAsync(stage, backend, deadline.Token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); Directory.Move(stage, runtime); owned.Remove(stage);
            }
            progress?.Report(100);
        }
        finally
        {
            try
            {
                Exception? cleanupFailure = null;
                foreach (var path in owned)
                {
                    try { await DeleteOwnedPartialAsync(path).ConfigureAwait(false); }
                    catch (IOException ex) { cleanupFailure ??= ex; }
                }
                if (cleanupFailure != null) throw new IOException("A partial cleanup download is still locked. Retry download cleanup.", cleanupFailure);
            }
            finally { installGate.Release(); }
        }
    }
    private static async Task DeleteOwnedPartialAsync(string path)
    {
        try
        {
            var exact = ContainedPath(Root, Path.GetRelativePath(Root, path));
            var name = Path.GetFileName(exact);
            if (!name.StartsWith(".download-", StringComparison.Ordinal) && !name.StartsWith(".runtime-", StringComparison.Ordinal)) throw new IOException("Cleanup refused an unowned partial path.");
            CheckPlainPath(exact);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    if (Directory.Exists(exact)) Directory.Delete(exact, true); else File.Delete(exact);
                    lock (retainedPartials) retainedPartials.Remove(path);
                    return;
                }
                catch (IOException) when (attempt < 20) { await Task.Delay(50).ConfigureAwait(false); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (retainedPartials) retainedPartials.Add(path);
            throw new IOException("A task-owned cleanup partial needs retry.", ex);
        }
    }
    private static async Task DownloadAsync(HttpClient client, string url, long expectedBytes, string expectedHash, string destination, IProgress<int>? progress, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url); request.Headers.UserAgent.ParseAdd("Sona-local-cleanup/1");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != expectedBytes) throw new IOException("Cleanup download size differs from its pinned catalog.");
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[131072]; long total = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                total += read; if (total > expectedBytes) throw new IOException("Cleanup download exceeded its pinned size.");
                hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                progress?.Report((int)(total * 100 / expectedBytes));
            }
            if (total != expectedBytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Cleanup download failed its pinned integrity check.");
            await output.FlushAsync(token).ConfigureAwait(false);
        }
        finally { Array.Clear(buffer); }
    }
    private static async Task ExtractAsync(string archive, string stage, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 512) throw new IOException("Cleanup archive has too many entries.");
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new IOException("Linked cleanup archive entries are not allowed.");
            var target = ContainedPath(stage, entry.FullName); CheckPlainPath(target);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(target); continue; }
            total += entry.Length;
            if (entry.Length > 1024L * 1024 * 1024 || total > 4L * 1024 * 1024 * 1024) throw new IOException("Cleanup archive exceeded its extraction limit.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target))
            {
                using var existing = File.OpenRead(target); using var incoming = entry.Open();
                if (!SHA256.HashData(existing).SequenceEqual(SHA256.HashData(incoming))) throw new IOException("Cleanup archives contain conflicting files.");
                continue;
            }
            await using var input = entry.Open(); await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
        }
    }
    internal static string ContainedPath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new IOException("Cleanup path is outside its directory.");
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Cleanup path is outside its directory.");
        return path;
    }
    private static void CheckPlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked cleanup paths were preserved.");
    }
    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();
    }
    private static async Task<bool> HashMatchesAsync(string path, long size, string hash, CancellationToken token)
    {
        if (!File.Exists(path) || new FileInfo(path).Length != size) return false;
        CheckPlainPath(path);
        return (await HashAsync(path, token).ConfigureAwait(false)).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }
}
