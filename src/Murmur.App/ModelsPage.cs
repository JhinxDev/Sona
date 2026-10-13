using System.Windows;
using System.Windows.Controls;
using Murmur.Core;
using Murmur.App.Audio;
namespace Murmur.App;

public partial class MainWindow
{
    private readonly ModelStore modelStore = Transcriber.Models;
    private CancellationTokenSource? modelDownloadCancellation;
    private Task modelDownloadOperation = Task.CompletedTask;
    private string? downloadingModel;
    private int downloadPercent;
    private string downloadOutcome = "idle";
    private bool preferencesApplying;
    private bool SettingsBusy => processing || recorder != null || shutdownPending || preferencesApplying || preparing || session?.State == SessionState.Recording;
    private void RefreshModelCards()
    {
        ModelCards.Children.Clear();
        foreach (var model in ModelCatalog.All)
        {
            var installed = modelStore.Present(model.Id);
            var selected = Transcriber.SelectedModel(preferences) == model.Id;
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = model.Name + (selected ? "  ·  Selected" : ""), FontSize = 15, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(ModelDescription($"{model.SizeLabel} download · {model.ParametersMillions}M parameters · {(installed ? "Present on this device" : "Not installed")}"));
            stack.Children.Add(ModelDescription(model.Description));
            stack.Children.Add(ModelDescription(model.Measurements));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var action = new Button { Content = installed ? selected ? "Verify selected" : "Verify & use" : "Download", Tag = model.Id, IsEnabled = downloadingModel == null };
            action.Click += ModelAction_Click; buttons.Children.Add(action); stack.Children.Add(buttons);
            ModelCards.Children.Add(new Border { Style = (Style)FindResource("SettingGroup"), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12), Child = stack });
        }
    }
    private TextBlock ModelDescription(string text) => new() { Text = text, Style = (Style)FindResource("Description"), Margin = new Thickness(0, 6, 0, 0) };
    private async void ModelAction_Click(object sender, RoutedEventArgs e)
    {
        var id = (string)((Button)sender).Tag;
        if (modelStore.Present(id))
        {
            if (SettingsBusy || downloadingModel != null) { ModelResult.Text = "Finish the current operation before selecting a model."; return; }
            try
            {
                if (!await modelStore.VerifyAsync(id)) { ModelResult.Text = "Integrity check failed; the existing file was preserved."; return; }
                if (SettingsBusy) { ModelResult.Text = "Finish the current dictation before selecting a model."; return; }
                if (ModelCatalog.NeedsCuda(id) && (!Transcriber.CudaEngineAvailable || preferences.Engine == "FastCpu")) { ModelResult.Text = "This model needs the CUDA engine. Choose GPU when installed and save the engine first."; return; }
                using var patch = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { model = id }));
                var candidate = AgentSettings.Apply(preferences, patch.RootElement); candidate.Save(preferencesPath);
                preferences.Model = id; RefreshModelCards(); ModelResult.Text = "Verified and selected. The next dictation uses this model.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ModelResult.Text = "The model could not be verified or selected. Existing files were preserved."; }
            return;
        }
        modelDownloadOperation = DownloadModelAsync(id); await modelDownloadOperation;
    }
    private async Task DownloadModelAsync(string id)
    {
        if (downloadingModel != null) throw new IOException("Another model download is running.");
        downloadingModel = id; downloadPercent = 0; downloadOutcome = "downloading"; modelDownloadCancellation?.Dispose(); modelDownloadCancellation = new(); RefreshModelCards();
        var model = ModelCatalog.Get(id);
        var progress = new Progress<long>(bytes => { downloadPercent = (int)(bytes * 100 / model.Bytes); ModelResult.Text = $"Downloading {model.Name}: {downloadPercent}%"; });
        try { await modelStore.InstallAsync(id, progress, modelDownloadCancellation.Token); downloadOutcome = "verified"; downloadPercent = 100; ModelResult.Text = "Download verified. Choose Verify & use to select it."; }
        catch (OperationCanceledException) { downloadOutcome = "cancelled"; ModelResult.Text = "Download cancelled. Partial data was removed."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException) { downloadOutcome = modelStore.CleanupPending ? "cleanup-pending" : "failed"; ModelResult.Text = "Download failed. Existing model files were preserved; check connectivity and disk space."; }
        finally { downloadingModel = null; RefreshModelCards(); }
    }
    private void CancelDownload_Click(object sender, RoutedEventArgs e) => modelDownloadCancellation?.Cancel();
}
