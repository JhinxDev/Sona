using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Sona.Core;
namespace Sona.App;

public partial class MainWindow
{
    private string reviewBaseline = "";
    private void SaveDraft_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || string.IsNullOrWhiteSpace(Draft.Text)) { DraftReviewResult.Text = "Finish dictation, then save the draft."; return; }
        if (savedDraftStorageFailed) { DraftReviewResult.Text = "Saved drafts could not be read or written. Restore access and restart; existing records are preserved."; return; }
        var prior = historyEntries.FirstOrDefault(x => x.Id == historySelection);
        if (prior?.Saved != true && historyEntries.Count(x => x.Saved) >= HistoryStore.Capacity) { DraftReviewResult.Text = "You have 200 saved drafts. Remove one before saving another."; return; }
        var item = new HistoryItem(prior?.Id ?? Guid.NewGuid(), prior?.CreatedAt ?? DateTimeOffset.Now, Original.Text, Draft.Text, prior?.Inserted ?? false, true, reviewBaseline);
        if (item.Draft.Length > 6000 || item.Original.Length > 6000 || item.Generated?.Length > 6000) { DraftReviewResult.Text = "Keep a saved draft within 6,000 characters."; return; }
        var saved = historyEntries.Where(x => x.Saved && x.Id != item.Id).Prepend(item).ToArray();
        try
        {
            HistoryStore.Save(SavedDraftPath, saved);
            historyEntries.RemoveAll(x => x.Id == item.Id); historyEntries.Insert(0, item); historySelection = item.Id; RefreshHistory(); SaveHistory();
            DraftReviewResult.Text = "Draft saved on this device, including original and corrected text. Open it from History.";
            ReviewCorrections_Click(sender, e);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DraftReviewResult.Text = "Saving failed. Your current draft and existing saved file were preserved."; }
    }
    private void DeleteSavedDraft_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || sender is not Button { Tag: HistoryItem item } || !item.Saved || savedDraftStorageFailed) return;
        try
        {
            // Remove any earlier recent-history mirror first. If this fails, the saved
            // copy still exists, so deletion cannot resurrect an old version later.
            if (File.Exists(HistoryPath))
            {
                var recent = HistoryStore.Load(HistoryPath);
                if (recent.Any(x => x.Id == item.Id)) HistoryStore.Save(HistoryPath, recent.Where(x => x.Id != item.Id));
            }
            HistoryStore.Save(SavedDraftPath, historyEntries.Where(x => x.Saved && x.Id != item.Id));
            historyEntries.RemoveAll(x => x.Id == item.Id); if (historySelection == item.Id) { historySelection = null; suppressCurrentDraftHistory = true; } RefreshHistory();
            HistoryResult.Text = "Saved record removed. The current editable draft was preserved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { HistoryResult.Text = "The saved draft could not be removed. Existing records were preserved."; }
    }
    private void ReviewCorrections_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy) { DraftReviewResult.Text = "Finish dictation before reviewing corrections."; return; }
        var suggestions = CorrectionLearning.Suggest(reviewBaseline, Draft.Text);
        CorrectionSuggestions.ItemsSource = suggestions;
        if (suggestions.Count == 0) DraftReviewResult.Text += " No short spelling correction detected. Use Vocabulary for a custom phrase.";
    }
    private void LearnCorrection_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || sender is not Button { Tag: Replacement item }) return;
        // Candidate must still describe the visible edit; stale buttons cannot teach.
        if (!CorrectionLearning.Suggest(reviewBaseline, Draft.Text).Contains(item)) { CorrectionSuggestions.ItemsSource = null; return; }
        if (preferences.Vocabulary.Any(x => x.Spoken.Equals(item.Spoken, StringComparison.OrdinalIgnoreCase)))
        { DraftReviewResult.Text = "That spoken form already has a rule. Review it in Vocabulary before changing it."; return; }
        try
        {
            var text = string.Join("\n", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Spoken + " = " + x.Written)).Append(item.Spoken + " = " + item.Written));
            SaveVocabulary(VocabularyRules.Parse(text)); CorrectionSuggestions.ItemsSource = null;
            DraftReviewResult.Text = $"Learned {item.Spoken} → {item.Written}. Review or remove this rule in Vocabulary.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException) { DraftReviewResult.Text = "Correction was not saved. " + ex.Message; }
    }
    private void ExportDraft_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsBusy || string.IsNullOrWhiteSpace(Draft.Text)) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export this draft for review", Filter = "Draft review (*.json)|*.json", FileName = "sona-draft-review.json", AddExtension = true, DefaultExt = ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var snapshot = new { Product = "Sona", Version = AppVersion.Number, BuildId = AppVersion.BuildId, ExportedAt = DateTimeOffset.Now, Original = Original.Text, Generated = reviewBaseline, Edited = Draft.Text, Model = Sona.App.Audio.Transcriber.SelectedModel(preferences), preferences.Language, preferences.CleanupEnabled, preferences.CleanupTone };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
            DraftReviewResult.Text = "This draft was exported locally. Review the text before sharing the file; nothing was uploaded.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DraftReviewResult.Text = "The draft could not be exported. Your draft remains here."; }
    }
}
