using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One reaction the picker offers (Arc's <c>ReactionOption</c>): an id, a label and a 24×24 Lucide glyph. Its properties
/// can be bound in XAML (for example a localized label); the bar follows later changes.
/// </summary>
public sealed class ReactionOption : AvaloniaObject
{
    /// <summary>Stable id, matched by <see cref="Reaction.Id"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ReactionOption, string>(nameof(Id), string.Empty);

    /// <summary>Name shown in the picker caption and tooltips, and read by assistive technology ("Love").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ReactionOption, string>(nameof(Label), string.Empty);

    /// <summary>24×24 Lucide geometry (<see cref="ArcIcons"/> or <see cref="ArcIcons.Parse"/>), drawn with a 1.75 stroke.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ReactionOption, Geometry?>(nameof(Icon));

    public ReactionOption()
    {
    }

    public ReactionOption(string id, string label, Geometry? icon)
    {
        Id = id;
        Label = label;
        Icon = icon;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>
/// The state of one reaction (Arc's <c>Reaction</c>): how many people chose it and whether the viewer did. A reaction
/// whose count reaches zero leaves the bar.
/// </summary>
/// <param name="Id">The <see cref="ReactionOption.Id"/> it counts.</param>
/// <param name="Count">How many people reacted.</param>
/// <param name="Reacted">True when the viewer's own reaction is included.</param>
public sealed record Reaction(string Id, int Count, bool Reacted = false)
{
    /// <summary>Names of other people who reacted (most relevant first), shown in the pill's tooltip.</summary>
    public IReadOnlyList<string>? ReactedBy { get; init; }
}

/// <summary>The change that produced a new reactions value.</summary>
/// <param name="Id">The reaction that changed.</param>
/// <param name="Reacted">True when the viewer added their reaction, false when they removed it.</param>
public readonly record struct ReactionChange(string Id, bool Reacted);

/// <summary>Arguments of <see cref="Reactions.ValueChanged"/> (Arc's <c>onValueChange(next, change)</c>).</summary>
public sealed class ReactionsValueChangedEventArgs : EventArgs
{
    public ReactionsValueChangedEventArgs(IReadOnlyList<Reaction> next, ReactionChange change)
    {
        Next = next;
        Change = change;
    }

    /// <summary>The next reactions, in bar order, without reactions whose count reached zero.</summary>
    public IReadOnlyList<Reaction> Next { get; }

    public ReactionChange Change { get; }
}
