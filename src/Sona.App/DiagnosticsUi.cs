using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Sona.App.Audio;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    // Content-free local runtime diagnostics for troubleshooting. Never include
    // draft text, history text, clipboard contents, or credentials.
    private string BuildDiagnosticsText()
    {
        var sb = new StringBuilder();
        var selectedDevice = (Device?.SelectedItem as DeviceOption)?.Name ?? "System default";
        var selectedModel = Transcriber.SelectedModel(preferences);
        var loadedModel = transcriber.LoadedModel ?? "none";
        var cleanupBackend = cleanupModel.LoadedBackend ?? "none";

        sb.Append("Sona ").Append(AppVersion.Number).Append(" (build ").Append(AppVersion.BuildId).AppendLine(")");
        sb.Append("System: ").Append(Environment.OSVersion.VersionString).Append(", ").Append(RuntimeInformation.ProcessArchitecture).AppendLine();
        sb.Append("Profile: ").Append(preferencesLoad.Status).Append(", keepHistory=").Append(preferences.KeepHistory).Append(", historyPaused=").Append(preferences.HistoryPaused).AppendLine();
        sb.Append("Speech model: selected=").Append(selectedModel).Append(", loaded=").Append(loadedModel).Append(", engine=").Append(preferences.Engine).Append(", language=").Append(preferences.Language).AppendLine();
        sb.Append("Text cleanup: ").Append(preferences.CleanupEnabled ? $"on ({preferences.CleanupMode}, {preferences.CleanupTone}, {preferences.CleanupEngine})" : "off").Append(", backend=").Append(cleanupBackend).AppendLine();
        sb.Append("Audio & mode: device=").Append(selectedDevice).Append(", shortcut=").Append(preferences.ShortcutMode).Append(", defaultStyle=").Append(preferences.Style).Append(", closeToTray=").Append(preferences.CloseToTray).Append(", startup=").Append(preferences.Startup).AppendLine();
        sb.Append("Discord protection: ").Append(preferences.ProtectionEnabled ? "enabled" : "off").Append(", connected=").Append(protection?.Connected ?? false).Append(", paused=").Append(protection?.Paused ?? false).AppendLine();
        if (lastDelivery is { } delivery)
        {
            sb.Append("Last delivery: outcome=").Append(delivery.Outcome)
              .Append(", route=").Append(delivery.DeliveryRoute)
              .Append(", capture=").Append(delivery.CaptureStatus)
              .Append(", clipboard=").Append(delivery.ClipboardStatus);
            if (!string.IsNullOrEmpty(delivery.ClipboardUnsupportedFormat))
                sb.Append(", rejectedFormat=").Append(delivery.ClipboardUnsupportedFormat);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Last delivery: none recorded in this session");
        }
        return sb.ToString().TrimEnd();
    }

    private void RefreshDiagnostics()
    {
        if (DiagnosticsSummary == null) return;
        DiagnosticsSummary.Text = BuildDiagnosticsText();
    }

    private void RefreshDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        RefreshDiagnostics();
        DiagnosticsCopyResult.Text = "Diagnostics refreshed.";
    }

    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var summary = BuildDiagnosticsText();
        DiagnosticsSummary.Text = summary;
        try
        {
            Clipboard.SetText(summary);
            DiagnosticsCopyResult.Text = "Diagnostics copied to clipboard. No dictation text or secrets were included.";
        }
        catch (COMException)
        {
            DiagnosticsCopyResult.Text = "The clipboard is busy. Try copying diagnostics again.";
        }
    }
}
