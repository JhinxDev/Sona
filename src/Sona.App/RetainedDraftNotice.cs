namespace Sona.App;

// Wording follows delivery facts, especially when a second paste could duplicate text.
internal sealed record RetainedDraftNotice(string Title, string Hint)
{
    internal static RetainedDraftNotice Select(bool limitReached, bool clipboardRecovery,
        bool pasteRequested, bool pasteConfirmed, bool sendAccepted, string? outcome)
    {
        if (limitReached) return new("Draft ready", "Recording limit reached.");
        if (pasteConfirmed && !sendAccepted) return new("Draft pasted", "Check the chat before sending.");
        if (pasteRequested && !pasteConfirmed) return new("Check the chat", "Check before pasting again.");
        if (clipboardRecovery) return new("Draft copied", outcome switch
        {
            "NoCapturedField" => "Focus a text field, then Ctrl+V.",
            "FocusChanged" => "Focus changed. Ctrl+V to paste.",
            _ => "Ctrl+V in the intended field."
        });
        return new("Draft ready", "Open to review or copy.");
    }
}
