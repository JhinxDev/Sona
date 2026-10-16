using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Automation;

namespace Sona.App.Windows;

public enum TargetCaptureStatus
{
    Ready, TrackingUnavailable, NoForegroundWindow, NoFocusedControl, SonaFocused,
    PasswordField, DisabledField, UnsupportedControl, ReadOnlyField,
    OutsideForegroundWindow, FocusChanged, ProviderUnavailable, SuggestionFocused
}

public sealed record TargetCaptureResult(DraftTarget? Target, TargetCaptureStatus Status)
{
    public int? ControlTypeId { get; init; }
    public int? ProviderProcessId { get; init; }
    public int? WindowProcessId { get; init; }
    public long ForegroundWindow { get; init; }
    public long FocusEpochBefore { get; init; }
    public long FocusEpochAfter { get; init; }
    public bool Stable { get; init; }
    public bool? FieldInForegroundWindow { get; init; }
    public string? RuntimeIdentityHash { get; init; }
    internal static string? HashRuntimeId(int[] runtimeId) => runtimeId.Length == 0 ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(",", runtimeId))));
    public string Detail => Status switch
    {
        TargetCaptureStatus.Ready => "The focused text field was captured.",
        TargetCaptureStatus.TrackingUnavailable => "Windows focus tracking is unavailable. Your draft stays in Sona; copy it explicitly.",
        TargetCaptureStatus.NoForegroundWindow => "Windows did not expose an active destination window. Your draft stays in Sona.",
        TargetCaptureStatus.NoFocusedControl => "Windows did not expose a focused control. Focus the destination, then try Test shortcut without using the microphone.",
        TargetCaptureStatus.SonaFocused => "Sona had focus when recording began. Focus the destination and use Ctrl + Windows, or copy this draft.",
        TargetCaptureStatus.PasswordField => "Password fields are protected. Your draft stays in Sona.",
        TargetCaptureStatus.DisabledField => "The focused field is disabled. Your draft stays in Sona.",
        TargetCaptureStatus.UnsupportedControl => "Windows did not identify an editable text field. Your draft stays in Sona. Use Copy draft, then paste into the destination yourself.",
        TargetCaptureStatus.SuggestionFocused => "Windows is reporting a suggestion or list item instead of the text field. Your draft stays in Sona. Use Copy draft, then paste into the field yourself.",
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
            var contained = ElementBelongsToWindow(element, window);
            // Re-read actual focused identity as well as the event epoch. A UIA
            // call may pump messages or race a provider even for rejected types.
            var finalElement = AutomationElement.FocusedElement;
            var sameElement = finalElement != null && finalElement.Current.ProcessId == field.ProcessId && finalElement.GetRuntimeId().SequenceEqual(field.RuntimeId);
            var sameWindow = window == GetForegroundWindow() && GetWindowThreadProcessId(window, out var finalOwner) != 0 && owner == finalOwner;
            var result = EvaluateCapture(field, window, (int)owner, contained, epoch, FocusEpoch);
            return sameElement && sameWindow ? result : result with { Target = null, Status = TargetCaptureStatus.FocusChanged, Stable = false };
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException)
        { return new(null, TargetCaptureStatus.ProviderUnavailable); }
    }

    internal static TargetCaptureResult EvaluateCapture(TargetFieldMetadata field, nint window, int owner, bool contained, long firstEpoch, long finalEpoch)
    {
        var stable = field.RuntimeId.Length > 0 && firstEpoch == finalEpoch;
        TargetCaptureResult Result(TargetCaptureStatus status, DraftTarget? target = null) => new(target, status)
        {
            ControlTypeId = field.ControlTypeId, ProviderProcessId = field.ProcessId, WindowProcessId = owner,
            ForegroundWindow = window.ToInt64(), FocusEpochBefore = firstEpoch, FocusEpochAfter = finalEpoch,
            Stable = stable, FieldInForegroundWindow = contained, RuntimeIdentityHash = TargetCaptureResult.HashRuntimeId(field.RuntimeId)
        };
        // Rejection reasons also need exact scope and stable identity. An early
        // unsupported-type result must not be mistaken for target-app evidence.
        if (!contained) return Result(TargetCaptureStatus.OutsideForegroundWindow);
        if (!stable) return Result(TargetCaptureStatus.FocusChanged);
        if (field.ProcessId == Environment.ProcessId) return Result(TargetCaptureStatus.SonaFocused);
        if (field.Password) return Result(TargetCaptureStatus.PasswordField);
        if (!field.Enabled) return Result(TargetCaptureStatus.DisabledField);
        // ARIA active descendants can expose a selected option while DOM input
        // focus remains elsewhere. A controls relationship alone is not proof
        // of keyboard ownership, so never promote a suggestion to its controller.
        if (field.ControlTypeId == ControlType.ListItem.Id) return Result(TargetCaptureStatus.SuggestionFocused);
        if (field.ControlTypeId != ControlType.Edit.Id && field.ControlTypeId != ControlType.Document.Id && !(field.ControlTypeId == ControlType.ComboBox.Id && field.ReadOnly == false))
            return Result(TargetCaptureStatus.UnsupportedControl);
        if (field.ReadOnly == true) return Result(TargetCaptureStatus.ReadOnlyField);
        // Hosted providers retain their own identity and the HWND owner's PID.
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
