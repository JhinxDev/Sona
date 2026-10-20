namespace Sona.Core;

// The keyboard thread records intent before the UI can freeze completion.
// Closing this owner prevents a late Enter from leaking into a later draft.
public sealed class PendingSubmission
{
    private int state;
    public bool IsOpen => Volatile.Read(ref state) != 0;
    public bool IsRequested => Volatile.Read(ref state) == 2;
    public void Begin(bool enabled) => Interlocked.Exchange(ref state, enabled ? 1 : 0);
    public bool TryRequest() { var prior = Interlocked.CompareExchange(ref state, 2, 1); return prior is 1 or 2; }
    public bool Complete() => Interlocked.Exchange(ref state, 0) == 2;
}
