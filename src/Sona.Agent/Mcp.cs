using System.Text;
using System.Text.Json;
using Murmur.Core;

internal static class Mcp
{
    private static readonly Dictionary<string, string> Methods = new()
    {
        ["sona_status"] = "status", ["sona_settings_get"] = "settings.get", ["sona_settings_patch"] = "settings.patch",
        ["sona_models_list"] = "models.list", ["sona_models_info"] = "models.info", ["sona_models_install"] = "models.install",
        ["sona_models_verify"] = "models.verify", ["sona_models_cancel"] = "models.cancel", ["sona_models_load"] = "models.load", ["sona_models_unload"] = "models.unload", ["sona_protection_status"] = "protection.status", ["sona_protection_pause"] = "protection.pause", ["sona_protection_resume"] = "protection.resume"
    };
    public static async Task RunAsync()
    {
        Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
        bool initialized = false;
        while (true)
        {
            var line = await BoundedLineAsync(); if (line == null) return;
            JsonElement? id = null;
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("jsonrpc", out var protocol) || protocol.GetString() != "2.0") { Error(null, -32600, "Invalid request."); continue; }
                if (root.TryGetProperty("id", out var value)) id = value.Clone();
                if (id is { ValueKind: not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.Null) }) { Error(null, -32600, "Invalid request ID."); continue; }
                var method = root.GetProperty("method").GetString();
                if (id == null) continue;
                var parameters = root.TryGetProperty("params", out var p) ? p : JsonSerializer.SerializeToElement(new { });
                if (method == "initialize")
                {
                    var requested = parameters.TryGetProperty("protocolVersion", out var version) ? version.GetString() : null;
                    var selected = requested is "2025-11-25" or "2025-06-18" or "2025-03-26" ? requested : "2025-11-25";
                    initialized = true;
                    Result(id, new { protocolVersion = selected, capabilities = new { tools = new { listChanged = false } }, serverInfo = new { name = "sona", version = typeof(Mcp).Assembly.GetName().Version?.ToString(3) } });
                }
                else if (method == "ping") Result(id, new { });
                else if (!initialized) Error(id, -32000, "Initialize first.");
                else if (method == "tools/list") Result(id, new { tools = Methods.Select(pair => new { name = pair.Key, description = Describe(pair.Value), inputSchema = Schema(pair.Value), annotations = new { readOnlyHint = pair.Value is "status" or "settings.get" or "models.list" or "models.info" or "models.verify" or "protection.status", destructiveHint = false, openWorldHint = pair.Value == "models.install" || pair.Value.StartsWith("protection.") && pair.Value != "protection.status" } }).ToArray() });
                else if (method == "tools/call")
                {
                    var name = parameters.GetProperty("name").GetString();
                    if (name == null || !Methods.TryGetValue(name, out var operation)) { Error(id, -32602, "Unknown tool."); continue; }
                    var arguments = parameters.TryGetProperty("arguments", out var a) ? a : JsonSerializer.SerializeToElement(new { });
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(16));
                    AgentReply reply;
                    try { reply = await AgentProtocol.SendAsync(operation, arguments, timeout.Token); }
                    catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException) { reply = new(false, Error: "Sona is unavailable or the operation timed out."); }
                    Result(id, new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(reply, AgentProtocol.Json) } }, structuredContent = reply, isError = !reply.Ok });
                }
                else Error(id, -32601, "Unknown method.");
            }
            catch (JsonException) { Error(id, -32700, "Invalid JSON."); }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentException) { Error(id, -32602, "Invalid parameters."); }
        }
    }
    private static async Task<string?> BoundedLineAsync()
    {
        var builder = new StringBuilder(); var one = new char[1]; bool overflow = false;
        while (await Console.In.ReadAsync(one) != 0)
        {
            if (one[0] == '\n') { if (overflow) { Error(null, -32600, "Message exceeds size limit."); builder.Clear(); overflow = false; continue; } return builder.ToString().TrimEnd('\r'); }
            if (builder.Length >= AgentProtocol.MaximumBytes) overflow = true;
            if (!overflow) builder.Append(one[0]);
        }
        if (overflow) { Error(null, -32600, "Message exceeds size limit."); return null; }
        return builder.Length == 0 ? null : builder.ToString();
    }
    private static string Describe(string method) => method switch
    {
        "status" => "Read local Sona status. No transcripts, credentials or microphone control.",
        "settings.get" => "Read the supported Sona settings.",
        "settings.patch" => "Change whitelisted local settings while Sona is idle. Unknown values are rejected.",
        "models.list" => "List pinned multilingual local models, file sizes and measured benchmark notes.",
        "models.load" => "Prepare the selected local model in memory without recording audio.",
        "models.unload" => "Release the resident local model without deleting downloaded files.",
        "models.info" => "Read exact model metadata, license, source revision and installed state.",
        "models.install" => "Explicitly download one pinned model from Hugging Face. Check status for progress and use models.cancel to cancel.",
        "models.verify" => "Check an existing model against its pinned size and SHA256.",
        "protection.status" => "Read local Discord protection status without credentials.",
        "protection.pause" => "Pause Discord protection and restore only Sona-owned voice changes.",
        "protection.resume" => "Resume Discord protection and reconcile currently active watched microphones.",
        _ => "Cancel the current model download and remove its partial data."
    };
    private static object Schema(string method)
    {
        if (method is "models.info" or "models.install" or "models.verify") return new { type = "object", properties = new { id = new { type = "string", @enum = ModelCatalog.All.Select(m => m.Id).ToArray() } }, required = new[] { "id" }, additionalProperties = false };
        if (method == "settings.patch") return new { type = "object", properties = new
        {
            language = new { type = "string", @enum = new[] { "auto", "en", "fr" } }, engine = new { type = "string", @enum = new[] { "Auto", "FastCpu" } },
            model = new { type = "string", @enum = ModelCatalog.All.Select(m => m.Id).Prepend("auto").ToArray() }, shortcutMode = new { type = "string", @enum = new[] { "Hold", "Toggle" } },
            theme = new { type = "string", @enum = ThemeCatalog.All.Select(t => t.Id).ToArray() }, recordingDisplay = new { type = "string", @enum = new[] { "Panel", "Capsule" } }, modelIdleMinutes = new { type = "integer", minimum = 0, maximum = 240 },
            style = new { type = "string", @enum = new[] { "Clean", "Verbatim", "Notes" } }, device = new { type = "integer", minimum = -1, maximum = 255 },
            autoInsert = new { type = "boolean" }, copyToClipboard = new { type = "boolean" }, closeToTray = new { type = "boolean" }, protectionEnabled = new { type = "boolean" }, silenceIncoming = new { type = "boolean" }, protectionReleaseDelayMs = new { type = "integer", minimum = 0, maximum = 5000 }, protectionShortcut = new { type = "string", maxLength = 64 }, watchedApps = new { type = "array", maxItems = 50, items = new { type = "string", maxLength = 160 } }
        }, additionalProperties = false };
        return new { type = "object", properties = new { }, additionalProperties = false };
    }
    private static void Result(JsonElement? id, object result) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, AgentProtocol.Json));
    private static void Error(JsonElement? id, int code, string message) => Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } }, AgentProtocol.Json));
}
