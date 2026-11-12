using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The bottom corner that holds an <see cref="ActionMorph"/>'s button; the surface opens upward and toward the other side.</summary>
public enum ActionMorphCorner
{
    /// <summary>The bottom right (in a left-to-right layout): the surface grows up and to the left.</summary>
    End,

    /// <summary>The bottom left: the surface grows up and to the right.</summary>
    Start
}

/// <summary>What an <see cref="ActionMorph"/>'s surface shows.</summary>
public enum ActionMorphView
{
    /// <summary>The round button.</summary>
    Button,

    /// <summary>The quick-action menu.</summary>
    Menu,

    /// <summary>The composer of one action.</summary>
    Composer
}

/// <summary>
/// One action of an <see cref="ActionMorph"/>: a row of its menu and the small form that row opens. Change an action by
/// replacing it in <see cref="ActionMorph.Actions"/>; edits to an action already on screen are not picked up.
/// </summary>
public sealed class ActionMorphAction
{
    /// <summary>Anything that tells the actions apart, such as "task"; the morph does not read it.</summary>
    public string? Id { get; set; }

    /// <summary>The row's name, also its accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>A short line under the name; null shows the name alone.</summary>
    public string? Description { get; set; }

    /// <summary>A 24 × 24 stroked glyph before the name, such as <c>ArcIcons.ListTodo</c>.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>A single letter that jumps from the open menu straight to this action's composer; the row shows it in a key cap at its end.</summary>
    public string? Shortcut { get; set; }

    /// <summary>The composer's title, also the field's accessible name; null uses <see cref="Label"/>.</summary>
    public string? Title { get; set; }

    /// <summary>The field's placeholder.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Whether the field takes several lines; Ctrl+Enter (⌘+Enter on macOS) submits it instead of Enter.</summary>
    public bool IsMultiline { get; set; }

    /// <summary>The small heading over <see cref="Choices"/>, also the radio group's accessible name.</summary>
    public string? ChoicesLabel { get; set; }

    /// <summary>Options shown as a radio group under the field (best with two to four short words); empty shows none.</summary>
    public AvaloniaList<string> Choices { get; } = new();

    /// <summary>The option picked when the composer first opens; null picks the first.</summary>
    public string? DefaultChoice { get; set; }

    /// <summary>The submit button's text; null shows "Save".</summary>
    public string? SubmitLabel { get; set; }

    /// <summary>Announced and added to the button's name after a successful submit; null uses "Saved".</summary>
    public string? SuccessLabel { get; set; }

    /// <summary>Shown when the field is submitted empty; null uses <see cref="ActionMorph.EmptyHint"/>.</summary>
    public string? EmptyHint { get; set; }

    /// <summary>Anything else you need to keep with the action; the morph does not read it.</summary>
    public object? Tag { get; set; }

    internal string EffectiveTitle => Title ?? Label ?? string.Empty;

    internal string EffectiveSubmitLabel => SubmitLabel ?? "Save";

    internal string EffectiveSuccessLabel => SuccessLabel ?? "Saved";

    internal string? EffectiveDefaultChoice => DefaultChoice is { } choice && Choices.Contains(choice)
        ? choice
        : Choices.Count > 0 ? Choices[0] : null;

    /// <summary>Whether <paramref name="symbol"/> (a typed character) is this action's shortcut.</summary>
    internal bool Matches(string? symbol) => Shortcut is { Length: > 0 } shortcut && symbol is { Length: > 0 }
        && string.Equals(shortcut.Trim(), symbol.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string ToString() => Label ?? Id ?? string.Empty;
}

/// <summary>What was submitted from an <see cref="ActionMorph"/>'s composer.</summary>
public sealed class ActionMorphSubmission
{
    /// <summary>Creates a submission.</summary>
    public ActionMorphSubmission(ActionMorphAction action, string text, string? choice)
    {
        Action = action;
        Text = text;
        Choice = choice;
    }

    /// <summary>The action whose composer was submitted.</summary>
    public ActionMorphAction Action { get; }

    /// <summary>The field's text with surrounding white space removed (never empty).</summary>
    public string Text { get; }

    /// <summary>The picked option, or null when the action has no choices.</summary>
    public string? Choice { get; }
}

/// <summary>Carries what an <see cref="ActionMorph"/> submitted.</summary>
public sealed class ActionMorphSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="submission"/>.</summary>
    public ActionMorphSubmittedEventArgs(RoutedEvent routedEvent, ActionMorphSubmission submission) : base(routedEvent)
    {
        Submission = submission;
    }

    /// <summary>The submitted action, text and option.</summary>
    public ActionMorphSubmission Submission { get; }
}
