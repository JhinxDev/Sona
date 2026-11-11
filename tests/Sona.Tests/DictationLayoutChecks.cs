using Sona.Core;

internal static class DictationLayoutChecks
{
    public static async Task RunAsync(Action<bool, string> Check)
    {
        var clean = new Preferences();
        const string prose = "I printed the sample.\nThe labels are faint.\nThe width measures 19.41 millimeters.";
        const string paragraph = "I printed the sample. The labels are faint. The width measures 19.41 millimeters.";
        Check(TextPipeline.Apply(prose, clean) == paragraph, "Clean joins transcript sentence lines into a paragraph");
        Check(TextPipeline.Apply(prose.Replace("\n", "\r\n"), clean) == paragraph, "Windows transcript line endings produce the same paragraph");
        Check(TextPipeline.Apply("First topic.\nStill first.\n\nSecond topic.\nStill second.", clean) == "First topic. Still first.\n\nSecond topic. Still second.", "Intentional blank-line paragraphs survive");
        const string list = "Check these sizes:\n- 15 mm\n- 20 mm\n\nThen compare them.";
        Check(TextPipeline.Apply(list, clean) == list, "Bullets keep their own lines and surrounding prose");
        const string numbered = "1. Check the label.\n2. Measure 19.41 mm.";
        Check(TextPipeline.Apply(numbered, clean) == numbered, "Numbered steps remain a list");
        const string code = "Use this:\n```python\nif ready:\n    print('um github')\n```\nThen check it.";
        Check(TextPipeline.Apply(code, clean) == code, "Fenced code and its indentation remain exact");
        Check(TextPipeline.Apply("Keep `first\nsecond` exactly.\nThen continue.", clean) == "Keep `first\nsecond` exactly. Then continue.", "Literal newlines survive prose formatting");
        Check(TextPipeline.Apply("First sentence. New paragraph Second sentence. New line Last line.", clean) == "First sentence.\n\nSecond sentence.\nLast line.", "Clean honors explicit paragraph and line commands");
        Check(TextPipeline.Apply("Bonjour. Nouveau paragraphe Ensuite. Nouvelle ligne Fin.", clean) == "Bonjour.\n\nEnsuite.\nFin.", "French layout commands use the same clean layout");
        Check(TextPipeline.Apply(prose, new Preferences { Style = "Verbatim" }) == prose, "Verbatim retains transcript line breaks");
        Check(TextPipeline.Apply("First.\nSecond.", new Preferences { Style = "Notes" }) == "First.\nSecond.", "Notes retains concise note lines");
        var longText = string.Join(" ", Enumerable.Repeat("The printed label stays readable after measuring the sample.", 24));
        var longDraft = TextPipeline.Apply(longText, clean);
        Check(longDraft.Contains("\n\n") && longDraft.Replace("\n\n", " ") == longText, "Long prose gains readable paragraphs without changing any words");
        var snippet = new Preferences { Snippets = [new("fixture text", "First.\nSecond.")] };
        Check(TextPipeline.Apply("fixture text", snippet) == "First.\nSecond.", "Exact snippet layout bypasses paragraph formatting");
        var p = new Preferences { CleanupEnabled = true, Terms = ["Sona"] };
        var rejected = await CleanupProcessor.RunAsync("Sona is ready.\nKeep it here.", p, "Codex", (_, _) => Task.FromResult("Another app is ready."), CancellationToken.None);
        Check(!rejected.Applied && rejected.Text == "Sona is ready. Keep it here.", "Rejected model output still receives safe paragraph formatting");
        var bounded = await CleanupProcessor.RunAsync(prose + "\n" + new string('a', 4001), p, "Codex", (_, _) => throw new Exception("Oversized input called the model"), CancellationToken.None);
        Check(!bounded.Applied && !bounded.Text.Contains('\n'), "Over-limit model bypass still formats ordinary prose");
        var accepted = await CleanupProcessor.RunAsync(prose, p, "Codex", (prompt, _) => Task.FromResult(prompt.Split("\ntext: ", 2)[1].Split("\nout:\n", 2)[0]), CancellationToken.None);
        Check(accepted.Applied && accepted.Text == paragraph, "Accepted local cleanup receives the same final paragraph layout");
        var numbers = CleanupRules.Prepare("The target is 20 mm, not 15 mm. I measured 19.41 mm.", p, "Codex")!;
        var masked = numbers.Prompt.Split("\ntext: ", 2)[1].Split("\nout:\n", 2)[0];
        Check(!masked.Contains("20") && !masked.Contains("15") && !masked.Contains("19.41"), "Numeric measurements are protected before model cleanup");
        Check(numbers.TryRestore(masked, out var restored) && restored.Contains("20 mm, not 15 mm") && restored.Contains("19.41 mm"), "Numeric measurements restore exactly");
        Check(!numbers.TryRestore(masked.Replace("[TERM_0]", "21"), out _), "A model cannot change a protected measurement");
    }
}
