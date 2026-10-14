using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
namespace Sona.App.Windows;

public sealed record DraftTarget(nint Window, int ProcessId, int[] RuntimeId, long FocusEpoch);
public sealed class Insertion : IDisposable
{
    private long focusEpoch;
    private readonly WinEventDelegate callback;
    private readonly nint hook;
    private readonly Func<DraftTarget?>? readTarget;
    private readonly Func<bool>? readModifiers;
    private readonly Func<string, uint>? send;
    internal long FocusEpoch => Interlocked.Read(ref focusEpoch);
    public Insertion() { callback = (_, _, _, _, _, _, _) => Interlocked.Increment(ref focusEpoch); hook = SetWinEventHook(0x8005, 0x8005, 0, callback, 0, 0, 0); }
    internal Insertion(Func<DraftTarget?> readTarget, Func<bool> readModifiers, Func<string, uint> send) { callback = (_, _, _, _, _, _, _) => { }; this.readTarget = readTarget; this.readModifiers = readModifiers; this.send = send; }
    public DraftTarget? Capture()
    {
        if (readTarget != null) return readTarget();
        try
        {
            if (hook == 0) return null;
            var element = AutomationElement.FocusedElement;
            if (element == null || element.Current.ProcessId == Environment.ProcessId || element.Current.IsPassword || !element.Current.IsEnabled) return null;
            if (element.Current.ControlType != ControlType.Edit && element.Current.ControlType != ControlType.Document) return null;
            return new(GetForegroundWindow(), element.Current.ProcessId, element.GetRuntimeId(), Interlocked.Read(ref focusEpoch));
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException) { return null; }
    }
    public static string AppName(DraftTarget? target)
    {
        if (target == null) return "No focused text field";
        try
        {
            using var process = Process.GetProcessById(target.ProcessId);
            var name = process.ProcessName;
            string? executable = null;
            try { executable = process.MainModule?.FileName; }
            catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            return ActiveApplicationObserver.FriendlyName(name, executable);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return "Captured app"; }
    }
    public static bool Same(DraftTarget? target, DraftTarget? current) => target != null && current != null && target.Window == current.Window && target.ProcessId == current.ProcessId && target.RuntimeId.SequenceEqual(current.RuntimeId) && target.FocusEpoch == current.FocusEpoch;
    public async Task<(bool Inserted, string Detail)> InsertAsync(DraftTarget? target, string text, CancellationToken cancellation = default)
    {
        if (target == null) return (false, "Focus an editable field, then use the recording shortcut. Your transcript is ready to copy.");
        for (var i = 0; i < 30 && ModifiersDown(); i++) await Task.Delay(20, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (ModifiersDown()) return (false, "Release the modifier keys before inserting. Your transcript is safe here.");
        if (text.Any(char.IsControl)) return (false, "This draft contains line breaks or control characters. Copy it explicitly for this app.");
        if (text.Length > 6000) return (false, "This draft is too long for safe typing. Copy it explicitly.");
        var input = new Input[text.Length * 2];
        for (var i = 0; i < text.Length; i++)
        {
            input[2 * i] = new Input { Type = 1, Data = new Union { Keyboard = new Keyboard { Scan = text[i], Flags = 4 } } };
            input[2 * i + 1] = new Input { Type = 1, Data = new Union { Keyboard = new Keyboard { Scan = text[i], Flags = 6 } } };
        }
        // One OS batch, only Unicode packets. No Return/Tab/navigation keys or clipboard mutations.
        if (!Same(target, Capture())) return (false, "Focus changed. Your transcript stayed here instead of going into another field.");
        if (ModifiersDown()) return (false, "A modifier key changed before insertion. Your transcript is safe here.");
        cancellation.ThrowIfCancellationRequested();
        var sent = send != null ? send(text) : SendInput((uint)input.Length, input, Marshal.SizeOf<Input>());
        if (sent == 0) return (false, "Windows blocked insertion. Check that the destination is not running as administrator.");
        if (sent != input.Length) return (true, "Windows accepted only part of the input. Check your draft; automatic retry is disabled to prevent duplicates.");
        return (true, "Typed into the captured field. Nothing was submitted.");
    }
    private bool ModifiersDown() => readModifiers?.Invoke() ?? new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
    public void Dispose() { if (hook != 0) UnhookWinEvent(hook); }
    private delegate void WinEventDelegate(nint hook, uint evt, nint hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public Union Data; }
    [StructLayout(LayoutKind.Explicit)] private struct Union { [FieldOffset(0)] public Mouse Mouse; [FieldOffset(0)] public Keyboard Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
}
