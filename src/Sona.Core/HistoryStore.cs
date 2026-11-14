using System.Text.Json;
using System.Text.Json.Serialization;
namespace Sona.Core;

public sealed record HistoryItem(Guid Id, DateTimeOffset CreatedAt, string Original, string Draft, bool Inserted = false, bool Saved = false, string? Generated = null)
{
    [JsonIgnore] public string SavedLabel => Saved ? "Saved on this device" : "Recent dictation";
    [JsonIgnore] public string TimeLabel => CreatedAt.ToLocalTime().ToString("MMM d · HH:mm");
    [JsonIgnore] public string Preview => Draft.Length > 240 ? Draft[..240] + "…" : Draft;
}

public static class HistoryStore
{
    public const int Capacity = 200;
    private const int MaximumFileBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions WriteOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static bool Valid(HistoryItem? item) => item != null && item.Id != Guid.Empty && item.CreatedAt != default &&
        item.Original is { Length: <= DraftLimits.MaximumCharacters } &&
        item.Draft is { Length: > 0 and <= DraftLimits.MaximumCharacters } &&
        item.Generated is not { Length: > DraftLimits.MaximumCharacters };
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
        if (new FileInfo(path).Length > MaximumFileBytes) throw new IOException("The saved history is too large. The existing file was preserved.");
        var entries = JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(path)) ?? throw new JsonException("Saved drafts must be a list.");
        if (entries.Any(item => !Valid(item))) throw new JsonException("Saved drafts contain an invalid record. The existing file was preserved.");
        return Bounded(entries);
    }
    public static void Save(string path, IEnumerable<HistoryItem> entries)
    {
        var records = entries.ToList();
        if (records.Any(item => !Valid(item))) throw new IOException("A draft exceeds the history limit or is invalid. The existing file was preserved.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Bounded(records), WriteOptions);
        if (bytes.Length > MaximumFileBytes) throw new IOException("History exceeds the 16 MiB storage limit. The existing file was preserved.");
        // Revalidate at the write boundary. A file damaged or made unavailable
        // since startup must not be replaced by an older in-memory snapshot.
        AtomicFile.WriteAllBytes(path, bytes, () =>
        {
            try { _ = Load(path); }
            catch (Exception ex) when (ex is JsonException or System.Security.SecurityException)
            { throw new IOException("Saved drafts could not be verified. The existing file was preserved.", ex); }
        });
    }
}
