using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A card of <see cref="ListItem"/> and <see cref="ListCell"/> rows with an optional <see cref="Header"/> above it and
/// <see cref="Footer"/> under it, the way settings screens group their rows. Put it in a scrolling <see cref="Page"/>.
/// <para>
/// A <see cref="ListItem"/> (or a <see cref="Picker"/>) or a <see cref="ListCell"/> written in the group is the row as it
/// is. Any other item becomes a <see cref="ListItem"/> row. When the item template's root is itself a
/// <see cref="ListItem"/> or a <see cref="ListCell"/>, that row is the container (it is not wrapped in a second row), so
/// its <c>Title</c>, <c>Subtitle</c>, <c>Command</c> and so on can be bound in the template. A template that returns plain
/// content is shown inside a row; style that row with a <c>ListItem</c> selector in <c>ListGroup.Styles</c>.
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
    {
        if (item is ListCell)
        {
            // A content cell is a row of its own, like a ListItem.
            recycleKey = null;
            return false;
        }
        return NeedsContainer<ListItem>(item, out recycleKey);
    }

    // Rows the item template built itself (a ListItem or a ListCell): they are the container and keep the content the
    // template gave them. Weak, so a removed row is not kept alive; a recycled one stays a template row and only takes the
    // new item.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> _templateRows = new();

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        if (this.FindDataTemplate(item, ItemTemplate)?.Build(item) is { } built and (ListItem or ListCell))
        {
            _templateRows.AddOrUpdate(built, built);
            return built;
        }
        return new ListItem();
    }

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        if (_templateRows.TryGetValue(container, out _))
        {
            // The base would set the item as the row's content (with the template again): only the bindings need it.
            container.DataContext = item;
            return;
        }
        base.PrepareContainerForItemOverride(container, item, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        if (_templateRows.TryGetValue(container, out _))
        {
            container.ClearValue(DataContextProperty);
            return;
        }
        base.ClearContainerForItemOverride(container);
    }
}
