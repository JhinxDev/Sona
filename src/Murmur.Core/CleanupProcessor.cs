namespace Murmur.Core;

public sealed record CleanupOutput(string Text, bool Applied, string Outcome);
public static class CleanupProcessor
{
    public static async Task<CleanupOutput> RunAsync(string original, Preferences settings, string app,
        Func<string, CancellationToken, Task<string>> runModel, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var basic = TextPipeline.Apply(original, settings);
        var snippet = settings.Snippets.Any(s => s.Trigger.Trim().Equals(original.Trim().TrimEnd('.', '!', '?'), StringComparison.OrdinalIgnoreCase));
        if (!settings.CleanupEnabled || settings.Style == "Verbatim" || snippet) return new(basic, false, "bypassed");
        var request = CleanupRules.Prepare(original, settings, app);
        if (request == null) return new(original.Trim(), false, "preserved");
        try
        {
            var output = await runModel(request.Prompt, token);
            token.ThrowIfCancellationRequested();
            if (!request.TryRestore(output, out var restored)) return new(original.Trim(), false, "preserved");
            return new(TextPipeline.Apply(restored, settings), true, "polished");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is IOException or System.Net.Http.HttpRequestException or System.Text.Json.JsonException or InvalidOperationException)
        { token.ThrowIfCancellationRequested(); return new(basic, false, "unavailable"); }
    }
}
