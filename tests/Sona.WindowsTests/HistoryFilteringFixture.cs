using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sona.App;
using Sona.Core;

internal static class HistoryFilteringFixture
{
    internal static object Run(string output, bool retentionOff = false)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh history proof directory.");
        Directory.CreateDirectory(output); var profile = Path.Combine(output, "profile");
        new Preferences { Theme = "amethyst", KeepHistory = !retentionOff, AutoInsert = false }.Save(Path.Combine(profile, "preferences.json"));
        var historyPath = Path.Combine(profile, "history.json");
        var existing = new HistoryItem(Guid.NewGuid(), new DateTimeOffset(DateTime.Today.AddDays(-1).AddHours(12)), "Previously stored", "Previously stored");
        if (retentionOff) HistoryStore.Save(historyPath, [existing]);
        Environment.SetEnvironmentVariable("SONA_DATA", profile);
        var checks = new List<string>(); Exception? failure = null;
        var foreground = GetForegroundWindow();
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            MainWindow? window = null;
            app.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    ThemeManager.Apply("amethyst"); window = new MainWindow(["--test-ui", "--no-activate"]);
                    window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -12000; window.Top = -12000;
                    app.MainWindow = window; window.Show();
                    var pages = (TabControl)window.FindName("Pages"); pages.SelectedIndex = 5; window.UpdateLayout();
                    var range = window.FindName("HistoryRange") as ComboBox;
                    Check(range != null && window.FindName("HistoryDay") is DatePicker && window.FindName("HistoryCountPreview") is TextBlock, "History exposes calendar range, specific day and exact clear count controls");
                    var history = (ListBox)window.FindName("History"); var search = (TextBox)window.FindName("HistorySearch"); var day = (DatePicker)window.FindName("HistoryDay");
                    var entries = (List<HistoryItem>)typeof(MainWindow).GetField("historyEntries", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    HistoryItem Entry(int days, string text, bool saved = false) => new(Guid.NewGuid(), new DateTimeOffset(DateTime.Today.AddDays(days).AddHours(12)), text, text, Saved: saved);
                    if (retentionOff)
                    {
                        Check(entries.Count == 1 && entries[0].Id == existing.Id && history.Items.OfType<HistoryItem>().Any(item => item.Id == existing.Id), "Turning off new history retention still loads prior local records so their dates remain visible and clearable");
                        Check(((TextBlock)window.FindName("HistoryLabel")).Text == "New dictations stay in this session. Previously stored history and explicitly saved drafts remain on this device.", "Retention-off copy accurately distinguishes new session dictations from existing stored history");
                        var priorBytes = File.ReadAllBytes(historyPath); entries.Add(Entry(0, "Session-only dictation")); Invoke("SaveHistory");
                        Check(File.ReadAllBytes(historyPath).SequenceEqual(priorBytes), "Retention off does not persist new session-only dictations while prior records stay available");
                    }
                    var todayA = Entry(0, "Today alpha"); var todayB = Entry(0, "Today beta"); var yesterday = Entry(-1, "Yesterday draft");
                    var daySix = Entry(-6, "Six days ago"); var daySeven = Entry(-7, "Seven days ago"); var dayTwentyNine = Entry(-29, "Twenty-nine days ago"); var dayThirty = Entry(-30, "Thirty days ago"); var saved = Entry(-60, "Important saved draft", true);
                    entries.Clear(); entries.AddRange([todayA, todayB, yesterday, daySix, daySeven, dayTwentyNine, dayThirty, saved]);
                    HistoryStore.Save(Path.Combine(profile, "history.json"), entries.Where(item => !item.Saved)); HistoryStore.Save(Path.Combine(profile, "saved-drafts.json"), [saved]);
                    var savedBytes = File.ReadAllBytes(Path.Combine(profile, "saved-drafts.json"));
                    var draft = (TextBox)window.FindName("Draft"); var original = (TextBox)window.FindName("Original"); draft.Text = "Current editable draft stays exactly here."; original.Text = "Current original stays exactly here.";
                    void Choose(string tag) { range!.SelectedItem = range.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, tag)); Invoke("RefreshHistory"); window.UpdateLayout(); }
                    int RecentVisible() => history.Items.OfType<HistoryItem>().Count(item => !item.Saved);
                    Choose("Today");
                    Check(RecentVisible() == 2 && history.Items.OfType<HistoryItem>().Count(item => item.Saved) == 1, "Today shows two local-day recent rows plus the separate preserved saved draft");
                    var view = CollectionViewSource.GetDefaultView(history.ItemsSource);
                    Check(view.Groups?.Count == 2 && view.Groups[0] is CollectionViewGroup { Name: "Saved drafts" }, "Native History groups saved drafts separately from local calendar days");
                    Invoke("ClearHistory_Click", window, new RoutedEventArgs());
                    Check(((FrameworkElement)window.FindName("HistoryClearConfirmation")).Visibility == Visibility.Visible && entries.Count == 8 && HistoryStore.Load(Path.Combine(profile, "history.json")).Count == 7, "Clear previews its exact range count before mutating memory or disk");
                    Invoke("CancelHistoryClear_Click", window, new RoutedEventArgs());
                    Check(((FrameworkElement)window.FindName("HistoryClearConfirmation")).Visibility == Visibility.Collapsed && entries.Count == 8, "Cancelling the in-app confirmation leaves every history record unchanged");
                    Choose("Last7Days"); Check(RecentVisible() == 4, "Last seven calendar days include today and day six, excluding day seven");
                    Choose("Last30Days"); Check(RecentVisible() == 6, "Last thirty calendar days include day twenty-nine and exclude day thirty");
                    Choose("All"); Check(RecentVisible() == 7, "All recent includes every unsaved date");
                    search.Text = "Yesterday"; window.UpdateLayout(); Check(RecentVisible() == 1 && history.Items.Count == 1, "Search narrows the visible records and destructive preview scope"); search.Text = "";
                    day.SelectedDate = DateTime.Today.AddDays(-7); Choose("SpecificDay"); Check(RecentVisible() == 1, "Specific day selects exactly its local calendar date");
                    var dateText = (TextBox)day.Template.FindName("PART_TextBox", day); Check(dateText.Template.FindName("PART_ContentHost", dateText) != null && dateText.Foreground == window.FindResource("Ink") && dateText.Background == window.FindResource("Inset"), "Specific date text uses the readable app text-field surface instead of a light native inner template");
                    Invoke("ClearHistory_Click", window, new RoutedEventArgs()); Capture("history-confirmation.png");
                    Invoke("ConfirmHistoryClear_Click", window, new RoutedEventArgs());
                    Check(entries.Count == 7 && entries.All(item => item.Id != daySeven.Id) && HistoryStore.Load(Path.Combine(profile, "history.json")).Count == 6, "Confirmation removes only the selected day's recent record in memory and persisted history");
                    Check(entries.Any(item => item.Id == saved.Id && item.Saved) && File.ReadAllBytes(Path.Combine(profile, "saved-drafts.json")).SequenceEqual(savedBytes), "Scoped clear leaves saved-draft bytes and records unchanged");
                    Check(draft.Text == "Current editable draft stays exactly here." && original.Text == "Current original stays exactly here.", "Scoped clear preserves the complete currently editable draft and original");
                    Choose("Today"); Invoke("ClearHistory_Click", window, new RoutedEventArgs()); Choose("All");
                    Check(((FrameworkElement)window.FindName("HistoryClearConfirmation")).Visibility == Visibility.Collapsed && entries.Count == 7, "Changing the filter cancels a pending destructive confirmation");
                    Capture("history-grouped.png"); window.Width = 740; window.Height = 530; window.UpdateLayout(); Capture("history-compact.png");
                    Check(((FrameworkElement)window.FindName("HistoryCountPreview")).ActualWidth > 0 && GetForegroundWindow() == foreground, "History remains usable at minimum size without changing the user's foreground window");
                    Choose("Today");
                    var beforeIds = entries.Select(item => item.Id).ToArray(); var validBytes = File.ReadAllBytes(historyPath);
                    Invoke("ClearHistory_Click", window, new RoutedEventArgs()); File.WriteAllText(historyPath, "not-json");
                    Invoke("ConfirmHistoryClear_Click", window, new RoutedEventArgs());
                    Check(entries.Select(item => item.Id).SequenceEqual(beforeIds) && File.ReadAllText(historyPath) == "not-json" && draft.Text == "Current editable draft stays exactly here.", "Unreadable persisted history aborts the clear without partial memory loss or replacing the corrupt file");
                    File.WriteAllBytes(historyPath, validBytes); Invoke("ClearHistory_Click", window, new RoutedEventArgs());
                    using (var locked = new FileStream(historyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) Invoke("ConfirmHistoryClear_Click", window, new RoutedEventArgs());
                    Check(entries.Select(item => item.Id).SequenceEqual(beforeIds) && File.ReadAllBytes(historyPath).SequenceEqual(validBytes) && draft.Text == "Current editable draft stays exactly here.", "A blocked atomic history write aborts the clear while preserving every record and the current draft");
                    Check(File.ReadAllBytes(Path.Combine(profile, "saved-drafts.json")).SequenceEqual(savedBytes) && GetForegroundWindow() == foreground, "Failed clears also preserve explicitly saved draft bytes and leave the foreground window unchanged");
                    void Capture(string name)
                    {
                        window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name)); encoder.Save(stream);
                    }
                }
                catch (Exception error) { failure = error; File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); }
                finally { if (window != null) window.RequestQuit(); else app.Shutdown(); }
                void Invoke(string method, params object[] arguments) => typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, arguments);
                void Check(bool value, string label) { if (!value) throw new IOException(label); checks.Add(label); }
            }));
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Sona isolated history ranges" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(25))) throw new TimeoutException("History fixture exceeded twenty-five seconds.");
        var report = new { Checks = checks, Failure = failure?.ToString(), Profile = profile, RetentionOff = retentionOff, MicrophoneOpened = false, GlobalInputSent = false, ProductionProfileChanged = false, Evidence = "Isolated native WPF window shown offscreen; fixture-only history records" };
        File.WriteAllText(Path.Combine(output, "history-proof.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        if (failure != null) throw new IOException("History fixture failed", failure); return report;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
}
