using System.Text.Json;
using System.Windows.Automation;
using Sona.App.Windows;

internal static class CapturePolicyChecks
{
    public static object Run()
    {
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string label) { if (value) checks.Add(label); else failures.Add(label); }
        var field = new TargetFieldMetadata(31001, ControlType.ListItem.Id, true, false, null, [1, 2, 3]);
        TargetCaptureResult Inspect(bool contained = true, long finalEpoch = 4) => Insertion.EvaluateCapture(field, 101, 31001, contained, 4, finalEpoch);
        var suggestion = Inspect();
        Check(suggestion.Target == null, "Selected suggestions never authorize typing");
        Check(suggestion.Status.ToString() == "SuggestionFocused", "A focused suggestion has a specific recoverable status");
        Check(suggestion.Detail.Contains("Copy draft", StringComparison.Ordinal) && !suggestion.Detail.Contains("caret", StringComparison.OrdinalIgnoreCase), "Suggestion guidance offers manual paste without another caret exercise");
        Check(Inspect(contained: false).Status == TargetCaptureStatus.OutsideForegroundWindow, "Window containment is checked before classifying an unsupported control");
        Check(Inspect(finalEpoch: 5).Status == TargetCaptureStatus.FocusChanged, "An unstable sample cannot be reported as a stable unsupported destination");
        var data = JsonSerializer.SerializeToElement(suggestion);
        Check(data.TryGetProperty("ForegroundWindow", out var window) && window.GetInt64() == 101, "The result identifies the exact inspected HWND");
        Check(data.TryGetProperty("FieldInForegroundWindow", out var contained) && contained.ValueKind == JsonValueKind.True, "The result distinguishes proved containment from unknown scope");
        Check(data.TryGetProperty("Stable", out var stable) && stable.ValueKind == JsonValueKind.True, "Stable rejected captures remain diagnosable");
        var drift = JsonSerializer.SerializeToElement(Inspect(finalEpoch: 5));
        Check(drift.TryGetProperty("Stable", out var unstable) && unstable.ValueKind == JsonValueKind.False, "Focus drift is retained as unstable metadata");
        Check(data.TryGetProperty("RuntimeIdentityHash", out var hash) && hash.GetString()?.Length == 64, "Rejected controls retain only a transient identity hash");
        Check(!JsonSerializer.Serialize(suggestion).Contains("[1,2,3]", StringComparison.Ordinal), "Rejected capture metadata does not export raw runtime IDs");
        return new { Checks = checks, Failures = failures, MicrophoneOpened = false, GlobalInputSent = false };
    }
}
