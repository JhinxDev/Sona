namespace Sona.App;

// Wording follows delivery facts, especially when a second paste could duplicate text.
internal sealed record RetainedDraftNotice(string Title, string Hint)
{
    internal static RetainedDraftNotice Select(bool limitReached, bool clipboardRecovery,
        bool pasteAttempted, bool pasteConfirmed, bool sendAccepted)
    {
        if (limitReached) return new("Draft ready", "Recording limit reached.");
        if (pasteConfirmed && !sendAccepted) return new("Draft pasted", "Check the chat before sending.");
        if (pasteAttempted && !pasteConfirmed) return new("Check the chat", "Check before pasting again.");
        if (clipboardRecovery && !pasteAttempted) return new("Draft copied", "Ctrl+V to paste.");
        return new("Draft ready", "Open to review or copy.");
    }
}
