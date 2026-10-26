using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A card of <see cref="ListItem"/> rows with an optional <see cref="Header"/> above it and <see cref="Footer"/> under it,
/// the way settings screens group their rows. Put it in a scrolling <see cref="Page"/>.
/// </summary>
public class ListGroup : ItemsControl
{
    /// <summary>A short label above the card.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<ListGroup, string?>(nameof(Header));

    /// <summary>A note under the card, for example what the settings in it do.</summary>
    public static readonly StyledProperty<string?> FooterProperty =
        AvaloniaProperty.Register<ListGroup, string?>(nameof(Footer));

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public string? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(ListGroup);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<ListItem>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new ListItem();
}
