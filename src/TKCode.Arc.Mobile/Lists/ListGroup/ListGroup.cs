using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A card of <see cref="ListItem"/> rows with an optional <see cref="Header"/> above it and <see cref="Footer"/> under it,
/// the way settings screens group their rows. Put it in a scrolling <see cref="Page"/>.
/// <para>
/// Each data item becomes a <see cref="ListItem"/> row. When the item template's root is itself a
/// <see cref="ListItem"/>, that row is the container (it is not wrapped in a second row), so its <c>Title</c>,
/// <c>Subtitle</c>, <c>Command</c> and so on can be bound in the template. A template that returns plain content is
/// shown inside a row; style that row with a <c>ListItem</c> selector in <c>ListGroup.Styles</c>.
/// </para>
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

    // Rows the item template built itself: they are the container and keep the content the template gave them. Weak,
    // so a removed row is not kept alive; a recycled one stays a template row and only takes the new item.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListItem, object> _templateRows = new();

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        if (this.FindDataTemplate(item, ItemTemplate)?.Build(item) is ListItem row)
        {
            _templateRows.AddOrUpdate(row, row);
            return row;
        }
        return new ListItem();
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (container is ListItem row && _templateRows.TryGetValue(row, out _))
        {
            // The base would set the item as the row's content (with the template again): only the bindings need it.
            row.DataContext = item;
            return;
        }
        base.PrepareContainerForItemOverride(container, item, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (container is ListItem row && _templateRows.TryGetValue(row, out _))
        {
            row.ClearValue(DataContextProperty);
            return;
        }
        base.ClearContainerForItemOverride(container);
    }
}
