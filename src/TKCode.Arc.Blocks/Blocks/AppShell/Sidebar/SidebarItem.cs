using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One group of pages in a <see cref="Sidebar"/>: an optional small heading over its <see cref="Items"/>. A collapsible
/// section folds its pages away under the heading; on the icon rail sections are separated by a thin line instead.
/// </summary>
public class SidebarSection : AvaloniaObject
{
    /// <summary>The small heading over the pages; null shows no heading (for example the first group: Home, Inbox).</summary>
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SidebarSection, string?>(nameof(Header));

    /// <summary>Whether the heading is a button that folds the pages away (it shows a small chevron). Needs a <see cref="Header"/>.</summary>
    public static readonly StyledProperty<bool> IsCollapsibleProperty =
        AvaloniaProperty.Register<SidebarSection, bool>(nameof(IsCollapsible));

    /// <summary>Whether the pages of a collapsible section are shown (two-way). Ignored when the section is not collapsible.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarSection, bool>(nameof(IsExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="HeaderProperty"/>
    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="IsCollapsibleProperty"/>
    public bool IsCollapsible
    {
        get => GetValue(IsCollapsibleProperty);
        set => SetValue(IsCollapsibleProperty, value);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>The pages of this section, in order (the XAML content).</summary>
    [Content]
    public AvaloniaList<SidebarItem> Items { get; } = new();

    public override string ToString() => Header ?? nameof(SidebarSection);
}

/// <summary>
/// One page of a <see cref="Sidebar"/>: a row with an icon, a label and an optional count. An item with
/// <see cref="Items"/> is a parent: it opens and closes its child pages instead of being selected (one level of nesting).
/// </summary>
public class SidebarItem : AvaloniaObject
{
    /// <summary>A stable id for the page (a route or view name); <see cref="Sidebar.SelectedKey"/> selects by it.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<SidebarItem, string?>(nameof(Key));

    /// <summary>The row text; also the tooltip on the icon rail and the text that typing and the filter match.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarItem, string>(nameof(Label), string.Empty);

    /// <summary>The 16px leading icon: a 24x24 Lucide <see cref="Avalonia.Media.Geometry"/> (for example an <c>ArcIcons</c> glyph) or any control.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<SidebarItem, object?>(nameof(Icon));

    /// <summary>A short count at the end of the row (for example "12"); on the icon rail it becomes a dot on the icon. Null shows nothing.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SidebarItem, string?>(nameof(Badge));

    /// <summary>Whether the child pages of a parent are shown (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the page can be chosen. Disabled rows are dimmed and skipped by the arrow keys.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SidebarItem, bool>(nameof(IsEnabled), true);

    /// <summary>Runs when the page is chosen (with <see cref="CommandParameter"/>).</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SidebarItem, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SidebarItem, object?>(nameof(CommandParameter));

    /// <summary>Any value you want back when the page is chosen.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<SidebarItem, object?>(nameof(Tag));

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    /// <summary>The child pages (the XAML content). One level is shown: children of children are ignored.</summary>
    [Content]
    public AvaloniaList<SidebarItem> Items { get; } = new();

    /// <summary>True when the item has child pages (it expands instead of being selected).</summary>
    public bool HasChildren => Items.Count > 0;

    public override string ToString() => Label;
}

/// <summary>Arguments of <see cref="Sidebar.ItemInvoked"/>.</summary>
public sealed class SidebarItemInvokedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="item"/>.</summary>
    public SidebarItemInvokedEventArgs(RoutedEvent routedEvent, SidebarItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The page that was chosen (it is now <see cref="Sidebar.SelectedItem"/>).</summary>
    public SidebarItem Item { get; }
}
