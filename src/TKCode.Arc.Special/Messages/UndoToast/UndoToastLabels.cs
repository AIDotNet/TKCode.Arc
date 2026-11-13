namespace TKCode.Arc;

/// <summary>
/// The words an <see cref="UndoToast"/> gives assistive technology, in English by default. Replace the whole object to
/// translate or reword them.
/// </summary>
public sealed class UndoToastLabels
{
    /// <summary>The English defaults.</summary>
    public static UndoToastLabels Default { get; } = new();

    /// <summary>The dismiss button's name.</summary>
    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>The undo button's name, from its label and the message: "Undo: Conversation archived".</summary>
    public Func<string, string, string> UndoName { get; init; } = (undo, message) => message.Length == 0 ? undo : undo + ": " + message;

    /// <summary>The toast's help text, from the undo shortcut: "Press Ctrl+Z to undo".</summary>
    public Func<string, string> ShortcutHint { get; init; } = gesture => "Press " + gesture + " to undo";
}
