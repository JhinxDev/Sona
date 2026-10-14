using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Sona.Core;

namespace Sona.App;

public partial class MainWindow
{
    private readonly DispatcherTimer updateCheckTimer = new() { Interval = TimeSpan.FromHours(12) };
    private readonly CancellationTokenSource updateCheckLifetime = new();
    private readonly HttpClient updateCheckClient = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private bool updateCheckRunning;
    private Uri? updateReleaseUrl;

    private void InitializeUpdateChecks()
    {
        CheckUpdates.IsChecked = preferences.CheckForUpdates;
        UpdateRelease.Visibility = Visibility.Collapsed; UpdateNotice.Visibility = Visibility.Collapsed;
        UpdateStatus.Text = "Update checks are optional. No downloads or installation run automatically.";
        updateCheckTimer.Tick += async (_, _) => await CheckForUpdateAsync(automatic: true);
        Closed += (_, _) => { updateCheckTimer.Stop(); updateCheckLifetime.Cancel(); updateCheckClient.Dispose(); };
        if (arguments.Contains("--test-ui")) return;
        updateCheckTimer.Start();
        if (preferences.CheckForUpdates) _ = CheckForUpdateAsync(automatic: true);
    }

    private async void UpdateCheck_Click(object sender, RoutedEventArgs e) => await CheckForUpdateAsync(automatic: false);

    private async Task CheckForUpdateAsync(bool automatic)
    {
        if (windowClosed || shutdownPending || arguments.Contains("--test-ui") || updateCheckRunning || (automatic && !preferences.CheckForUpdates)) return;
        if (recorder != null || processing || preparing)
        {
            if (!automatic) UpdateStatus.Text = "Finish the current dictation before checking for updates.";
            return;
        }
        updateCheckRunning = true;
        if (!automatic) UpdateStatus.Text = "Checking the public Sona release page…";
        try
        {
            using var checkTimeout = CancellationTokenSource.CreateLinkedTokenSource(updateCheckLifetime.Token);
            checkTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/JhinxDev/Sona/releases/latest");
            request.Headers.UserAgent.ParseAdd("Sona/" + AppVersion.Number);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await updateCheckClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, checkTimeout.Token);
            if (windowClosed || shutdownPending) return;
            if (response.StatusCode == HttpStatusCode.NotFound) { updateReleaseUrl = null; UpdateRelease.Visibility = Visibility.Collapsed; UpdateNotice.Visibility = Visibility.Collapsed; UpdateStatus.Text = "No public release is available yet."; return; }
            if (!response.IsSuccessStatusCode) { UpdateStatus.Text = $"Update check unavailable (HTTP {(int)response.StatusCode})."; return; }
            if (response.Content.Headers.ContentLength > ReleaseVersion.MaximumBytes) throw new FormatException("Release response is too large.");
            // One cancellation deadline covers both response headers and body.
            await using var stream = await response.Content.ReadAsStreamAsync(checkTimeout.Token);
            using var bytes = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, checkTimeout.Token)) != 0)
            {
                if (bytes.Length + read > ReleaseVersion.MaximumBytes) throw new FormatException("Release response is too large.");
                bytes.Write(buffer, 0, read);
            }
            var release = ReleaseVersion.Parse(bytes.ToArray());
            if (!Version.TryParse(AppVersion.Number, out _)) { UpdateStatus.Text = "Installed version is unknown; compare versions on the release page."; return; }
            if (ReleaseVersion.IsNewer(release, AppVersion.Number))
            {
                updateReleaseUrl = release.Url;
                UpdateRelease.Visibility = Visibility.Visible; UpdateNotice.Visibility = Visibility.Visible;
                UpdateStatus.Text = $"Sona {release.Version.ToString(3)} is available. Installed: {AppVersion.Number}.";
            }
            else
            {
                updateReleaseUrl = null;
                UpdateRelease.Visibility = Visibility.Collapsed; UpdateNotice.Visibility = Visibility.Collapsed;
                UpdateStatus.Text = "The installed version is current with the latest public release.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or FormatException or JsonException or ObjectDisposedException)
        {
            if (!windowClosed && !shutdownPending && !updateCheckLifetime.IsCancellationRequested)
                UpdateStatus.Text = "Could not check for updates. Try again later.";
        }
        finally { updateCheckRunning = false; }
    }

    private void UpdateNotice_Click(object sender, RoutedEventArgs e) => Pages.SelectedIndex = 1;

    private void UpdateRelease_Click(object sender, RoutedEventArgs e)
    {
        if (updateReleaseUrl == null) return;
        try { Process.Start(new ProcessStartInfo(updateReleaseUrl.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { UpdateStatus.Text = "Could not open the release page in your browser."; }
    }
}
