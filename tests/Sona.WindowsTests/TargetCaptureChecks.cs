using System.Windows.Automation;
using Sona.App.Windows;

internal static class TargetCaptureChecks
{
    public static int Run()
    {
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
        var field = new TargetFieldMetadata(31001, ControlType.Edit.Id, true, false, false, [1, 2, 3]);
        TargetCaptureResult Inspect(TargetFieldMetadata candidate, bool contained = true, long endEpoch = 4, int owner = 31001) => Insertion.EvaluateCapture(candidate, 101, owner, contained, 4, endEpoch);
        Check(Inspect(field).Target != null, "An enabled writable ordinary field remains capturable");
        Check(Inspect(field with { Password = true }).Status == TargetCaptureStatus.PasswordField, "Password rejection has a specific safe reason");
        Check(Inspect(field with { Enabled = false }).Status == TargetCaptureStatus.DisabledField, "Disabled fields remain protected");
        Check(Inspect(field with { ReadOnly = true }).Status == TargetCaptureStatus.ReadOnlyField, "Read-only editors remain protected");
        Check(Inspect(field with { ControlTypeId = ControlType.Document.Id, ReadOnly = true }).Target == null, "A browser document exposed as read-only is never an insertion destination");
        Check(Inspect(field, contained: false).Target == null, "An element in a different window is rejected even with the same process ID");
        Check(Inspect(field, endEpoch: 5).Status == TargetCaptureStatus.FocusChanged, "A focus change during capture revokes the destination");
        Check(Inspect(field with { RuntimeId = [] }).Target == null, "An element without a stable runtime identity is rejected");
        var hosted = Inspect(field, owner: 31002);
        Check(hosted.Target != null && hosted.Target.ProcessId == field.ProcessId && hosted.Target.OwnerProcessId == 31002, "A writable hosted provider inside the exact foreground window can be captured");
        Check(Inspect(field with { ControlTypeId = ControlType.ComboBox.Id }).Target != null, "A writable editable combobox is a supported text destination");
        Check(Inspect(field with { ControlTypeId = ControlType.ComboBox.Id, ReadOnly = null }).Target == null, "Comboboxes without writable-value proof remain rejected");
        Check(Inspect(field with { ControlTypeId = ControlType.ComboBox.Id, ReadOnly = true }).Target == null, "Selection-only or read-only comboboxes remain rejected");
        Check(Inspect(field with { ControlTypeId = ControlType.Button.Id }).Status == TargetCaptureStatus.UnsupportedControl, "A button cannot become a text destination");
        var hostedTarget = hosted.Target!;
        Check(!Insertion.Same(hostedTarget, hostedTarget with { WindowProcessId = 31003 }), "A changed HWND owner cannot reuse a hosted field identity");
        var context = new RecordingDestinationContext(); context.Begin(hostedTarget, "Hosted editor");
        context.Observe(new ActiveApplication(101, 31002, "Hosted editor", 4));
        Check(!context.DestinationChanged, "The foreground host identity keeps the admitted provider destination stable");
        context.Observe(new ActiveApplication(101, 31003, "Other host", 4));
        Check(context.DestinationChanged, "A reused host window revokes the original provider destination");
        return count;
    }
}
