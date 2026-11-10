using System.Windows.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="ShareSheet"/> opens.</summary>
public enum ShareSheetPresentation
{
    /// <summary>A sheet from the bottom on a narrow viewport (a phone), a popover under the trigger otherwise.</summary>
    Auto,

    /// <summary>Always a popover anchored to the trigger.</summary>
    Popover,

    /// <summary>Always a sheet from the bottom of the window.</summary>
    Sheet
}

/// <summary>
/// Someone or somewhere a <see cref="ShareSheet"/> can share to: a person, drawn as their initials in a soft circle, or
/// a destination (an app, a channel, a folder), drawn as its glyph in a tile. Change a target by replacing it in its
/// list; edits to a target already on screen are not picked up.
/// </summary>
public sealed class ShareSheetTarget
{
    /// <summary>The name under the tile, also its accessible name.</summary>
    public string? Name { get; set; }

    /// <summary>A second line read by assistive technology, such as an address or a team; not drawn.</summary>
    public string? Detail { get; set; }

    /// <summary>A 24 × 24 stroked glyph; when set the tile shows it instead of initials.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>The letters in a person's circle; null takes the first letters of the first two words of <see cref="Name"/>.</summary>
    public string? Initials { get; set; }

    /// <summary>Anything you need to tell the targets apart, such as an id; the sheet does not read it.</summary>
    public object? Tag { get; set; }

    /// <summary>The initials drawn for this target.</summary>
    public string EffectiveInitials => Initials is { Length: > 0 } initials ? initials : InitialsOf(Name);

    internal static string InitialsOf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var first = char.ToUpperInvariant(words[0][0]).ToString();
        return words.Length > 1 ? first + char.ToUpperInvariant(words[^1][0]) : first;
    }

    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}

/// <summary>A row at the bottom of a <see cref="ShareSheet"/>: something to do with the item rather than someone to send it to.</summary>
public sealed class ShareSheetAction
{
    /// <summary>The row's text, also its accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>A 24 × 24 stroked glyph before the text.</summary>
    public Geometry? Glyph { get; set; }

    /// <summary>Command run when the row is chosen.</summary>
    public ICommand? Command { get; set; }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter { get; set; }

    /// <summary>Anything you need to tell the actions apart; the sheet does not read it.</summary>
    public object? Tag { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label ?? string.Empty;
}

/// <summary>Carries the target a <see cref="ShareSheet"/> shared to.</summary>
public sealed class ShareSheetTargetEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="target"/>.</summary>
    public ShareSheetTargetEventArgs(RoutedEvent routedEvent, ShareSheetTarget target, bool isPerson) : base(routedEvent)
    {
        Target = target;
        IsPerson = isPerson;
    }

    /// <summary>The person or destination that was chosen.</summary>
    public ShareSheetTarget Target { get; }

    /// <summary>True for one of <see cref="ShareSheet.People"/>, false for one of <see cref="ShareSheet.Destinations"/>.</summary>
    public bool IsPerson { get; }
}

/// <summary>Carries the action a <see cref="ShareSheet"/> ran.</summary>
public sealed class ShareSheetActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="action"/>.</summary>
    public ShareSheetActionEventArgs(RoutedEvent routedEvent, ShareSheetAction action) : base(routedEvent)
    {
        Action = action;
    }

    /// <summary>The action that was chosen.</summary>
    public ShareSheetAction Action { get; }
}
