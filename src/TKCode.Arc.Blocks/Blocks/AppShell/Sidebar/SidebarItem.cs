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

    /// <summary>An icon after the label, in the label's muted color (for example a chevron on a "More" row): a 24x24 Lucide <see cref="Avalonia.Media.Geometry"/> or any control.</summary>
    public static readonly StyledProperty<object?> TrailingIconProperty =
        AvaloniaProperty.Register<SidebarItem, object?>(nameof(TrailingIcon));

    /// <summary>A short count at the end of the row (for example "12"); on the icon rail it becomes a dot on the icon. Null shows nothing.</summary>
    public static readonly StyledProperty<string?> BadgeProperty =
        AvaloniaProperty.Register<SidebarItem, string?>(nameof(Badge));

    /// <summary>Marks the page as having something new: a small accent dot at the end of the row, a stronger label, and a dot on the icon on the rail.</summary>
    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<SidebarItem, bool>(nameof(IsUnread));

    /// <summary>
    /// Makes the row an action instead of a page: muted, never selected. Choosing it raises <see cref="Sidebar.ItemInvoked"/>
    /// and runs the commands, but <see cref="Sidebar.SelectedItem"/> stays (for example "New project", "More").
    /// </summary>
    public static readonly StyledProperty<bool> IsActionProperty =
        AvaloniaProperty.Register<SidebarItem, bool>(nameof(IsAction));

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

    /// <inheritdoc cref="TrailingIconProperty"/>
    public object? TrailingIcon
    {
        get => GetValue(TrailingIconProperty);
        set => SetValue(TrailingIconProperty, value);
    }

    /// <inheritdoc cref="BadgeProperty"/>
    public string? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    /// <inheritdoc cref="IsUnreadProperty"/>
    public bool IsUnread
    {
        get => GetValue(IsUnreadProperty);
        set => SetValue(IsUnreadProperty, value);
    }

    /// <inheritdoc cref="IsActionProperty"/>
    public bool IsAction
    {
        get => GetValue(IsActionProperty);
        set => SetValue(IsActionProperty, value);
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

/// <summary>Where an open <see cref="Sidebar"/> shows its collapse button.</summary>
public enum SidebarCollapseButtonPlacement
{
    /// <summary>At the end of the header row, beside <see cref="Sidebar.Header"/>.</summary>
    Header,

    /// <summary>Beside the filter box (needs <see cref="Sidebar.IsFilterVisible"/>; otherwise it stays in the header row).</summary>
    Filter
}

/// <summary>How a <see cref="Sidebar"/>'s change of width reaches the page next to it.</summary>
public enum SidebarResizeMode
{
    /// <summary>The layout width springs, so the page next to the sidebar is laid out again on every frame. Works in any host.</summary>
    Push,

    /// <summary>
    /// The layout width jumps at once (the page is laid out once) and only the pane springs, drawn above the page.
    /// Bind the page's <c>RenderTransform</c> to <see cref="Sidebar.PageShift"/> so the page slides along; this is much
    /// cheaper than <see cref="Push"/> for pages with a lot of text or controls.
    /// </summary>
    Overlay
}
