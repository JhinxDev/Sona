using System.Text.Json;
namespace Sona.Core;

// Local speech workers share a bounded text reply. Parakeet readiness and
// authenticated inventory must identify the requested device and pinned model.
public static class SpeechProtocol
{
    public const int MaximumBytes = 1024 * 1024;
    public const string ParakeetVersion = "0.2.0";
    public const string ParakeetNativeId = "parakeet-tdt-0.6b-v3.oss-align.q8_0";
    private static JsonDocument Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumBytes) throw new IOException("Local speech response exceeded its limit.");
        return JsonDocument.Parse(bytes);
    }
    public static string Text(byte[] bytes)
    {
        using var json = Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("text", out var value) || value.ValueKind != JsonValueKind.String)
            throw new IOException("Local speech returned an invalid text response.");
        var text = value.GetString() ?? "";
        if (text.Length > 24000) throw new IOException("Local speech text exceeded its limit.");
        return text.Trim();
    }
    public static bool ParakeetReady(byte[] bytes, bool cuda)
    {
        using var json = Parse(bytes); var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("ready", out var ready) || ready.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("device", out var device) || device.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("capabilities", out var caps) || caps.ValueKind != JsonValueKind.Array) return false;
        var target = device.GetString();
        var matches = cuda ? string.Equals(target, "CUDA0", StringComparison.OrdinalIgnoreCase) || string.Equals(target, "cuda:0", StringComparison.OrdinalIgnoreCase) : string.Equals(target, "cpu", StringComparison.OrdinalIgnoreCase);
        return matches && caps.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.String && c.GetString() == "asr");
    }
    public static bool ParakeetIdentity(byte[] versionBytes, byte[] inventoryBytes)
    {
        using var version = Parse(versionBytes); using var inventory = Parse(inventoryBytes);
        return version.RootElement.ValueKind == JsonValueKind.Object && version.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() == ParakeetVersion &&
            inventory.RootElement.ValueKind == JsonValueKind.Object && inventory.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.EnumerateArray().Any(m =>
                m.ValueKind == JsonValueKind.Object && m.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() == ParakeetNativeId &&
                m.TryGetProperty("capability", out var capability) && capability.ValueKind == JsonValueKind.String && capability.GetString() == "transcription");
    }
}
