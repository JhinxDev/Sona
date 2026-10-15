using Sona.App.Windows;

internal static class InsertionChecks
{
    public static async Task<int> RunAsync()
    {
        var passed = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed++; }
        var target = new DraftTarget((nint)1, 10, [1, 2], 0);
        var current = target; var sends = 0;
        using var insertion = new Insertion(() => current, () => false, text => { sends++; return (uint)text.Length * 2; });
        insertion.ObserveFocusEvent((nint)2, (nint)1);
        Check(insertion.FocusEpoch == 0, "Background accessibility focus must not invalidate the foreground composer");
        insertion.ObserveFocusEvent((nint)1, (nint)1);
        Check(insertion.FocusEpoch == 1, "Foreground field focus change invalidates captured destination");
        insertion.ObserveFocusEvent(0, 0);
        Check(insertion.FocusEpoch == 2, "Unknown foreground focus remains fail closed");
        Check((await insertion.InsertAsync(target, "Bonjour Discord")).Inserted && sends == 1, "Same focused composer accepts one Unicode batch");
        current = target with { FocusEpoch = 1 };
        Check(!(await insertion.InsertAsync(target, "test")).Inserted && sends == 1, "Focus leave and return refuses input");
        current = target with { RuntimeId = [3] };
        Check(!(await insertion.InsertAsync(target, "test")).Inserted, "Different field in same window refuses input");
        Check(!(await insertion.InsertAsync(null, "test")).Inserted, "Hover without focused field never sends");
        Check(!(await insertion.InsertAsync(target, "test\nsubmit")).Inserted && sends == 1, "No line break submission or clipboard transport");
        return passed;
    }
}
