using Sona.Core;
internal static class LiveDraftChecks
{
    public static void Run(Action<bool, string> check)
    {
        var live = new LiveDraft();
        check(live.Preview(1, "first words") && live.Draft == "first words", "Live words are provisional recognition output");
        check(!live.Preview(1, "stale") && live.Raw == "first words", "Stale preview cannot replace newer recognition");
        live.Edit("My corrected words"); live.Preview(2, "first words plus more");
        check(live.Draft == "My corrected words" && live.Raw.EndsWith("more"), "Manual edits survive later live updates while raw continues");
        live.Complete("Final original", "Final cleaned");
        check(live.Final && live.Draft == "My corrected words" && live.Cleaned == "Final cleaned", "Cleanup stays a separate suggestion after manual edits");
        check(!live.Preview(3, "late") && !live.Complete("duplicate", "duplicate"), "Final state refuses late preview and duplicate completion");
        var cancelled = new LiveDraft(); cancelled.Preview(1, "retained"); cancelled.Cancel();
        check(!cancelled.Preview(2, "late") && !cancelled.Complete("late", "late") && cancelled.Draft == "retained", "Cancellation preserves review text and rejects pending results");
        var untouched = new LiveDraft(); untouched.Preview(1, "raw"); untouched.Complete("original", "cleaned");
        check(untouched.Draft == "cleaned" && untouched.Raw == "original", "Unedited final result exposes original and cleaned text");
        var empty = new LiveDraft(); empty.Edit(""); empty.Complete("recognized", "cleaned");
        check(empty.Draft == "" && empty.ManuallyEdited, "Intentional deletion is not refilled by cleanup");
    }
}
