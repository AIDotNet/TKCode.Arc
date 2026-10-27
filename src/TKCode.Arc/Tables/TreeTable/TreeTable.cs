using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A table whose rows nest. Rows expand and collapse on a spring with indent guides, parents carry tri-state checkboxes
/// (<see cref="IsSelectable"/>), sorting reorders siblings without breaking the hierarchy, and children can load on
/// first expand (<see cref="ChildrenLoader"/>). The first column shown holds the tree. Keyboard follows the treegrid
/// pattern: ↑/↓ move, → opens or enters, ← closes or goes to the parent, Home/End, PageUp/PageDown move ten rows,
/// <c>*</c> opens all siblings, Space selects, Enter toggles a parent or activates a leaf, Ctrl/Cmd+A selects all.
/// <para>
/// Rows come from <see cref="ItemsSource"/> and <see cref="ChildrenSelector"/>; each row is identified by
/// <see cref="KeySelector"/> (the item itself when not set), which is what <see cref="ExpandedKeys"/> and
/// <see cref="SelectedKeys"/> hold. Like Arc, rows are not virtualized: every visible row exists and animates, so keep
/// an expanded tree to a few hundred rows and load deeper levels on demand.
/// </para>
/// </summary>
[TemplatePart(HeaderSlotPart, typeof(Border))]
[TemplatePart(RowsSlotPart, typeof(Border))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(EmptyPart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
public class TreeTable : TemplatedControl
{
    public const string HeaderSlotPart = "PART_HeaderSlot";
    public const string RowsSlotPart = "PART_RowsSlot";
    public const string ScrollPart = "PART_Scroll";
    public const string EmptyPart = "PART_Empty";
    public const string StatusPart = "PART_Status";

    #region Properties

    /// <summary>The top-level rows.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<TreeTable, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Children of a row item (Arc <c>children</c>). Return null when the children are not loaded; an empty list means a leaf.</summary>
    public static readonly StyledProperty<Func<object, IEnumerable?>?> ChildrenSelectorProperty =
        AvaloniaProperty.Register<TreeTable, Func<object, IEnumerable?>?>(nameof(ChildrenSelector));

    /// <summary>Marks a row whose children are not loaded yet (Arc <c>hasChildren</c>); needs <see cref="ChildrenLoader"/>.</summary>
    public static readonly StyledProperty<Func<object, bool>?> HasChildrenSelectorProperty =
        AvaloniaProperty.Register<TreeTable, Func<object, bool>?>(nameof(HasChildrenSelector));

    /// <summary>Loads the children of a row with <see cref="HasChildrenSelector"/> the first time it expands (Arc <c>loadChildren</c>). A faulted task shows a retry row.</summary>
    public static readonly StyledProperty<Func<object, Task<IEnumerable?>>?> ChildrenLoaderProperty =
        AvaloniaProperty.Register<TreeTable, Func<object, Task<IEnumerable?>>?>(nameof(ChildrenLoader));

    /// <summary>Identifies a row; the keys of <see cref="ExpandedKeys"/> and <see cref="SelectedKeys"/> (Arc <c>id</c>). The item itself when not set.</summary>
    public static readonly StyledProperty<Func<object, object>?> KeySelectorProperty =
        AvaloniaProperty.Register<TreeTable, Func<object, object>?>(nameof(KeySelector));

    /// <summary>Names a row for its checkbox and for assistive technology (Arc <c>rowLabel</c>), such as the file or team name.</summary>
    public static readonly StyledProperty<Func<object, string?>?> RowLabelSelectorProperty =
        AvaloniaProperty.Register<TreeTable, Func<object, string?>?>(nameof(RowLabelSelector));

    /// <summary>The accessible name of the table (Arc <c>label</c>).</summary>
    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<TreeTable, string>(nameof(Caption), "Tree table");

    /// <summary>Shown when there are no rows (Arc <c>emptyState</c>).</summary>
    public static readonly StyledProperty<string> EmptyMessageProperty =
        AvaloniaProperty.Register<TreeTable, string>(nameof(EmptyMessage), "Nothing to show");

    /// <summary>Adds a checkbox column. Checking a parent checks everything under it (Arc <c>selectable</c>).</summary>
    public static readonly StyledProperty<bool> IsSelectableProperty =
        AvaloniaProperty.Register<TreeTable, bool>(nameof(IsSelectable));

    /// <summary>The keys of the expanded rows (two-way; Arc <c>expanded</c> / <c>defaultExpanded</c>).</summary>
    public static readonly StyledProperty<IReadOnlyList<object>?> ExpandedKeysProperty =
        AvaloniaProperty.Register<TreeTable, IReadOnlyList<object>?>(nameof(ExpandedKeys), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The keys of the selected rows (two-way; Arc <c>selected</c> / <c>defaultSelected</c>). Parents are added or removed to match their children.</summary>
    public static readonly StyledProperty<IReadOnlyList<object>?> SelectedKeysProperty =
        AvaloniaProperty.Register<TreeTable, IReadOnlyList<object>?>(nameof(SelectedKeys), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the column rows are sorted by, or null (two-way; Arc <c>sort.columnId</c>).</summary>
    public static readonly StyledProperty<string?> SortKeyProperty =
        AvaloniaProperty.Register<TreeTable, string?>(nameof(SortKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The sort direction (two-way; Arc <c>sort.direction</c>). A header cycles ascending, descending, none.</summary>
    public static readonly StyledProperty<DataTableSortDirection> SortDirectionProperty =
        AvaloniaProperty.Register<TreeTable, DataTableSortDirection>(nameof(SortDirection), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised when rows expand or collapse (Arc <c>onExpandedChange</c>).</summary>
    public static readonly RoutedEvent<TreeTableExpandedChangedEventArgs> RowExpandedChangedEvent =
        RoutedEvent.Register<TreeTable, TreeTableExpandedChangedEventArgs>(nameof(RowExpandedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised with the new selection (Arc <c>onSelectedChange</c>).</summary>
    public static readonly RoutedEvent<TreeTableSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<TreeTable, TreeTableSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a header cycles the sort (Arc <c>onSortChange</c>).</summary>
    public static readonly RoutedEvent<TreeTableSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<TreeTable, TreeTableSortChangedEventArgs>(nameof(SortChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a row without children is clicked or gets Enter (Arc <c>onRowActivate</c>). Parent rows toggle instead.</summary>
    public static readonly RoutedEvent<TreeTableRowEventArgs> RowActivatedEvent =
        RoutedEvent.Register<TreeTable, TreeTableRowEventArgs>(nameof(RowActivated), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="ChildrenLoader"/> faults; the row shows a retry line.</summary>
    public static readonly RoutedEvent<TreeTableRowEventArgs> ChildrenLoadFailedEvent =
        RoutedEvent.Register<TreeTable, TreeTableRowEventArgs>(nameof(ChildrenLoadFailed), RoutingStrategies.Bubble);

    // Geometry and colors, set by the theme.
    internal static readonly StyledProperty<double> RowHeightProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(RowHeight), 44);
    internal static readonly StyledProperty<double> IndentWidthProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(IndentWidth), 20);
    internal static readonly StyledProperty<double> ChevronExtentProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(ChevronExtent), 24);
    internal static readonly StyledProperty<double> CheckColumnWidthProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(CheckColumnWidth), 44);
    internal static readonly StyledProperty<double> TreeCellMarginProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(TreeCellMargin), 10);
    internal static readonly StyledProperty<double> CellInlinePaddingProperty = AvaloniaProperty.Register<TreeTable, double>(nameof(CellInlinePadding), 12);
    internal static readonly StyledProperty<IBrush?> TextSecondaryBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(TextSecondaryBrush));
    internal static readonly StyledProperty<IBrush?> TextMutedBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(TextMutedBrush));
    internal static readonly StyledProperty<IBrush?> DividerBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(DividerBrush));
    internal static readonly StyledProperty<IBrush?> GuideBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(GuideBrush));
    internal static readonly StyledProperty<IBrush?> GuideActiveBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(GuideActiveBrush));
    internal static readonly StyledProperty<IBrush?> SkeletonBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(SkeletonBrush));
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(DangerBrush));
    internal static readonly StyledProperty<IBrush?> FocusRingBrushProperty = AvaloniaProperty.Register<TreeTable, IBrush?>(nameof(FocusRingBrush));

    private static readonly AvaloniaProperty[] s_paletteProperties =
    [
        ForegroundProperty, FontFamilyProperty, TextSecondaryBrushProperty, TextMutedBrushProperty, DividerBrushProperty,
        GuideBrushProperty, GuideActiveBrushProperty, SkeletonBrushProperty, DangerBrushProperty, FocusRingBrushProperty
    ];

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="ChildrenSelectorProperty"/>
    public Func<object, IEnumerable?>? ChildrenSelector { get => GetValue(ChildrenSelectorProperty); set => SetValue(ChildrenSelectorProperty, value); }

    /// <inheritdoc cref="HasChildrenSelectorProperty"/>
    public Func<object, bool>? HasChildrenSelector { get => GetValue(HasChildrenSelectorProperty); set => SetValue(HasChildrenSelectorProperty, value); }

    /// <inheritdoc cref="ChildrenLoaderProperty"/>
    public Func<object, Task<IEnumerable?>>? ChildrenLoader { get => GetValue(ChildrenLoaderProperty); set => SetValue(ChildrenLoaderProperty, value); }

    /// <inheritdoc cref="KeySelectorProperty"/>
    public Func<object, object>? KeySelector { get => GetValue(KeySelectorProperty); set => SetValue(KeySelectorProperty, value); }

    /// <inheritdoc cref="RowLabelSelectorProperty"/>
    public Func<object, string?>? RowLabelSelector { get => GetValue(RowLabelSelectorProperty); set => SetValue(RowLabelSelectorProperty, value); }

    /// <inheritdoc cref="CaptionProperty"/>
    public string Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="EmptyMessageProperty"/>
    public string EmptyMessage { get => GetValue(EmptyMessageProperty); set => SetValue(EmptyMessageProperty, value); }

    /// <inheritdoc cref="IsSelectableProperty"/>
    public bool IsSelectable { get => GetValue(IsSelectableProperty); set => SetValue(IsSelectableProperty, value); }

    /// <inheritdoc cref="ExpandedKeysProperty"/>
    public IReadOnlyList<object>? ExpandedKeys { get => GetValue(ExpandedKeysProperty); set => SetValue(ExpandedKeysProperty, value); }

    /// <inheritdoc cref="SelectedKeysProperty"/>
    public IReadOnlyList<object>? SelectedKeys { get => GetValue(SelectedKeysProperty); set => SetValue(SelectedKeysProperty, value); }

    /// <inheritdoc cref="SortKeyProperty"/>
    public string? SortKey { get => GetValue(SortKeyProperty); set => SetValue(SortKeyProperty, value); }

    /// <inheritdoc cref="SortDirectionProperty"/>
    public DataTableSortDirection SortDirection { get => GetValue(SortDirectionProperty); set => SetValue(SortDirectionProperty, value); }

    /// <summary>The columns (the XAML content). The first one shown holds the tree.</summary>
    [Content]
    public AvaloniaList<TreeTableColumn> Columns { get; } = new();

    /// <summary>Raised when rows expand or collapse; the argument carries every expanded key.</summary>
    public event EventHandler<TreeTableExpandedChangedEventArgs>? RowExpandedChanged
    {
        add => AddHandler(RowExpandedChangedEvent, value);
        remove => RemoveHandler(RowExpandedChangedEvent, value);
    }

    /// <summary>Raised with the new selection.</summary>
    public event EventHandler<TreeTableSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <summary>Raised when a header cycles the sort.</summary>
    public event EventHandler<TreeTableSortChangedEventArgs>? SortChanged
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    /// <summary>Raised when a row without children is clicked or gets Enter.</summary>
    public event EventHandler<TreeTableRowEventArgs>? RowActivated
    {
        add => AddHandler(RowActivatedEvent, value);
        remove => RemoveHandler(RowActivatedEvent, value);
    }

    /// <summary>Raised when <see cref="ChildrenLoader"/> faults.</summary>
    public event EventHandler<TreeTableRowEventArgs>? ChildrenLoadFailed
    {
        add => AddHandler(ChildrenLoadFailedEvent, value);
        remove => RemoveHandler(ChildrenLoadFailedEvent, value);
    }

    internal double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    internal double IndentWidth { get => GetValue(IndentWidthProperty); set => SetValue(IndentWidthProperty, value); }
    internal double ChevronExtent { get => GetValue(ChevronExtentProperty); set => SetValue(ChevronExtentProperty, value); }
    internal double CheckColumnWidth { get => GetValue(CheckColumnWidthProperty); set => SetValue(CheckColumnWidthProperty, value); }
    internal double TreeCellMargin { get => GetValue(TreeCellMarginProperty); set => SetValue(TreeCellMarginProperty, value); }
    internal double CellInlinePadding { get => GetValue(CellInlinePaddingProperty); set => SetValue(CellInlinePaddingProperty, value); }
    internal IBrush? TextSecondaryBrush { get => GetValue(TextSecondaryBrushProperty); set => SetValue(TextSecondaryBrushProperty, value); }
    internal IBrush? TextMutedBrush { get => GetValue(TextMutedBrushProperty); set => SetValue(TextMutedBrushProperty, value); }
    internal IBrush? DividerBrush { get => GetValue(DividerBrushProperty); set => SetValue(DividerBrushProperty, value); }
    internal IBrush? GuideBrush { get => GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }
    internal IBrush? GuideActiveBrush { get => GetValue(GuideActiveBrushProperty); set => SetValue(GuideActiveBrushProperty, value); }
    internal IBrush? SkeletonBrush { get => GetValue(SkeletonBrushProperty); set => SetValue(SkeletonBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    #endregion

    private readonly HashSet<object> _expanded = new();
    private readonly HashSet<object> _selected = new();
    private readonly HashSet<object> _mixed = new();
    private readonly Dictionary<object, List<object>> _loaded = new();
    private readonly Dictionary<object, bool> _failed = new();
    private readonly HashSet<object> _loading = new();
    private readonly HashSet<object> _arrived = new();
    private readonly Dictionary<object, object> _itemByKey = new();
    private readonly Dictionary<object, object?> _parentOf = new();
    private readonly Dictionary<object, List<object>> _childKeys = new();
    private readonly Dictionary<object, List<object>?> _kids = new();
    private readonly List<object> _allKeys = new();
    private readonly List<object> _expandableKeys = new();
    private readonly Dictionary<TreeTableColumn, TableBindingProbe> _probes = new();
    private readonly HashSet<INotifyCollectionChanged> _watched = new();
    private readonly List<TreeTableLine> _lines = new();
    private readonly List<TreeTableLine> _rowLines = new();
    private readonly Dictionary<object, TreeTableLine> _byKey = new();
    private TreeTableRowsHost? _host;
    private TreeTableHeader? _header;
    private ScrollViewer? _scroll;
    private TextBlock? _empty;
    private TextBlock? _status;
    private List<TreeTableColumn> _shown = new();
    private double _width = double.NaN;
    private object? _focusKey;
    private object? _hoverKey;
    private int _expandedVersion;
    private int _arrivedAt = -1;
    private bool _syncing;
    private bool _refreshPosted;
    private bool _inRebuild;
    private bool _templated;
    private bool _everyOpen;

    internal TreeTablePalette Palette { get; } = new();

    /// <summary>Bumped when the set of shown columns changes; rows rebuild their cells when it moves.</summary>
    internal int ColumnVersion { get; private set; }

    internal IReadOnlyList<TreeTableColumn> ShownColumns => _shown;

    internal TreeTableRowsHost? RowsHost => _host;

    internal TreeTableHeader? Header => _header;

    /// <summary>The visible row lines in order (tests).</summary>
    internal IReadOnlyList<TreeTableLine> VisibleRows => _rowLines;

    internal object? FocusKey => _focusKey;

    static TreeTable()
    {
        FocusableProperty.OverrideDefaultValue<TreeTable>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<TreeTable>(AutomationControlType.DataGrid);
    }

    public TreeTable()
    {
        Columns.CollectionChanged += OnColumnsChanged;
        AutomationProperties.SetName(this, Caption);
    }

    #region Public API

    /// <summary>Whether the row of <paramref name="item"/> is expanded.</summary>
    public bool IsExpanded(object item) => _expanded.Contains(KeyOf(item));

    /// <summary>Expands a row (and loads its children when they are not loaded yet).</summary>
    public void Expand(object item) => SetOpen(KeyOf(item), true);

    /// <summary>Collapses a row.</summary>
    public void Collapse(object item) => SetOpen(KeyOf(item), false);

    /// <summary>Expands every row whose children are loaded (Arc <c>collectExpandableIds</c>).</summary>
    public void ExpandAll()
    {
        if (!_everyOpen)
        {
            ToggleEverything();
        }
    }

    /// <summary>Collapses every row.</summary>
    public void CollapseAll()
    {
        if (_expanded.Count > 0)
        {
            _expanded.Clear();
            _expandedVersion++;
            CommitExpanded();
            Rebuild(animate: true);
            Announce("Collapsed all rows");
        }
    }

    /// <summary>Selects every row, or clears the selection when every top-level row is already selected.</summary>
    public void ToggleSelectAll()
    {
        var allOn = IsEverythingSelected();
        CommitSelection(allOn ? new HashSet<object>() : Normalize(new HashSet<object>(_allKeys)));
        Announce(allOn ? "Selection cleared" : "All rows selected");
    }

    /// <summary>Clears the selection.</summary>
    public void ClearSelection()
    {
        if (_selected.Count > 0)
        {
            CommitSelection(new HashSet<object>());
        }
    }

    /// <summary>Moves keyboard focus to the row of <paramref name="item"/> when it is visible.</summary>
    public void FocusItem(object item) => FocusRow(KeyOf(item));

    /// <summary>Reads the data again (after items changed without a collection notification).</summary>
    public void Refresh() => Rebuild(animate: true);

    #endregion

    #region Data

    private object KeyOf(object item) => KeySelector?.Invoke(item) ?? item;

    private List<object>? KidsOf(object item, object key)
    {
        if (_kids.TryGetValue(key, out var cached))
        {
            return cached;
        }
        List<object>? list = null;
        if (ChildrenSelector?.Invoke(item) is { } known)
        {
            list = known.Cast<object>().ToList();
            Watch(known);
        }
        else if (_loaded.TryGetValue(key, out var loaded))
        {
            list = loaded;
        }
        _kids[key] = list;
        return list;
    }

    private bool CanExpand(object item, object key)
    {
        var kids = KidsOf(item, key);
        if (kids is { Count: > 0 })
        {
            return true;
        }
        return kids is null && ChildrenLoader is not null && HasChildrenSelector?.Invoke(item) == true;
    }

    private HashSet<INotifyCollectionChanged>? _nextWatched;

    private void Watch(IEnumerable? source)
    {
        if (source is INotifyCollectionChanged changes)
        {
            _nextWatched?.Add(changes);
        }
    }

    private void SyncWatchers()
    {
        var next = _nextWatched ?? new HashSet<INotifyCollectionChanged>();
        foreach (var stale in _watched.Where(w => !next.Contains(w)).ToList())
        {
            stale.CollectionChanged -= OnSourceChanged;
            _watched.Remove(stale);
        }
        foreach (var fresh in next)
        {
            if (_watched.Add(fresh))
            {
                fresh.CollectionChanged += OnSourceChanged;
            }
        }
        _nextWatched = null;
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPosted = false;
            Rebuild(animate: true);
        }, DispatcherPriority.Background);
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TreeTableColumn column in e.OldItems)
            {
                column.DefinitionChanged -= OnColumnDefinitionChanged;
                _probes.Remove(column);
            }
        }
        if (e.NewItems is not null)
        {
            foreach (TreeTableColumn column in e.NewItems)
            {
                column.DefinitionChanged += OnColumnDefinitionChanged;
            }
        }
        RebuildColumns();
    }

    private void OnColumnDefinitionChanged(object? sender, EventArgs e)
    {
        if (sender is TreeTableColumn column)
        {
            _probes.Remove(column);
        }
        RebuildColumns();
    }

    internal object? ValueOf(TreeTableColumn column, object item)
    {
        if (column.ValueSelector is { } selector)
        {
            return selector(item);
        }
        if (column.Binding is { } binding)
        {
            if (!_probes.TryGetValue(column, out var probe))
            {
                probe = new TableBindingProbe(binding);
                _probes[column] = probe;
            }
            return probe.Evaluate(item);
        }
        return null;
    }

    internal string CellText(TreeTableColumn column, object item)
    {
        var value = ValueOf(column, item);
        if (column.Formatter is { } formatter)
        {
            return formatter(value);
        }
        if (value is null || value is DBNull)
        {
            return string.Empty;
        }
        if (column.StringFormat is { Length: > 0 } format)
        {
            if (format.Contains('{'))
            {
                return string.Format(CultureInfo.CurrentCulture, format, value);
            }
            if (value is IFormattable formattable)
            {
                return formattable.ToString(format, CultureInfo.CurrentCulture);
            }
        }
        return value as string ?? Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    internal string LabelOf(object item)
    {
        if (RowLabelSelector?.Invoke(item) is { } label)
        {
            return label;
        }
        var first = _shown.Count > 0 ? _shown[0] : Columns.FirstOrDefault();
        return first is null ? item.ToString() ?? string.Empty : CellText(first, item);
    }

    internal string RowLabelFor(TreeTableRow row) => RowLabelSelector is not null && row.Item is { } item
        ? $"Select {LabelOf(item)}"
        : $"Select row {row.Line.Position} of {row.Line.Size}";

    #endregion

    #region Rebuild

    private TreeTableColumn? SortColumn =>
        SortKey is { } key ? Columns.FirstOrDefault(c => c.EffectiveKey == key && c.IsSortable && c.HasSortValue) : null;

    private List<object> Order(List<object> items)
    {
        var column = SortColumn;
        var direction = SortDirection;
        if (column is null || direction == DataTableSortDirection.None)
        {
            return items;
        }
        var sign = direction == DataTableSortDirection.Ascending ? 1 : -1;
        var keyed = items.Select((item, index) =>
        {
            var value = column.SortValueSelector is { } sorter ? sorter(item) : ValueOf(column, item);
            return (item, index, value);
        }).ToList();
        keyed.Sort((a, b) =>
        {
            var aEmpty = TableValues.IsEmpty(a.value);
            var bEmpty = TableValues.IsEmpty(b.value);
            if (aEmpty || bEmpty)
            {
                return aEmpty == bEmpty ? a.index.CompareTo(b.index) : aEmpty ? 1 : -1;
            }
            var result = TableValues.Compare(a.value!, b.value!, null);
            return result == 0 ? a.index.CompareTo(b.index) : sign * result;
        });
        return keyed.Select(k => k.item).ToList();
    }

    private void Rebuild(bool animate, bool flip = false, bool rebuildCells = false)
    {
        if (!_templated || _host is null || _inRebuild)
        {
            return;
        }
        _inRebuild = true;
        try
        {
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as TreeTableRow;
            _nextWatched = new HashSet<INotifyCollectionChanged>();
            _itemByKey.Clear();
            _parentOf.Clear();
            _childKeys.Clear();
            _kids.Clear();
            _allKeys.Clear();
            _expandableKeys.Clear();
            var roots = ItemsSource is { } source ? source.Cast<object>().ToList() : new List<object>();
            Watch(ItemsSource);
            IndexItems(roots, null);
            SyncWatchers();
            ComputeMixed(roots);

            _lines.Clear();
            _rowLines.Clear();
            _byKey.Clear();
            Walk(roots, 1, null, []);
            foreach (var line in _lines)
            {
                if (line.Kind == TreeTableLineKind.Row)
                {
                    _rowLines.Add(line);
                    _byKey[line.Key] = line;
                }
            }

            _everyOpen = _expandableKeys.Count > 0 && _expandableKeys.All(_expanded.Contains);
            var handoff = _arrivedAt == _expandedVersion
                ? new HashSet<object>(_arrived.Where(id => _byKey.TryGetValue(id, out var l) && l.Expanded))
                : new HashSet<object>();
            _host.Reconcile(_lines, handoff, animate, flip, rebuildCells);
            if (_empty is not null)
            {
                _empty.IsVisible = _rowLines.Count == 0;
            }

            // Pull focus up to the nearest visible ancestor when its row collapsed away.
            if (focused is not null && !_byKey.ContainsKey(focused.Key) && focused.Line.Kind == TreeTableLineKind.Row)
            {
                var id = focused.Key;
                while (id is not null && !_byKey.ContainsKey(id))
                {
                    id = _parentOf.GetValueOrDefault(id)!;
                }
                _focusKey = id ?? _rowLines.FirstOrDefault()?.Key;
                if (_focusKey is { } fallback)
                {
                    _host.RowFor(fallback)?.Focus(NavigationMethod.Directional);
                }
            }
            SyncState(animate);
            AutomationProperties.SetName(this, Caption);
        }
        finally
        {
            _inRebuild = false;
        }

        // Rows opened from outside, such as a controlled expand all, still fetch their children.
        foreach (var line in _lines.Where(l => l.Kind == TreeTableLineKind.Loading).ToList())
        {
            if (line.ParentKey is { } parent && !_loading.Contains(parent) && _itemByKey.TryGetValue(parent, out var item))
            {
                BeginLoad(parent, item);
            }
        }
    }

    private void IndexItems(List<object> items, object? parentKey)
    {
        foreach (var item in items)
        {
            var key = KeyOf(item);
            if (_itemByKey.ContainsKey(key))
            {
                continue;
            }
            _itemByKey[key] = item;
            _parentOf[key] = parentKey;
            _allKeys.Add(key);
            var kids = KidsOf(item, key);
            if (kids is { Count: > 0 })
            {
                _childKeys[key] = kids.Select(KeyOf).ToList();
                _expandableKeys.Add(key);
                IndexItems(kids, key);
            }
        }
    }

    private void Walk(List<object> items, int depth, object? parentKey, IReadOnlyList<object> ancestors)
    {
        var sorted = Order(items);
        for (var i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];
            var key = KeyOf(item);
            var open = _expanded.Contains(key);
            var expandable = CanExpand(item, key);
            _lines.Add(new TreeTableLine
            {
                Kind = TreeTableLineKind.Row,
                Key = key,
                Item = item,
                Depth = depth,
                ParentKey = parentKey,
                Ancestors = ancestors,
                Position = i + 1,
                Size = sorted.Count,
                Expandable = expandable,
                Expanded = open && expandable
            });
            if (!open || !expandable)
            {
                continue;
            }
            var next = ancestors.Append(key).ToList();
            var kids = KidsOf(item, key);
            if (kids is not null)
            {
                Walk(kids, depth + 1, key, next);
            }
            else
            {
                _lines.Add(new TreeTableLine
                {
                    Kind = _failed.ContainsKey(key) ? TreeTableLineKind.Error : TreeTableLineKind.Loading,
                    Key = new TreeTablePendingKey(key),
                    Depth = depth + 1,
                    ParentKey = key,
                    Ancestors = next
                });
            }
        }
    }

    private void ComputeMixed(List<object> roots)
    {
        _mixed.Clear();
        bool Visit(object key)
        {
            var any = _selected.Contains(key);
            if (_childKeys.TryGetValue(key, out var kids))
            {
                foreach (var kid in kids)
                {
                    if (Visit(kid))
                    {
                        any = true;
                    }
                }
            }
            if (any && !_selected.Contains(key))
            {
                _mixed.Add(key);
            }
            return any;
        }
        foreach (var root in roots)
        {
            Visit(KeyOf(root));
        }
    }

    /// <summary>Pushes the current state into the rows and the header (no structural change).</summary>
    private void SyncState(bool animate)
    {
        if (_host is null)
        {
            return;
        }
        var active = _hoverKey ?? _focusKey;
        object? branch = null;
        if (active is not null && _byKey.TryGetValue(active, out var activeLine))
        {
            branch = activeLine.Expanded ? active : activeLine.ParentKey;
        }
        var tab = _focusKey is not null && _byKey.ContainsKey(_focusKey) ? _focusKey : _rowLines.FirstOrDefault()?.Key;
        foreach (var row in _host.Rows)
        {
            if (row.IsExiting)
            {
                continue;
            }
            var line = row.Line;
            if (line.Kind != TreeTableLineKind.Row)
            {
                row.Focusable = false;
                row.IsTabStop = false;
                row.Sync(false, CheckboxState.Unchecked, branch, false, animate);
                continue;
            }
            var check = _selected.Contains(line.Key) ? CheckboxState.Checked : _mixed.Contains(line.Key) ? CheckboxState.Indeterminate : CheckboxState.Unchecked;
            row.Focusable = true;
            row.IsTabStop = Equals(line.Key, tab);
            row.Sync(check == CheckboxState.Checked, check, branch, _loading.Contains(line.Key), animate);
        }
        _header?.Sync(_expandableKeys.Count > 0, _everyOpen, animate, SelectionState());
        UpdateHeaderSort(animate);
    }

    private void UpdateHeaderSort(bool animate)
    {
        if (_header is null)
        {
            return;
        }
        var sorted = SortKey;
        foreach (var cell in _header.Cells)
        {
            var sortable = cell.Column.IsSortable && cell.Column.HasSortValue;
            var direction = sortable && cell.Column.EffectiveKey == sorted ? SortDirection : DataTableSortDirection.None;
            cell.Update(sortable, direction, animate);
        }
    }

    private int SelectionState()
    {
        if (IsEverythingSelected())
        {
            return 2;
        }
        return _allKeys.Any(_selected.Contains) ? 1 : 0;
    }

    private bool IsEverythingSelected()
    {
        var roots = ItemsSource is { } source ? source.Cast<object>().ToList() : new List<object>();
        return roots.Count > 0 && roots.All(r => _selected.Contains(KeyOf(r)));
    }

    private void RebuildColumns()
    {
        if (!_templated || _header is null)
        {
            return;
        }
        UpdateShown(force: true);
    }

    private void UpdateShown(bool force)
    {
        var width = double.IsNaN(_width) ? double.PositiveInfinity : _width;
        var shown = Columns.Where(c => c.HideBelow <= 0 || double.IsInfinity(width) || width >= c.HideBelow).ToList();
        if (!force && shown.SequenceEqual(_shown))
        {
            return;
        }
        _shown = shown;
        ColumnVersion++;
        _header?.Build(_shown, IsSelectable);
        Rebuild(animate: false, rebuildCells: true);
        _header?.Sync(_expandableKeys.Count > 0, _everyOpen, false, SelectionState());
        UpdateHeaderSort(false);
    }

    #endregion

    #region State changes

    internal void SetOpen(object key, bool open)
    {
        if (open == _expanded.Contains(key))
        {
            return;
        }
        if (open)
        {
            _expanded.Add(key);
        }
        else
        {
            _expanded.Remove(key);
        }
        _expandedVersion++;
        if (open && _itemByKey.TryGetValue(key, out var item) && KidsOf(item, key) is null && HasChildrenSelector?.Invoke(item) == true)
        {
            BeginLoad(key, item);
        }
        CommitExpanded();
        Rebuild(animate: true);
    }

    internal void ToggleEverything()
    {
        if (_everyOpen)
        {
            _expanded.Clear();
        }
        else
        {
            foreach (var key in _expandableKeys)
            {
                _expanded.Add(key);
            }
        }
        _expandedVersion++;
        CommitExpanded();
        Rebuild(animate: true);
        Announce(_everyOpen ? "Expanded all rows" : "Collapsed all rows");
    }

    private void CommitExpanded()
    {
        var list = _expanded.ToList();
        _syncing = true;
        try
        {
            SetCurrentValue(ExpandedKeysProperty, list);
        }
        finally
        {
            _syncing = false;
        }
        RaiseEvent(new TreeTableExpandedChangedEventArgs(RowExpandedChangedEvent, list) { Source = this });
    }

    internal void ToggleSelect(object key, bool? on = null)
    {
        var next = new HashSet<object>(_selected);
        var value = on ?? !_selected.Contains(key);
        foreach (var member in Descendants(key).Prepend(key))
        {
            if (value)
            {
                next.Add(member);
            }
            else
            {
                next.Remove(member);
            }
        }
        CommitSelection(Normalize(next));
    }

    private IEnumerable<object> Descendants(object key)
    {
        if (!_childKeys.TryGetValue(key, out var kids))
        {
            yield break;
        }
        foreach (var kid in kids)
        {
            yield return kid;
            foreach (var deeper in Descendants(kid))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>A parent is selected when all its known children are, and not otherwise.</summary>
    private HashSet<object> Normalize(HashSet<object> next)
    {
        bool Settle(object key)
        {
            if (!_childKeys.TryGetValue(key, out var kids))
            {
                return next.Contains(key);
            }
            var all = true;
            foreach (var kid in kids)
            {
                if (!Settle(kid))
                {
                    all = false;
                }
            }
            if (all)
            {
                next.Add(key);
            }
            else
            {
                next.Remove(key);
            }
            return all;
        }
        var roots = ItemsSource is { } source ? source.Cast<object>() : [];
        foreach (var root in roots)
        {
            Settle(KeyOf(root));
        }
        return next;
    }

    private void CommitSelection(HashSet<object> next)
    {
        _selected.Clear();
        foreach (var key in next)
        {
            _selected.Add(key);
        }
        var list = _selected.ToList();
        _syncing = true;
        try
        {
            SetCurrentValue(SelectedKeysProperty, list);
        }
        finally
        {
            _syncing = false;
        }
        var items = list.Where(_itemByKey.ContainsKey).Select(k => _itemByKey[k]).ToList();
        ComputeMixedFromSource();
        SyncState(animate: true);
        RaiseEvent(new TreeTableSelectionChangedEventArgs(SelectionChangedEvent, list, items) { Source = this });
    }

    private void ComputeMixedFromSource()
    {
        var roots = ItemsSource is { } source ? source.Cast<object>().ToList() : new List<object>();
        ComputeMixed(roots);
    }

    internal void CycleSort(TreeTableColumn column)
    {
        if (!column.IsSortable || !column.HasSortValue)
        {
            return;
        }
        var key = column.EffectiveKey;
        DataTableSortDirection direction;
        string? nextKey = key;
        if (SortKey != key || SortDirection == DataTableSortDirection.None)
        {
            direction = DataTableSortDirection.Ascending;
        }
        else if (SortDirection == DataTableSortDirection.Ascending)
        {
            direction = DataTableSortDirection.Descending;
        }
        else
        {
            direction = DataTableSortDirection.None;
            nextKey = null;
        }
        _syncing = true;
        try
        {
            SetCurrentValue(SortKeyProperty, nextKey);
            SetCurrentValue(SortDirectionProperty, direction);
        }
        finally
        {
            _syncing = false;
        }
        Rebuild(animate: true, flip: true);
        Announce(direction == DataTableSortDirection.None
            ? "Sort cleared"
            : $"Sorted by {column.Header.ToLowerInvariant()}, {(direction == DataTableSortDirection.Ascending ? "ascending" : "descending")}");
        RaiseEvent(new TreeTableSortChangedEventArgs(SortChangedEvent, direction == DataTableSortDirection.None ? null : column, direction) { Source = this });
    }

    #endregion

    #region Lazy loading

    private void BeginLoad(object key, object item)
    {
        if (ChildrenLoader is not { } loader || !_loading.Add(key))
        {
            return;
        }
        _failed.Remove(key);
        Announce("Loading");
        _ = LoadAsync(loader, key, item);
    }

    private async Task LoadAsync(Func<object, Task<IEnumerable?>> loader, object key, object item)
    {
        try
        {
            var result = await loader(item);
            var kids = result?.Cast<object>().ToList() ?? new List<object>();
            _loading.Remove(key);
            _loaded[key] = kids;
            if (_arrivedAt != _expandedVersion)
            {
                _arrived.Clear();
            }
            _arrived.Add(key);
            _arrivedAt = _expandedVersion;
            Rebuild(animate: true);
            Announce($"{kids.Count} {(kids.Count == 1 ? "row" : "rows")} loaded");
            // Children that arrive under a checked parent start checked.
            if (_selected.Contains(key))
            {
                var next = new HashSet<object>(_selected);
                foreach (var added in Descendants(key))
                {
                    next.Add(added);
                }
                CommitSelection(Normalize(next));
            }
        }
        catch (Exception error)
        {
            _loading.Remove(key);
            _failed[key] = true;
            Rebuild(animate: true);
            Announce("Couldn't load rows");
            RaiseEvent(new TreeTableRowEventArgs(ChildrenLoadFailedEvent, item, key, error) { Source = this });
        }
    }

    internal void Retry(TreeTableRow pending)
    {
        if (pending.Line.ParentKey is { } parent && _itemByKey.TryGetValue(parent, out var item))
        {
            _failed.Remove(parent);
            BeginLoad(parent, item);
            Rebuild(animate: true);
        }
    }

    #endregion

    #region Row input

    internal void OnRowFocused(TreeTableRow row)
    {
        if (row.Line.Kind == TreeTableLineKind.Row && !Equals(_focusKey, row.Key))
        {
            _focusKey = row.Key;
            SyncState(animate: false);
        }
    }

    internal void OnRowHover(TreeTableRow row, bool entered)
    {
        var key = row.Line.Kind == TreeTableLineKind.Row ? row.Key : row.Line.ParentKey;
        if (entered)
        {
            _hoverKey = key;
        }
        else if (Equals(_hoverKey, key))
        {
            _hoverKey = null;
        }
        else
        {
            return;
        }
        SyncState(animate: false);
    }

    internal void OnRowClicked(TreeTableRow row)
    {
        _focusKey = row.Key;
        if (row.Line.Expandable)
        {
            SetOpen(row.Key, !row.Line.Expanded);
        }
        else if (row.Item is { } item)
        {
            RaiseEvent(new TreeTableRowEventArgs(RowActivatedEvent, item, row.Key) { Source = this });
        }
    }

    internal void ToggleFromChevron(TreeTableRow row)
    {
        _focusKey = row.Key;
        SetOpen(row.Key, !row.Line.Expanded);
    }

    private void FocusRow(object key)
    {
        if (_host?.RowFor(key) is { } row && !row.IsExiting)
        {
            _focusKey = key;
            row.Focus(NavigationMethod.Directional);
            row.BringIntoView();
            SyncState(animate: false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not TreeTableRow { Line.Kind: TreeTableLineKind.Row } row)
        {
            return;
        }
        var line = row.Line;
        var at = _rowLines.FindIndex(l => Equals(l.Key, line.Key));
        if (at < 0)
        {
            return;
        }
        void Go(int to)
        {
            e.Handled = true;
            FocusRow(_rowLines[Math.Clamp(to, 0, _rowLines.Count - 1)].Key);
        }
        switch (e.Key)
        {
            case Key.Down:
                Go(at + 1);
                break;
            case Key.Up:
                Go(at - 1);
                break;
            case Key.Home:
                Go(0);
                break;
            case Key.End:
                Go(_rowLines.Count - 1);
                break;
            case Key.PageDown:
                Go(at + 10);
                break;
            case Key.PageUp:
                Go(at - 10);
                break;
            case Key.Right:
                e.Handled = true;
                if (line.Expandable && !line.Expanded)
                {
                    SetOpen(line.Key, true);
                }
                else if (line.Expanded && at + 1 < _rowLines.Count && Equals(_rowLines[at + 1].ParentKey, line.Key))
                {
                    FocusRow(_rowLines[at + 1].Key);
                }
                break;
            case Key.Left:
                e.Handled = true;
                if (line.Expanded)
                {
                    SetOpen(line.Key, false);
                }
                else if (line.ParentKey is { } parent)
                {
                    FocusRow(parent);
                }
                break;
            case Key.Multiply:
            case Key.D8 when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                e.Handled = true;
                ExpandSiblings(line);
                break;
            case Key.Space when IsSelectable:
                e.Handled = true;
                ToggleSelect(line.Key);
                break;
            case Key.Enter:
                e.Handled = true;
                if (_failed.ContainsKey(line.Key))
                {
                    _failed.Remove(line.Key);
                    if (line.Item is { } retryItem)
                    {
                        BeginLoad(line.Key, retryItem);
                        Rebuild(animate: true);
                    }
                }
                else if (line.Expandable)
                {
                    SetOpen(line.Key, !line.Expanded);
                }
                else if (line.Item is { } item)
                {
                    RaiseEvent(new TreeTableRowEventArgs(RowActivatedEvent, item, line.Key) { Source = this });
                }
                break;
            case Key.A when IsSelectable && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)):
                e.Handled = true;
                ToggleSelectAll();
                break;
        }
    }

    private void ExpandSiblings(TreeTableLine line)
    {
        var siblings = _rowLines.Where(o => Equals(o.ParentKey, line.ParentKey) && o.Expandable).ToList();
        var changed = false;
        foreach (var sibling in siblings)
        {
            if (_expanded.Add(sibling.Key))
            {
                changed = true;
                if (_itemByKey.TryGetValue(sibling.Key, out var item) && KidsOf(item, sibling.Key) is null && HasChildrenSelector?.Invoke(item) == true)
                {
                    BeginLoad(sibling.Key, item);
                }
            }
        }
        if (changed)
        {
            _expandedVersion++;
            CommitExpanded();
            Rebuild(animate: true);
        }
    }

    #endregion

    #region Template and lifecycle

    private void Announce(string message)
    {
        if (_status is not null)
        {
            _status.Text = message;
        }
    }

    internal string? LastAnnouncement => _status?.Text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        var headerSlot = e.NameScope.Find<Border>(HeaderSlotPart);
        var rowsSlot = e.NameScope.Find<Border>(RowsSlotPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _empty = e.NameScope.Find<TextBlock>(EmptyPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        if (headerSlot is null || rowsSlot is null)
        {
            return;
        }
        _header = new TreeTableHeader(this);
        _host = new TreeTableRowsHost(this);
        headerSlot.Child = _header;
        rowsSlot.Child = _host;
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
        _templated = true;
        if (_empty is not null)
        {
            _empty.Text = EmptyMessage;
        }
        UpdatePalette();
        foreach (var column in Columns)
        {
            column.DefinitionChanged -= OnColumnDefinitionChanged;
            column.DefinitionChanged += OnColumnDefinitionChanged;
        }
        ReadKeys(ExpandedKeys, _expanded);
        ReadKeys(SelectedKeys, _selected);
        UpdateShown(force: true);
    }

    private static void ReadKeys(IReadOnlyList<object>? source, HashSet<object> target)
    {
        target.Clear();
        if (source is null)
        {
            return;
        }
        foreach (var key in source)
        {
            target.Add(key);
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // A scroll bar that takes room (not an overlay) narrows the rows; the pinned header gives up the same width.
        if (_scroll is not null && _header is not null)
        {
            var taken = Math.Max(0, _scroll.Bounds.Width - _scroll.Viewport.Width);
            _header.Margin = new Thickness(0, 0, taken, 0);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_templated && Math.Abs(_width - finalSize.Width) > 0.5 || double.IsNaN(_width))
        {
            _width = finalSize.Width;
            if (_templated)
            {
                UpdateShown(force: false);
            }
        }
        return result;
    }

    private void UpdatePalette()
    {
        Palette.Update(Foreground, TextSecondaryBrush, TextMutedBrush, DividerBrush, SkeletonBrush, DangerBrush,
            GuideBrush, GuideActiveBrush, FocusRingBrush, FontFamily, 12);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (Array.IndexOf(s_paletteProperties, property) >= 0)
        {
            if (_templated)
            {
                UpdatePalette();
                _host?.RefreshPalette();
                Rebuild(animate: false, rebuildCells: true);
            }
            return;
        }
        if (property == ItemsSourceProperty || property == ChildrenSelectorProperty || property == HasChildrenSelectorProperty
            || property == ChildrenLoaderProperty || property == KeySelectorProperty || property == RowLabelSelectorProperty)
        {
            _loaded.Clear();
            _failed.Clear();
            _loading.Clear();
            Rebuild(animate: false, rebuildCells: property != ItemsSourceProperty);
        }
        else if (property == IsSelectableProperty)
        {
            if (_templated)
            {
                UpdateShown(force: true);
            }
        }
        else if (property == ExpandedKeysProperty && !_syncing)
        {
            ReadKeys(ExpandedKeys, _expanded);
            _expandedVersion++;
            Rebuild(animate: true);
        }
        else if (property == SelectedKeysProperty && !_syncing)
        {
            ReadKeys(SelectedKeys, _selected);
            if (_templated)
            {
                Normalize(_selected);
                ComputeMixedFromSource();
                SyncState(animate: true);
            }
        }
        else if ((property == SortKeyProperty || property == SortDirectionProperty) && !_syncing)
        {
            Rebuild(animate: true, flip: true);
        }
        else if (property == EmptyMessageProperty && _empty is not null)
        {
            _empty.Text = EmptyMessage;
        }
        else if (property == CaptionProperty)
        {
            AutomationProperties.SetName(this, Caption);
        }
    }

    #endregion
}
