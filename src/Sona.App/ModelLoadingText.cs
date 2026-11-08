using System.Globalization;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

/// <summary>
/// Exact model-loading copy. Every value comes from the real loader state and a
/// measured stopwatch: the selected model's own catalog name, the actual stage
/// (file verification then worker memory load), and elapsed seconds. There are no
/// invented percentages, random phrases or fake delays.
/// </summary>
internal static class ModelLoadingText
{
    internal const string CleanupModelName = "Spokenly Cleanup 1";

    internal static string ModelName(ModelPurpose purpose, Preferences preferences) =>
        purpose == ModelPurpose.Speech ? ModelCatalog.Get(Audio.Transcriber.SelectedModel(preferences)).Name : CleanupModelName;

    internal static string Hint(ModelLoadStage stage, string modelName, double elapsedSeconds) => stage switch
    {
        ModelLoadStage.Verifying => "Checking " + modelName + " files · " + Seconds(elapsedSeconds) + " · Esc cancels",
        ModelLoadStage.Loading => "Loading " + modelName + " · " + Seconds(elapsedSeconds) + " · Esc cancels",
        _ => ""
    };

    internal static string Status(ModelPurpose purpose, ModelLoadStage stage, string modelName) => stage switch
    {
        ModelLoadStage.Verifying => "Checking " + modelName,
        ModelLoadStage.Loading => "Loading " + modelName,
        _ => purpose == ModelPurpose.Speech ? "Making your draft" : "Polishing your draft"
    };

    internal static string Ready(string modelName, double seconds) => modelName + " loaded in " + Seconds(seconds);

    private static string Seconds(double value) =>
        Math.Max(0, double.IsFinite(value) ? value : 0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
}
