using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Murmur.App.Audio;

internal static class ParakeetAssets
{
    internal const string Version = "0.2.0";
    internal const string CompatibilityPatch = "scheduler-metadata-v1";
    internal const string CompatibilityHash = "ada0590d50035510b7b4e09577ce849f7ef27ff4f43818c98b199da88839ec79";
    private const long CompatibilityBytes = 679936;
    private const string ManifestName = "integrity.json";
    private static readonly SemaphoreSlim installGate = new(1, 1);
    private static readonly HashSet<string> retainedPartials = new(StringComparer.OrdinalIgnoreCase);
    public static string Root => Path.Combine(Transcriber.RuntimeRoot, "parakeet");
    public static string RuntimePath(bool cuda) => Path.Combine(Root, cuda ? "runtime-cuda" : "runtime-cpu");
    public static string Executable(bool cuda) => Path.Combine(RuntimePath(cuda), "nemo-speech.exe");
    public static bool Present(bool cuda) => File.Exists(Executable(cuda)) && File.Exists(Path.Combine(RuntimePath(cuda), ManifestName));
    public static bool CleanupPending { get { lock (retainedPartials) return retainedPartials.Count != 0; } }
    private sealed record Asset(string Name, long Bytes, string Sha256)
    {
        public string Url => $"https://github.com/NVIDIA/NeMo-Speech.cpp/releases/download/v{Version}/{Name}.zip";
    }
    private static Asset Archive(bool cuda) => cuda
        ? new("nemo-speech-0.2.0-windows-x86_64-cuda", 102183465, "9ffe5e842c39a7d3211f3b2c3b3aa1bca8c0b2455d96d961ed5f22ec5108f98d")
        : new("nemo-speech-0.2.0-windows-x86_64-cpu", 5528418, "82c80451086e86194aba9af19f800da77cd66d1c28d993d0efceffd6cdb49bb8");
    private sealed record FileEntry(string Path, long Bytes, string Sha256);
    private sealed record Manifest(string Runtime, string Backend, string ArchiveName, long ArchiveBytes, string ArchiveSha256, string Patch, string PatchedDllSha256, FileEntry[] Files);

    public static async Task<bool> VerifyAsync(bool cuda, CancellationToken token = default)
    {
        if (!Present(cuda)) return false;
        return await VerifyAtAsync(RuntimePath(cuda), cuda, token).ConfigureAwait(false);
    }
    private static async Task<bool> VerifyAtAsync(string directory, bool cuda, CancellationToken token)
    {
        var actual = RuntimeFiles(directory);
        var manifestPath = Path.Combine(directory, ManifestName);
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 256 * 1024) return false;
        Manifest? manifest;
        try { manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllBytesAsync(manifestPath, token).ConfigureAwait(false)); }
        catch (JsonException) { return false; }
        var archive = Archive(cuda);
        if (manifest == null || manifest.Runtime != Version || manifest.Backend != (cuda ? "cuda" : "cpu") ||
            manifest.ArchiveName != archive.Name + ".zip" || manifest.ArchiveBytes != archive.Bytes || manifest.ArchiveSha256 != archive.Sha256 ||
            manifest.Patch != CompatibilityPatch || manifest.PatchedDllSha256 != CompatibilityHash ||
            manifest.Files == null || manifest.Files.Length is < 1 or > 512) return false;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            token.ThrowIfCancellationRequested();
            if (file == null || !AllowedRuntimeFile(file.Path) || !names.Add(file.Path) || file.Bytes < 0 ||
                file.Sha256 is not { Length: 64 } || !file.Sha256.All(Uri.IsHexDigit)) return false;
            var path = ContainedPath(directory, file.Path);
            if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes ||
                !file.Sha256.Equals(await HashAsync(path, token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase)) return false;
        }
        if (!await CompatibilityMatchesAsync(directory, token).ConfigureAwait(false)) return false;
        return names.SetEquals(actual) && RequiredFiles(names);
    }
    private static async Task WriteManifestAtAsync(string directory, bool cuda, CancellationToken token)
    {
        if (!await CompatibilityMatchesAsync(directory, token).ConfigureAwait(false)) throw new IOException("Parakeet scheduler compatibility repair is missing or differs.");
        var files = RuntimeFiles(directory);
        if (!RequiredFiles(files)) throw new IOException("Parakeet executable or license notices are missing.");
        var entries = new List<FileEntry>();
        foreach (var name in files.Order(StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();
            var path = ContainedPath(directory, name);
            entries.Add(new(name, new FileInfo(path).Length, await HashAsync(path, token).ConfigureAwait(false)));
        }
        var archive = Archive(cuda);
        var manifest = new Manifest(Version, cuda ? "cuda" : "cpu", archive.Name + ".zip", archive.Bytes, archive.Sha256, CompatibilityPatch, CompatibilityHash, entries.ToArray());
        await using var output = new FileStream(Path.Combine(directory, ManifestName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
        await output.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(manifest), token).ConfigureAwait(false);
    }
    public static Task InstallAsync(bool cuda, IProgress<int>? progress, CancellationToken token) => InstallCoreAsync(cuda, null, progress, token);
    // A cached archive follows the exact same pinned integrity, gated staging,
    // compatibility repair, verification and finite cleanup path as a download.
    internal static Task FromVerifiedArchiveAsync(bool cuda, string archivePath, IProgress<int>? progress, CancellationToken token) =>
        InstallCoreAsync(cuda, Path.GetFullPath(archivePath), progress, token);
    private static async Task InstallCoreAsync(bool cuda, string? localArchive, IProgress<int>? progress, CancellationToken token)
    {
        if (!await installGate.WaitAsync(0, token).ConfigureAwait(false)) throw new IOException("A Parakeet runtime download is already running.");
        var owned = new List<string>();
        try
        {
            if (CleanupPending) throw new IOException("Retry Parakeet partial download cleanup first.");
            CheckPlainPath(Root); Directory.CreateDirectory(Root); CheckPlainPath(Root);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromMinutes(30));
            var archive = Archive(cuda);
            FileStream? cached = null;
            if (localArchive != null)
            {
                CheckPlainPath(localArchive);
                cached = new FileStream(localArchive, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
            }
            await using var archiveLease = cached;
            if (cached != null)
            {
                if (cached.Length != archive.Bytes || !Convert.ToHexString(await SHA256.HashDataAsync(cached, deadline.Token).ConfigureAwait(false)).Equals(archive.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Cached Parakeet archive failed its pinned integrity check.");
                cached.Position = 0;
            }
            var runtime = RuntimePath(cuda); CheckPlainPath(runtime);
            if (File.Exists(runtime)) throw new IOException("An existing Parakeet runtime path was preserved.");
            if (Directory.Exists(runtime))
            {
                if (!await VerifyAsync(cuda, token).ConfigureAwait(false)) throw new IOException("The existing Parakeet runtime differs or is unreceipted and was preserved.");
                progress?.Report(100); return;
            }
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            var zip = localArchive;
            if (zip == null)
            {
                zip = Path.Combine(Root, ".download-" + Guid.NewGuid().ToString("N")); owned.Add(zip);
                await DownloadAsync(client, archive, zip, progress, deadline.Token).ConfigureAwait(false);
            }
            var stage = Path.Combine(Root, ".runtime-" + Guid.NewGuid().ToString("N")); owned.Add(stage); Directory.CreateDirectory(stage);
            await ExtractAsync(zip, stage, archive, deadline.Token).ConfigureAwait(false);
            await ApplyCompatibilityAsync(stage, cuda, deadline.Token).ConfigureAwait(false);
            await WriteManifestAtAsync(stage, cuda, deadline.Token).ConfigureAwait(false);
            if (!await VerifyAtAsync(stage, cuda, deadline.Token).ConfigureAwait(false)) throw new IOException("Parakeet staging integrity verification failed.");
            deadline.Token.ThrowIfCancellationRequested(); CheckPlainPath(runtime);
            Directory.Move(stage, runtime); owned.Remove(stage);
            progress?.Report(100);
        }
        finally
        {
            try
            {
                Exception? failure = null;
                foreach (var path in owned)
                {
                    try { await DeleteOwnedPartialAsync(path).ConfigureAwait(false); }
                    catch (IOException ex) { failure ??= ex; }
                }
                if (failure != null) throw new IOException("A Parakeet partial is still locked. Retry download cleanup.", failure);
            }
            finally { installGate.Release(); }
        }
    }
    private static async Task<bool> CompatibilityMatchesAsync(string directory, CancellationToken token)
    {
        var path = Path.Combine(directory, "ggml-base.dll"); CheckPlainPath(path);
        return File.Exists(path) && new FileInfo(path).Length == CompatibilityBytes &&
            CompatibilityHash.Equals(await HashAsync(path, token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase);
    }
    private static async Task ApplyCompatibilityAsync(string stage, bool cuda, CancellationToken token)
    {
        var target = Path.Combine(stage, "ggml-base.dll"); CheckPlainPath(target);
        var originalHash = cuda ? "21747a2010b6e13f44d264f1746ca900e42b02175a72e7f9257ce7b991403513" : "460afaa830b390add25ac11ee4f247ba2ea99368d337d068a5d84158e737f923";
        if (!File.Exists(target) || new FileInfo(target).Length != 680960 || !originalHash.Equals(await HashAsync(target, token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Original Parakeet ggml-base member differs from the pinned archive.");
        var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "Parakeet"); CheckPlainPath(bundled);
        if (!await CompatibilityMatchesAsync(bundled, token).ConfigureAwait(false)) throw new IOException("Bundled Parakeet scheduler repair failed its pinned integrity check.");
        // Replacement is confined to this install's private verified staging tree.
        await using (var input = new FileStream(Path.Combine(bundled, "ggml-base.dll"), FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true))
        await using (var output = new FileStream(target, FileMode.Truncate, FileAccess.Write, FileShare.None, 131072, true))
            await input.CopyToAsync(output, token).ConfigureAwait(false);
        if (!await CompatibilityMatchesAsync(stage, token).ConfigureAwait(false)) throw new IOException("Staged Parakeet scheduler repair integrity differs.");
        var licenseDirectory = Path.Combine(stage, "licenses", "sona-scheduler-repair"); Directory.CreateDirectory(licenseDirectory); CheckPlainPath(licenseDirectory);
        foreach (var name in new[] { "GGML-LICENSE.txt", "NVIDIA-LICENSE.txt", "NVIDIA-NOTICE.txt", "PATCH-NOTICE.txt" })
        {
            var source = Path.Combine(bundled, name); CheckPlainPath(source);
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await using var output = new FileStream(Path.Combine(licenseDirectory, name), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
        }
    }
    public static async Task RetryCleanupAsync()
    {
        await installGate.WaitAsync().ConfigureAwait(false);
        try
        {
            string[] paths; lock (retainedPartials) paths = retainedPartials.ToArray();
            Exception? failure = null;
            foreach (var path in paths)
            {
                try { await DeleteOwnedPartialAsync(path).ConfigureAwait(false); }
                catch (IOException ex) { failure ??= ex; }
            }
            if (failure != null) throw new IOException("A Parakeet partial still needs cleanup.", failure);
        }
        finally { installGate.Release(); }
    }
    private static async Task DeleteOwnedPartialAsync(string path)
    {
        try
        {
            var exact = ContainedPath(Root, Path.GetFileName(path));
            if (!exact.Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
                !(Path.GetFileName(exact).StartsWith(".download-", StringComparison.Ordinal) || Path.GetFileName(exact).StartsWith(".runtime-", StringComparison.Ordinal)))
                throw new IOException("Parakeet refused an unowned cleanup path.");
            CheckPlainPath(exact);
            if (Directory.Exists(exact)) CheckTree(exact);
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
            throw new IOException("A task-owned Parakeet partial needs retry.", ex);
        }
    }
    private static async Task DownloadAsync(HttpClient client, Asset asset, string path, IProgress<int>? progress, CancellationToken token)
    {
        CheckPlainPath(path);
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.Url); request.Headers.UserAgent.ParseAdd("Sona-Parakeet/1");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != asset.Bytes) throw new IOException("Parakeet archive size differs from its pin.");
        await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[131072]; long total = 0;
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
            {
                total += read; if (total > asset.Bytes) throw new IOException("Parakeet download exceeded its pinned size.");
                hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                progress?.Report((int)(total * 90 / asset.Bytes));
            }
            if (total != asset.Bytes || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Parakeet archive failed its pinned hash check.");
            await output.FlushAsync(token).ConfigureAwait(false);
        }
        finally { Array.Clear(buffer); }
    }
    private static async Task ExtractAsync(string archivePath, string stage, Asset asset, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        if (zip.Entries.Count > 2048) throw new IOException("Parakeet archive has too many entries.");
        var archiveNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            var directory = name.EndsWith('/'); name = name.TrimEnd('/');
            ContainedPath(stage, name);
            if (!archiveNames.Add(name) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked or duplicate Parakeet archive paths are not allowed.");
            if (directory && name == asset.Name) continue;
            if (!name.StartsWith(asset.Name + "/", StringComparison.Ordinal)) throw new IOException("Parakeet archive root differs from its pin.");
            if (directory) continue;
            total = checked(total + entry.Length);
            if (entry.Length < 0 || entry.Length > 512L * 1024 * 1024 || total > 1024L * 1024 * 1024) throw new IOException("Parakeet archive exceeded its extraction bound.");
            var relative = name[(asset.Name.Length + 1)..];
            string targetName;
            if (relative.StartsWith("bin/", StringComparison.Ordinal)) targetName = relative[4..];
            else if (relative.StartsWith("share/licenses/nemo-speech/", StringComparison.Ordinal)) targetName = "licenses/" + relative["share/licenses/nemo-speech/".Length..];
            else continue;
            if (!AllowedRuntimeFile(targetName) || !targets.Add(targetName)) throw new IOException("Parakeet runtime contains an unsupported or duplicate file.");
            var target = ContainedPath(stage, targetName); CheckPlainPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open(); await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true);
            await input.CopyToAsync(output, token).ConfigureAwait(false);
            if (output.Length != entry.Length) throw new IOException("Parakeet extracted member size differs.");
        }
    }
    private static bool RequiredFiles(HashSet<string> names) => names.Contains("nemo-speech.exe") && names.Contains("licenses/LICENSE") && names.Contains("licenses/NOTICE") && names.Contains("licenses/THIRD_PARTY_NOTICES.md") &&
        new[] { "GGML-LICENSE.txt", "NVIDIA-LICENSE.txt", "NVIDIA-NOTICE.txt", "PATCH-NOTICE.txt" }.All(n => names.Contains("licenses/sona-scheduler-repair/" + n));
    private static bool AllowedRuntimeFile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\\')) return false;
        if (name.StartsWith("licenses/", StringComparison.Ordinal)) return name.Length > 9;
        return !name.Contains('/') && (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }
    private static HashSet<string> RuntimeFiles(string directory)
    {
        CheckPlainPath(directory); CheckTree(directory);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(directory, path).Replace('\\', '/');
            if (name == ManifestName) continue;
            if (!AllowedRuntimeFile(name) || !names.Add(name) || names.Count > 512) throw new IOException("Parakeet runtime contains an unexpected file.");
        }
        return names;
    }
    private static void CheckTree(string directory)
    {
        CheckPlainPath(directory);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            CheckPlainPath(entry);
            if (Directory.Exists(entry)) CheckTree(entry);
        }
    }
    private static string ContainedPath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains(':') || Path.IsPathRooted(relative)) throw new IOException("Parakeet path escaped its runtime directory.");
        var segments = relative.Replace('\\', '/').Split('/');
        if (segments.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith('.') || p.EndsWith(' '))) throw new IOException("Parakeet path has an unsafe segment.");
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Parakeet path escaped its runtime directory.");
        return path;
    }
    private static void CheckPlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked Parakeet paths were preserved.");
    }
    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        CheckPlainPath(path);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        return Convert.ToHexString(await SHA256.HashDataAsync(input, token).ConfigureAwait(false)).ToLowerInvariant();
    }
}
