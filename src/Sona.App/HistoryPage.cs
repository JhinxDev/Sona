using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Text.Json;
using Sona.Core;
namespace Sona.App;

public partial class MainWindow
{
    private readonly List<HistoryItem> historyEntries = [];
    private Guid? historySelection;
    private bool historyStorageFailed;
    private bool savedDraftStorageFailed;
    private Guid[]? pendingHistoryClear;
    private bool historyRefreshing;
    private bool suppressCurrentDraftHistory;
    private Guid? suppressedHistorySession;
    private string SavedDraftPath => Path.Combine(Path.GetDirectoryName(preferencesPath)!, "saved-drafts.json");
    private string HistoryPath => Path.Combine(Path.GetDirectoryName(preferencesPath)!, "history.json");
    private void RetainCurrentDraft()
    {
        if (preferences.HistoryPaused || suppressCurrentDraftHistory || preparedInsertionTest || string.IsNullOrWhiteSpace(Draft.Text) || Draft.Text.Length > 6000 || Original.Text.Length > 6000) return;
        var index = historyEntries.FindIndex(x => x.Id == historySelection);
        if (index >= 0) { historyEntries[index] = historyEntries[index] with { Original = Original.Text, Draft = Draft.Text }; SaveHistory(); RefreshHistory(); return; }
        if (historyEntries.Any(x => x.Original == Original.Text && x.Draft == Draft.Text)) return;
        var retained = new DictationSession(); retained.Begin(); retained.Stop(); retained.Complete(Original.Text, Draft.Text);
        if (session?.State == SessionState.Inserted) retained.MarkInserted();
        AddHistory(retained);
    }
    private void LoadHistory()
    {
        try { historyEntries.AddRange(HistoryStore.Load(SavedDraftPath).Select(x => x with { Saved = true })); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { savedDraftStorageFailed = true; HistoryResult.Text = "Saved drafts could not be read. The existing file is preserved."; }
        try { historyEntries.AddRange(HistoryStore.Load(HistoryPath).Where(x => !historyEntries.Any(saved => saved.Id == x.Id))); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { historyStorageFailed = true; HistoryResult.Text = "Saved history could not be read. The existing file was preserved."; }
        RefreshHistory();
    }
    private void AddHistory(DictationSession current)
    {
        if (preferences.HistoryPaused || suppressedHistorySession == current.Id) { suppressCurrentDraftHistory = true; suppressedHistorySession = current.Id; return; }
        if (string.IsNullOrWhiteSpace(current.Draft)) return;
        suppressCurrentDraftHistory = false;
        var item = new HistoryItem(current.Id, DateTimeOffset.Now, current.Original, current.Draft, current.State == SessionState.Inserted, Generated: reviewBaseline);
        historyEntries.RemoveAll(x => x.Id == item.Id); historyEntries.Insert(0, item);
        while (historyEntries.Count(x => !x.Saved) > HistoryStore.Capacity)
            historyEntries.RemoveAt(historyEntries.FindLastIndex(x => !x.Saved));
        historySelection = item.Id; SaveHistory(); RefreshHistory();
    }
    private void SaveHistory()
    {
        if (!savedDraftStorageFailed && historyEntries.Any(x => x.Saved))
        {
            try { HistoryStore.Save(SavedDraftPath, historyEntries.Where(x => x.Saved)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { savedDraftStorageFailed = true; DraftReviewResult.Text = "Saved draft storage failed. Your current draft is still here; the saved file was preserved."; }
        }
        if (preferences.HistoryPaused || !preferences.KeepHistory || historyStorageFailed) return;
        try { HistoryStore.Save(HistoryPath, historyEntries.Where(x => !x.Saved)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { historyStorageFailed = true; HistoryResult.Text = "History is still available in this session. Saving failed; existing records were preserved."; }
    }
    private void MarkHistoryInserted()
    {
        if (preferences.HistoryPaused || suppressCurrentDraftHistory) return;
        var index = historyEntries.FindIndex(x => x.Id == historySelection);
        if (index < 0) return;
        historyEntries[index] = historyEntries[index] with { Inserted = true };
        SaveHistory(); RefreshHistory();
    }
    private HistoryFilter CurrentHistoryFilter()
    {
        var tag = (HistoryRange?.SelectedItem as ComboBoxItem)?.Tag as string;
        var range = Enum.TryParse<Sona.Core.HistoryRange>(tag, out var parsed) ? parsed : Sona.Core.HistoryRange.All;
        return new(range, HistoryDay?.SelectedDate is DateTime selected ? DateOnly.FromDateTime(selected) : null);
    }
    private void RefreshHistory()
    {
        if (History == null || HistorySearch == null || HistoryRange == null || HistoryDay == null || HistoryCountPreview == null || historyRefreshing) return;
        historyRefreshing = true;
        try
        {
            var filter = CurrentHistoryFilter(); var now = DateTimeOffset.Now;
            var visible = HistoryFiltering.Visible(historyEntries, filter, HistorySearch.Text, now, TimeZoneInfo.Local);
            var view = new ListCollectionView(visible);
            view.GroupDescriptions.Add(new PropertyGroupDescription { Converter = new HistoryDayGroupConverter() });
            History.IsSynchronizedWithCurrentItem = false;
            History.ItemsSource = view;
            History.SelectedItem = null;
            HistoryDay.Visibility = filter.Range == Sona.Core.HistoryRange.SpecificDay ? Visibility.Visible : Visibility.Collapsed;
            HistoryEmpty.Text = historyEntries.Count == 0 ? "No history yet. Your dictations will appear here." : "No dictations match this range and search.";
            HistoryEmpty.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryLabel.Text = preferences.KeepHistory ? "Your past dictations, stored locally on this device." : "New dictations stay in this session. Previously stored history and explicitly saved drafts remain on this device.";
            var ids = HistoryFiltering.ClearableIds(historyEntries, filter, HistorySearch.Text, now, TimeZoneInfo.Local);
            var saved = historyEntries.Count(item => item.Saved);
            HistoryCountPreview.Text = $"{ids.Length} recent {(ids.Length == 1 ? "record" : "records")} in {HistoryFiltering.RangeLabel(filter)}{(string.IsNullOrWhiteSpace(HistorySearch.Text) ? "" : " matching your search")}. {saved} saved {(saved == 1 ? "draft stays" : "drafts stay")} separate.";
            ClearHistoryButton.Content = "Clear recent history";
            RefreshHistoryActions();
            if (pendingHistoryClear != null && pendingHistoryClear.Any(id => !ids.Contains(id))) ResetHistoryClear();
        }
        finally { historyRefreshing = false; }
    }
    private void RefreshHistoryActions()
    {
        if (ClearHistoryButton == null || PauseHistoryButton == null || HistoryRange == null || HistoryDay == null || HistorySearch == null) return;
        var count = HistoryFiltering.ClearableIds(historyEntries, CurrentHistoryFilter(), HistorySearch.Text, DateTimeOffset.Now, TimeZoneInfo.Local).Length;
        ClearHistoryButton.IsEnabled = !SettingsBusy && count > 0;
        ConfirmHistoryClear.IsEnabled = !SettingsBusy && pendingHistoryClear != null;
        PauseHistoryButton.IsEnabled = preferencesLoad.CanSave && !preferencesApplying && !shutdownPending;
        PauseHistoryButton.Content = preferences.HistoryPaused ? "Resume history" : "Pause history";
        System.Windows.Automation.AutomationProperties.SetName(PauseHistoryButton, (string)PauseHistoryButton.Content);
        HistoryLabel.Text = preferences.HistoryPaused
            ? "History is paused. New dictations are not added. Your current draft still works."
            : preferences.KeepHistory ? "Your past dictations, stored locally on this device."
            : "New dictations stay in this session. Previously stored history and explicitly saved drafts remain on this device.";
    }
    private void PauseHistory_Click(object sender, RoutedEventArgs e)
    {
        if (!preferencesLoad.CanSave || preferencesApplying || shutdownPending) { HistoryResult.Text = "History pause is unavailable until preferences can be saved."; return; }
        var next = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(preferences))!;
        next.HistoryPaused = !preferences.HistoryPaused;
        try
        {
            next.Save(preferencesPath);
            preferences.HistoryPaused = next.HistoryPaused;
            // Resuming never retroactively collects the draft dictated while paused.
            suppressedHistorySession = session?.Id;
            suppressCurrentDraftHistory = true;
            ResetHistoryClear(); RefreshHistory();
            HistoryResult.Text = preferences.HistoryPaused
                ? "History paused. New dictations will not be added; existing drafts stay available."
                : "History resumed for future dictations. Drafts made while paused were not added.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { HistoryResult.Text = "History pause could not be saved. The previous choice was retained."; }
        RefreshSettingsActions();
    }
    private void DeleteHistoryDraft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistoryItem item }) return;
        if (item.Saved) { DeleteSavedDraft_Click(sender, e); return; }
        if (SettingsBusy) { HistoryResult.Text = "Finish dictation before deleting a history draft."; return; }
        if (!historyEntries.Any(entry => !entry.Saved && entry.Id == item.Id)) return;
        pendingHistoryClear = [item.Id];
        HistoryClearPreview.Text = "Delete this recent draft from History? Your current editable draft will stay.";
        ConfirmHistoryClear.Content = "Delete draft";
        HistoryClearConfirmation.Visibility = Visibility.Visible;
        RefreshHistoryActions();
    }
    private void HistorySearch_Changed(object sender, TextChangedEventArgs e) { ResetHistoryClear(); RefreshHistory(); }
    private void HistoryRange_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryRange == null || HistoryDay == null) return;
        if (Equals((HistoryRange.SelectedItem as ComboBoxItem)?.Tag, "SpecificDay") && HistoryDay.SelectedDate == null) HistoryDay.SelectedDate = DateTime.Today;
        ResetHistoryClear(); RefreshHistory();
    }
    private void HistoryDay_Changed(object sender, SelectionChangedEventArgs e) { ResetHistoryClear(); RefreshHistory(); }
    private sealed class HistoryDayGroupConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is HistoryItem item ? HistoryFiltering.GroupLabel(item, TimeZoneInfo.Local, culture) : "Recent dictations";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private void History_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (historyRefreshing) return;
        if (History.SelectedItem is HistoryItem item) RestoreHistoryDraft(item);
    }
    private void OpenHistoryDraft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: HistoryItem item }) RestoreHistoryDraft(item);
    }
    private void RestoreHistoryDraft(HistoryItem item)
    {
        if (SettingsBusy) { HistoryResult.Text = "Finish or cancel dictation before opening a history draft."; return; }
        cancellation?.Cancel(); preparedInsertionTest = insertionTestHolding = false; target = null;
        session = new(); session.Begin(); session.Stop(); session.Complete(item.Original, item.Draft);
        if (item.Inserted) session.MarkInserted();
        suppressCurrentDraftHistory = preferences.HistoryPaused;
        suppressedHistorySession = preferences.HistoryPaused ? session.Id : null;
        ResetLiveReview(); historySelection = item.Id; Render(session); reviewBaseline = item.Generated ?? item.Draft; Pages.SelectedIndex = 0;
        Status.Text = "History draft opened"; StatusDetail.Text = "Review or copy this text. It was not inserted automatically.";
    }
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { HistoryResult.Text = "Finish dictation before clearing history."; return; }
        var filter = CurrentHistoryFilter();
        var ids = HistoryFiltering.ClearableIds(historyEntries, filter, HistorySearch.Text, DateTimeOffset.Now, TimeZoneInfo.Local);
        if (ids.Length == 0) { ResetHistoryClear(); HistoryResult.Text = "No recent records match this range and search. Your saved drafts are kept."; return; }
        pendingHistoryClear = ids;
        var description = HistoryFiltering.RangeLabel(filter) + (string.IsNullOrWhiteSpace(HistorySearch.Text) ? "" : " matching the current search");
        HistoryClearPreview.Text = $"Clear {ids.Length} recent {(ids.Length == 1 ? "record" : "records")} from {description}? Saved drafts and the current editable draft will stay.";
        ConfirmHistoryClear.Content = $"Clear {ids.Length} recent";
        HistoryClearConfirmation.Visibility = Visibility.Visible;
        RefreshHistoryActions();
    }
    private void ConfirmHistoryClear_Click(object sender, RoutedEventArgs e)
    {
        if (pendingHistoryClear == null) return;
        if (SettingsBusy) { ResetHistoryClear(); HistoryResult.Text = "Finish dictation, then preview the records to clear again."; return; }
        var ids = pendingHistoryClear.ToHashSet();
        if (historyEntries.Count(item => !item.Saved && ids.Contains(item.Id)) != ids.Count || historyEntries.Any(item => item.Saved && ids.Contains(item.Id)))
        { ResetHistoryClear(); RefreshHistory(); HistoryResult.Text = "The records changed. Preview the count again before clearing."; return; }
        try
        {
            // Preserve excluded on-disk records even if automatic history is now off.
            // Write first, then remove the exact previewed IDs from session memory.
            if (File.Exists(HistoryPath))
            {
                var stored = HistoryStore.Load(HistoryPath);
                if (stored.Any(item => item.Saved && ids.Contains(item.Id)))
                { ResetHistoryClear(); HistoryResult.Text = "A record was saved after the preview. Nothing was cleared; preview again."; return; }
                HistoryStore.Save(HistoryPath, stored.Where(item => item.Saved || !ids.Contains(item.Id)));
            }
            else if (preferences.KeepHistory)
                HistoryStore.Save(HistoryPath, historyEntries.Where(item => !item.Saved && !ids.Contains(item.Id)));
            historyEntries.RemoveAll(item => !item.Saved && ids.Contains(item.Id));
            if (historySelection.HasValue && ids.Contains(historySelection.Value)) { historySelection = null; suppressCurrentDraftHistory = true; }
            historyStorageFailed = false; ResetHistoryClear(); RefreshHistory();
            HistoryResult.Text = $"Cleared {ids.Count} recent {(ids.Count == 1 ? "record" : "records")}. Saved drafts and your current editable draft were preserved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { ResetHistoryClear(); HistoryResult.Text = "History could not be updated. Existing records and your current draft were preserved."; }
    }
    private void CancelHistoryClear_Click(object sender, RoutedEventArgs e) { ResetHistoryClear(); HistoryResult.Text = "Nothing was cleared."; }
    private void ResetHistoryClear()
    {
        pendingHistoryClear = null;
        if (HistoryClearConfirmation != null) HistoryClearConfirmation.Visibility = Visibility.Collapsed;
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { FileResult.Text = "Finish or cancel the current dictation first."; return; }
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
