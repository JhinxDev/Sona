using System.Text;
namespace Sona.Core;

/// <summary>
/// Durable atomic replacement for small local state files. The candidate is fully
/// written and flushed to disk before it replaces the existing file, so a torn
/// write or power loss cannot leave a half-written profile behind.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string contents, Action? beforeCommit = null) =>
        WriteAllBytes(path, new UTF8Encoding(false).GetBytes(contents), beforeCommit);

    public static void WriteAllBytes(string path, byte[] contents, Action? beforeCommit = null)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full) ?? throw new IOException("The destination has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
            beforeCommit?.Invoke();
            File.Move(temporary, full, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
