using System.Windows;
using System.Windows.Controls;
using Murmur.Core;
namespace Murmur.App;

public partial class MainWindow
{
    private readonly List<HistoryItem> historyEntries = [];
    private Guid? historySelection;
    private bool historyStorageFailed;
    private string HistoryPath => Path.Combine(Path.GetDirectoryName(preferencesPath)!, "history.json");
    private void RetainCurrentDraft()
    {
        if (preparedInsertionTest || string.IsNullOrWhiteSpace(Draft.Text) || Draft.Text.Length > 6000 || Original.Text.Length > 6000) return;
        var index = historyEntries.FindIndex(x => x.Id == historySelection);
        if (index >= 0) { historyEntries[index] = historyEntries[index] with { Original = Original.Text, Draft = Draft.Text }; SaveHistory(); RefreshHistory(); return; }
        var retained = new DictationSession(); retained.Begin(); retained.Stop(); retained.Complete(Original.Text, Draft.Text);
        if (session?.State == SessionState.Inserted) retained.MarkInserted();
        AddHistory(retained);
    }
    private void LoadHistory()
    {
        if (preferences.KeepHistory)
        {
            try { historyEntries.AddRange(HistoryStore.Load(HistoryPath)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { historyStorageFailed = true; HistoryResult.Text = "Saved history could not be read. The existing file was preserved."; }
        }
        RefreshHistory();
    }
    private void AddHistory(DictationSession current)
    {
        if (string.IsNullOrWhiteSpace(current.Draft)) return;
        var item = new HistoryItem(current.Id, DateTimeOffset.Now, current.Original, current.Draft, current.State == SessionState.Inserted);
        historyEntries.RemoveAll(x => x.Id == item.Id); historyEntries.Insert(0, item);
        if (historyEntries.Count > HistoryStore.Capacity) historyEntries.RemoveRange(HistoryStore.Capacity, historyEntries.Count - HistoryStore.Capacity);
        historySelection = item.Id; SaveHistory(); RefreshHistory();
    }
    private void SaveHistory()
    {
        if (!preferences.KeepHistory || historyStorageFailed) return;
        try { HistoryStore.Save(HistoryPath, historyEntries); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { historyStorageFailed = true; HistoryResult.Text = "History is still available in this session. Saving failed; existing records were preserved."; }
    }
    private void MarkHistoryInserted()
    {
        var index = historyEntries.FindIndex(x => x.Id == historySelection);
        if (index < 0) return;
        historyEntries[index] = historyEntries[index] with { Inserted = true };
        SaveHistory(); RefreshHistory();
    }
    private void RefreshHistory()
    {
        if (History == null || HistorySearch == null) return;
        History.ItemsSource = HistoryStore.Search(historyEntries, HistorySearch.Text);
        HistoryEmpty.Text = historyEntries.Count == 0 ? "No history yet. Your dictations will appear here." : "No transcriptions match your search.";
        HistoryEmpty.Visibility = History.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryLabel.Text = preferences.KeepHistory ? "Your past dictations, stored locally on this device." : "Your dictations from this session. Saving to disk is off.";
    }
    private void HistorySearch_Changed(object sender, TextChangedEventArgs e) => RefreshHistory();
    private void History_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (processing || recorder != null || History.SelectedItem is not HistoryItem item) return;
        cancellation?.Cancel(); preparedInsertionTest = insertionTestHolding = false; target = null;
        session = new(); session.Begin(); session.Stop(); session.Complete(item.Original, item.Draft);
        if (item.Inserted) session.MarkInserted();
        historySelection = item.Id; Render(session); Pages.SelectedIndex = 0;
        Status.Text = "History draft opened"; StatusDetail.Text = "Review or copy this text. It was not inserted automatically.";
    }
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (processing || recorder != null) { HistoryResult.Text = "Finish dictation before clearing history."; return; }
        try
        {
            if (File.Exists(HistoryPath)) File.Delete(HistoryPath);
            historyEntries.Clear(); historySelection = null; historyStorageFailed = false;
            RefreshHistory(); HistoryResult.Text = "History cleared. Your current draft is still here.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { HistoryResult.Text = "Saved history could not be cleared. Records were preserved."; }
    }
    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        if (processing || recorder != null) { FileResult.Text = "Finish or cancel the current dictation first."; return; }
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Transcribe a WAV file", Filter = "WAV audio (*.wav)|*.wav", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 4_000_000) throw new IOException("Choose a WAV file up to 2 minutes (4 MB).");
            using var wav = new NAudio.Wave.WaveFileReader(dialog.FileName);
            if (wav.WaveFormat.Encoding != NAudio.Wave.WaveFormatEncoding.Pcm || wav.WaveFormat.SampleRate != 16000 || wav.WaveFormat.BitsPerSample != 16 || wav.WaveFormat.Channels != 1 || wav.TotalTime > TimeSpan.FromMinutes(2)) throw new IOException("Use a 16 kHz, 16-bit mono PCM WAV, up to 2 minutes.");
            Pages.SelectedIndex = 0; operation = ReplayAsync(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException) { FileResult.Text = ex.Message; }
    }
}
