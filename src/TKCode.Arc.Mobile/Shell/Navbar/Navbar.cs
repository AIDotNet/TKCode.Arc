using Avalonia;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The top bar of a <see cref="Page"/>: a <see cref="Leading"/> slot (usually a back button), a centered
/// <see cref="Title"/> and a <see cref="Trailing"/> slot for actions. It is 44 high, and the slots are at least 44 wide.
/// </summary>
public class Navbar : TemplatedControl
{
    /// <summary>The bar's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Navbar, string?>(nameof(Title));

    /// <summary>The content at the start of the bar, usually a back button.</summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<Navbar, object?>(nameof(Leading));

    /// <summary>The content at the end of the bar, usually action buttons.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<Navbar, object?>(nameof(Trailing));

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Navbar);
}
