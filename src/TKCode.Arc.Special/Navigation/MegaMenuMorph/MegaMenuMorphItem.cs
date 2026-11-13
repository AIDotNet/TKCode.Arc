using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One top-level entry of a <see cref="MegaMenuMorph"/>: a label in the bar and, when <see cref="Content"/> is set, the
/// panel it opens. An item without content is a plain link: activating it raises <see cref="MegaMenuMorph.LinkInvoked"/>.
/// </summary>
public class MegaMenuMorphItem : AvaloniaObject
{
    /// <summary>The label shown in the bar and used as the name of the trigger and of its panel.</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<MegaMenuMorphItem, string?>(nameof(Header));

    /// <summary>
    /// What the panel shows: a control (usually a grid of <see cref="MegaMenuMorphLink"/>s) or data shown through
    /// <see cref="ContentTemplate"/>. Null makes the item a plain link without a panel.
    /// </summary>
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<MegaMenuMorphItem, object?>(nameof(Content));

    /// <summary>The template that turns <see cref="Content"/> into controls when it is data.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        AvaloniaProperty.Register<MegaMenuMorphItem, IDataTemplate?>(nameof(ContentTemplate));

    /// <summary>Set to mark the item that belongs to the current page: its label keeps the full text color and is announced as current.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<MegaMenuMorphItem, bool>(nameof(IsCurrent));

    /// <summary>Anything the app wants to keep with the item (a route, a page key). The menu never reads it.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<MegaMenuMorphItem, object?>(nameof(Tag));

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <inheritdoc cref="IsCurrentProperty"/>
    public bool IsCurrent
    {
        get => GetValue(IsCurrentProperty);
        set => SetValue(IsCurrentProperty, value);
    }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    /// <summary>True when the item opens a panel (it has <see cref="Content"/>).</summary>
    public bool HasPanel => Content is not null;

    internal MegaMenuMorph? Owner { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TagProperty)
        {
            Owner?.OnItemChanged(this, change.Property);
        }
    }
}

/// <summary>Arguments of <see cref="MegaMenuMorph.LinkInvoked"/>: the top-level item and, inside its panel, the link.</summary>
public sealed class MegaMenuMorphLinkInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MegaMenuMorphLinkInvokedEventArgs(RoutedEvent routedEvent, MegaMenuMorphItem item, MegaMenuMorphLink? link)
        : base(routedEvent)
    {
        Item = item;
        Link = link;
    }

    /// <summary>The top-level item: the one whose panel held the link, or the plain link item itself.</summary>
    public MegaMenuMorphItem Item { get; }

    /// <summary>The link inside the panel, or null when a top-level item without a panel was activated.</summary>
    public MegaMenuMorphLink? Link { get; }

    /// <summary>The words of what was activated: the link's title, or the item's header.</summary>
    public string Title => Link?.Title ?? Item.Header ?? string.Empty;
}
