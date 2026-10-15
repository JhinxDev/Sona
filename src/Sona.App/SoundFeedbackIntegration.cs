using Sona.App.Audio;

namespace Sona.App;

public partial class MainWindow
{
    private readonly SoundFeedback soundFeedback = new();
    internal Func<FeedbackCue, Task>? FeedbackForTests { get; set; }
    private Task PlayFeedbackAsync(FeedbackCue cue, CancellationToken cancellation = default)
    {
        if (!preferences.SoundEffects || !preferencesLoad.CanSave || windowClosed || shutdownPending || recorder != null) return Task.CompletedTask;
        if (arguments.Contains("--test-ui")) return FeedbackForTests?.Invoke(cue) ?? Task.CompletedTask;
        return soundFeedback.PlayAsync(cue, cancellation);
    }
}
