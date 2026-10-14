using System.Text.Json;
namespace Sona.Core;

public sealed record HistoryItem(Guid Id, DateTimeOffset CreatedAt, string Original, string Draft, bool Inserted = false)
{
    public string TimeLabel => CreatedAt.ToLocalTime().ToString("MMM d · HH:mm");
    public string Preview => Draft.Length > 240 ? Draft[..240] + "…" : Draft;
}

public static class HistoryStore
{
    public const int Capacity = 200;
    public static List<HistoryItem> Bounded(IEnumerable<HistoryItem> entries) => entries
        .Where(x => x != null && x.Id != Guid.Empty && x.CreatedAt != default && x.Original != null && x.Draft != null && x.Original.Length <= 6000 && x.Draft.Length is > 0 and <= 6000)
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
        return Bounded(JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(path)) ?? []);
    }
    public static void Save(string path, IEnumerable<HistoryItem> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(Bounded(entries))); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
