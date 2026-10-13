using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly Dictionary<string, TextBlock> modelStateLabels = new();

    private void RefreshModelCards()
    {
        overlay.UpdateModel(ModelCatalog.Get(Transcriber.SelectedModel(preferences)).Name);
        RefreshResidentStatusIfReady();
        ModelCards.Children.Clear(); modelStateLabels.Clear();
        foreach (var model in ModelCatalog.All)
        {
            var installed = modelStore.Present(model.Id);
            var active = Transcriber.SelectedModel(preferences) == model.Id;
            var loaded = transcriber.LoadedModel == model.Id;
            var downloading = downloadingModel == model.Id;
            var row = new Grid { Margin = new Thickness(16, 13, 16, 13) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(94) });
            // Original typographic provider mark, deliberately not an OpenAI logo.
            var mark = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0), ToolTip = "OpenAI Whisper models. Original Sona provider mark; not an official logo." };
            mark.SetResourceReference(Border.BackgroundProperty, "Inset"); mark.SetResourceReference(Border.BorderBrushProperty, "Line");
            mark.Child = ModelText("O", 12, false); ((TextBlock)mark.Child).HorizontalAlignment = HorizontalAlignment.Center; ((TextBlock)mark.Child).VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(mark, "OpenAI Whisper provider"); row.Children.Add(mark);

            var identity = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; Grid.SetColumn(identity, 1); row.Children.Add(identity);
            var nameLine = new WrapPanel { Orientation = Orientation.Horizontal };
            var name = ModelText(model.Name, 13, false); name.FontWeight = FontWeights.SemiBold; name.Margin = new Thickness(0, 0, 8, 0); nameLine.Children.Add(name);
            if (active)
            {
                var badge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
                badge.SetResourceReference(Border.BackgroundProperty, "Panel"); badge.SetResourceReference(Border.BorderBrushProperty, "Success");
                var badgeText = ModelText("Active", 10, false); badgeText.SetResourceReference(TextBlock.ForegroundProperty, "Success");
                var badgeContent = new StackPanel { Orientation = Orientation.Horizontal }; var dot = new System.Windows.Shapes.Ellipse { Width = 5, Height = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) }; dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Success"); badgeContent.Children.Add(dot); badgeContent.Children.Add(badgeText); badge.Child = badgeContent;
                AutomationProperties.SetAutomationId(badgeText, "ModelActive-" + model.Id); AutomationProperties.SetName(badgeText, model.Name + ", active for the next dictation"); nameLine.Children.Add(badge);
            }
            identity.Children.Add(nameLine);
            var metadata = ModelText($"OpenAI · Multilingual · {(ModelCatalog.NeedsCuda(model.Id) ? "CUDA required" : "CPU / CUDA")}", 11, true); metadata.Margin = new Thickness(0, 4, 0, 0); identity.Children.Add(metadata);
            var state = ModelText(ModelState(model, installed, active, loaded, downloading), 11, true); state.Margin = new Thickness(0, 3, 0, 0); identity.Children.Add(state); modelStateLabels[model.Id] = state;
            var details = new Expander { Header = "Details & measurements", Margin = new Thickness(0, 5, 0, 0) };
            var detailText = ModelText($"{model.Description}\n{model.ParametersMillions}M parameters · whisper.cpp local runtime\n{model.Measurements}", 11, true); detailText.Margin = new Thickness(0, 6, 0, 0); details.Content = detailText;
            AutomationProperties.SetName(details, model.Name + " details and measured benchmarks"); identity.Children.Add(details);

            var action = new Button { Content = downloading ? "Downloading" : installed ? active ? "Verify" : "Use model" : "Download", Tag = model.Id, IsEnabled = !SettingsBusy && downloadingModel == null, Padding = new Thickness(9, 7, 9, 7), MinHeight = 32, VerticalAlignment = VerticalAlignment.Top, FontSize = 11 };
            action.SetResourceReference(Button.BackgroundProperty, "Inset"); action.SetResourceReference(Button.ForegroundProperty, "Ink"); action.SetResourceReference(Button.BorderBrushProperty, "Line");
            AutomationProperties.SetAutomationId(action, "ModelAction-" + model.Id); AutomationProperties.SetName(action, (installed ? active ? "Verify active " : "Verify and use " : "Download ") + model.Name); action.Click += ModelAction_Click; Grid.SetColumn(action, 2); row.Children.Add(action);
            var shell = new ModelRowBorder { BorderThickness = new Thickness(0, 0, 0, model == ModelCatalog.All[^1] ? 0 : 1), Child = row };
            shell.SetResourceReference(Border.BorderBrushProperty, "Line");
            AutomationProperties.SetAutomationId(shell, "ModelRow-" + model.Id);
            AutomationProperties.SetName(shell, model.Name + ", " + (active ? "active for next use, " : "") + (loaded ? "loaded in memory, " : "not loaded in memory, ") + (installed ? "installed" : "not installed")); ModelCards.Children.Add(shell);
        }
    }

    private static string ModelState(LocalModel model, bool installed, bool active, bool loaded, bool downloading) => downloading
        ? $"Downloading {model.SizeLabel}"
        : $"{(installed ? "Installed" : "Download")} · {model.SizeLabel}" + (loaded ? " · Loaded in memory" : active ? " · Selected for next use; not loaded" : "");

    private static TextBlock ModelText(string text, double size, bool muted)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Muted" : "Ink"); return block;
    }

    // Border normally has no UI Automation peer, so each visual row needs one.
    private sealed class ModelRowBorder : Border
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new ModelRowPeer(this);
    }
    private sealed class ModelRowPeer(ModelRowBorder owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => "ModelRow";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }

    private async void ModelAction_Click(object sender, RoutedEventArgs e)
    {
        var id = (string)((Button)sender).Tag;
        if (SettingsBusy || downloadingModel != null) { ModelResult.Text = "Finish the current operation before changing models."; return; }
        if (modelStore.Present(id))
        {
            modelDownloadOperation = VerifyAndUseModelAsync(id); await modelDownloadOperation; return;
        }
        modelDownloadOperation = DownloadModelAsync(id); await modelDownloadOperation;
    }

    private async Task VerifyAndUseModelAsync(string id)
    {
        preferencesApplying = true; RefreshModelCards();
        var saved = false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(modelLifetime.Token); deadline.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            ModelResult.Text = "Verifying " + ModelCatalog.Get(id).Name + "…";
            if (!await modelStore.VerifyAsync(id, deadline.Token)) { ModelResult.Text = "Integrity check failed; the existing file was preserved."; return; }
            if (shutdownPending) return;
            if (ModelCatalog.NeedsCuda(id) && (!Transcriber.CudaEngineAvailable || preferences.Engine == "FastCpu")) { ModelResult.Text = "This model requires CUDA. Choose GPU when installed and save the engine first."; return; }
            using var patch = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { model = id }));
            var candidate = AgentSettings.Apply(preferences, patch.RootElement); candidate.Save(preferencesPath);
            preferences.Model = id; saved = true;
            ModelResult.Text = "Verified and active for the next dictation. Preparing the model…";
            await transcriber.ReconcileAsync(preferences, deadline.Token);
            ModelResult.Text = transcriber.LoadedModel == id ? "Verified and active. The model is loaded in memory." : "Verified and active for the next dictation. The model is not loaded in memory.";
        }
        catch (OperationCanceledException) { ModelResult.Text = saved ? "The model is active for next use, but preparation reached its time limit. Try Verify again." : "Model verification reached its time limit. The selected model was preserved."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { ModelResult.Text = saved ? "The model is active for next use, but could not be loaded. Check the local engine and try Verify again." : "The model could not be verified or selected. Existing files were preserved."; }
        finally { preferencesApplying = false; RefreshModelCards(); RefreshResidentStatus(); }
    }

    private async Task DownloadModelAsync(string id)
    {
        if (SettingsBusy) { ModelResult.Text = "Finish the current operation before downloading a model."; return; }
        if (downloadingModel != null) throw new IOException("Another model download is running.");
        downloadingModel = id; downloadPercent = 0; downloadOutcome = "downloading"; modelDownloadCancellation?.Dispose(); modelDownloadCancellation = new(); RefreshModelCards();
        var model = ModelCatalog.Get(id);
        var progress = new Progress<long>(bytes =>
        {
            if (downloadingModel != id) return;
            downloadPercent = (int)Math.Clamp(bytes * 100 / model.Bytes, 0, 100);
            ModelResult.Text = $"Downloading {model.Name}: {downloadPercent}%";
            if (modelStateLabels.TryGetValue(id, out var label)) label.Text = $"Downloading · {downloadPercent}% of {model.SizeLabel}";
        });
        try { await modelStore.InstallAsync(id, progress, modelDownloadCancellation.Token); downloadOutcome = "verified"; downloadPercent = 100; ModelResult.Text = "Download verified. Choose Use model to make it active."; }
        catch (OperationCanceledException) { downloadOutcome = "cancelled"; ModelResult.Text = "Download cancelled. Partial data was removed."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException) { downloadOutcome = modelStore.CleanupPending ? "cleanup-pending" : "failed"; ModelResult.Text = "Download failed. Existing model files were preserved; check connectivity and disk space."; }
        finally { downloadingModel = null; RefreshModelCards(); }
    }
    private void CancelDownload_Click(object sender, RoutedEventArgs e) => modelDownloadCancellation?.Cancel();
}
