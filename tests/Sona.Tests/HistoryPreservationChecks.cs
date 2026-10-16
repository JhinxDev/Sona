using Sona.Core;

internal static class HistoryPreservationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "sona-history-preservation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "saved-drafts.json");
        var draft = new HistoryItem(Guid.NewGuid(), DateTimeOffset.Now, "Synthetic original", "Keep this synthetic draft", Saved: true);
        try
        {
            foreach (var damaged in new[]
            {
                "{damaged after load", "null", "{\"unexpected\":true}", "[null]",
                System.Text.Json.JsonSerializer.Serialize(new[] { draft with { Id = Guid.Empty } }),
                System.Text.Json.JsonSerializer.Serialize(new[] { draft with { Draft = "" } })
            })
            {
                File.Delete(path);
                HistoryStore.Save(path, [draft]);
                var loaded = HistoryStore.Load(path);
                File.WriteAllText(path, damaged);
                var before = File.ReadAllBytes(path);
                var refused = false;
                try { HistoryStore.Save(path, loaded); }
                catch (IOException) { refused = true; }
                check(refused && File.ReadAllBytes(path).SequenceEqual(before), "A history save preserves an existing file damaged after startup: " + damaged[..Math.Min(12, damaged.Length)]);
                check(loaded.Single() == draft && Directory.GetFiles(directory, "*.tmp").Length == 0, "A refused history save retains session records and removes its temporary file");
            }
            File.Delete(path);
            using (var oversized = File.Create(path)) oversized.SetLength(16L * 1024 * 1024 + 1);
            var refusedSize = false;
            try { HistoryStore.Save(path, [draft]); }
            catch (IOException) { refusedSize = true; }
            check(refusedSize && new FileInfo(path).Length == 16L * 1024 * 1024 + 1, "Saving cannot replace an oversized existing history file");
            File.Delete(path);
            HistoryStore.Save(path, [draft]);
            var updated = draft with { Draft = "An intentional synthetic correction" };
            HistoryStore.Save(path, [updated]);
            check(HistoryStore.Load(path).Single() == updated, "An intact history file still accepts an intentional update");
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
