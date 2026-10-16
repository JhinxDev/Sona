using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Sona.App.Windows;

public enum TargetCaptureStatus
{
    Ready, TrackingUnavailable, NoForegroundWindow, NoFocusedControl, SonaFocused,
    PasswordField, DisabledField, UnsupportedControl, ReadOnlyField,
    OutsideForegroundWindow, FocusChanged, ProviderUnavailable
}

public sealed record TargetCaptureResult(DraftTarget? Target, TargetCaptureStatus Status)
{
    public int? ControlTypeId { get; init; }
    public int? ProviderProcessId { get; init; }
    public int? WindowProcessId { get; init; }
    public string Detail => Status switch
    {
        TargetCaptureStatus.Ready => "The focused text field was captured.",
        TargetCaptureStatus.TrackingUnavailable => "Windows focus tracking is unavailable. Your draft stays in Sona; copy it explicitly.",
        TargetCaptureStatus.NoForegroundWindow => "Windows did not expose an active destination window. Your draft stays in Sona.",
        TargetCaptureStatus.NoFocusedControl => "Windows did not expose a focused control. Focus the destination, then try Test shortcut without using the microphone.",
        TargetCaptureStatus.SonaFocused => "Sona had focus when recording began. Focus the destination and use Ctrl + Windows, or copy this draft.",
        TargetCaptureStatus.PasswordField => "Password fields are protected. Your draft stays in Sona.",
        TargetCaptureStatus.DisabledField => "The focused field is disabled. Your draft stays in Sona.",
        TargetCaptureStatus.UnsupportedControl => "Windows exposed a control that Sona cannot safely type into. Try Test shortcut with the caret in the field, or copy this draft.",
        TargetCaptureStatus.ReadOnlyField => "Windows exposed the focused control as read-only. Your draft stays in Sona; try Test shortcut in the editable field.",
        TargetCaptureStatus.OutsideForegroundWindow => "Windows reported a focused field outside the active window. Your draft stays in Sona.",
        TargetCaptureStatus.FocusChanged => "Focus changed while Sona checked the destination. Your draft stays in Sona.",
        _ => "Windows accessibility could not inspect the destination. Your draft stays in Sona; try Test shortcut or copy it explicitly."
    };
}

internal sealed record TargetFieldMetadata(int ProcessId, int ControlTypeId, bool Enabled, bool Password, bool? ReadOnly, int[] RuntimeId);

public sealed partial class Insertion
{
    public TargetCaptureResult CaptureWithStatus()
    {
        if (readTarget != null)
        {
            var injected = readTarget();
            return new(injected, injected == null ? TargetCaptureStatus.NoFocusedControl : TargetCaptureStatus.Ready);
        }
        if (hook == 0 || foregroundHook == 0) return new(null, TargetCaptureStatus.TrackingUnavailable);
        var epoch = FocusEpoch;
        var window = GetForegroundWindow();
        if (window == 0 || GetWindowThreadProcessId(window, out var owner) == 0) return new(null, TargetCaptureStatus.NoForegroundWindow);
        try
        {
            var element = AutomationElement.FocusedElement;
            if (element == null) return new(null, TargetCaptureStatus.NoFocusedControl);
            var current = element.Current;
            bool? readOnly = null;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var value)) readOnly = ((ValuePattern)value).Current.IsReadOnly;
            else if (element.TryGetCurrentPattern(TextPattern.Pattern, out var text))
            {
                var attribute = ((TextPattern)text).DocumentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);
                if (attribute is bool state) readOnly = state;
            }
            var field = new TargetFieldMetadata(current.ProcessId, current.ControlType.Id, current.IsEnabled, current.IsPassword, readOnly, element.GetRuntimeId());
            var preliminary = EvaluateCapture(field, window, (int)owner, true, epoch, epoch);
            if (preliminary.Target == null) return preliminary;
            var contained = ElementBelongsToWindow(element, window);
            if (window != GetForegroundWindow() || GetWindowThreadProcessId(window, out var finalOwner) == 0 || owner != finalOwner)
                return new(null, TargetCaptureStatus.FocusChanged);
            return EvaluateCapture(field, window, (int)owner, contained, epoch, FocusEpoch);
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException)
        { return new(null, TargetCaptureStatus.ProviderUnavailable); }
    }

    internal static TargetCaptureResult EvaluateCapture(TargetFieldMetadata field, nint window, int owner, bool contained, long firstEpoch, long finalEpoch)
    {
        TargetCaptureResult Result(TargetCaptureStatus status, DraftTarget? target = null) => new(target, status) { ControlTypeId = field.ControlTypeId, ProviderProcessId = field.ProcessId, WindowProcessId = owner };
        if (field.ProcessId == Environment.ProcessId) return Result(TargetCaptureStatus.SonaFocused);
        if (field.Password) return Result(TargetCaptureStatus.PasswordField);
        if (!field.Enabled) return Result(TargetCaptureStatus.DisabledField);
        if (field.ControlTypeId != ControlType.Edit.Id && field.ControlTypeId != ControlType.Document.Id && !(field.ControlTypeId == ControlType.ComboBox.Id && field.ReadOnly == false))
            return Result(TargetCaptureStatus.UnsupportedControl);
        if (field.ReadOnly == true) return Result(TargetCaptureStatus.ReadOnlyField);
        // Native HWND/UIA containment is required even when process IDs match.
        // Hosted providers retain their own identity and the HWND owner's PID.
        if (!contained) return Result(TargetCaptureStatus.OutsideForegroundWindow);
        if (field.RuntimeId.Length == 0 || firstEpoch != finalEpoch) return Result(TargetCaptureStatus.FocusChanged);
        return Result(TargetCaptureStatus.Ready, new DraftTarget(window, field.ProcessId, field.RuntimeId, finalEpoch, owner));
    }

    internal static bool ElementBelongsToWindow(AutomationElement element, nint window)
    {
        var root = AutomationElement.FromHandle(window);
        AutomationElement? node = element;
        // Prove membership in this exact HWND, including hosted providers. A
        // shared PID or process ancestry is never evidence of window membership.
        for (var depth = 0; node != null && depth < 32; depth++)
        {
            if (Automation.Compare(node, root)) return true;
            var handle = (nint)node.Current.NativeWindowHandle;
            if (handle != 0 && GetAncestor(handle, 2) != window) return false;
            node = TreeWalker.RawViewWalker.GetParent(node);
        }
        return false;
    }
}
