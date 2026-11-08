using System.Diagnostics;
using System.Windows.Threading;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

internal enum ModelPurpose { Speech, Cleanup }

public partial class MainWindow
{
    internal Func<ModelPurpose, ModelLoadStage>? ModelLoadStageForTests { get; set; }
    // Real stopwatch tracking per purpose. A note is produced only when a genuine
    // cold load completed, and it is cleared when the next load starts or a new
    // dictation session begins.
    private readonly long[] loadStartedAt = new long[2];
    private readonly string?[] loadReadyNote = new string?[2];
    private readonly bool[] loadReadyTaken = new bool[2];

    private ModelLoadStage GetModelLoadStage(ModelPurpose purpose) => arguments.Contains("--test-ui") && ModelLoadStageForTests != null
        ? ModelLoadStageForTests(purpose) : purpose == ModelPurpose.Speech ? transcriber.LoadStage : cleanupModel.LoadStage;

    private double TrackModelLoad(ModelPurpose purpose, ModelLoadStage stage)
    {
        var index = (int)purpose;
        var now = Stopwatch.GetTimestamp();
        if (stage != ModelLoadStage.None)
        {
            if (loadStartedAt[index] == 0) loadStartedAt[index] = now;
            loadReadyNote[index] = null; loadReadyTaken[index] = false;
            return Stopwatch.GetElapsedTime(loadStartedAt[index]).TotalSeconds;
        }
        if (loadStartedAt[index] != 0)
        {
            var seconds = Stopwatch.GetElapsedTime(loadStartedAt[index]).TotalSeconds;
            loadStartedAt[index] = 0;
            loadReadyNote[index] = ModelLoadingText.Ready(ModelLoadingText.ModelName(purpose, preferences), seconds);
            loadReadyTaken[index] = false;
        }
        return 0;
    }

    /// <summary>Returns the measured completion note once, so it never repeats.</summary>
    private string? TakeLoadReadyNote(ModelPurpose purpose)
    {
        var index = (int)purpose;
        if (loadReadyNote[index] is not { } note || loadReadyTaken[index]) return null;
        loadReadyTaken[index] = true;
        return note;
    }

    private void ResetModelLoadTracking()
    {
        Array.Clear(loadStartedAt); Array.Clear(loadReadyNote); Array.Clear(loadReadyTaken);
    }

    private string? LoadingHint(ModelPurpose purpose, ModelLoadStage stage, double elapsedSeconds) =>
        stage == ModelLoadStage.None ? null : ModelLoadingText.Hint(stage, ModelLoadingText.ModelName(purpose, preferences), elapsedSeconds);

    private async Task<T> WithModelProgressAsync<T>(Func<Task<T>> run, ModelPurpose purpose, CancellationToken token)
    {
        var owner = session;
        void Refresh()
        {
            // Startup and settings warmup never show a pill. An old, cancelled
            // preview cannot replace a newer session or a retained-draft notice.
            if (token.IsCancellationRequested || shutdownPending || windowClosed || owner == null ||
                !ReferenceEquals(owner, session) || owner.State != SessionState.Transcribing || !processing) return;
            var stage = GetModelLoadStage(purpose);
            var elapsed = TrackModelLoad(purpose, stage);
            var modelName = ModelLoadingText.ModelName(purpose, preferences);
            var label = stage == ModelLoadStage.Verifying ? "Checking model" : stage == ModelLoadStage.Loading ? "Loading model"
                : purpose == ModelPurpose.Speech ? "Transcribing" : "Polishing";
            string detail;
            if (stage != ModelLoadStage.None) detail = ModelLoadingText.Hint(stage, modelName, elapsed);
            else if (TakeLoadReadyNote(purpose) is { } note) detail = note + " · Esc cancels";
            else detail = "Esc cancels";
            Status.Text = ModelLoadingText.Status(purpose, stage, modelName);
            StatusDetail.Text = detail;
            overlay.Update(label, detail);
        }
        var progress = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(80) };
        progress.Tick += (_, _) => Refresh();
        try
        {
            var work = run(); Refresh(); progress.Start();
            var result = await work;
            Refresh();
            return result;
        }
        finally { progress.Stop(); }
    }
}
