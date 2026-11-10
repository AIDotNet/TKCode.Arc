namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="AiInlineEdit"/> shows or reads out, with English defaults. Words built from values are
/// functions, so an app can apply its own grammar. Assign a new instance to <see cref="AiInlineEdit.Labels"/> to change them.
/// </summary>
public sealed class AiInlineEditLabels
{
    /// <summary>Accessible name of the editor ("Text with AI editing").</summary>
    public string Region { get; init; } = "Text with AI editing";

    /// <summary>The hint while nothing is selected ("Select text, then press Edit with AI or Ctrl+K").</summary>
    public string Hint { get; init; } = "Select text, then press Edit with AI or Ctrl+K";

    /// <summary>The line while text is selected, from the word count ("12 words selected").</summary>
    public Func<int, string> Selected { get; init; } = static words => words == 1 ? "1 word selected" : $"{words} words selected";

    /// <summary>The button that opens the instruction field ("Edit with AI").</summary>
    public string Edit { get; init; } = "Edit with AI";

    /// <summary>The instruction field's placeholder ("Tell the AI what to change").</summary>
    public string Placeholder { get; init; } = "Tell the AI what to change";

    /// <summary>The send button ("Rewrite").</summary>
    public string Send { get; init; } = "Rewrite";

    /// <summary>The cancel button ("Cancel").</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The line while the rewrite is written ("Rewriting").</summary>
    public string Thinking { get; init; } = "Rewriting";

    /// <summary>The review line, from changes, accepted and rejected ("3 changes · 1 accepted · 0 rejected").</summary>
    public Func<int, int, int, string> Review { get; init; } =
        static (changes, accepted, rejected) => $"{(changes == 1 ? "1 change" : $"{changes} changes")} · {accepted} accepted · {rejected} rejected";

    /// <summary>The line when the rewrite matches the passage ("No changes suggested").</summary>
    public string NoChanges { get; init; } = "No changes suggested";

    /// <summary>The review buttons: "Accept all", "Reject all".</summary>
    public string AcceptAll { get; init; } = "Accept all";

    /// <inheritdoc cref="AcceptAll"/>
    public string RejectAll { get; init; } = "Reject all";

    /// <summary>Accessible names of a change's buttons, from the words out and in ("Accept: replace "a" with "b"").</summary>
    public Func<string, string, string> AcceptChange { get; init; } = static (removed, added) => $"Accept: {Describe(removed, added)}";

    /// <inheritdoc cref="AcceptChange"/>
    public Func<string, string, string> RejectChange { get; init; } = static (removed, added) => $"Reject: {Describe(removed, added)}";

    /// <summary>What screen readers hear when the rewrite is ready ("3 changes suggested").</summary>
    public Func<int, string> Ready { get; init; } = static changes => changes == 1 ? "1 change suggested" : $"{changes} changes suggested";

    /// <summary>What screen readers hear when the edit is applied, from accepted and rejected ("Applied 2 changes, rejected 1").</summary>
    public Func<int, int, string> Applied { get; init; } = static (accepted, rejected) => $"Applied {accepted} changes, rejected {rejected}";

    /// <summary>The error line when the rewrite failed, from the message ("Rewrite failed: message").</summary>
    public Func<string, string> Failed { get; init; } = static message => $"Rewrite failed: {message}";

    private static string Describe(string removed, string added)
    {
        removed = removed.Trim();
        added = added.Trim();
        if (removed.Length == 0)
        {
            return $"add \"{added}\"";
        }
        return added.Length == 0 ? $"remove \"{removed}\"" : $"replace \"{removed}\" with \"{added}\"";
    }
}
