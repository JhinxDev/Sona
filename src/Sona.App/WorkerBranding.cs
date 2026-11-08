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
        ["whisper-cli.exe"] = "Sona Transcription.exe",
        ["llama-server.exe"] = "Sona Text Polish.exe"
    };

    internal static string? FriendlyName(string executable) =>
        FriendlyNames.TryGetValue(Path.GetFileName(executable), out var name) ? name : null;

    /// <summary>
    /// Returns a clearly named copy of a worker executable, or the original path if
    /// branding is not possible. Copies refresh when the upstream file changes and
    /// are written outside hash-verified runtime directories when one is supplied.
    /// </summary>
    internal static string PathFor(string executable, string? copyDirectory = null)
    {
        try
        {
            if (!File.Exists(executable) || FriendlyName(executable) is not { } name) return executable;
            var directory = copyDirectory ?? Path.GetDirectoryName(executable);
            if (string.IsNullOrWhiteSpace(directory)) return executable;
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, name);
            if (IsCurrent(executable, target)) return target;
            File.Copy(executable, target, true);
            return File.Exists(target) ? target : executable;
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
            // File.Copy preserves the source timestamp, so an unchanged engine stays
            // a no-op and an updated engine refreshes the copy.
            return targetInfo.Exists && targetInfo.Length == sourceInfo.Length && targetInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return false; }
    }
}
