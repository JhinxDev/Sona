namespace Sona.Core;

// A session-local review model. Recognition may revise provisional text, but
// neither a late result nor cleanup may replace words the user has edited.
public sealed class LiveDraft
{
    public string Raw { get; private set; } = "";
    public string Draft { get; private set; } = "";
    public string Cleaned { get; private set; } = "";
    public bool ManuallyEdited { get; private set; }
    public bool Final { get; private set; }
    public bool Cancelled { get; private set; }
    public long Revision { get; private set; }
    public bool Preview(long revision, string raw)
    {
        if (Final || Cancelled || revision <= Revision) return false;
        Revision = revision; Raw = raw;
        if (!ManuallyEdited) Draft = raw;
        return true;
    }
    public void Edit(string text)
    {
        ManuallyEdited = true; Draft = text;
    }
    public bool Complete(string raw, string cleaned)
    {
        if (Cancelled || Final) return false;
        Raw = raw; Cleaned = cleaned; Final = true;
        if (!ManuallyEdited) Draft = cleaned;
        return true;
    }
    public void Cancel() => Cancelled = true;
}
