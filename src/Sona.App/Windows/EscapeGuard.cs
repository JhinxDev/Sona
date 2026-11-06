using System.Runtime.InteropServices;
namespace Sona.App.Windows;

/// <summary>
/// Active-only Esc cancellation. The low-level hook lives on its own message-loop
/// thread: a stalled WPF dispatcher must not delay the hook past the Windows
/// LowLevelHooksTimeout, or the cancel path would silently stop working.
/// </summary>
public sealed class EscapeGuard : IDisposable
{
    private readonly HookCallback callback;
    private readonly Action cancelImmediately;
    private readonly Thread listener;
    private readonly ManualResetEventSlim ready = new();
    private readonly object requestGate = new();
    private nint hook;
    private uint listenerId;
    private int disposed;
    private volatile bool armed;
    private Exception? startupError;
    private ManualResetEventSlim? requestDone;
    private Exception? requestError;
    private const uint ArmMessage = 0x8004;
    private const uint DisarmMessage = 0x8005;
    private const uint QuitMessage = 0x0012;

    public EscapeGuard(Action cancelImmediately)
    {
        this.cancelImmediately = cancelImmediately;
        callback = OnKey;
        listener = new Thread(Listen) { IsBackground = true, Name = "Sona escape" };
        listener.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(3)))
        {
            Interlocked.Exchange(ref disposed, 1);
            throw new IOException("Windows could not enable cancellation. Recording was not started.");
        }
        if (startupError != null)
        {
            Interlocked.Exchange(ref disposed, 1);
            throw new IOException("Windows could not enable cancellation. Recording was not started.", startupError);
        }
    }

    internal bool Hooked { get { lock (requestGate) return hook != 0; } }
    internal bool Armed => armed;
    internal bool ListenerAlive => listener.IsAlive;
    internal int ListenerThreadId => (int)listenerId;

    public void Arm(bool active)
    {
        if (!active)
        {
            // Best effort: the armed flag is cleared first, so even an orphaned hook
            // forwards Esc instead of suppressing it.
            try { Request(DisarmMessage); }
            catch (IOException) { }
            return;
        }
        Request(ArmMessage);
    }

    private void Request(uint message)
    {
        lock (requestGate)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            if (listenerId == 0 || !listener.IsAlive) throw new IOException("Windows could not enable cancellation. Recording was not started.");
            using var done = new ManualResetEventSlim();
            requestError = null;
            requestDone = done;
            try
            {
                if (!PostThreadMessage(listenerId, message, 0, 0)) throw new IOException("Windows could not enable cancellation. Recording was not started.");
                if (!done.Wait(TimeSpan.FromSeconds(2))) throw new IOException("Windows did not confirm cancellation availability.");
            }
            finally { requestDone = null; }
            if (requestError != null) throw new IOException("Windows could not enable cancellation. Recording was not started.", requestError);
        }
    }

    private void Listen()
    {
        listenerId = GetCurrentThreadId();
        // Create the thread message queue before the first PostThreadMessage.
        PeekMessage(out _, 0, 0, 0, 0);
        try
        {
            if (Volatile.Read(ref disposed) != 0) return;
            ready.Set();
            while (true)
            {
                if (GetMessage(out var message, 0, 0, 0) <= 0) break;
                if (message.Message == ArmMessage) Apply(true);
                else if (message.Message == DisarmMessage) Apply(false);
                else if (message.Message == QuitMessage) break;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { startupError = ex; }
        finally
        {
            armed = false;
            if (hook != 0) { UnhookWindowsHookEx(hook); hook = 0; }
            ready.Set();
            requestDone?.Set();
        }
    }

    private void Apply(bool active)
    {
        try
        {
            if (active)
            {
                if (hook == 0) hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
                if (hook == 0) throw new IOException("Windows could not enable cancellation. Recording was not started.");
                armed = true;
            }
            else
            {
                armed = false;
                if (hook != 0) { UnhookWindowsHookEx(hook); hook = 0; }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException) { requestError = ex; }
        finally { requestDone?.Set(); }
    }

    private nint OnKey(int code, nint message, nint data)
    {
        // Active-only, Esc-only. No key history, text, or modifier state is recorded.
        if (armed && code >= 0 && (message == 0x100 || message == 0x104) && Marshal.ReadInt32(data) == 0x1B)
        {
            cancelImmediately();
            return 1;
        }
        return CallNextHookEx(hook, code, message, data);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lock (requestGate)
        {
            armed = false;
            if (listenerId != 0) PostThreadMessage(listenerId, QuitMessage, 0, 0);
        }
        listener.Join(TimeSpan.FromSeconds(1));
    }

    [StructLayout(LayoutKind.Sequential)] private struct MessageData { public nint Window; public uint Message; public nuint WParam; public nint LParam; public uint Time; public int X; public int Y; public uint Private; }
    private delegate nint HookCallback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, HookCallback callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", EntryPoint = "GetMessageW")] private static extern int GetMessage(out MessageData message, nint window, uint minimum, uint maximum);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] private static extern bool PeekMessage(out MessageData message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")] private static extern bool PostThreadMessage(uint thread, uint message, nuint wparam, nint lparam);
}
