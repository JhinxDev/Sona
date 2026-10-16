using System.Text.Json;
namespace Sona.Core;

public sealed record HistoryItem(Guid Id, DateTimeOffset CreatedAt, string Original, string Draft, bool Inserted = false, bool Saved = false, string? Generated = null)
{
    public string SavedLabel => Saved ? "Saved on this device" : "Recent dictation";
    public string TimeLabel => CreatedAt.ToLocalTime().ToString("MMM d · HH:mm");
    public string Preview => Draft.Length > 240 ? Draft[..240] + "…" : Draft;
}

public static class HistoryStore
{
    public const int Capacity = 200;
    private static bool Valid(HistoryItem? item) => item != null && item.Id != Guid.Empty && item.CreatedAt != default && item.Original != null && item.Draft != null && item.Original.Length <= 6000 && item.Draft.Length is > 0 and <= 6000 && (item.Generated == null || item.Generated.Length <= 6000);
    public static List<HistoryItem> Bounded(IEnumerable<HistoryItem> entries) => entries
        .Where(Valid)
        .DistinctBy(x => x.Id).Take(Capacity).ToList();
    public static List<HistoryItem> Search(IEnumerable<HistoryItem> entries, string query)
    {
        var text = query.Trim();
        return entries.Where(x => text.Length == 0 || x.Draft.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Original.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }
    public static List<HistoryItem> Load(string path)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new IOException("The saved history is too large. The existing file was preserved.");
        var entries = JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(path)) ?? throw new JsonException("Saved drafts must be a list.");
        if (entries.Any(item => !Valid(item))) throw new JsonException("Saved drafts contain an invalid record. The existing file was preserved.");
        return Bounded(entries);
    }
    public static void Save(string path, IEnumerable<HistoryItem> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Bounded(entries)));
            // Revalidate at the write boundary. A file damaged or made unavailable
            // since startup must not be replaced by an older in-memory snapshot.
            try { _ = Load(path); }
            catch (Exception ex) when (ex is JsonException or System.Security.SecurityException)
            { throw new IOException("Saved drafts could not be verified. The existing file was preserved.", ex); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
