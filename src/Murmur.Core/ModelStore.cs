using System.Security.Cryptography;
namespace Murmur.Core;

public sealed class ModelStore(string directory)
{
    private readonly SemaphoreSlim installGate = new(1, 1);
    private readonly HashSet<string> retained = new(StringComparer.OrdinalIgnoreCase);
    public bool CleanupPending { get { lock (retained) return retained.Count > 0; } }
    public async Task RetryCleanupAsync()
    {
        string[] paths; lock (retained) paths = retained.ToArray();
        foreach (var path in paths) await CleanupAsync(path);
    }
    private async Task CleanupAsync(string path)
    {
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                try { File.Delete(path); lock (retained) retained.Remove(path); return; }
                catch (IOException) when (attempt < 20) { await Task.Delay(50); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (retained) retained.Add(path);
            throw new IOException("A partial model download is still locked. Cleanup needs retry.");
        }
    }
    public string PathFor(string id) => Path.Combine(directory, ModelCatalog.Get(id).File);
    public bool Present(string id) => File.Exists(PathFor(id)) && new FileInfo(PathFor(id)).Length == ModelCatalog.Get(id).Bytes;
    public async Task<bool> VerifyAsync(string id, CancellationToken cancellation = default)
    {
        var model = ModelCatalog.Get(id); var path = PathFor(id);
        if (!File.Exists(path) || new FileInfo(path).Length != model.Bytes) return false;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("A linked model file was preserved.");
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, cancellation)).Equals(model.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    public async Task InstallAsync(string id, IProgress<long>? progress, CancellationToken cancellation, HttpClient? suppliedClient = null)
    {
        var model = ModelCatalog.Get(id);
        if (!await installGate.WaitAsync(0, cancellation)) throw new IOException("Another model download is running.");
        string? temporary = null;
        try
        {
            Directory.CreateDirectory(directory);
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("A linked model directory was preserved.");
            var path = PathFor(id);
            if (File.Exists(path))
            {
                if (await VerifyAsync(id, cancellation)) return;
                throw new IOException("An existing model differs from the catalog and was preserved. Move it yourself before installing.");
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(30));
            using var ownedClient = suppliedClient == null ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan } : null;
            var client = suppliedClient ?? ownedClient!;
            using var response = await client.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != model.Bytes) throw new IOException("The download size differs from the pinned catalog.");
            temporary = Path.Combine(directory, ".sona-download-" + Guid.NewGuid().ToString("N"));
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token))
            {
                var buffer = new byte[131072]; long total = 0;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                while (true)
                {
                    var count = await input.ReadAsync(buffer, timeout.Token); if (count == 0) break;
                    total += count; if (total > model.Bytes) throw new IOException("The download exceeded the pinned model size.");
                    hash.AppendData(buffer, 0, count); await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token); progress?.Report(total);
                }
                if (total != model.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(model.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Model integrity check failed. The download was discarded.");
                await output.FlushAsync(timeout.Token);
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, path, false); temporary = null;
        }
        finally
        {
            try { if (temporary != null) await CleanupAsync(temporary); }
            finally { installGate.Release(); }
        }
    }
}
