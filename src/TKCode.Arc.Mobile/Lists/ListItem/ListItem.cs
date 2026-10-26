using Avalonia;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One row of a <see cref="ListGroup"/>: an optional <see cref="Icon"/>, the title (the content), an optional
/// <see cref="Subtitle"/>, optional <see cref="Trailing"/> content and an optional chevron. It is a button: give it a
/// <c>Command</c> or handle <c>Click</c> and a finger on it shades the row. It has no hover state.
/// </summary>
public class ListItem : AvaloniaButton
{
    /// <summary>The glyph geometry shown before the title, for example a Lucide icon.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ListItem, Geometry?>(nameof(Icon));

    /// <summary>A second, smaller line under the title.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ListItem, string?>(nameof(Subtitle));

    /// <summary>The content at the end of the row: a value, a badge or a control such as a <see cref="Switch"/>.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<ListItem, object?>(nameof(Trailing));

    /// <summary>Whether a chevron at the end of the row shows that it opens another screen.</summary>
    public static readonly StyledProperty<bool> ShowChevronProperty =
        AvaloniaProperty.Register<ListItem, bool>(nameof(ShowChevron));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    /// <inheritdoc cref="ShowChevronProperty"/>
    public bool ShowChevron
    {
        get => GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ListItem);
}
