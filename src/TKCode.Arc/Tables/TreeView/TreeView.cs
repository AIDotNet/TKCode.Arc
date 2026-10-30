using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaTreeView = Avalonia.Controls.TreeView;

namespace TKCode.Arc;

/// <summary>Arguments for <see cref="TreeView"/> item events.</summary>
public sealed class TreeViewItemEventArgs : RoutedEventArgs
{
    public TreeViewItemEventArgs(RoutedEvent routedEvent, object item) : base(routedEvent) => Item = item;

    /// <summary>The node: a <see cref="TreeViewItem"/> or an item of <see cref="TreeView.ItemsSource"/>.</summary>
    public object Item { get; }
}

/// <summary>Arguments for <see cref="TreeView.SelectionChanged"/>.</summary>
public sealed class TreeViewSelectionChangedEventArgs : RoutedEventArgs
{
    public TreeViewSelectionChangedEventArgs(RoutedEvent routedEvent, object? oldItem, object? newItem) : base(routedEvent)
    {
        OldItem = oldItem;
        NewItem = newItem;
    }

    public object? OldItem { get; }

    public object? NewItem { get; }
}

/// <summary>
/// A keyboard-navigable file tree with animated folders and a selection fill that glides between rows. Folders
/// open with their space growing on a smooth spring while the new rows stagger in; the chevron turns on a snappy
/// spring and the folder icon pops open. Keyboard: ↑/↓ move, → opens or enters, ← closes or goes to the parent,
/// Home/End, PageUp/PageDown, Enter/Space select and toggle, and typing jumps to a matching row.
/// <para>
/// Unlike <see cref="AvaloniaTreeView"/> (nested item containers), rows are a flat, virtualized list of pooled,
/// self-drawn rows (see <see cref="TablesTreeRowsPanel"/>): thousands of open nodes cost one screenful of rows,
/// and the open / close motion never relayouts the whole tree. Nodes come from inline <see cref="Items"/>
/// (<see cref="TreeViewItem"/>) or from <see cref="ItemsSource"/> with <see cref="ChildrenSelector"/>.
/// </para>
/// </summary>
[TemplatePart(RowsPart, typeof(TreeViewRowsPanel))]
public class TreeView : TemplatedControl
{
    public const string RowsPart = "PART_Rows";

    #region Properties

    /// <summary>Root nodes from data; takes precedence over <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TreeView, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Children of a data item (null or empty: a file). Not needed for <see cref="TreeViewItem"/> nodes.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<TreeView, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Row label of a data item; <c>ToString()</c> when not set.</summary>
    public static readonly StyledProperty<Func<object, string?>?> HeaderSelectorProperty =
        AvaloniaProperty.Register<TreeView, Func<object, string?>?>(nameof(HeaderSelector));

    /// <summary>Optional icon of a data item (24x24 stroked glyph); folders and files get Arc's icons otherwise.</summary>
    public static readonly StyledProperty<Func<object, Geometry?>?> IconSelectorProperty =
        AvaloniaProperty.Register<TreeView, Func<object, Geometry?>?>(nameof(IconSelector));

    /// <summary>
    /// Content of each row's label area (after the chevron and icon), built from the node: a data item or a
    /// <see cref="TreeViewItem"/>. The tree still draws the row: height, fills, indent, guides, chevron and icon. Rows
    /// are pooled, so a template is built once per pooled row and scrolling only changes its data. A template that
    /// does not match a node leaves it to the <c>DataTemplates</c> of the tree and its ancestors. When null the tree
    /// draws the header text. Type-ahead and screen readers use <see cref="HeaderSelector"/> either way.
    /// </summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<TreeView, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>
    /// Returns true for data items that are shown dimmed and cannot be selected or invoked (inline nodes use
    /// <see cref="TreeViewItem.IsDisabled"/>). Keyboard focus can still reach them, so a disabled folder still opens.
    /// </summary>
    public static readonly StyledProperty<Func<object, bool>?> DisabledSelectorProperty =
        AvaloniaProperty.Register<TreeView, Func<object, bool>?>(nameof(DisabledSelector));

    /// <summary>
    /// Height of every row in pixels, including the 2px gap above it (default 38, from the theme). All rows share it,
    /// so the tree stays virtualized; raise it for two-line content.
    /// </summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TreeView, double>(nameof(RowHeight), double.NaN);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<TreeView, object?>(nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Draws the short branch line beside nested rows (Arc's indent guide).</summary>
    public static readonly StyledProperty<bool> ShowIndentGuidesProperty =
        AvaloniaProperty.Register<TreeView, bool>(nameof(ShowIndentGuides), true);

    /// <summary>Shows each row's icon (16px slot before the label); when false the label starts at the indent.</summary>
    public static readonly StyledProperty<bool> ShowIconsProperty =
        AvaloniaProperty.Register<TreeView, bool>(nameof(ShowIcons), true);

    /// <summary>Raised when the selected item changes; the argument carries the old and new item.</summary>
    public static readonly RoutedEvent<TreeViewSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<TreeView, TreeViewSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>A row was clicked or activated with Enter / Space (Arc <c>onSelect</c>), even when already selected.</summary>
    public static readonly RoutedEvent<TreeViewItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<TreeView, TreeViewItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a branch item expands.</summary>
    public static readonly RoutedEvent<TreeViewItemEventArgs> ItemExpandedEvent =
        RoutedEvent.Register<TreeView, TreeViewItemEventArgs>(nameof(ItemExpanded), RoutingStrategies.Bubble);

    /// <summary>Raised when a branch item collapses.</summary>
    public static readonly RoutedEvent<TreeViewItemEventArgs> ItemCollapsedEvent =
        RoutedEvent.Register<TreeView, TreeViewItemEventArgs>(nameof(ItemCollapsed), RoutingStrategies.Bubble);

    // Row colors, set by the theme.
    internal static readonly StyledProperty<IBrush?> RowForegroundProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(RowForeground));
    internal static readonly StyledProperty<IBrush?> RowActiveForegroundProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(RowActiveForeground));
    internal static readonly StyledProperty<IBrush?> RowHoverFillProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(RowHoverFill));
    internal static readonly StyledProperty<IBrush?> SelectionFillProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(SelectionFill));
    internal static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(MutedForeground));
    internal static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(GuideBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(FocusRingBrush));
    internal static readonly StyledProperty<IBrush?> TypeScriptBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(TypeScriptBrush));
    internal static readonly StyledProperty<IBrush?> StylesheetBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(StylesheetBrush));
    internal static readonly StyledProperty<IBrush?> MarkdownBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(MarkdownBrush));
    internal static readonly StyledProperty<IBrush?> JsonBrushProperty =
        AvaloniaProperty.Register<TreeView, IBrush?>(nameof(JsonBrush));
    internal static readonly StyledProperty<double> RowSlotHeightProperty =
        AvaloniaProperty.Register<TreeView, double>(nameof(RowSlotHeight), 38);
    internal static readonly StyledProperty<double> IndentWidthProperty =
        AvaloniaProperty.Register<TreeView, double>(nameof(IndentWidth), 18);

    private static readonly AvaloniaProperty[] s_paletteProperties =
    [
        RowForegroundProperty, RowActiveForegroundProperty, RowHoverFillProperty, SelectionFillProperty, MutedForegroundProperty,
        GuideBrushProperty, FocusRingBrushProperty, TypeScriptBrushProperty, StylesheetBrushProperty, MarkdownBrushProperty,
        JsonBrushProperty, RowSlotHeightProperty, IndentWidthProperty, FontFamilyProperty, FontSizeProperty, ShowIndentGuidesProperty,
        ShowIconsProperty, RowHeightProperty
    ];

    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public Func<object, IEnumerable?>? ChildrenSelector { get => GetValue(ChildrenSelectorProperty); set => SetValue(ChildrenSelectorProperty, value); }
    public Func<object, string?>? HeaderSelector { get => GetValue(HeaderSelectorProperty); set => SetValue(HeaderSelectorProperty, value); }
    public Func<object, Geometry?>? IconSelector { get => GetValue(IconSelectorProperty); set => SetValue(IconSelectorProperty, value); }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <inheritdoc cref="DisabledSelectorProperty"/>
    public Func<object, bool>? DisabledSelector { get => GetValue(DisabledSelectorProperty); set => SetValue(DisabledSelectorProperty, value); }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>The selected node (two-way). Setting it to a hidden node opens its ancestors.</summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    public bool ShowIndentGuides { get => GetValue(ShowIndentGuidesProperty); set => SetValue(ShowIndentGuidesProperty, value); }
    /// <inheritdoc cref="ShowIconsProperty"/>
    public bool ShowIcons { get => GetValue(ShowIconsProperty); set => SetValue(ShowIconsProperty, value); }

    /// <summary>Inline nodes (the XAML content).</summary>
    [Content]
    public AvaloniaList<TreeViewItem> Items { get; } = new();

    /// <summary>Raised when the selected item changes; the argument carries the old and new item.</summary>
    public event EventHandler<TreeViewSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public event EventHandler<TreeViewItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>Raised when a branch item expands.</summary>
    public event EventHandler<TreeViewItemEventArgs>? ItemExpanded
    {
        add => AddHandler(ItemExpandedEvent, value);
        remove => RemoveHandler(ItemExpandedEvent, value);
    }

    /// <summary>Raised when a branch item collapses.</summary>
    public event EventHandler<TreeViewItemEventArgs>? ItemCollapsed
    {
        add => AddHandler(ItemCollapsedEvent, value);
        remove => RemoveHandler(ItemCollapsedEvent, value);
    }

    internal IBrush? RowForeground { get => GetValue(RowForegroundProperty); set => SetValue(RowForegroundProperty, value); }
    internal IBrush? RowActiveForeground { get => GetValue(RowActiveForegroundProperty); set => SetValue(RowActiveForegroundProperty, value); }
    internal IBrush? RowHoverFill { get => GetValue(RowHoverFillProperty); set => SetValue(RowHoverFillProperty, value); }
    internal IBrush? SelectionFill { get => GetValue(SelectionFillProperty); set => SetValue(SelectionFillProperty, value); }
    internal IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    internal IBrush? GuideBrush { get => GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }
    internal IBrush? TypeScriptBrush { get => GetValue(TypeScriptBrushProperty); set => SetValue(TypeScriptBrushProperty, value); }
    internal IBrush? StylesheetBrush { get => GetValue(StylesheetBrushProperty); set => SetValue(StylesheetBrushProperty, value); }
    internal IBrush? MarkdownBrush { get => GetValue(MarkdownBrushProperty); set => SetValue(MarkdownBrushProperty, value); }
    internal IBrush? JsonBrush { get => GetValue(JsonBrushProperty); set => SetValue(JsonBrushProperty, value); }
    internal double RowSlotHeight { get => GetValue(RowSlotHeightProperty); set => SetValue(RowSlotHeightProperty, value); }
    internal double IndentWidth { get => GetValue(IndentWidthProperty); set => SetValue(IndentWidthProperty, value); }

    /// <summary>The row slot in use: <see cref="RowHeight"/> when set, else the theme's.</summary>
    internal double EffectiveRowHeight => RowHeight > 0 ? RowHeight : RowSlotHeight;

    #endregion

    private readonly HashSet<object> _expanded = new();
    private Dictionary<object, TreeViewNode> _nodes = new();
    private List<object> _rows = new();
    private readonly HashSet<INotifyCollectionChanged> _subscriptions = new();
    private readonly List<TreeViewItem> _sourceLogicalItems = new();
    private readonly ListKeyboardNavigator _navigator;
    private TreeViewRowsPanel? _panel;
    private object? _focusKey;
    private bool _rebuildPosted;
    private bool _watching;
    private int _bulk;
    private object? _pendingStagger;
    private bool _syncingSelection;

    internal TreeViewPalette Palette { get; } = new();

    static TreeView()
    {
        FocusableProperty.OverrideDefaultValue<TreeView>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<TreeView>(AutomationControlType.Tree);
    }

    public TreeView()
    {
        _navigator = new ListKeyboardNavigator(() => _rows.Count, _ => true, i => ((TreeViewNode)_rows[i]).Label)
        {
            Wrap = false
        };
        Items.CollectionChanged += OnInlineItemsChanged;
        AutomationProperties.SetName(this, "File tree");
    }

    internal TreeViewRowsPanel? RowsPanel => _panel;

    /// <summary>The visible rows in order (tests).</summary>
    internal IReadOnlyList<object> VisibleRows => _rows;

    internal object? FocusKey => _focusKey;

    #region Public API

    /// <summary>Whether <paramref name="item"/> is an open folder.</summary>
    public bool IsItemExpanded(object item) => item is TreeViewItem tvi ? tvi.IsExpanded : _expanded.Contains(item);

    /// <summary>Opens a folder.</summary>
    public void Expand(object item) => SetExpanded(item, true, animate: true);

    /// <summary>Closes a folder.</summary>
    public void Collapse(object item) => SetExpanded(item, false, animate: true);

    /// <summary>Opens a closed folder or closes an open one.</summary>
    public void ToggleExpanded(object item) => SetExpanded(item, !IsItemExpanded(item), animate: true);

    /// <summary>Opens every folder.</summary>
    public void ExpandAll() => SetAll(true);

    /// <summary>Closes every folder.</summary>
    public void CollapseAll() => SetAll(false);

    /// <summary>Opens the ancestors of <paramref name="item"/> and scrolls its row into view.</summary>
    public void ScrollIntoView(object item)
    {
        if (!RevealItem(item))
        {
            return;
        }
        var index = IndexOf(item);
        if (_panel?.Realize(index) is { } row)
        {
            row.BringIntoView();
        }
    }

    /// <summary>Moves keyboard focus to <paramref name="item"/>'s row (opening its ancestors).</summary>
    public void FocusItem(object item)
    {
        if (RevealItem(item))
        {
            MoveFocus(IndexOf(item), NavigationMethod.Directional);
        }
    }

    #endregion

    #region Structure

    private IEnumerable Roots => ItemsSource ?? Items;

    internal IEnumerable? ChildrenOf(object item) => item is TreeViewItem tvi ? tvi.Items : ChildrenSelector?.Invoke(item);

    private static bool HasAny(IEnumerable? children)
    {
        if (children is null)
        {
            return false;
        }
        if (children is ICollection collection)
        {
            return collection.Count > 0;
        }
        var enumerator = children.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private string LabelOf(object item) => item is TreeViewItem tvi
        ? tvi.Header ?? string.Empty
        : HeaderSelector?.Invoke(item) ?? item.ToString() ?? string.Empty;

    private Geometry? IconOf(object item) => item is TreeViewItem tvi ? tvi.Icon : IconSelector?.Invoke(item);

    private bool DisabledOf(object item) => item is TreeViewItem tvi ? tvi.IsDisabled : DisabledSelector?.Invoke(item) == true;

    private bool IsDisabledItem(object? item) => item is not null && (_nodes.TryGetValue(item, out var node) ? node.IsDisabled : DisabledOf(item));

    private int IndexOf(object? item) => _panel?.IndexOfKey(item) ?? (item is null ? -1 : _rows.FindIndex(r => Equals(((TreeViewNode)r).Item, item)));

    private void Rebuild(bool animate, object? staggerAfter = null)
    {
        var rows = new List<object>(Math.Max(16, _rows.Count));
        var nodes = new Dictionary<object, TreeViewNode>(_nodes.Count);
        var subscriptions = new HashSet<INotifyCollectionChanged>();
        if (Roots is INotifyCollectionChanged rootChanges)
        {
            subscriptions.Add(rootChanges);
        }
        Visit(Roots, null, 1, rows, nodes, subscriptions);
        var previousNodes = _nodes;
        _nodes = nodes;
        _rows = rows;
        UpdateSubscriptions(subscriptions);
        // Keep the tab stop: the focused row, else the nearest visible ancestor, else the first row.
        var hadFocus = IsKeyboardFocusWithin;
        if (_focusKey is null || !nodes.ContainsKey(_focusKey) || IndexInRows(_focusKey) < 0)
        {
            var fallback = NearestVisibleAncestor(_focusKey, previousNodes) ?? (rows.Count > 0 ? ((TreeViewNode)rows[0]).Item : null);
            SetFocusKey(fallback);
        }
        if (_panel is not null)
        {
            _panel.PinnedKey = _focusKey;
            _panel.SetRows(rows, animate && IsLoaded, staggerAfter);
            // A selected row that left drops its fill at once; one that comes back gets it in place.
            var highlight = SelectedItem is not null && nodes.ContainsKey(SelectedItem) ? SelectedItem : null;
            if (!Equals(_panel.HighlightKey, highlight))
            {
                _panel.SetHighlight(highlight, animate: false);
            }
            if (hadFocus && FocusManagerFocusIsInsideRemovedRow())
            {
                MoveFocus(IndexOf(_focusKey), NavigationMethod.Unspecified);
            }
        }
    }

    private bool FocusManagerFocusIsInsideRemovedRow()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return focused is null || (RowOf(focused) is { } row && (row.IsGhost || row.Model is null));
    }

    private int IndexInRows(object key) => _panel?.IndexOfKey(key) ?? _rows.FindIndex(r => Equals(((TreeViewNode)r).Item, key));

    private object? NearestVisibleAncestor(object? key, Dictionary<object, TreeViewNode> previous)
    {
        if (key is null || !previous.TryGetValue(key, out var node))
        {
            return null;
        }
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (_nodes.ContainsKey(parent.Item))
            {
                return parent.Item;
            }
        }
        return null;
    }

    private void Visit(IEnumerable? children, TreeViewNode? parent, int depth, List<object> rows,
                       Dictionary<object, TreeViewNode> nodes, HashSet<INotifyCollectionChanged> subscriptions)
    {
        if (children is null)
        {
            return;
        }
        var list = children as IList ?? children.Cast<object>().ToList();
        var size = list.Count;
        for (var i = 0; i < size; i++)
        {
            var item = list[i];
            if (item is null || nodes.ContainsKey(item))
            {
                continue;
            }
            if (!_nodes.TryGetValue(item, out var node))
            {
                node = new TreeViewNode(item);
            }
            nodes[item] = node;
            node.Parent = parent;
            node.Depth = depth;
            node.Position = i + 1;
            node.SetSize = size;
            node.Label = LabelOf(item);
            node.Icon = IconOf(item);
            node.IsDisabled = DisabledOf(item);
            var grandChildren = ChildrenOf(item);
            node.HasChildren = HasAny(grandChildren);
            node.IsExpanded = node.HasChildren && IsItemExpanded(item);
            rows.Add(node);
            if (node.IsExpanded)
            {
                if (grandChildren is INotifyCollectionChanged changes && item is not TreeViewItem)
                {
                    subscriptions.Add(changes);
                }
                Visit(grandChildren, node, depth + 1, rows, nodes, subscriptions);
            }
        }
    }

    private void UpdateSubscriptions(HashSet<INotifyCollectionChanged> next)
    {
        if (!_watching)
        {
            next.Clear();
        }
        foreach (var old in _subscriptions)
        {
            if (!next.Contains(old))
            {
                old.CollectionChanged -= OnSourceCollectionChanged;
            }
        }
        foreach (var added in next)
        {
            if (!_subscriptions.Contains(added))
            {
                added.CollectionChanged += OnSourceCollectionChanged;
            }
        }
        _subscriptions.Clear();
        _subscriptions.UnionWith(next);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ItemsSource))
        {
            UpdateSourceLogicalItems();
        }
        PostRebuild();
    }

    private void OnInlineItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TreeViewItem item in e.OldItems)
            {
                LogicalChildren.Remove(item);
            }
        }
        if (e.NewItems is not null)
        {
            foreach (TreeViewItem item in e.NewItems)
            {
                LogicalChildren.Add(item);
            }
        }
        if (ItemsSource is null)
        {
            PostRebuild();
        }
    }

    /// <summary>TreeViewItem roots given through <see cref="ItemsSource"/> join the logical tree so their changes reach the tree.</summary>
    private void UpdateSourceLogicalItems()
    {
        foreach (var item in _sourceLogicalItems)
        {
            LogicalChildren.Remove(item);
        }
        _sourceLogicalItems.Clear();
        if (ItemsSource is null)
        {
            return;
        }
        foreach (var item in ItemsSource)
        {
            if (item is TreeViewItem tvi && tvi.Parent is null)
            {
                _sourceLogicalItems.Add(tvi);
                LogicalChildren.Add(tvi);
            }
        }
    }

    /// <summary>Whether the TreeViewItem roots of <see cref="ItemsSource"/> differ from those that joined the logical tree.</summary>
    private bool SourceLogicalItemsChanged()
    {
        var index = 0;
        foreach (var item in ItemsSource ?? Array.Empty<object>())
        {
            if (item is not TreeViewItem tvi)
            {
                continue;
            }
            if (index < _sourceLogicalItems.Count && ReferenceEquals(_sourceLogicalItems[index], tvi))
            {
                index++;
            }
            else if (tvi.Parent is null)
            {
                return true;
            }
        }
        return index != _sourceLogicalItems.Count;
    }

    private void PostRebuild()
    {
        if (_rebuildPosted)
        {
            return;
        }
        _rebuildPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildPosted = false;
            Rebuild(animate: true);
        }, DispatcherPriority.Background);
    }

    internal void OnInlineItemChanged(TreeViewItem item, bool structure, bool expansion)
    {
        if (_bulk > 0)
        {
            return;
        }
        if (expansion)
        {
            var stagger = item.IsExpanded ? item : null;
            if (_pendingStagger is not null)
            {
                stagger = ReferenceEquals(_pendingStagger, item) && item.IsExpanded ? item : stagger;
            }
            _pendingStagger = null;
            Rebuild(animate: true, stagger);
            RaiseEvent(new TreeViewItemEventArgs(item.IsExpanded ? ItemExpandedEvent : ItemCollapsedEvent, item));
            return;
        }
        if (structure)
        {
            PostRebuild();
            return;
        }
        if (_nodes.TryGetValue(item, out var node))
        {
            node.Label = LabelOf(item);
            node.Icon = IconOf(item);
            node.IsDisabled = DisabledOf(item);
            _panel?.RowFor(item)?.Refresh();
        }
    }

    private void SetExpanded(object item, bool expanded, bool animate)
    {
        if (IsItemExpanded(item) == expanded || !HasAny(ChildrenOf(item)))
        {
            return;
        }
        if (item is TreeViewItem tvi)
        {
            _pendingStagger = expanded ? item : null;
            tvi.SetCurrentValue(TreeViewItem.IsExpandedProperty, expanded);
            return;
        }
        if (expanded)
        {
            _expanded.Add(item);
        }
        else
        {
            _expanded.Remove(item);
        }
        Rebuild(animate, expanded ? item : null);
        RaiseEvent(new TreeViewItemEventArgs(expanded ? ItemExpandedEvent : ItemCollapsedEvent, item));
    }

    private void SetAll(bool expanded)
    {
        _bulk++;
        try
        {
            var stack = new Stack<object>();
            foreach (var root in Roots)
            {
                if (root is not null)
                {
                    stack.Push(root);
                }
            }
            var seen = new HashSet<object>();
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                if (!seen.Add(item))
                {
                    continue;
                }
                var children = ChildrenOf(item);
                if (!HasAny(children))
                {
                    continue;
                }
                if (item is TreeViewItem tvi)
                {
                    tvi.SetCurrentValue(TreeViewItem.IsExpandedProperty, expanded);
                }
                else if (expanded)
                {
                    _expanded.Add(item);
                }
                else
                {
                    _expanded.Remove(item);
                }
                foreach (var child in children!)
                {
                    if (child is not null)
                    {
                        stack.Push(child);
                    }
                }
            }
        }
        finally
        {
            _bulk--;
        }
        Rebuild(animate: true);
    }

    /// <summary>Opens every ancestor of <paramref name="item"/>; false when the item is not in the tree.</summary>
    private bool RevealItem(object item)
    {
        if (_nodes.ContainsKey(item))
        {
            return true;
        }
        var path = new List<object>();
        if (!FindPath(Roots, item, path, new HashSet<object>()))
        {
            return false;
        }
        _bulk++;
        try
        {
            foreach (var ancestor in path)
            {
                if (ancestor is TreeViewItem tvi)
                {
                    tvi.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
                }
                else
                {
                    _expanded.Add(ancestor);
                }
            }
        }
        finally
        {
            _bulk--;
        }
        Rebuild(animate: true);
        return _nodes.ContainsKey(item);
    }

    private bool FindPath(IEnumerable? children, object target, List<object> path, HashSet<object> seen)
    {
        if (children is null)
        {
            return false;
        }
        foreach (var child in children)
        {
            if (child is null || !seen.Add(child))
            {
                continue;
            }
            if (Equals(child, target))
            {
                return true;
            }
            path.Add(child);
            if (FindPath(ChildrenOf(child), target, path, seen))
            {
                return true;
            }
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }

    #endregion

    #region Selection and focus

    private void SetFocusKey(object? key)
    {
        if (Equals(_focusKey, key))
        {
            return;
        }
        var previous = _focusKey;
        _focusKey = key;
        if (_panel is not null)
        {
            _panel.PinnedKey = key;
            _panel.RowFor(previous)?.Refresh();
            _panel.RowFor(key)?.Refresh();
        }
    }

    internal bool IsTabStopKey(object? key) => key is not null && Equals(key, _focusKey);

    private void MoveFocus(int index, NavigationMethod method)
    {
        if (_panel is null || index < 0 || index >= _rows.Count)
        {
            return;
        }
        var node = (TreeViewNode)_rows[index];
        SetFocusKey(node.Item);
        var row = _panel.Realize(index);
        if (row is null)
        {
            return;
        }
        row.Focus(method);
        if (method != NavigationMethod.Pointer)
        {
            row.BringIntoView();
        }
    }

    /// <summary>Selects and raises <see cref="ItemInvoked"/> (click, Enter, Space).</summary>
    private void Invoke(TreeViewNode node)
    {
        if (node.IsDisabled)
        {
            // Shown but not selectable: a disabled folder still opens and closes.
            if (node.HasChildren)
            {
                ToggleExpanded(node.Item);
            }
            return;
        }
        SetCurrentValue(SelectedItemProperty, node.Item);
        RaiseEvent(new TreeViewItemEventArgs(ItemInvokedEvent, node.Item));
        if (node.HasChildren)
        {
            ToggleExpanded(node.Item);
        }
    }

    internal void OnRowClicked(TreeViewRow row)
    {
        if (row.Model is not TreeViewNode node || (node.IsDisabled && !node.HasChildren))
        {
            return;
        }
        MoveFocus(IndexOf(node.Item), NavigationMethod.Pointer);
        Invoke(node);
    }

    internal void OnRowFocused(TreeViewRow row)
    {
        if (row.Model is TreeViewNode node)
        {
            SetFocusKey(node.Item);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0
            || KeyRowOf(e.Source, out var fromContent) is not { Model: TreeViewNode node })
        {
            return;
        }
        // Enter and Space inside a row's content belong to that content (a button, a check box).
        if (fromContent && e.Key is Key.Enter or Key.Space)
        {
            return;
        }
        var index = IndexOf(node.Item);
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.Home:
            case Key.End:
            case Key.PageDown:
            case Key.PageUp:
                _navigator.PageSize = Math.Max(1, (int)((_panel?.Bounds.Height ?? 380) / Math.Max(1, EffectiveRowHeight)) - 1);
                if (_navigator.Navigate(index, e.Key) is { } target)
                {
                    MoveFocus(target, NavigationMethod.Directional);
                }
                e.Handled = true;
                break;
            case Key.Right:
                if (node.HasChildren)
                {
                    if (!node.IsExpanded)
                    {
                        Expand(node.Item);
                    }
                    else if (index + 1 < _rows.Count && ((TreeViewNode)_rows[index + 1]).Parent == node)
                    {
                        MoveFocus(index + 1, NavigationMethod.Directional);
                    }
                }
                e.Handled = true;
                break;
            case Key.Left:
                if (node.HasChildren && node.IsExpanded)
                {
                    Collapse(node.Item);
                }
                else if (node.Parent is { } parent)
                {
                    MoveFocus(IndexOf(parent.Item), NavigationMethod.Directional);
                }
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Invoke(node);
                e.Handled = true;
                break;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || string.IsNullOrWhiteSpace(e.Text) || KeyRowOf(e.Source, out _) is not { Model: TreeViewNode node })
        {
            return;
        }
        if (_navigator.TypeAhead(IndexOf(node.Item), e.Text) is { } target)
        {
            MoveFocus(target, NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    /// <summary>The row that holds <paramref name="element"/> (the row itself or an element of its content).</summary>
    private static TreeViewRow? RowOf(object? element)
    {
        for (var visual = element as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TreeViewRow row)
            {
                return row;
            }
        }
        return null;
    }

    /// <summary>
    /// The row whose keys <paramref name="source"/> raises, or null when the tree must leave them alone: text typed
    /// into a text box inside a row's content stays there.
    /// </summary>
    private TreeViewRow? KeyRowOf(object? source, out bool fromContent)
    {
        fromContent = false;
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TreeViewRow row)
            {
                return ReferenceEquals(row.Owner, this) ? row : null;
            }
            if (visual is TextBox)
            {
                return null;
            }
            fromContent = true;
        }
        return null;
    }

    #endregion

    #region Lifecycle

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_panel is not null)
        {
            _panel.Owner = null;
        }
        _panel = e.NameScope.Find<TreeViewRowsPanel>(RowsPart);
        UpdatePalette();
        if (_panel is not null)
        {
            _panel.Owner = this;
            _panel.Highlight.Fill = SelectionFill;
            _panel.Highlight.CornerRadius = new CornerRadius(Palette.RowRadius);
            _panel.HighlightInset = new Thickness(0, Palette.RowGap, 0, 0);
            _panel.PinnedKey = _focusKey;
            _panel.SetRows(_rows, animate: false);
            _panel.SetHighlight(SelectedItem is not null && _nodes.ContainsKey(SelectedItem) ? SelectedItem : null, animate: false);
        }
    }

    // The data may outlive the view (a view model): the tree listens to its collections only while it is attached, so
    // the data does not keep it alive, and rebuilds from the data when it comes back to show what changed meanwhile.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        if (SourceLogicalItemsChanged())
        {
            UpdateSourceLogicalItems();
        }
        Rebuild(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        UpdateSubscriptions([]);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (_rows.Count == 0)
        {
            Rebuild(animate: false);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            UpdateSourceLogicalItems();
            Rebuild(animate: false);
        }
        else if (change.Property == ChildrenSelectorProperty || change.Property == HeaderSelectorProperty || change.Property == IconSelectorProperty
                 || change.Property == DisabledSelectorProperty)
        {
            Rebuild(animate: false);
        }
        else if (change.Property == ItemTemplateProperty)
        {
            // Realized rows rebuild their content now; pooled rows pick the template up when they are bound again.
            if (_panel is not null)
            {
                foreach (var row in _panel.RealizedRows)
                {
                    row.Refresh();
                }
                _panel.InvalidateMeasure();
            }
        }
        else if (change.Property == SelectedItemProperty)
        {
            OnSelectedItemChanged(change.OldValue, change.NewValue);
        }
        else if (Array.IndexOf(s_paletteProperties, change.Property) >= 0)
        {
            UpdatePalette();
        }
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (_syncingSelection)
        {
            return;
        }
        if (IsDisabledItem(newItem))
        {
            // A disabled node cannot become the selection; a two-way binding gets the previous item back.
            _syncingSelection = true;
            try
            {
                SetCurrentValue(SelectedItemProperty, oldItem);
            }
            finally
            {
                _syncingSelection = false;
            }
            return;
        }
        if (newItem is not null && !_nodes.ContainsKey(newItem))
        {
            _syncingSelection = true;
            try
            {
                RevealItem(newItem);
            }
            finally
            {
                _syncingSelection = false;
            }
        }
        _panel?.SetHighlight(newItem is not null && _nodes.ContainsKey(newItem) ? newItem : null, animate: true);
        _panel?.RowFor(oldItem)?.Refresh();
        _panel?.RowFor(newItem)?.Refresh();
        RaiseEvent(new TreeViewSelectionChangedEventArgs(SelectionChangedEvent, oldItem, newItem));
    }

    private void UpdatePalette()
    {
        var p = Palette;
        p.Foreground = ColorOf(RowForeground);
        p.ActiveForeground = ColorOf(RowActiveForeground);
        p.HoverFill = RowHoverFill;
        p.Muted = MutedForeground;
        p.MutedColor = ColorOf(MutedForeground);
        p.Guide = GuideBrush;
        p.FocusRing = FocusRingBrush is { } ring ? new Pen(ring, 2) : null;
        p.TypeScript = TypeScriptBrush;
        p.Stylesheet = StylesheetBrush;
        p.Markdown = MarkdownBrush;
        p.Json = JsonBrush;
        p.FolderRest = RowForeground;
        p.FolderActive = RowActiveForeground;
        p.FontFamily = FontFamily;
        p.FontSize = FontSize;
        p.SlotHeight = EffectiveRowHeight;
        p.Indent = IndentWidth;
        p.ShowGuides = ShowIndentGuides;
        p.ShowIcons = ShowIcons;
        p.Version++;
        if (_panel is not null)
        {
            _panel.RowHeight = EffectiveRowHeight;
            _panel.Highlight.Fill = SelectionFill;
            foreach (var row in _panel.RealizedRows)
            {
                ((TreeViewRow)row).Refresh();
            }
            _panel.InvalidateMeasure();
        }
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Transparent;

    #endregion
}
