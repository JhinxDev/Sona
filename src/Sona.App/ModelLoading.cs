using System.Windows.Threading;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

internal enum ModelPurpose { Speech, Cleanup }

public partial class MainWindow
{
    internal Func<ModelPurpose, ModelLoadStage>? ModelLoadStageForTests { get; set; }
    private ModelLoadStage GetModelLoadStage(ModelPurpose purpose) => arguments.Contains("--test-ui") && ModelLoadStageForTests != null
        ? ModelLoadStageForTests(purpose) : purpose == ModelPurpose.Speech ? transcriber.LoadStage : cleanupModel.LoadStage;

    private static string? LoadingHint(ModelPurpose purpose, ModelLoadStage stage)
    {
        var name = purpose == ModelPurpose.Speech ? "Speech" : "Cleanup";
        return stage switch
        {
            ModelLoadStage.Verifying => $"Checking {name.ToLowerInvariant()} model · Esc cancels",
            ModelLoadStage.Loading => $"{name} model into memory · Esc cancels",
            _ => null
        };
    }

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
            var label = stage == ModelLoadStage.Verifying ? "Checking model" : stage == ModelLoadStage.Loading ? "Loading model"
                : purpose == ModelPurpose.Speech ? "Transcribing" : "Polishing";
            var hint = LoadingHint(purpose, stage) ?? "Esc cancels";
            Status.Text = stage != ModelLoadStage.None ? (stage == ModelLoadStage.Verifying ? "Checking " : "Loading ") + (purpose == ModelPurpose.Speech ? "speech model" : "cleanup model")
                : purpose == ModelPurpose.Speech ? "Making your draft" : "Polishing your draft";
            StatusDetail.Text = hint;
            overlay.Update(label, hint);
        }
        var progress = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(80) };
        progress.Tick += (_, _) => Refresh();
        try
        {
            var work = run(); Refresh(); progress.Start();
            return await work;
        }
        finally { progress.Stop(); }
    }
}
