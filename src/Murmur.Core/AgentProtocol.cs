using System.IO.Pipes;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Murmur.Core;

public sealed record AgentRequest(string Method, JsonElement Parameters);
public sealed record AgentReply(bool Ok, object? Data = null, string? Error = null);
public static class AgentProtocol
{
    public const int MaximumBytes = 65536;
    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public static string PipeName
    {
        get
        {
            var scope = Environment.GetEnvironmentVariable("MURMUR_DATA") ?? "production";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + ":" + scope)))[..20];
            return "Sona-Agent-" + hash;
        }
    }
    public static async Task WriteAsync<T>(Stream stream, T value, CancellationToken cancellation)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > MaximumBytes) throw new IOException("Agent message exceeded its size limit.");
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellation); await stream.WriteAsync(bytes, cancellation); await stream.FlushAsync(cancellation);
    }
    public static async Task<T> ReadAsync<T>(Stream stream, CancellationToken cancellation)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, cancellation);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length < 1 || length > MaximumBytes) throw new IOException("Invalid agent message size.");
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, cancellation);
        return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new JsonException("Missing message.");
    }
    public static async Task<AgentReply> SendAsync(string method, JsonElement parameters, CancellationToken cancellation)
    {
        await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(2000, cancellation);
        await WriteAsync(pipe, new AgentRequest(method, parameters), cancellation);
        return await ReadAsync<AgentReply>(pipe, cancellation);
    }
}

public static class AgentSettings
{
    // No credentials, transcripts, microphone start, paths or startup registration.
    public static object View(Preferences p) => new { p.Language, p.Engine, p.Model, p.ShortcutMode, p.Style, p.Device, p.AutoInsert, p.CloseToTray, p.ProtectionEnabled, p.SilenceIncoming, p.ProtectionReleaseDelayMs, p.ProtectionShortcut, p.WatchedApps };
    public static Preferences Apply(Preferences current, JsonElement patch)
    {
        if (patch.ValueKind != JsonValueKind.Object || patch.EnumerateObject().Count() > 13) throw new ArgumentException("Settings must be a bounded JSON object.");
        var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(current))!;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in patch.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new ArgumentException("Duplicate setting.");
            switch (property.Name)
            {
                case "language": next.Language = Choice(property.Value, "auto", "en", "fr"); break;
                case "engine": next.Engine = Choice(property.Value, "Auto", "FastCpu"); break;
                case "model": next.Model = Choice(property.Value, ModelCatalog.All.Select(m => m.Id).Prepend("auto").ToArray()); break;
                case "shortcutMode": next.ShortcutMode = Choice(property.Value, "Hold", "Toggle"); break;
                case "style": next.Style = Choice(property.Value, "Clean", "Verbatim", "Notes"); break;
                case "device": if (!property.Value.TryGetInt32(out var device) || device is < -1 or > 255) throw new ArgumentException("Invalid device."); next.Device = device; break;
                case "autoInsert": next.AutoInsert = Boolean(property.Value); break;
                case "closeToTray": next.CloseToTray = Boolean(property.Value); break;
                case "protectionEnabled": next.ProtectionEnabled = Boolean(property.Value); break;
                case "silenceIncoming": next.SilenceIncoming = Boolean(property.Value); break;
                case "protectionShortcut": if (property.Value.ValueKind != JsonValueKind.String || !ProtectionShortcut.TryParse(property.Value.GetString(), out _, out _)) throw new ArgumentException("Invalid protection shortcut."); next.ProtectionShortcut = property.Value.GetString()!.Trim(); break;
                case "protectionReleaseDelayMs": if (!property.Value.TryGetInt32(out var delay) || delay is < 0 or > 5000) throw new ArgumentException("Invalid protection delay."); next.ProtectionReleaseDelayMs = delay; break;
                case "watchedApps":
                    if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() > 50) throw new ArgumentException("Invalid watched apps.");
                    var apps = property.Value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new ArgumentException("Invalid watched app.")).ToArray();
                    if (apps.Any(a => string.IsNullOrWhiteSpace(a) || a.Length > 160 || a.Any(char.IsControl) || a.Contains('/') || a.Contains('\\'))) throw new ArgumentException("Invalid watched app.");
                    next.WatchedApps = apps.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); break;
                default: throw new ArgumentException("Setting is unsupported.");
            }
        }
        return next;
    }
    private static bool Boolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw new ArgumentException("A setting requires a boolean.");
    private static string Choice(JsonElement value, params string[] values) => value.ValueKind == JsonValueKind.String && values.Contains(value.GetString()) ? value.GetString()! : throw new ArgumentException("A setting has an invalid choice.");
}
