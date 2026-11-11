namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="InlineEdit"/> that screen readers hear: announcements, button names and help text.
/// Replace the whole object to localize it. Each function gets the control's <see cref="InlineEdit.Label"/> as written
/// (the English defaults lower-case it themselves). The failed-save sentence and the retry link are
/// <see cref="InlineEdit.FailedMessageFormat"/> and <see cref="InlineEdit.RetryText"/>.
/// </summary>
public sealed class InlineEditLabels
{
    /// <summary>Announced when a save starts: label ⇒ "Saving name".</summary>
    public Func<string, string> Saving { get; init; } = label => $"Saving {label.ToLowerInvariant()}";

    /// <summary>Announced when a save finishes: label ⇒ "Name saved".</summary>
    public Func<string, string> Saved { get; init; } = label => $"{label} saved";

    /// <summary>Name of the save button while editing: label ⇒ "Save name".</summary>
    public Func<string, string> SaveButton { get; init; } = label => $"Save {label.ToLowerInvariant()}";

    /// <summary>Name of the cancel button while editing: "Cancel editing".</summary>
    public string CancelButton { get; init; } = "Cancel editing";

    /// <summary>Help text of the shown value: "Activate to edit.".</summary>
    public string EditHelp { get; init; } = "Activate to edit.";

    /// <summary>Help text of a single-line field: "Enter saves, Escape cancels.".</summary>
    public string KeysHelp { get; init; } = "Enter saves, Escape cancels.";

    /// <summary>Help text of a multi-line field: "Enter saves, Shift+Enter adds a line break, Escape cancels.".</summary>
    public string MultilineKeysHelp { get; init; } = "Enter saves, Shift+Enter adds a line break, Escape cancels.";
}
