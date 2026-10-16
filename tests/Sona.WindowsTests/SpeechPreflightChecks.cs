using System.Diagnostics;
using System.Security;
using Sona.App.Audio;
using Sona.Core;

internal static class SpeechPreflightChecks
{
    internal sealed record Report(string[] Checks, string[] Failures, bool MicrophoneOpened = false, bool WorkerStarted = false, bool ProductionProfileChanged = false);

    public static async Task<Report> RunAsync()
    {
        var checks = new List<string>(); var failures = new List<string>();
        var preferences = new Preferences { Model = "tiny", Engine = "FastCpu" };
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add(name); }
        void Released(ResidentModel model, string name) => Check(model.ProcessId == null && model.LoadedModel == null && !model.CleanupPending, name);
        async Task Case(string name, Func<Task> run)
        {
            try { await run(); }
            catch (Exception error) { failures.Add(name + ": " + error.GetType().Name + ": " + error.Message); }
        }

        foreach (var denied in new Exception[] { new UnauthorizedAccessException("Synthetic denied model read"), new SecurityException("Synthetic denied runtime read") })
        foreach (var recognition in new[] { false, true })
        {
            var label = (recognition ? "Recognition" : "Warmup") + " " + denied.GetType().Name;
            await Case(label, async () =>
            {
                var attempts = 0; var audio = Audio();
                var model = new ResidentModel(verifyModel: (_, _) => ++attempts == 1 ? Task.FromException<bool>(denied) : Task.FromResult(false));
                var error = await FailureAsync(recognition ? model.RunAsync(audio, preferences, CancellationToken.None) : model.WarmAsync(preferences, CancellationToken.None));
                Check(error is IOException && ReferenceEquals(error.InnerException, denied) && error.Message.Contains("access", StringComparison.OrdinalIgnoreCase), label + " becomes an actionable I/O failure");
                if (recognition) Check(audio.All(value => value == 0), label + " clears the input audio");
                Released(model, label + " leaves no worker or pending cleanup");
                var retry = await FailureAsync(model.WarmAsync(preferences, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)));
                Check(attempts == 2 && retry is IOException && retry.Message.Contains("Install and verify", StringComparison.Ordinal), label + " releases the gate for a fresh integrity check");
                await model.UnloadAsync().WaitAsync(TimeSpan.FromSeconds(1));
            });
        }

        foreach (var recognition in new[] { false, true })
        {
            var label = recognition ? "Recognition startup timeout" : "Warmup startup timeout";
            await Case(label, async () =>
            {
                var waiting = true; var verificationStopped = false; var attempts = 0;
                var model = new ResidentModel(verifyModel: async (_, token) =>
                {
                    attempts++;
                    if (!waiting) return false;
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, token); return false; }
                    finally { verificationStopped = true; }
                }, startupTimeout: TimeSpan.FromMilliseconds(75));
                using var rescue = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var audio = Audio(); var elapsed = Stopwatch.StartNew();
                var error = await FailureAsync(recognition ? model.RunAsync(audio, preferences, rescue.Token) : model.WarmAsync(preferences, rescue.Token));
                Check(error is IOException && error.Message.Contains("startup time limit", StringComparison.OrdinalIgnoreCase) && !rescue.IsCancellationRequested, label + " covers verification before the fixture rescue deadline");
                Check(verificationStopped && elapsed.Elapsed < TimeSpan.FromSeconds(2), label + " cancels and drains verification");
                if (recognition) Check(audio.All(value => value == 0), label + " clears the input audio");
                Released(model, label + " leaves no worker or pending cleanup");
                waiting = false;
                var retry = await FailureAsync(model.WarmAsync(preferences, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)));
                Check(attempts == 2 && retry is IOException && retry.Message.Contains("Install and verify", StringComparison.Ordinal), label + " allows another verification attempt");
            });
        }

        foreach (var recognition in new[] { false, true })
        {
            var label = recognition ? "Recognition owner cancellation" : "Warmup owner cancellation";
            await Case(label, async () =>
            {
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var waiting = true; var verificationStopped = false; var attempts = 0;
                var model = new ResidentModel(verifyModel: async (_, token) =>
                {
                    attempts++;
                    if (!waiting) return false;
                    entered.SetResult();
                    try { await Task.Delay(Timeout.InfiniteTimeSpan, token); return false; }
                    finally { verificationStopped = true; }
                }, startupTimeout: TimeSpan.FromSeconds(5));
                using var owner = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var audio = Audio();
                var operation = recognition ? model.RunAsync(audio, preferences, owner.Token) : model.WarmAsync(preferences, owner.Token);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(1)); owner.Cancel();
                var error = await FailureAsync(operation.WaitAsync(TimeSpan.FromSeconds(1)));
                Check(error is OperationCanceledException && verificationStopped, label + " remains cancellation rather than a timeout");
                if (recognition) Check(audio.All(value => value == 0), label + " clears the input audio");
                Released(model, label + " leaves no worker or pending cleanup");
                waiting = false;
                var retry = await FailureAsync(model.WarmAsync(preferences, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)));
                Check(attempts == 2 && retry is IOException && retry.Message.Contains("Install and verify", StringComparison.Ordinal), label + " allows another verification attempt");
            });
        }

        await Case("Queued recognition cancellation", async () =>
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var attempts = 0;
            var model = new ResidentModel(verifyModel: async (_, token) =>
            {
                if (++attempts != 1) return false;
                entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return false;
            }, startupTimeout: TimeSpan.FromSeconds(5));
            using var holder = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var queued = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var warming = model.WarmAsync(preferences, holder.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var audio = Audio(); var recognition = model.RunAsync(audio, preferences, queued.Token);
            Check(!recognition.IsCompleted, "A recognition request queues behind active verification");
            queued.Cancel(); var error = await FailureAsync(recognition.WaitAsync(TimeSpan.FromSeconds(1)));
            Check(error is OperationCanceledException && audio.All(value => value == 0) && attempts == 1 && !warming.IsCompleted, "Queued cancellation clears audio without interrupting the owning verification");
            holder.Cancel(); error = await FailureAsync(warming.WaitAsync(TimeSpan.FromSeconds(1)));
            Check(error is OperationCanceledException, "The owning verification can be cancelled independently");
            Released(model, "Queued cancellation and owner cancellation leave no pending worker");
            var retry = await FailureAsync(model.WarmAsync(preferences, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1)));
            Check(attempts == 2 && retry is IOException && retry.Message.Contains("Install and verify", StringComparison.Ordinal), "The model gate accepts a new attempt after queued cancellation");
        });
        return new(checks.ToArray(), failures.ToArray());
    }

    private static byte[] Audio() => Enumerable.Repeat((byte)93, 64).ToArray();
    private static async Task<Exception> FailureAsync(Task operation)
    {
        try { await operation; return new InvalidOperationException("The operation unexpectedly succeeded."); }
        catch (Exception error) { return error; }
    }
}