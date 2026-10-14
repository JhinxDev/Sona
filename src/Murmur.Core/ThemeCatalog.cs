using System.Collections.ObjectModel;

namespace Murmur.Core;

public sealed record ThemeDefinition(string Id, string Label, bool IsDark, IReadOnlyDictionary<string, string> Colors)
{
    public string Group => IsDark ? "Dark" : "Light";
    /// <summary>Every selectable palette uses the shared glass material treatment.</summary>
    public bool UsesGlassMaterial => true;
    public override string ToString() => Label;
}

/// <summary>Original Sona UI palettes. Pastel and cool families draw on Catppuccin, Nord and Dracula's documented design patterns.</summary>
public static class ThemeCatalog
{
    public const string DefaultId = "charcoal";
    public static IReadOnlyList<ThemeDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        Palette("charcoal", "Charcoal", true, "1F1F1F", "2B2B2D", "29292B", "797983", "F1F1F4", "B8B8C2", "6DC4FF", "006CCB", "203D60", "D4ECFF", "353B43"),
        Palette("amethyst", "Amethyst", true, "09070E", "181320", "0D0B12", "655775", "FAF7FF", "C3BCD0", "C4A6FF", "68418E", "3B2856", "FCF9FF", "241C31"),
        Palette("fjord", "Fjord", true, "272F3B", "333E4C", "2D3744", "8092A5", "EBF1F7", "B6C6D7", "91CEDD", "306B83", "294B5E", "D6F2F8", "3E4D5D"),
        Palette("mocha", "Mocha", true, "242330", "302F40", "292838", "86819A", "EEEAF7", "C1BDD3", "E0B4CD", "8A466D", "52374E", "F7DEEE", "3D3749"),
        Palette("oled", "OLED", true, "09090C", "15151B", "101016", "66667B", "F4F4FA", "BDBDCB", "96B8FF", "315EB8", "1B2C50", "E3ECFF", "242430"),
        Palette("paper", "Paper", false, "F6F7F5", "FFFFFF", "EFF1F1", "7C858D", "252B30", "58636A", "075DA9", "075DA9", "DCE8F0", "204E70", "E7ECEF"),
        Palette("linen", "Linen", false, "F6F2EA", "FFFCF6", "F0EADF", "8E8375", "302B25", "62584D", "805135", "805135", "EDDFCF", "684027", "EBE3D7"),
        Palette("mist", "Mist", false, "EAF3F1", "F6FBF9", "E3EFEC", "738A84", "233732", "526B65", "2C7369", "2C7369", "D3E9E3", "1E554D", "DCEBE6")
    });

    /// <summary>Unknown IDs resolve to the accepted default; the former glass palette remains a saved-setting alias.</summary>
    public static ThemeDefinition Resolve(string? id)
    {
        if (string.Equals(id, "amethyst-glass", StringComparison.OrdinalIgnoreCase))
            return All.First(theme => theme.Id == "amethyst");
        return All.FirstOrDefault(theme => string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
    }

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
