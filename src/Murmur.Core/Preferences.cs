using System.Text.Json;
namespace Murmur.Core;

public sealed record Replacement(string Spoken, string Written);
public sealed record Snippet(string Trigger, string Expansion);
public sealed class Preferences
{
    public string Language { get; set; } = "auto";
    public string Engine { get; set; } = "Auto";
    public string ShortcutMode { get; set; } = "Hold";
    public bool KeepHistory { get; set; }
    public string Style { get; set; } = "Clean";
    public int Device { get; set; } = -1;
    public bool AutoInsert { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool Startup { get; set; }
    public List<string> Terms { get; set; } = [];
    public List<Replacement> Vocabulary { get; set; } = [new("codex", "Codex"), new("jhinx", "Jhinx"), new("github", "GitHub"), new("next cloud", "Nextcloud"), new("power shell", "PowerShell")];
    public List<Snippet> Snippets { get; set; } = [new("review my changes", "Review the current changes for correctness, accessibility, and regressions. Preserve unrelated work.")];
    public static Preferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        try { var settings = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new(); settings.Normalize(); return settings; }
        catch (Exception ex) when (ex is JsonException or IOException) { return new(); }
    }
    public void Normalize()
    {
        if (Language is not ("auto" or "en" or "fr")) Language = "auto";
        if (Engine is not ("Auto" or "FastCpu")) Engine = "Auto";
        if (ShortcutMode is not ("Hold" or "Toggle")) ShortcutMode = "Hold";
        if (Style is not ("Clean" or "Verbatim" or "Notes")) Style = "Clean";
        if (Device < -1 || Device > 255) Device = -1;
        Terms = (Terms ?? []).Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 120).Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        Vocabulary = (Vocabulary ?? new Preferences().Vocabulary).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Spoken) && !string.IsNullOrWhiteSpace(x.Written) && x.Spoken.Length <= 120 && x.Written.Length <= 120).DistinctBy(x => x.Spoken, StringComparer.OrdinalIgnoreCase).Take(200).ToList();
        Snippets = (Snippets ?? new Preferences().Snippets).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Trigger) && !string.IsNullOrWhiteSpace(x.Expansion) && x.Trigger.Length <= 120 && x.Expansion.Length <= 4000).DistinctBy(x => x.Trigger, StringComparer.OrdinalIgnoreCase).Take(200).ToList();
    }
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
