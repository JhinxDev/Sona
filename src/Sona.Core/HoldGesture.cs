namespace Sona.Core;

public enum HoldAction { None, Begin, Finish, Cancel, QueueSend }
public readonly record struct HoldChange(HoldAction Action, long Generation, bool Suppress = false, bool Toggle = false, bool HandsFree = false, bool Submit = false);

// Only current physical down-state, never typed text or key history. One listener
// thread advances this reducer; the UI reads only the admitted generation.
public sealed class HoldGesture
{
    private readonly bool[] down = new bool[256];
    private long generation;
    private long current;
    private long due;
    private bool arming;
    private bool holding;
    private bool draining;
    private bool latched;
    private bool toggleGesture;
    private bool toggleSession;
    private bool handsFree;
    private bool swallowEnter;
    private int pendingSubmit;
    public bool PendingSubmit => Volatile.Read(ref pendingSubmit) != 0;
    private bool tapPending;
    private bool secondTap;
    private long tapReleased;
    private long pendingToggle;
    public long PendingToggle => Interlocked.Read(ref pendingToggle);
    public bool ConsumeToggle(long token) => Interlocked.CompareExchange(ref pendingToggle, 0, token) == token;
    public long Current => Interlocked.Read(ref current);
    private bool Control => down[0xA2] || down[0xA3];
    private bool Windows => down[0x5B] || down[0x5C];
    private bool Other { get { for (var key = 0; key < down.Length; key++) if (down[key] && key is not (0xA2 or 0xA3 or 0x5B or 0x5C)) return true; return false; } }
    private static int Normalize(int key) => key switch { 0x11 => 0xA2, 0x10 => 0xA0, 0x12 => 0xA4, _ => key };
    public void Baseline(IEnumerable<int> pressed)
    {
        Array.Clear(down);
        foreach (var key in pressed.Select(Normalize).Where(x => x >= 0 && x < 256)) down[key] = true;
        Invalidate();
    }
    public void Invalidate()
    {
        Interlocked.Exchange(ref current, 0); Interlocked.Exchange(ref pendingToggle, 0); arming = holding = false;
        handsFree = toggleSession = tapPending = secondTap = false; Volatile.Write(ref pendingSubmit, 0);
        draining = Control || Windows; latched = latched && draining;
    }
    public HoldChange Key(int key, bool pressed, long milliseconds, bool injected = false, bool toggle = false, bool finishOnEnter = false, bool completing = false)
    {
        key = Normalize(key);
        if (injected || key < 0 || key >= 256) return default;
        if (key == 0x0D && swallowEnter)
        {
            down[key] = pressed; if (!pressed) swallowEnter = false;
            return new(HoldAction.None, generation, Suppress: true);
        }
        if (key == 0x0D && pressed && completing && finishOnEnter && !Control && !Windows && !Other)
        {
            down[key] = true; swallowEnter = true; Volatile.Write(ref pendingSubmit, 1);
            return new(HoldAction.QueueSend, generation, Suppress: true, Submit: true);
        }
        // Only an admitted hands-free session consumes a bare physical Enter.
        // Drain repeat/up events so the original key cannot submit stale text.
        if (key == 0x0D && pressed && (handsFree || toggleSession) && PendingToggle == 0 && finishOnEnter && !Control && !Windows && !Other)
        {
            var token = generation; Invalidate(); down[key] = true; swallowEnter = true; Volatile.Write(ref pendingSubmit, 1);
            return new(HoldAction.Finish, token, Suppress: true, Toggle: true, HandsFree: true, Submit: true);
        }
        if (key == 0x0D && pressed && PendingSubmit && finishOnEnter && !Control && !Windows && !Other)
        { down[key] = true; swallowEnter = true; return new(HoldAction.None, generation, Suppress: true); }
        var repeat = pressed && down[key];
        if (repeat) return new(HoldAction.None, generation, latched && key is (0x5B or 0x5C));
        down[key] = pressed;
        if (pressed && Other) tapPending = false;
        if (PendingToggle != 0 && pressed && Other)
        {
            var token = PendingToggle; Invalidate();
            return new(HoldAction.Cancel, token, Toggle: true);
        }
        if (draining)
        {
            if (!Control && !Windows) { draining = false; latched = false; }
            return default;
        }
        if (arming || holding)
        {
            if (!Control || !Windows || Other)
            {
                if (arming && !toggleGesture && !Other && milliseconds < due)
                {
                    var tapToken = generation;
                    var startHandsFree = secondTap;
                    Interlocked.Exchange(ref current, 0); arming = holding = false;
                    draining = Control || Windows; latched = latched && draining;
                    tapPending = !startHandsFree; tapReleased = milliseconds; secondTap = false;
                    if (startHandsFree)
                    {
                        handsFree = true;
                        Interlocked.Exchange(ref pendingToggle, tapToken);
                        return new(HoldAction.Begin, tapToken, Toggle: true, HandsFree: true);
                    }
                    return default;
                }
                var action = holding ? (Other ? HoldAction.Cancel : HoldAction.Finish) : HoldAction.None;
                var token = generation;
                if (toggleGesture && !Other)
                {
                    Interlocked.Exchange(ref current, 0); arming = holding = false;
                    draining = Control || Windows; latched = latched && draining;
                }
                else Invalidate();
                return new(action, token, Toggle: toggleGesture);
            }
            return default;
        }
        if (Control && Windows && Other) { draining = true; return default; }
        if (Control && Windows && !Other)
        {
            if (handsFree)
            {
                var token = generation; Invalidate();
                return new(HoldAction.Finish, token, Toggle: true, HandsFree: true);
            }
            if (PendingToggle != 0) { var pending = PendingToggle; Invalidate(); return new(HoldAction.Cancel, pending, Toggle: true); }
            arming = latched = true; due = milliseconds + 200;
            toggleGesture = toggle;
            secondTap = !toggle && tapPending && milliseconds - tapReleased <= 350;
            tapPending = false;
            Interlocked.Exchange(ref current, ++generation);
            if (toggle) { arming = false; holding = true; toggleSession = true; Interlocked.Exchange(ref pendingToggle, generation); return new(HoldAction.Begin, generation, Toggle: true); }
        }
        return default;
    }
    public HoldChange Tick(long milliseconds)
    {
        if (!arming || milliseconds < due) return default;
        arming = false; holding = true;
        tapPending = secondTap = false;
        return new(HoldAction.Begin, generation);
    }
    public HoldChange Reconcile(long admittedGeneration, bool physicalChord)
    {
        // A normal release revokes Current before its terminal action reaches the
        // UI. Reconciliation must not replace that already queued Finish.
        if (Current != admittedGeneration || physicalChord) return default;
        Invalidate();
        return new(HoldAction.Cancel, admittedGeneration);
    }
}
