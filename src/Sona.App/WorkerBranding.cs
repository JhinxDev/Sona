using System.Security.Cryptography;

namespace Sona.App;

/// <summary>
/// Launches Sona's upstream worker binaries under clear Sona names so Windows Task
/// Manager and Process Explorer show what each background process does. Copies are
/// byte-identical to the verified upstream files; hash-pinned originals stay in
/// place and keep their integrity checks.
/// </summary>
internal static class WorkerBranding
{
    internal static readonly IReadOnlyDictionary<string, string> FriendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["whisper-server.exe"] = "Sona Speech Engine.exe",
        ["nemo-speech.exe"] = "Sona Speech Engine.exe",
        ["whisper-cli.exe"] = "Sona Transcription.exe",
        ["llama-server.exe"] = "Sona Text Polish.exe"
    };

    internal static string? FriendlyName(string executable) =>
        FriendlyNames.TryGetValue(Path.GetFileName(executable), out var name) ? name : null;

    /// <summary>
    /// Returns a clearly named copy of a worker executable, or the original path if
    /// branding is not possible. Copies refresh when the upstream file changes or
    /// its SHA-256 differs and are written outside hash-verified runtime directories
    /// when one is supplied.
    /// </summary>
    internal static string PathFor(string executable, string? copyDirectory = null)
    {
        try
        {
            if (!File.Exists(executable) || HasReparsePoint(executable) || FriendlyName(executable) is not { } name) return executable;
            var directory = copyDirectory ?? Path.GetDirectoryName(executable);
            if (string.IsNullOrWhiteSpace(directory)) return executable;
            Directory.CreateDirectory(directory);
            if (HasReparsePoint(directory)) return executable;
            var target = Path.Combine(directory, name);
            if (IsCurrent(executable, target)) return target;
            if (File.Exists(target) && HasReparsePoint(target)) return executable;
            File.Copy(executable, target, true);
            return File.Exists(target) && IsCurrent(executable, target) ? target : executable;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // A read-only or locked runtime keeps the original name rather than
            // losing dictation. The hash-verified original is the launch fallback.
            return executable;
        }
    }

    private static bool IsCurrent(string source, string target)
    {
        try
        {
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);
            if (!targetInfo.Exists || HasReparsePoint(target) || targetInfo.Length != sourceInfo.Length) return false;
            using var sourceStream = File.OpenRead(source);
            using var targetStream = File.OpenRead(target);
            return SHA256.HashData(sourceStream).AsSpan().SequenceEqual(SHA256.HashData(targetStream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return false; }
    }

    private static bool HasReparsePoint(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        return false;
    }
}
