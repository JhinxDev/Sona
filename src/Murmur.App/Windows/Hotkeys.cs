using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Murmur.Core;
namespace Murmur.App.Windows;

public sealed class Hotkeys : IDisposable
{
    private readonly nint handle;
    private readonly HwndSource source;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Func<bool, Task<bool>> begin;
    private readonly Action finish;
    private readonly Action cancel;
    private readonly Action insert;
    private readonly Func<bool> toggleMode;
    private readonly bool legacyToggleRegistered;
    private readonly bool legacyInsertRegistered;
    private bool activeIsHold;
    public string LegacyWarning { get; }
    private readonly HoldGesture gesture = new();
    private readonly HookCallback callback;
    private readonly Thread listener;
    private readonly ManualResetEventSlim ready = new();
    private readonly DispatcherTimer watchdog = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private Exception? startupError;
    private uint listenerId;
    private nint keyboardHook;
    private long activeGeneration;
    private long startingGeneration;
    private HoldChange? startingTerminal;
    private long admissionEpoch;
    private int admitsHold = 1;
    private int disposed;
    private const uint ResetMessage = 0x8001;
    private const uint BaselineMessage = 0x8002;
    private const uint ReconcileMessage = 0x8003;
    public Hotkeys(nint handle, Func<bool, bool> begin, Action finish, Action cancel, Action insert, Func<bool>? toggleMode = null) : this(handle, begin, finish, cancel, insert, true, toggleMode) { }
    internal Hotkeys(nint handle, Func<bool, bool> begin, Action finish, Action cancel, Action insert, bool registerLegacy, Func<bool>? toggleMode = null) : this(handle, hold => Task.FromResult(begin(hold)), finish, cancel, insert, registerLegacy, toggleMode) { }
    public Hotkeys(nint handle, Func<bool, Task<bool>> begin, Action finish, Action cancel, Action insert, Func<bool>? toggleMode = null) : this(handle, begin, finish, cancel, insert, true, toggleMode) { }
    private Hotkeys(nint handle, Func<bool, Task<bool>> begin, Action finish, Action cancel, Action insert, bool registerLegacy, Func<bool>? toggleMode = null)
    {
        this.handle = handle; this.begin = begin; this.finish = finish; this.cancel = cancel; this.insert = insert;
        this.toggleMode = toggleMode ?? (() => false);
        source = HwndSource.FromHwnd(handle); source.AddHook(WindowHook);
        legacyToggleRegistered = registerLegacy && RegisterHotKey(handle, 2, 0x4007, 0x20);
        legacyInsertRegistered = registerLegacy && RegisterHotKey(handle, 4, 0x4007, 0x56);
        LegacyWarning = registerLegacy ? string.Join(" ", new[] { legacyToggleRegistered ? "" : "The alternate toggle shortcut is in use.", legacyInsertRegistered ? "" : "The alternate insertion shortcut is in use." }.Where(x => x.Length != 0)) : "";
        callback = OnKey;
        listener = new Thread(Listen) { IsBackground = true, Name = "Sona shortcut" };
        listener.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(3)) || startupError != null)
        {
            Dispose(); throw new IOException("Windows could not enable Ctrl+Windows. Recording was not started.", startupError);
        }
        watchdog.Tick += (_, _) =>
        {
            if (activeGeneration != 0 && !listener.IsAlive)
            {
                activeGeneration = 0; watchdog.Stop(); Reset(); cancel();
            }
            else if (activeGeneration != 0 && activeIsHold && !PhysicalChord()) PostThreadMessage(listenerId, ReconcileMessage, (nuint)activeGeneration, 0);
        };
    }
    private static long Now => (long)Stopwatch.GetElapsedTime(0).TotalMilliseconds;
    private static bool Held(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static bool PhysicalChord() => (Held(0xA2) || Held(0xA3)) && (Held(0x5B) || Held(0x5C)) && !Held(0x12) && !Held(0x10);
    private void ReadBaseline() => gesture.Baseline(Enumerable.Range(8, 248).Where(key => key is not (0x10 or 0x11 or 0x12) && Held(key)));
    public bool CanBeginHold => startingGeneration != 0 && gesture.Current == startingGeneration && Volatile.Read(ref admitsHold) != 0 && PhysicalChord();
    public bool IsToggleAdmission => startingGeneration != 0;
    public bool CanBeginToggle => startingGeneration != 0 && gesture.PendingToggle == startingGeneration && Volatile.Read(ref admitsHold) != 0;
    internal bool ListenerAlive => listener.IsAlive;
    private void Listen()
    {
        listenerId = GetCurrentThreadId();
        PeekMessage(out _, 0, 0, 0, 0);
        nuint timerId = 0;
        try
        {
            if (Volatile.Read(ref disposed) != 0) return;
            ReadBaseline();
            keyboardHook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
            timerId = SetTimer(0, 1, 25, 0);
            if (keyboardHook == 0 || timerId == 0) throw new IOException("Shortcut listener unavailable.");
            ready.Set();
            while (Volatile.Read(ref disposed) == 0 && GetMessage(out var message, 0, 0, 0) > 0)
            {
                if (message.Message == 0x0113 && message.WParam == timerId) Dispatch(gesture.Tick(Now));
                else if (message.Message == ResetMessage) { gesture.Invalidate(); Volatile.Write(ref admitsHold, 1); }
                else if (message.Message == BaselineMessage) { ReadBaseline(); Volatile.Write(ref admitsHold, 1); }
                else if (message.Message == ReconcileMessage)
                {
                    var change = gesture.Reconcile((long)message.WParam, PhysicalChord());
                    if (change.Action != HoldAction.None) { ReadBaseline(); Dispatch(change); }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { startupError = ex; ready.Set(); }
        finally { gesture.Invalidate(); if (timerId != 0) KillTimer(0, timerId); if (keyboardHook != 0) UnhookWindowsHookEx(keyboardHook); keyboardHook = 0; }
    }
    private nint OnKey(int code, nint message, nint data)
    {
        if (code >= 0 && Volatile.Read(ref disposed) == 0 && (message == 0x100 || message == 0x101 || message == 0x104 || message == 0x105))
        {
            var change = gesture.Key(Marshal.ReadInt32(data), message == 0x100 || message == 0x104, Now, (Marshal.ReadInt32(data, 8) & 0x10) != 0, toggleMode());
            Dispatch(change);
            if (change.Suppress) return 1;
        }
        return CallNextHookEx(keyboardHook, code, message, data);
    }
    private void Dispatch(HoldChange change)
    {
        if (change.Action == HoldAction.None) return;
        var epoch = Interlocked.Read(ref admissionEpoch);
        dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(async () =>
        {
            if (Volatile.Read(ref disposed) != 0) return;
            if (change.Action == HoldAction.Begin)
            {
                if (epoch != Interlocked.Read(ref admissionEpoch) || Volatile.Read(ref admitsHold) == 0 || (change.Toggle ? gesture.PendingToggle != change.Generation : gesture.Current != change.Generation || !PhysicalChord())) return;
                if (startingGeneration != 0) return;
                startingGeneration = change.Generation;
                startingTerminal = null;
                try
                {
                    var began = await begin(!change.Toggle);
                    if (began && epoch == Interlocked.Read(ref admissionEpoch) && Volatile.Read(ref disposed) == 0)
                    {
                        if (startingTerminal is { } terminal)
                        {
                            if (terminal.Action == HoldAction.Finish) finish(); else cancel();
                        }
                        else
                        {
                            activeGeneration = change.Generation; activeIsHold = !change.Toggle;
                            if (change.Toggle) gesture.ConsumeToggle(change.Generation);
                            watchdog.Start();
                        }
                    }
                    else { if (began && !change.Toggle) cancel(); Reset(); }
                }
                finally { startingGeneration = 0; startingTerminal = null; }
            }
            // Legacy Toggle releases are informational; only an explicit handsfree
            // stop or a physical Hold release is a terminal Finish.
            else if (change.Action == HoldAction.Finish && change.Toggle && !change.HandsFree) return;
            else if (startingGeneration == change.Generation) startingTerminal = change;
            else if (activeGeneration == change.Generation)
            {
                activeGeneration = 0; watchdog.Stop();
                if (change.Action == HoldAction.Finish) finish(); else cancel();
            }
        }));
    }
    private nint WindowHook(nint hwnd, int msg, nint wparam, nint lparam, ref bool handled)
    {
        if (msg == 0x0312 && (wparam == 2 || wparam == 4))
        {
            handled = true;
            if (wparam == 2) _ = begin(false); else insert();
        }
        return 0;
    }
    private void Reset() { Interlocked.Increment(ref admissionEpoch); Volatile.Write(ref admitsHold, 0); if (listenerId != 0) PostThreadMessage(listenerId, ResetMessage, 0, 0); }
    public void ResetBaseline() { activeGeneration = 0; watchdog.Stop(); Interlocked.Increment(ref admissionEpoch); Volatile.Write(ref admitsHold, 0); if (listenerId != 0) PostThreadMessage(listenerId, BaselineMessage, 0, 0); }
    public void Recording(bool active) { if (!active) { activeGeneration = 0; watchdog.Stop(); Reset(); } }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        watchdog.Stop(); source.RemoveHook(WindowHook); if (legacyToggleRegistered) UnregisterHotKey(handle, 2); if (legacyInsertRegistered) UnregisterHotKey(handle, 4);
        if (listenerId != 0) PostThreadMessage(listenerId, 0x0012, 0, 0);
        listener.Join(TimeSpan.FromSeconds(1));
    }
    [StructLayout(LayoutKind.Sequential)] private struct MessageData { public nint Window; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X; public int Y; public uint Private; }
    private delegate nint HookCallback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetMessageW")] private static extern int GetMessage(out MessageData message, nint window, uint minimum, uint maximum);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] private static extern bool PeekMessage(out MessageData message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")] private static extern bool PostThreadMessage(uint thread, uint message, nuint wparam, nint lparam);
    [DllImport("user32.dll")] private static extern nuint SetTimer(nint window, nuint id, uint milliseconds, nint callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(nint window, nuint id);
}
