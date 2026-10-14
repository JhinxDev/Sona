namespace Sona.Core;

public static class ProtectionShortcut
{
    public static bool TryParse(string? text, out uint modifiers, out uint key)
    {
        modifiers = 0x4000; key = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 64) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 5 || parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parts.Length) return false;
        foreach (var part in parts[..^1])
        {
            var flag = part.ToLowerInvariant() switch { "ctrl" or "control" => 2u, "shift" => 4u, "alt" => 1u, "win" or "windows" => 8u, _ => 0u };
            if (flag == 0 || (modifiers & flag) != 0) return false; modifiers |= flag;
        }
        var last = parts[^1].ToUpperInvariant();
        if (last.Length == 1 && char.IsAsciiLetterOrDigit(last[0])) key = last[0];
        else if (last.StartsWith('F') && int.TryParse(last[1..], out var f) && f is >= 1 and <= 24) key = (uint)(0x6F + f);
        else if (last == "SPACE") key = 0x20;
        return key != 0 && (modifiers & (1 | 2 | 8)) != 0;
    }
}
