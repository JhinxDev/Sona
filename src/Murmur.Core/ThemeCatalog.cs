using System.Collections.ObjectModel;

namespace Murmur.Core;

public sealed record ThemeDefinition(string Id, string Label, bool IsDark, IReadOnlyDictionary<string, string> Colors)
{
    public string Group => IsDark ? "Dark" : "Light";
    public bool UsesGlassMaterial => Id == "amethyst-glass";
    public override string ToString() => Label;
}

/// <summary>Original Sona UI palettes. Pastel and cool families draw on Catppuccin, Nord and Dracula's documented design patterns.</summary>
public static class ThemeCatalog
{
    public const string DefaultId = "charcoal";
    public static IReadOnlyList<ThemeDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        Palette("charcoal", "Charcoal", true, "1F1F1F", "2B2B2D", "29292B", "797983", "F1F1F4", "B8B8C2", "6DC4FF", "006CCB", "203D60", "D4ECFF", "353B43"),
        Palette("amethyst", "Amethyst", true, "24212D", "302C3C", "292533", "877D99", "F3EFFA", "C0B7CF", "C4A6FF", "7050AC", "45345F", "ECDFFF", "3B3449"),
        Palette("amethyst-glass", "Amethyst Glass", true, "171321", "2D233F", "201A2D", "8872AA", "FAF7FF", "CEC3DF", "C4A6FF", "794BC4", "53327C", "FCF9FF", "3D2B57"),
        Palette("fjord", "Fjord", true, "272F3B", "333E4C", "2D3744", "8092A5", "EBF1F7", "B6C6D7", "91CEDD", "306B83", "294B5E", "D6F2F8", "3E4D5D"),
        Palette("mocha", "Mocha", true, "242330", "302F40", "292838", "86819A", "EEEAF7", "C1BDD3", "E0B4CD", "8A466D", "52374E", "F7DEEE", "3D3749"),
        Palette("oled", "OLED", true, "09090C", "15151B", "101016", "66667B", "F4F4FA", "BDBDCB", "96B8FF", "315EB8", "1B2C50", "E3ECFF", "242430"),
        Palette("paper", "Paper", false, "F5F6F8", "FFFFFF", "F0F2F5", "7A8290", "222831", "515B68", "075DA9", "075DA9", "DCEBFA", "144D83", "E6EAF0"),
        Palette("linen", "Linen", false, "F6F2EA", "FFFCF6", "F0EADF", "8E8375", "302B25", "62584D", "805135", "805135", "EDDFCF", "684027", "EBE3D7"),
        Palette("mist", "Mist", false, "EEF3F6", "F9FCFE", "E7EEF3", "788A99", "243541", "4D6271", "235F79", "235F79", "D3E6EF", "1E536B", "DDE8EF")
    });

    /// <summary>Unknown or legacy IDs safely resolve to the accepted default.</summary>
    public static ThemeDefinition Resolve(string? id) => All.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    private static ThemeDefinition Palette(string id, string label, bool dark, string canvas, string panel, string inset,
        string line, string ink, string muted, string accent, string primary, string selection, string selectedInk, string hover)
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Canvas"] = canvas, ["Sidebar"] = canvas, ["Panel"] = panel, ["Inset"] = inset,
            ["Line"] = line, ["Ink"] = ink, ["Muted"] = muted, ["Accent"] = accent,
            ["Button"] = inset, ["Primary"] = primary, ["PrimaryInk"] = "FFFFFF", ["Hover"] = hover,
            ["Selection"] = selection, ["SelectedInk"] = selectedInk, ["Focus"] = accent,
            ["SwitchOff"] = line, ["SwitchKnob"] = "FFFFFF", ["SwitchKnobOn"] = dark ? canvas : "FFFFFF", ["ScrollThumb"] = line,
            ["Success"] = dark ? "9BDEAD" : "276339", ["Warning"] = dark ? "F0CF8D" : "795515",
            ["Danger"] = dark ? "FFA9B1" : "A5293D"
        };
        return new ThemeDefinition(id, label, dark, new ReadOnlyDictionary<string, string>(colors.ToDictionary(pair => pair.Key, pair => "#" + pair.Value, StringComparer.Ordinal)));
    }
}
