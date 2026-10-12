namespace Murmur.Core;

public enum SessionState { Idle, Recording, Transcribing, Ready, Inserted, Cancelled, Failed }
public sealed class DictationSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public SessionState State { get; private set; } = SessionState.Idle;
    public string Original { get; private set; } = "";
    public string Draft { get; private set; } = "";
    public string Error { get; private set; } = "";
    public bool Begin() { if (State != SessionState.Idle) return false; State = SessionState.Recording; return true; }
    public bool Stop() { if (State != SessionState.Recording) return false; State = SessionState.Transcribing; return true; }
    public bool Complete(string original, string draft)
    {
        if (State != SessionState.Transcribing) return false;
        Original = original; Draft = draft;
        if (string.IsNullOrWhiteSpace(draft)) { Fail("No speech was recognized. Your draft was left untouched."); return false; }
        State = SessionState.Ready; return true;
    }
    public bool MarkInserted() { if (State != SessionState.Ready) return false; State = SessionState.Inserted; return true; }
    public void Edit(string text) { if (State == SessionState.Ready) Draft = text; }
    public void Cancel() { if (State is SessionState.Recording or SessionState.Transcribing or SessionState.Ready) State = SessionState.Cancelled; }
    public void Fail(string error) { if (State is SessionState.Cancelled or SessionState.Inserted) return; Error = error; State = SessionState.Failed; }
}
