using System.Text.Json;
using Murmur.Core;

if (args.Contains("--mcp")) { await Mcp.RunAsync(); return; }
if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("sona-agent status | doctor | settings get | settings patch <JSON> | models list | models info/install/verify <id> | models cancel/load/unload | protection status/pause/resume | request <method> [JSON] | --mcp\nAll results are JSON. Requires running Sona. Local current-user pipe; no account or microphone control."); return;
}
try
{
    var command = args.Where(a => a != "--json").ToArray();
    string method; string json = "{}";
    if (command[0] is "status" or "doctor") method = "status";
    else if (command[0] == "request" && command.Length is 2 or 3) { method = command[1]; if (command.Length == 3) json = command[2]; }
    else if (command[0] == "settings" && command.Length >= 2) { method = "settings." + command[1]; if (command[1] == "patch" && command.Length == 3) json = command[2]; else if (command[1] != "get") throw new ArgumentException(); }
    else if (command[0] == "models" && command.Length is 2 or 3) { method = "models." + command[1]; if (command.Length == 3) json = JsonSerializer.Serialize(new { id = command[2] }); }
    else if (command[0] == "protection" && command.Length == 2) method = "protection." + command[1];
    else throw new ArgumentException();
    if (json.Length > AgentProtocol.MaximumBytes) throw new ArgumentException();
    using var document = JsonDocument.Parse(json); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(16));
    var reply = await AgentProtocol.SendAsync(method, document.RootElement, timeout.Token);
    Console.WriteLine(JsonSerializer.Serialize(reply, AgentProtocol.Json)); Environment.ExitCode = reply.Ok ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException or ArgumentException)
{
    Console.WriteLine(JsonSerializer.Serialize(new AgentReply(false, Error: "Request failed. Check arguments and that Sona is running."), AgentProtocol.Json)); Environment.ExitCode = 1;
}
