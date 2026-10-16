using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Sona.App;
using Sona.App.Audio;
using Sona.App.Windows;
using Sona.Core;

internal static class AgentSettingsConflictFixture
{
    internal sealed record Report(string[] Checks, string[] Failures, bool WindowShown = false, bool MicrophoneOpened = false, bool GlobalInputSent = false, bool ProductionProfileUsed = false);

    public static Report Run(string output)
    {
        output = Path.GetFullPath(output);
        if (Directory.Exists(output)) throw new IOException("Use a fresh agent settings fixture output directory.");
        Directory.CreateDirectory(output);
        var profile = Path.Combine(output, "profile"); var path = Path.Combine(profile, "preferences.json");
        var baseline = new Preferences { Theme = "amethyst", Language = "en", SoundEffects = false, AutoInsert = false, Device = -1, ProtectionEnabled = false, SilenceIncoming = false, ProtectionReleaseDelayMs = 321, WatchedApps = ["fixture.exe"], ProtectionShortcut = "ctrl+shift+h" };
        var available = new Transcriber();
        var selected = ModelCatalog.All.FirstOrDefault(model =>
        {
            baseline.Model = model.Id; baseline.Engine = ModelCatalog.NeedsCuda(model.Id) ? "Auto" : "FastCpu";
            return available.AvailableFor(baseline);
        }) ?? throw new IOException("The fixture needs one existing compatible runtime and model; no downloads or worker launches are allowed.");
        baseline.Model = selected.Id; baseline.Save(path);
        var priorData = Environment.GetEnvironmentVariable("SONA_DATA");
        var priorTransfer = Environment.GetEnvironmentVariable("SONA_DRAFT_TRANSFER");
        Environment.SetEnvironmentVariable("SONA_DATA", profile); Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", null);
        var checks = new List<string>(); var failures = new List<string>();
        void Check(bool value, string label) { if (value) checks.Add(label); else failures.Add(label); }
        var thread = new Thread(() =>
        {
            var app = new ThemeResourceTestApplication(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Exit += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
            {
                MainWindow? window = null;
                try
                {
                    Check(app.StartupCallbackObserved && app.MainWindow == null, "Production application startup is intercepted");
                    window = new MainWindow(["--test-ui"]);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    T Field<T>(string name) => (T)window.FindName(name);
                    object? Invoke(string name) => typeof(MainWindow).GetMethod(name, flags)!.Invoke(window, null);
                    var committed = (Preferences)typeof(MainWindow).GetField("preferences", flags)!.GetValue(window)!;
                    Check(Field<CheckBox>("ProtectionEnabled").IsChecked == baseline.ProtectionEnabled && Field<CheckBox>("SilenceIncoming").IsChecked == baseline.SilenceIncoming
                        && Field<TextBox>("WatchedApps").Text == string.Join("\n", baseline.WatchedApps) && Field<TextBox>("ProtectionDelay").Text == baseline.ProtectionReleaseDelayMs.ToString()
                        && Field<TextBox>("ProtectionShortcutInput").Text == baseline.ProtectionShortcut, "Untouched Discord controls begin from saved preferences before agent requests");

                    bool NoServices() => !window.IsVisible && !window.IsLoaded && new[] { "recorder", "protection", "credentialVault", "agentServer", "hotkeys", "activeApplication" }.All(name => typeof(MainWindow).GetField(name, flags)!.GetValue(window) == null)
                        && ((Transcriber)typeof(MainWindow).GetField("transcriber", flags)!.GetValue(window)!).ResidentProcessId == null;
                    Check(NoServices(), "Actual MainWindow controls stay unshown without recorder, worker, protection, credentials, pipe server or focus observer");
                    async Task<AgentReply> Request(string method, string json = "{}", CancellationToken cancellation = default)
                    {
                        using var document = JsonDocument.Parse(json);
                        return await (Task<AgentReply>)typeof(MainWindow).GetMethod("HandleAgentOnUiAsync", flags)!.Invoke(window, [new AgentRequest(method, document.RootElement), cancellation])!;
                    }
                    async Task<bool> Unsaved()
                    {
                        var status = await Request("status");
                        var data = JsonSerializer.SerializeToElement(status.Data, AgentProtocol.Json);
                        return data.TryGetProperty("unsavedSettings", out var value) && value.ValueKind == JsonValueKind.True;
                    }
                    string Controls() => JsonSerializer.Serialize(new
                    {
                        Sound = Field<CheckBox>("SoundEffects").IsChecked,
                        Language = Field<ComboBox>("SpeechLanguage").SelectedIndex,
                        ShortcutMode = Field<ComboBox>("ShortcutMode").SelectedIndex,
                        Enabled = Field<CheckBox>("ProtectionEnabled").IsChecked,
                        Incoming = Field<CheckBox>("SilenceIncoming").IsChecked,
                        Apps = Field<TextBox>("WatchedApps").Text,
                        Delay = Field<TextBox>("ProtectionDelay").Text,
                        Shortcut = Field<TextBox>("ProtectionShortcutInput").Text
                    });
                    void Reset()
                    {
                        committed.Language = baseline.Language;
                        committed.ProtectionEnabled = baseline.ProtectionEnabled; committed.SilenceIncoming = baseline.SilenceIncoming;
                        committed.ProtectionReleaseDelayMs = baseline.ProtectionReleaseDelayMs; committed.WatchedApps = baseline.WatchedApps.ToList();
                        Field<ComboBox>("SpeechLanguage").SelectedIndex = 1;
                        Field<CheckBox>("SoundEffects").IsChecked = baseline.SoundEffects;
                        Field<ComboBox>("ShortcutMode").SelectedIndex = 0;
                        Invoke("RefreshProtectionFields"); baseline.Save(path); Invoke("RefreshSettingsActions");
                    }
                    Check(!await Unsaved(), "A clean form reports no unsaved settings");
                    var accepted = await Request("settings.patch", "{\"language\":\"fr\"}");
                    Check(accepted.Ok && Preferences.Load(path).Language == "fr" && Field<ComboBox>("SpeechLanguage").SelectedIndex == 2, "A clean form accepts and persists the intended agent patch");
                    Check(!await Unsaved(), "An accepted clean patch leaves the form synchronized");

                    var edits = new (string Label, bool General, Action Edit)[]
                    {
                        ("General sound choice", true, () => Field<CheckBox>("SoundEffects").IsChecked = true),
                        ("General shortcut mode", true, () => Field<ComboBox>("ShortcutMode").SelectedIndex = 1),
                        ("Discord enabled choice", false, () => Field<CheckBox>("ProtectionEnabled").IsChecked = true),
                        ("Discord incoming choice", false, () => Field<CheckBox>("SilenceIncoming").IsChecked = true),
                        ("Discord watched applications", false, () => Field<TextBox>("WatchedApps").Text = "pending-fixture.exe\nsecond-fixture.exe"),
                        ("Discord release delay", false, () => Field<TextBox>("ProtectionDelay").Text = "876"),
                        ("Discord pause shortcut", false, () => Field<TextBox>("ProtectionShortcutInput").Text = "ctrl+alt+k"),
                        ("Partially typed Discord delay", false, () => Field<TextBox>("ProtectionDelay").Text = "pending")
                    };
                    foreach (var edit in edits)
                    {
                        Reset(); edit.Edit();
                        Check(Field<Border>("PreferencesSaveBar").Visibility == (edit.General ? Visibility.Visible : Visibility.Collapsed), edit.Label + " has the expected separate save affordance");
                        Check(await Unsaved(), edit.Label + " is visible as unsaved status without exposing its value");
                        var beforeBytes = File.ReadAllBytes(path); var beforeControls = Controls();
                        var reply = await Request("settings.patch", "{\"language\":\"fr\"}");
                        Check(!reply.Ok && reply.Error?.Contains("unsaved", StringComparison.OrdinalIgnoreCase) == true, edit.Label + " rejects a competing agent patch with recovery guidance");
                        Check(File.ReadAllBytes(path).SequenceEqual(beforeBytes), edit.Label + " leaves persisted preferences unchanged");
                        Check(Controls() == beforeControls, edit.Label + " remains intact in the actual controls");
                        Check(await Unsaved(), edit.Label + " stays pending after rejection");
                    }
                    Reset(); Field<TextBox>("ProtectionDelay").Text = "pending";
                    var invalid = await Request("settings.patch", "{\"model\":\"invalid-fixture-model\"}");
                    Check(!invalid.Ok && invalid.Error?.Contains("unsaved", StringComparison.OrdinalIgnoreCase) == true, "Unsaved edits are guarded before competing patch validation or side effects");
                    Reset();
                    Check(!await Unsaved() && (await Request("settings.patch", "{\"language\":\"fr\"}")).Ok, "Restoring the saved form permits a later clean agent patch");
                    Check(NoServices(), "All agent settings checks finish with the window unshown and no runtime services");
                    var raceEdits = new (string Label, Func<Control> Control, Action Edit)[]
                    {
                        ("In-flight general sound edit", () => Field<CheckBox>("SoundEffects"), () => Field<CheckBox>("SoundEffects").IsChecked = true),
                        ("In-flight watched-app edit", () => Field<TextBox>("WatchedApps"), () => Field<TextBox>("WatchedApps").Text = "pending-race-fixture.exe"),
                        ("In-flight release-delay edit", () => Field<TextBox>("ProtectionDelay"), () => Field<TextBox>("ProtectionDelay").Text = "876")
                    };
                    foreach (var edit in raceEdits)
                    {
                        Reset();
                        var unusedVault = Path.Combine(profile, "unused-race-credentials.dpapi");
                        await using var service = new ProtectionService(new ProtectedCredentials(unusedVault), "sona-agent-settings-race-" + Guid.NewGuid().ToString("N") + "-", _ => [], () => false);
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await service.ConfigureAsync(committed, timeout.Token);
                        window.AttachProtectionForTests(service);
                        try
                        {
                            Check(!await Unsaved(), edit.Label + " begins with a clean form");
                            var beforeBytes = File.ReadAllBytes(path);
                            var serviceGate = (SemaphoreSlim)typeof(ProtectionService).GetField("gate", flags)!.GetValue(service)!;
                            await serviceGate.WaitAsync(timeout.Token);
                            Task<AgentReply> pending;
                            string editedControls;
                            try
                            {
                                pending = Request("settings.patch", "{\"language\":\"fr\",\"protectionEnabled\":true,\"silenceIncoming\":true,\"protectionReleaseDelayMs\":654,\"watchedApps\":[\"agent-race-fixture.exe\"]}", timeout.Token);
                                Check(!pending.IsCompleted && (bool)typeof(MainWindow).GetField("preferencesApplying", flags)!.GetValue(window)!, edit.Label + " suspends inside the actual protection configuration gate");
                                Check(edit.Control().IsEnabled, edit.Label + " is still editable while the patch awaits protection");
                                edit.Edit(); editedControls = Controls();
                            }
                            finally { serviceGate.Release(); }
                            var reply = await pending.WaitAsync(timeout.Token);
                            Check(!reply.Ok && reply.Error?.Contains("unsaved", StringComparison.OrdinalIgnoreCase) == true, edit.Label + " rejects the now-conflicting patch");
                            Check(File.ReadAllBytes(path).SequenceEqual(beforeBytes), edit.Label + " preserves exact persisted preference bytes");
                            Check(Controls() == editedControls, edit.Label + " preserves the newly edited native controls");
                            var controller = (MicProtection)typeof(ProtectionService).GetField("controller", flags)!.GetValue(service)!;
                            Check((bool)typeof(ProtectionService).GetField("enabled", flags)!.GetValue(service)! == baseline.ProtectionEnabled
                                && ((string[])typeof(ProtectionService).GetField("watched", flags)!.GetValue(service)!).SequenceEqual(baseline.WatchedApps)
                                && (int)typeof(MicProtection).GetField("delayMs", flags)!.GetValue(controller)! == baseline.ProtectionReleaseDelayMs
                                && (bool)typeof(MicProtection).GetField("deafen", flags)!.GetValue(controller)! == baseline.SilenceIncoming,
                                edit.Label + " restores the actual protection configuration to committed preferences");
                            Check(await Unsaved() && !(bool)typeof(MainWindow).GetField("preferencesApplying", flags)!.GetValue(window)!, edit.Label + " leaves edits pending and clears transaction busy state");
                            Check(!service.Connected && !service.OwnsChanges && !File.Exists(unusedVault), edit.Label + " uses no Discord connection, voice change or credential file");
                            Reset();
                            var recovered = await Request("settings.patch", "{\"language\":\"fr\"}", timeout.Token);
                            Check(recovered.Ok && Preferences.Load(path).Language == "fr" && !await Unsaved(), edit.Label + " permits an idle clean patch after the form is restored");
                        }
                        finally { typeof(MainWindow).GetField("protection", flags)!.SetValue(window, null); }
                    }
                    Check(NoServices(), "Race checks release fake protection and leave the window unshown without runtime services");
                }
                catch (Exception error) { failures.Add("Fixture failure: " + error.GetType().Name + ": " + error.Message); }
                finally
                {
                    if (window != null)
                    {
                        typeof(MainWindow).GetField("quitting", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
                        window.Close();
                    }
                    app.Shutdown();
                }
            }));
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Sona isolated agent settings conflict fixture" };
        thread.SetApartmentState(ApartmentState.STA);
        try
        {
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(20))) throw new TimeoutException("The isolated agent settings fixture did not finish.");
        }
        finally { Environment.SetEnvironmentVariable("SONA_DATA", priorData); Environment.SetEnvironmentVariable("SONA_DRAFT_TRANSFER", priorTransfer); }
        var report = new Report(checks.ToArray(), failures.ToArray());
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return report;
    }
}