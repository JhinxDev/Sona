using System.Windows;
using System.Windows.Controls;
using Murmur.Core;
namespace Murmur.App;

public partial class MainWindow
{
    private sealed record VocabularyRow(string Word, string HeardAs, bool Correction) { public string RemoveLabel => "Remove " + Word; }
    private sealed record SnippetRow(string Trigger, string Expansion) { public string EditLabel => "Edit " + Trigger; public string RemoveLabel => "Remove " + Trigger; }
    private Snippet? editingSnippet;
    private void RefreshEditors()
    {
        Vocabulary.Text = string.Join("\n", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Spoken + " = " + x.Written)));
        Snippets.Text = SnippetRules.Format(preferences.Snippets);
        WordList.ItemsSource = preferences.Terms.Select(x => new VocabularyRow(x, "", false)).Concat(preferences.Vocabulary.Select(x => new VocabularyRow(x.Written, x.Spoken, true))).OrderBy(x => x.Word).ToArray();
        SnippetList.ItemsSource = preferences.Snippets.Select(x => new SnippetRow(x.Trigger, x.Expansion)).ToArray();
    }
    private bool EditorsBusy(TextBlock result)
    {
        if (!SettingsBusy) return false;
        result.Text = "Finish or cancel dictation before editing."; return true;
    }
    private void SaveVocabulary(VocabularyRules rules)
    {
        var priorTerms = preferences.Terms; var priorCorrections = preferences.Vocabulary;
        preferences.Terms = rules.Terms; preferences.Vocabulary = rules.Replacements;
        try { preferences.Save(preferencesPath); }
        catch { preferences.Terms = priorTerms; preferences.Vocabulary = priorCorrections; throw; }
        RefreshEditors();
    }
    private void AddWord_Click(object sender, RoutedEventArgs e)
    {
        if (EditorsBusy(VocabularyResult)) return;
        try
        {
            var word = WordInput.Text.Trim(); var heard = HeardInput.Text.Trim();
            if (word.Length == 0 || word.IndexOfAny(['\r', '\n', '=']) >= 0 || heard.IndexOfAny(['\r', '\n', '=']) >= 0) throw new FormatException("Enter one word or phrase. Leave Heard as empty unless you want a spelling correction.");
            var current = string.Join("\n", preferences.Terms.Concat(preferences.Vocabulary.Select(x => x.Spoken + " = " + x.Written)));
            SaveVocabulary(VocabularyRules.Parse(current + "\n" + (heard.Length == 0 ? word : heard + " = " + word)));
            WordInput.Clear(); HeardInput.Clear(); VocabularyResult.Text = "Word saved.";
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException) { VocabularyResult.Text = ex.Message; }
    }
    private void RemoveWord_Click(object sender, RoutedEventArgs e)
    {
        if (EditorsBusy(VocabularyResult) || (sender as Button)?.Tag is not VocabularyRow row) return;
        try
        {
            var terms = preferences.Terms.Where(x => row.Correction || x != row.Word).ToList();
            var corrections = preferences.Vocabulary.Where(x => !row.Correction || x.Spoken != row.HeardAs || x.Written != row.Word).ToList();
            SaveVocabulary(new(terms, corrections)); VocabularyResult.Text = "Word removed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { VocabularyResult.Text = ex.Message; }
    }
    private void SaveSnippets(List<Snippet> snippets)
    {
        var prior = preferences.Snippets; preferences.Snippets = snippets;
        try { preferences.Save(preferencesPath); } catch { preferences.Snippets = prior; throw; }
        RefreshEditors();
    }
    private void AddSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (EditorsBusy(SnippetResult)) return;
        try
        {
            var trigger = SnippetTrigger.Text.Trim(); var expansion = SnippetExpansion.Text;
            if (trigger.Length is 0 or > 120 || trigger.IndexOfAny(['\r', '\n']) >= 0 || string.IsNullOrWhiteSpace(expansion) || expansion.Length > 4000) throw new FormatException("Use a trigger up to 120 characters and expanded text up to 4000 characters.");
            var snippets = preferences.Snippets.Where(x => x != editingSnippet).ToList();
            if (snippets.Any(x => x.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase))) throw new FormatException("That trigger already exists. Edit its snippet instead.");
            if (snippets.Count >= 200) throw new FormatException("Keep at most 200 snippets.");
            snippets.Add(new(trigger, expansion)); SaveSnippets(snippets); ResetSnippetForm(); SnippetResult.Text = "Snippet saved.";
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException) { SnippetResult.Text = ex.Message; }
    }
    private void EditSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (EditorsBusy(SnippetResult) || (sender as Button)?.Tag is not SnippetRow row) return;
        editingSnippet = preferences.Snippets.Single(x => x.Trigger == row.Trigger && x.Expansion == row.Expansion);
        SnippetTrigger.Text = row.Trigger; SnippetExpansion.Text = row.Expansion; AddSnippetButton.Content = "Save changes"; CancelSnippetEdit.Visibility = Visibility.Visible;
    }
    private void RemoveSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (EditorsBusy(SnippetResult) || (sender as Button)?.Tag is not SnippetRow row) return;
        try { SaveSnippets(preferences.Snippets.Where(x => x.Trigger != row.Trigger || x.Expansion != row.Expansion).ToList()); if (editingSnippet?.Trigger == row.Trigger) ResetSnippetForm(); SnippetResult.Text = "Snippet removed."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SnippetResult.Text = ex.Message; }
    }
    private void ResetSnippetForm() { editingSnippet = null; SnippetTrigger.Clear(); SnippetExpansion.Clear(); AddSnippetButton.Content = "Add snippet"; CancelSnippetEdit.Visibility = Visibility.Collapsed; }
    private void CancelSnippetEdit_Click(object sender, RoutedEventArgs e) => ResetSnippetForm();
    private sealed record TransferredDraft(string Draft, string Original);
    private void RestoreTransferredDraft()
    {
        var payload = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
        if (string.IsNullOrEmpty(payload) || arguments.Contains("--test-ui")) return;
        try
        {
            var draft = System.Text.Json.JsonSerializer.Deserialize<TransferredDraft>(payload);
            if (draft == null || draft.Draft == null || draft.Original == null || draft.Draft.Length > 6000 || draft.Original.Length > 6000) return;
            session = new(); session.Begin(); session.Stop(); session.Complete(draft.Original, draft.Draft); Render(session);
            Status.Text = "Your draft was restored"; StatusDetail.Text = "Your text is here. Hold Ctrl + Windows to start a new dictation.";
        }
        catch (System.Text.Json.JsonException) { StatusDetail.Text = "The draft transfer could not be read."; }
    }
}
