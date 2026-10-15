using Sona.App.Windows;
using System.Windows.Threading;

public static class ActiveApplicationChecks
{
    public static async Task<int> RunAsync(bool includeIcons = true)
    {
        var passed = 0;
        void Check(bool value, string name) { if (!value) throw new IOException(name); Console.WriteLine("PASS " + name); passed++; }
        var codex = new ActiveApplication((nint)1, 10, "Codex", 0);
        var discord = new ActiveApplication((nint)2, 20, "Discord", 1);
        var current = codex; var reads = 0; var events = 0;
        using var observer = new ActiveApplicationObserver(Dispatcher.CurrentDispatcher, () => { reads++; return current; });
        observer.Changed += _ => events++;
        observer.Refresh(); observer.Refresh();
        Check(events == 1 && reads == 2 && observer.Current == codex, "Unchanged app snapshots do not repeatedly update labels");
        current = discord; observer.Refresh();
        Check(events == 2 && observer.Current == discord, "Clicking another foreground app changes its observed identity");
        current = discord with { FocusEpoch = 2 }; observer.Refresh();
        Check(events == 3, "Same-app focus changes still reach destination safety state");
        observer.Dispose(); var stoppedReads = reads; current = codex; observer.Refresh();
        Check(reads == stoppedReads && events == 3, "Disposed observer stops reads and notifications");
        Check(ActiveApplicationObserver.FriendlyName("codex") == "Codex" && ActiveApplicationObserver.FriendlyName("DISCORD") == "Discord" && ActiveApplicationObserver.FriendlyName("msedge") == "msedge", "Process names normalize without inferring browser websites");
        var context = new RecordingDestinationContext(); context.Observe(codex);
        Check(context.ActiveAppName == "Codex" && context.Label == "Active app: Codex", "Idle label follows the active app before recording");
        var target = new DraftTarget((nint)1, 10, [1, 2], 0);
        context.Begin(target, "Codex"); context.Observe(codex);
        Check(context.Destination == target && !context.DestinationChanged && context.Label == "Codex", "Recording freezes the admitted destination");
        context.Observe(discord);
        Check(context.Destination == target && context.DestinationChanged && context.ActiveAppName == "Discord" && context.Label.StartsWith("Draft stays in Sona") && context.Label.Contains("Codex → Discord"), "Switching app shows retention and never silently retargets the draft");
        context.Observe(codex);
        Check(context.DestinationChanged, "Leaving then returning cannot clear the retention warning");
        context.End(); context.Observe(discord);
        Check(context.Destination == null && !context.DestinationChanged && context.Label == "Active app: Discord", "Ending the session restores live idle labels");
        context.Begin(target, "Codex"); context.Observe(codex with { FocusEpoch = 1 });
        Check(context.DestinationChanged && context.Label.Contains("focus changed"), "A different field in the same app is clearly flagged");
        context.End(); context.Begin(target, "Codex"); context.Observe(new ActiveApplication((nint)1, 11, "Other app", 0));
        Check(context.DestinationChanged && context.Label.Contains("Codex → Other app"), "A reused foreground window with a different process is flagged as destination drift");
        context.End(); context.Begin(null, "No focused text field"); context.Observe(discord);
        Check(context.Destination == null && context.Label == "Discord", "Active-app identity outside an editable field never creates an insertion destination");
        var sends = 0;
        using var insertion = new Insertion(() => target, () => false, text => { sends++; return (uint)text.Length * 2; });
        Check((await insertion.InsertAsync(target, "Hello Codex.")).Inserted && sends == 1, "Stable single-line destination still reaches the injected input sink once");
        Check(!(await insertion.InsertAsync(target, "First\nSecond")).Inserted && !(await insertion.InsertAsync(target, new string('x', 6001))).Inserted && sends == 1, "Multiline and oversized payloads retain their existing refusal");
        using var changed = new Insertion(() => target with { FocusEpoch = 1 }, () => false, _ => { sends++; return 2; });
        Check(!(await changed.InsertAsync(target, "Do not redirect")).Inserted && sends == 1, "Actual insertion guard independently refuses focus drift");
        using var modifiers = new Insertion(() => target, () => true, _ => { sends++; return 2; });
        Check(!(await modifiers.InsertAsync(target, "Held modifiers")).Inserted && sends == 1, "Held modifier keys still refuse insertion");
        return passed + (includeIcons ? await ApplicationIconChecks.RunAsync() : 0);
    }
}
