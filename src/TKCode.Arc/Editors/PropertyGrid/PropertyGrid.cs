using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PropertyGrid.PropertyValueChanged"/>.</summary>
public sealed class PropertyGridValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PropertyGridValueChangedEventArgs(RoutedEvent routedEvent, PropertyGridRow row, IReadOnlyList<object?> oldValues, object? newValue)
        : base(routedEvent)
    {
        Row = row;
        OldValues = oldValues;
        NewValue = newValue;
    }

    /// <summary>The row that was written; for an item added to or removed from a list, the list's row.</summary>
    public PropertyGridRow Row { get; }

    /// <summary>The value each owner had before (one per selected object).</summary>
    public IReadOnlyList<object?> OldValues { get; }

    /// <summary>The value written (the list itself after an item was added or removed).</summary>
    public object? NewValue { get; }
}

/// <summary>
/// Edits the properties of one or more objects in rows of label and editor, grouped by category, with search, a
/// description area, nested values and lists. Properties come from <see cref="PropertyGridTypes"/>: types marked
/// <see cref="PropertyGridModelAttribute"/> (registered by the generator), types registered in code, and the Avalonia
/// properties of an <see cref="AvaloniaObject"/>. Nothing is reflected, so it is safe for AOT and trimming.
/// </summary>
/// <remarks>
/// Values follow the objects: when an object raises <see cref="INotifyPropertyChanged.PropertyChanged"/> (or a list
/// raises <see cref="INotifyCollectionChanged.CollectionChanged"/>) the rows read it again. With several objects selected,
/// the rows are the properties they share; a value they disagree on shows <see cref="MixedValueText"/>, and an edit writes
/// every object. The built-in editors are Arc's fields at the small size; <see cref="Editors"/> adds your own by property
/// type, name or key. Rows are <see cref="PropertyGridItem"/> and category headers <see cref="PropertyGridCategoryItem"/>,
/// which can be styled by their pseudo-classes and <see cref="PropertyGridItem.PropertyName"/>.
/// </remarks>
[TemplatePart(ItemsPart, typeof(ItemsControl))]
[TemplatePart(SearchPart, typeof(SearchField))]
[PseudoClasses(EmptyPseudoClass)]
public class PropertyGrid : TemplatedControl
{
    public const string ItemsPart = "PART_Items";
    public const string SearchPart = "PART_Search";

    /// <summary>Set while there are no rows to show.</summary>
    public const string EmptyPseudoClass = ":empty";

    private const double MinLabelWidth = 64;
    private const double MinEditorWidth = 96;

    #region Properties

    /// <summary>The object whose properties are edited (ignored while <see cref="SelectedObjects"/> has items).</summary>
    public static readonly StyledProperty<object?> SelectedObjectProperty =
        AvaloniaProperty.Register<PropertyGrid, object?>(nameof(SelectedObject));

    /// <summary>Several objects edited together: the rows are the properties they share, and an edit writes all of them.</summary>
    public static readonly StyledProperty<IEnumerable?> SelectedObjectsProperty =
        AvaloniaProperty.Register<PropertyGrid, IEnumerable?>(nameof(SelectedObjects));

    /// <summary>Shows only the rows whose label or name contains this text (two-way with the search field).</summary>
    public static readonly StyledProperty<string?> FilterTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string?>(nameof(FilterText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the search field is shown above the rows.</summary>
    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(IsSearchVisible), true);

    /// <summary>Whether the description area under the rows shows the selected row's name and description.</summary>
    public static readonly StyledProperty<bool> IsDescriptionVisibleProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(IsDescriptionVisible), true);

    /// <summary>Whether rows are grouped under category headers (true) or listed alphabetically (false).</summary>
    public static readonly StyledProperty<bool> IsCategorizedProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(IsCategorized), true);

    /// <summary>When true, every row shows its value without an editor.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<PropertyGrid, bool>(nameof(IsReadOnly));

    /// <summary>Width of the label column; the line between label and editor can be dragged. Inherited by the rows.</summary>
    public static readonly StyledProperty<double> LabelWidthProperty =
        AvaloniaProperty.Register<PropertyGrid, double>(nameof(LabelWidth), 140, inherits: true,
            coerce: static (_, value) => double.IsNaN(value) ? 140 : Math.Max(MinLabelWidth, value));

    /// <summary>Filters the properties shown (true keeps one); applied to every level.</summary>
    public static readonly StyledProperty<Func<PropertyGridProperty, bool>?> PropertyFilterProperty =
        AvaloniaProperty.Register<PropertyGrid, Func<PropertyGridProperty, bool>?>(nameof(PropertyFilter));

    /// <summary>The category of properties that name none.</summary>
    public static readonly StyledProperty<string> MiscCategoryTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(MiscCategoryText), "Misc");

    /// <summary>What a value shows while the selected objects disagree on it.</summary>
    public static readonly StyledProperty<string> MixedValueTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(MixedValueText), "Varies");

    /// <summary>Text of a list's row; <c>{0}</c> is the item count.</summary>
    public static readonly StyledProperty<string> CollectionSummaryFormatProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(CollectionSummaryFormat), "{0} items");

    /// <summary>Placeholder of the search field.</summary>
    public static readonly StyledProperty<string> SearchPlaceholderProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(SearchPlaceholder), "Search properties");

    /// <summary>Shown when there is nothing to edit, or nothing matches the search.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(EmptyText), "No properties");

    /// <summary>Accessible name of a list's add button.</summary>
    public static readonly StyledProperty<string> AddItemTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(AddItemText), "Add item");

    /// <summary>Accessible name of a list item's remove button.</summary>
    public static readonly StyledProperty<string> RemoveItemTextProperty =
        AvaloniaProperty.Register<PropertyGrid, string>(nameof(RemoveItemText), "Remove item");

    /// <summary>The selected row (two-way): highlighted, and described in the description area.</summary>
    public static readonly DirectProperty<PropertyGrid, PropertyGridRow?> SelectedRowProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, PropertyGridRow?>(nameof(SelectedRow), o => o.SelectedRow,
            (o, v) => o.SelectedRow = v, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The top-level rows (the properties of the selected objects).</summary>
    public static readonly DirectProperty<PropertyGrid, IReadOnlyList<PropertyGridRow>> RowsProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, IReadOnlyList<PropertyGridRow>>(nameof(Rows), o => o.Rows);

    internal static readonly DirectProperty<PropertyGrid, string?> DescriptionTitleProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, string?>(nameof(DescriptionTitle), o => o.DescriptionTitle);

    internal static readonly DirectProperty<PropertyGrid, string?> DescriptionTextProperty =
        AvaloniaProperty.RegisterDirect<PropertyGrid, string?>(nameof(DescriptionText), o => o.DescriptionText);

    /// <summary>Raised after an edit wrote a value (or added or removed a list item).</summary>
    public static readonly RoutedEvent<PropertyGridValueChangedEventArgs> PropertyValueChangedEvent =
        RoutedEvent.Register<PropertyGrid, PropertyGridValueChangedEventArgs>(nameof(PropertyValueChanged), RoutingStrategies.Bubble);

    /// <summary>The object whose properties are edited.</summary>
    public object? SelectedObject { get => GetValue(SelectedObjectProperty); set => SetValue(SelectedObjectProperty, value); }

    /// <summary>Several objects edited together.</summary>
    public IEnumerable? SelectedObjects { get => GetValue(SelectedObjectsProperty); set => SetValue(SelectedObjectsProperty, value); }

    /// <summary>The search text.</summary>
    public string? FilterText { get => GetValue(FilterTextProperty); set => SetValue(FilterTextProperty, value); }

    /// <summary>Whether the search field is shown.</summary>
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }

    /// <summary>Whether the description area is shown.</summary>
    public bool IsDescriptionVisible { get => GetValue(IsDescriptionVisibleProperty); set => SetValue(IsDescriptionVisibleProperty, value); }

    /// <summary>Whether rows are grouped by category.</summary>
    public bool IsCategorized { get => GetValue(IsCategorizedProperty); set => SetValue(IsCategorizedProperty, value); }

    /// <summary>Whether every row is read-only.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>Width of the label column.</summary>
    public double LabelWidth { get => GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>Filters the properties shown.</summary>
    public Func<PropertyGridProperty, bool>? PropertyFilter { get => GetValue(PropertyFilterProperty); set => SetValue(PropertyFilterProperty, value); }

    /// <summary>The category of properties that name none.</summary>
    public string MiscCategoryText { get => GetValue(MiscCategoryTextProperty); set => SetValue(MiscCategoryTextProperty, value); }

    /// <summary>What a mixed value shows.</summary>
    public string MixedValueText { get => GetValue(MixedValueTextProperty); set => SetValue(MixedValueTextProperty, value); }

    /// <summary>Text of a list's row.</summary>
    public string CollectionSummaryFormat { get => GetValue(CollectionSummaryFormatProperty); set => SetValue(CollectionSummaryFormatProperty, value); }

    /// <summary>Placeholder of the search field.</summary>
    public string SearchPlaceholder { get => GetValue(SearchPlaceholderProperty); set => SetValue(SearchPlaceholderProperty, value); }

    /// <summary>Shown when there are no rows.</summary>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>Accessible name of the add button.</summary>
    public string AddItemText { get => GetValue(AddItemTextProperty); set => SetValue(AddItemTextProperty, value); }

    /// <summary>Accessible name of the remove button.</summary>
    public string RemoveItemText { get => GetValue(RemoveItemTextProperty); set => SetValue(RemoveItemTextProperty, value); }

    /// <summary>Custom editors, matched by key, then property name, then property type.</summary>
    public AvaloniaList<PropertyGridEditor> Editors { get; } = [];

    /// <summary>Raised after an edit wrote a value.</summary>
    public event EventHandler<PropertyGridValueChangedEventArgs>? PropertyValueChanged
    {
        add => AddHandler(PropertyValueChangedEvent, value);
        remove => RemoveHandler(PropertyValueChangedEvent, value);
    }

    #endregion

    private readonly AvaloniaList<object> _items = [];
    private readonly HashSet<string> _expandedPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _collapsedCategories = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, PropertyGridTypeInfo> _avaloniaInfos = [];
    private readonly Dictionary<string, PropertyGridCategory> _categoryCache = new(StringComparer.Ordinal);
    private readonly HashSet<object> _observed = new(ReferenceEqualityComparer.Instance);
    private object[] _targets = [];
    private IReadOnlyList<PropertyGridRow> _rows = [];
    private List<PropertyGridCategory> _categories = [];
    private PropertyGridRow? _selectedRow;
    private string? _descriptionTitle;
    private string? _descriptionText;
    private ItemsControl? _host;
    private INotifyCollectionChanged? _observedSelection;
    private bool _reloadPending;
    private bool _rebuildPending;
    private bool _isAttached;

    static PropertyGrid()
    {
        FocusableProperty.OverrideDefaultValue<PropertyGrid>(false);
        SelectedObjectProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Rebuild());
        SelectedObjectsProperty.Changed.AddClassHandler<PropertyGrid>((grid, e) => grid.OnSelectedObjectsChanged(e));
        PropertyFilterProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Rebuild());
        IsReadOnlyProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Reload());
        MixedValueTextProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Reload());
        CollectionSummaryFormatProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Reload());
        MiscCategoryTextProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.Rebuild());
        FilterTextProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.UpdateItems());
        IsCategorizedProperty.Changed.AddClassHandler<PropertyGrid>((grid, _) => grid.UpdateItems());
    }

    /// <summary>Creates an empty grid.</summary>
    public PropertyGrid()
    {
        Editors.CollectionChanged += (_, _) => RefreshEditors();
        AddHandler(KeyDownEvent, OnRowKeyDown, RoutingStrategies.Tunnel);
        UpdatePseudoClasses();
    }

    /// <summary>The selected row.</summary>
    public PropertyGridRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (value is not null && !ReferenceEquals(value.Grid, this))
            {
                throw new ArgumentException("The row belongs to another grid.", nameof(value));
            }
            var old = _selectedRow;
            if (SetAndRaise(SelectedRowProperty, ref _selectedRow, value))
            {
                DescriptionTitle = value?.DisplayName;
                DescriptionText = value?.Description;
                foreach (var container in Containers())
                {
                    if (container is PropertyGridItem item && (ReferenceEquals(item.Row, old) || ReferenceEquals(item.Row, value)))
                    {
                        item.UpdateSelection();
                    }
                }
            }
        }
    }

    /// <summary>The top-level rows.</summary>
    public IReadOnlyList<PropertyGridRow> Rows
    {
        get => _rows;
        private set => SetAndRaise(RowsProperty, ref _rows, value);
    }

    /// <summary>The category headers of the current rows.</summary>
    public IReadOnlyList<PropertyGridCategory> Categories => _categories;

    internal string? DescriptionTitle
    {
        get => _descriptionTitle;
        private set => SetAndRaise(DescriptionTitleProperty, ref _descriptionTitle, value);
    }

    internal string? DescriptionText
    {
        get => _descriptionText;
        private set => SetAndRaise(DescriptionTextProperty, ref _descriptionText, value);
    }

    /// <summary>The rows and headers shown, in order (what the list displays).</summary>
    internal IReadOnlyList<object> VisibleItems => _items;

    internal bool IsEnabledForEdits => !IsReadOnly;

    #region Public methods

    /// <summary>Reads every value again (for objects that do not raise change notifications).</summary>
    public void Refresh() => Reload();

    /// <summary>Expands every expandable row, one level at a time down to <paramref name="depth"/> levels.</summary>
    public void ExpandAll(int depth = 1)
    {
        for (var level = 0; level < depth; level++)
        {
            var changed = false;
            foreach (var row in AllRows())
            {
                if (row.Depth == level && row.IsExpandable && !row.IsExpanded)
                {
                    _expandedPaths.Add(row.Path);
                    changed = true;
                }
            }
            if (!changed)
            {
                break;
            }
            ReloadRows();
        }
        foreach (var category in _categories)
        {
            _collapsedCategories.Remove(category.Name);
        }
        Rebuild();
    }

    /// <summary>Collapses every row and category.</summary>
    public void CollapseAll()
    {
        _expandedPaths.Clear();
        foreach (var category in _categories)
        {
            _collapsedCategories.Add(category.Name);
        }
        Rebuild();
    }

    /// <summary>The row at <paramref name="path"/> (such as <c>Start.X</c> or <c>Points[1]</c>), if it exists now.</summary>
    public PropertyGridRow? FindRow(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var row in AllRows())
        {
            if (string.Equals(row.Path, path, StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    #endregion

    #region Rows

    internal bool IsPathExpanded(string path) => _expandedPaths.Contains(path);

    internal bool IsCategoryCollapsed(string name) => _collapsedCategories.Contains(name);

    /// <summary>The description of a value a row expands into; null when it does not expand.</summary>
    internal PropertyGridTypeInfo? Describe(object value, PropertyGridExpand expand)
    {
        if (PropertyGridTypes.Find(value.GetType()) is { } info)
        {
            return info;
        }
        return expand == PropertyGridExpand.Always && value is AvaloniaObject avaloniaObject ? DescribeAvalonia(avaloniaObject) : null;
    }

    private PropertyGridTypeInfo? DescribeTarget(object target)
        => PropertyGridTypes.Find(target.GetType()) ?? (target is AvaloniaObject avaloniaObject ? DescribeAvalonia(avaloniaObject) : null);

    private PropertyGridTypeInfo DescribeAvalonia(AvaloniaObject target)
    {
        var type = target.GetType();
        if (!_avaloniaInfos.TryGetValue(type, out var info))
        {
            info = PropertyGridTypes.DescribeAvaloniaObject(target);
            _avaloniaInfos[type] = info;
        }
        return info;
    }

    /// <summary>The rows of the properties every owner shares (by name and type), in the first owner's order.</summary>
    internal List<PropertyGridRow> BuildRows(PropertyGridRow? parent, object[] owners)
    {
        var rows = new List<PropertyGridRow>();
        if (owners.Length == 0)
        {
            return rows;
        }
        var infos = new PropertyGridTypeInfo[owners.Length];
        for (var i = 0; i < owners.Length; i++)
        {
            var info = parent is null ? DescribeTarget(owners[i]) : Describe(owners[i], parent.Property.Expand);
            if (info is null)
            {
                return rows;
            }
            infos[i] = info;
        }
        var filter = PropertyFilter;
        var lookups = new Dictionary<string, PropertyGridProperty>[owners.Length];
        for (var i = 1; i < owners.Length; i++)
        {
            var lookup = new Dictionary<string, PropertyGridProperty>(StringComparer.Ordinal);
            foreach (var property in infos[i].Properties)
            {
                lookup.TryAdd(property.Name, property);
            }
            lookups[i] = lookup;
        }
        var ordered = new List<(PropertyGridProperty[] Properties, int Position)>();
        var position = 0;
        foreach (var property in infos[0].Properties)
        {
            if (filter is not null && !filter(property))
            {
                continue;
            }
            var set = new PropertyGridProperty[owners.Length];
            set[0] = property;
            var shared = true;
            for (var i = 1; i < owners.Length && shared; i++)
            {
                shared = lookups[i].TryGetValue(property.Name, out var other) && other.PropertyType == property.PropertyType;
                if (shared)
                {
                    set[i] = other!;
                }
            }
            if (shared)
            {
                ordered.Add((set, position++));
            }
        }
        ordered.Sort(static (a, b) =>
        {
            var byOrder = a.Properties[0].Order.CompareTo(b.Properties[0].Order);
            return byOrder != 0 ? byOrder : a.Position.CompareTo(b.Position);
        });
        foreach (var (properties, _) in ordered)
        {
            rows.Add(new PropertyGridRow(this, parent, properties, (object[])owners.Clone(), -1));
        }
        return rows;
    }

    /// <summary>The objects being edited.</summary>
    internal IReadOnlyList<object> Targets => _targets;

    /// <summary>A struct selected object was changed: keep the changed copy.</summary>
    internal void ReplaceTarget(int index, object value) => _targets[index] = value;

    private void OnSelectedObjectsChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectionCollectionChanged;
            _observedSelection = null;
        }
        if (e.NewValue is INotifyCollectionChanged notify && _isAttached)
        {
            _observedSelection = notify;
            notify.CollectionChanged += OnSelectionCollectionChanged;
        }
        Rebuild();
    }

    private void OnSelectionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private object[] CollectTargets()
    {
        if (SelectedObjects is { } many)
        {
            var list = new List<object>();
            foreach (var item in many)
            {
                if (item is not null)
                {
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                return [.. list];
            }
        }
        return SelectedObject is { } single ? [single] : [];
    }

    /// <summary>Builds the rows again from the selected objects (keeping expanded rows and the selection by path).</summary>
    private void Rebuild()
    {
        _rebuildPending = false;
        var selectedPath = _selectedRow?.Path;
        _targets = CollectTargets();
        var rows = BuildRows(null, _targets);
        foreach (var row in rows)
        {
            row.Reload();
        }
        Rows = rows;
        UpdateItems();
        SelectedRow = selectedPath is null ? null : FindRow(selectedPath);
        Observe();
    }

    /// <summary>Reads every row again; rebuilds the visible list when rows appeared or went away.</summary>
    private void Reload()
    {
        _reloadPending = false;
        if (ReloadRows())
        {
            var selectedPath = _selectedRow?.Path;
            UpdateItems();
            if (selectedPath is not null && (_selectedRow is null || !_items.Contains(_selectedRow)))
            {
                SelectedRow = FindRow(selectedPath);
            }
        }
        Observe();
    }

    private bool ReloadRows()
    {
        var structural = false;
        foreach (var row in _rows)
        {
            structural |= row.Reload();
        }
        return structural;
    }

    private void ScheduleReload()
    {
        if (_reloadPending)
        {
            return;
        }
        _reloadPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_reloadPending)
            {
                Reload();
            }
        }, DispatcherPriority.Background);
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }
        _rebuildPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_rebuildPending)
            {
                Rebuild();
            }
        }, DispatcherPriority.Background);
    }

    internal void OnRowExpandedChanged(PropertyGridRow row)
    {
        if (row.IsExpanded)
        {
            _expandedPaths.Add(row.Path);
        }
        else
        {
            _expandedPaths.Remove(row.Path);
        }
        row.Reload();
        UpdateItems();
        Observe();
    }

    internal void OnCategoryExpandedChanged(PropertyGridCategory category)
    {
        if (category.IsExpanded)
        {
            _collapsedCategories.Remove(category.Name);
        }
        else
        {
            _collapsedCategories.Add(category.Name);
        }
        UpdateItems();
    }

    /// <summary>Writes a value typed or picked in an editor into every owner of the row.</summary>
    internal void Write(PropertyGridRow row, object? value)
    {
        if (row.IsReadOnly)
        {
            return;
        }
        if (!PropertyGridValues.TryConvert(value, row.PropertyType, out var converted))
        {
            row.ErrorMessage = string.Format(CultureInfo.CurrentCulture, "'{0}' is not a valid {1}.",
                PropertyGridValues.Format(value, null), PropertyGridValues.Underlying(row.PropertyType).Name);
            return;
        }
        var oldValues = row.Values.ToArray();
        try
        {
            for (var i = 0; i < row.OwnersArray.Length; i++)
            {
                row.WriteAt(i, converted);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            row.ErrorMessage = exception.Message;
            Reload();
            return;
        }
        row.ErrorMessage = null;
        Reload();
        RaiseEvent(new PropertyGridValueChangedEventArgs(PropertyValueChangedEvent, row, oldValues, converted));
    }

    internal void AddItem(PropertyGridRow row)
    {
        if (!row.CanAddItem || row.Property.CreateElement is not { } create)
        {
            return;
        }
        var oldValues = row.Values.ToArray();
        try
        {
            foreach (var value in row.Values)
            {
                ((IList)value!).Add(create());
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            row.ErrorMessage = exception.Message;
            return;
        }
        row.ErrorMessage = null;
        _expandedPaths.Add(row.Path);
        if (!row.IsExpanded)
        {
            row.IsExpanded = true;
        }
        Reload();
        RaiseEvent(new PropertyGridValueChangedEventArgs(PropertyValueChangedEvent, row, oldValues, row.Value));
    }

    internal void RemoveItem(PropertyGridRow row)
    {
        if (!row.CanRemove || row.Parent is not { } parent)
        {
            return;
        }
        var oldValues = parent.Values.ToArray();
        var wasSelected = ReferenceEquals(_selectedRow, row);
        try
        {
            foreach (var owner in row.OwnersArray)
            {
                var list = (IList)owner;
                if (row.Index < list.Count)
                {
                    list.RemoveAt(row.Index);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            parent.ErrorMessage = exception.Message;
            return;
        }
        // Item rows are keyed by index: forget expanded items at and after the removed one.
        _expandedPaths.RemoveWhere(path => path.StartsWith(parent.Path + "[", StringComparison.Ordinal));
        Reload();
        if (wasSelected)
        {
            SelectedRow = parent;
        }
        RaiseEvent(new PropertyGridValueChangedEventArgs(PropertyValueChangedEvent, parent, oldValues, parent.Value));
    }

    private IEnumerable<PropertyGridRow> AllRows()
    {
        var stack = new Stack<PropertyGridRow>();
        for (var i = _rows.Count - 1; i >= 0; i--)
        {
            stack.Push(_rows[i]);
        }
        while (stack.Count > 0)
        {
            var row = stack.Pop();
            yield return row;
            if (row.ChildRows is { } children)
            {
                for (var i = children.Count - 1; i >= 0; i--)
                {
                    stack.Push(children[i]);
                }
            }
        }
    }

    #endregion

    #region Visible list

    /// <summary>Lays the rows out as the list shows them: category headers, rows, and expanded rows under their parent,
    /// filtered by the search text.</summary>
    private void UpdateItems()
    {
        var filter = FilterText?.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);
        var items = new List<object>();
        var categories = new List<PropertyGridCategory>();
        if (IsCategorized)
        {
            var groups = new List<(string Name, List<PropertyGridRow> Rows)>();
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in _rows)
            {
                if (!index.TryGetValue(row.Category, out var position))
                {
                    position = groups.Count;
                    index.Add(row.Category, position);
                    groups.Add((row.Category, []));
                }
                groups[position].Rows.Add(row);
            }
            foreach (var (name, rows) in groups)
            {
                if (!_categoryCache.TryGetValue(name, out var category))
                {
                    category = new PropertyGridCategory(this, name);
                    _categoryCache.Add(name, category);
                }
                category.Rows = rows;
                categories.Add(category);
                var start = items.Count;
                items.Add(category);
                var shown = false;
                foreach (var row in rows)
                {
                    shown |= AddRow(items, row, filter, hasFilter);
                }
                if (!shown)
                {
                    items.RemoveRange(start, items.Count - start);
                }
                else if (!category.IsExpanded && !hasFilter)
                {
                    items.RemoveRange(start + 1, items.Count - start - 1);
                }
            }
        }
        else
        {
            var sorted = _rows.ToList();
            sorted.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            foreach (var row in sorted)
            {
                AddRow(items, row, filter, hasFilter);
            }
        }
        _categories = categories;
        ApplyItems(items);
        if (_selectedRow is not null && !items.Contains(_selectedRow) && !hasFilter)
        {
            SelectedRow = null;
        }
        UpdatePseudoClasses();
    }

    /// <summary>Adds a row and its expanded rows; with a filter, only rows that match or lead to a match. Returns whether
    /// anything was added.</summary>
    private static bool AddRow(List<object> items, PropertyGridRow row, string? filter, bool hasFilter)
    {
        var matches = !hasFilter || Matches(row, filter!);
        var start = items.Count;
        items.Add(row);
        var childShown = false;
        if (row.IsExpanded && row.ChildRows is { } children)
        {
            foreach (var child in children)
            {
                // Under a matching row every expanded row shows; otherwise only the ones that match.
                childShown |= AddRow(items, child, matches ? null : filter, hasFilter && !matches);
            }
        }
        if (!matches && !childShown)
        {
            items.RemoveRange(start, items.Count - start);
            return false;
        }
        return true;
    }

    private static bool Matches(PropertyGridRow row, string filter)
        => row.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
           || row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Replaces only the changed middle of the list, so the rows around an expanded or collapsed row keep their
    /// containers (and keyboard focus).</summary>
    private void ApplyItems(List<object> items)
    {
        var prefix = 0;
        var max = Math.Min(_items.Count, items.Count);
        while (prefix < max && ReferenceEquals(_items[prefix], items[prefix]))
        {
            prefix++;
        }
        var suffix = 0;
        while (suffix < max - prefix && ReferenceEquals(_items[_items.Count - 1 - suffix], items[items.Count - 1 - suffix]))
        {
            suffix++;
        }
        var removed = _items.Count - prefix - suffix;
        if (removed > 0)
        {
            _items.RemoveRange(prefix, removed);
        }
        var added = items.Count - prefix - suffix;
        if (added > 0)
        {
            _items.InsertRange(prefix, items.GetRange(prefix, added));
        }
    }

    private void UpdatePseudoClasses() => PseudoClasses.Set(EmptyPseudoClass, _items.Count == 0);

    #endregion

    #region Editors

    private void RefreshEditors()
    {
        foreach (var container in Containers())
        {
            if (container is PropertyGridItem item)
            {
                item.RefreshEditor();
            }
        }
    }

    /// <summary>The editor of a row: a matching <see cref="PropertyGridEditor"/>, else the built-in one for its type.</summary>
    internal IDataTemplate ResolveEditor(PropertyGridRow row)
    {
        foreach (var editor in Editors)
        {
            if (editor.Template is not null && editor.MatchesKey(row))
            {
                return editor.Template;
            }
        }
        foreach (var editor in Editors)
        {
            if (editor.Template is not null && editor.MatchesName(row))
            {
                return editor.Template;
            }
        }
        foreach (var editor in Editors)
        {
            if (editor.Template is not null && editor.MatchesTypeOnly(row))
            {
                return editor.Template;
            }
        }
        if (row.IsReadOnly || row.IsCollection)
        {
            return PropertyGridEditors.Text;
        }
        return PropertyGridEditors.For(PropertyGridValues.KindOf(row.Property, row.PropertyType));
    }

    #endregion

    #region Change notifications

    /// <summary>Listens to the selected objects, the values of expanded rows and lists, and nothing else.</summary>
    private void Observe()
    {
        var wanted = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (_isAttached)
        {
            foreach (var target in _targets)
            {
                wanted.Add(target);
            }
            foreach (var row in AllRows())
            {
                if (row.IsExpanded || row.IsCollection)
                {
                    foreach (var value in row.Values)
                    {
                        if (value is INotifyPropertyChanged or INotifyCollectionChanged && !value.GetType().IsValueType)
                        {
                            wanted.Add(value);
                        }
                    }
                }
            }
        }
        foreach (var old in _observed)
        {
            if (!wanted.Contains(old))
            {
                Unsubscribe(old);
            }
        }
        foreach (var item in wanted)
        {
            if (!_observed.Contains(item))
            {
                Subscribe(item);
            }
        }
        _observed.Clear();
        _observed.UnionWith(wanted);
    }

    private void Subscribe(object item)
    {
        if (item is INotifyPropertyChanged notify)
        {
            notify.PropertyChanged += OnObservedPropertyChanged;
        }
        if (item is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnObservedCollectionChanged;
        }
    }

    private void Unsubscribe(object item)
    {
        if (item is INotifyPropertyChanged notify)
        {
            notify.PropertyChanged -= OnObservedPropertyChanged;
        }
        if (item is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnObservedCollectionChanged;
        }
    }

    private void OnObservedPropertyChanged(object? sender, PropertyChangedEventArgs e) => ScheduleReload();

    private void OnObservedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleReload();

    private void OnTypeRegistered(object? sender, Type type)
    {
        _avaloniaInfos.Clear();
        ScheduleRebuild();
    }

    #endregion

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_host is not null)
        {
            _host.ItemsSource = null;
        }
        _host = e.NameScope.Find<ItemsControl>(ItemsPart);
        if (_host is not null)
        {
            _host.ItemsSource = _items;
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _isAttached = true;
        PropertyGridTypes.Registered += OnTypeRegistered;
        if (SelectedObjects is INotifyCollectionChanged notify && _observedSelection is null)
        {
            _observedSelection = notify;
            notify.CollectionChanged += OnSelectionCollectionChanged;
        }
        Rebuild();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        _isAttached = false;
        PropertyGridTypes.Registered -= OnTypeRegistered;
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectionCollectionChanged;
            _observedSelection = null;
        }
        Observe();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && Bounds.Width > 0)
        {
            var max = Math.Max(MinLabelWidth, Bounds.Width - MinEditorWidth);
            if (LabelWidth > max)
            {
                SetCurrentValue(LabelWidthProperty, max);
            }
        }
    }

    /// <summary>Moves the label column edge, kept between the minimum label and editor widths.</summary>
    internal void ResizeLabel(double width)
    {
        var max = Bounds.Width > 0 ? Math.Max(MinLabelWidth, Bounds.Width - MinEditorWidth) : double.PositiveInfinity;
        SetCurrentValue(LabelWidthProperty, Math.Clamp(width, MinLabelWidth, max));
    }

    private IEnumerable<Control> Containers()
    {
        if (_host is null)
        {
            yield break;
        }
        foreach (var container in _host.GetRealizedContainers())
        {
            yield return container;
        }
    }

    #endregion

    #region Keyboard

    /// <summary>Row keys, handled on the way down: the scroll viewer would take the arrow keys on the way up. Only a focused
    /// row or header is the source here; keys typed in an editor are left to the editor.</summary>
    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var source = e.Source as Control;
        var index = source switch
        {
            PropertyGridItem { Row: { } row } => _items.IndexOf(row),
            PropertyGridCategoryItem { Category: { } category } => _items.IndexOf(category),
            _ => -1
        };
        if (index < 0)
        {
            return;
        }
        var item = _items[index];
        switch (e.Key)
        {
            case Key.Down:
                e.Handled = FocusItem(Math.Min(_items.Count - 1, index + 1));
                break;
            case Key.Up:
                e.Handled = FocusItem(Math.Max(0, index - 1));
                break;
            case Key.Home:
                e.Handled = FocusItem(0);
                break;
            case Key.End:
                e.Handled = FocusItem(_items.Count - 1);
                break;
            case Key.Right:
                if (item is PropertyGridCategory { IsExpanded: false } closed)
                {
                    closed.IsExpanded = true;
                }
                else if (item is PropertyGridRow { IsExpandable: true } row)
                {
                    if (!row.IsExpanded)
                    {
                        row.IsExpanded = true;
                    }
                    else if (row.ChildRows is { Count: > 0 })
                    {
                        FocusItem(index + 1);
                    }
                }
                e.Handled = true;
                break;
            case Key.Left:
                if (item is PropertyGridCategory { IsExpanded: true } open)
                {
                    open.IsExpanded = false;
                }
                else if (item is PropertyGridRow { IsExpanded: true } expanded)
                {
                    expanded.IsExpanded = false;
                }
                else if (item is PropertyGridRow { Parent: { } parent })
                {
                    FocusItem(_items.IndexOf(parent));
                }
                else if (item is PropertyGridRow top)
                {
                    var header = _categories.Find(c => c.Name == top.Category);
                    if (header is not null)
                    {
                        FocusItem(_items.IndexOf(header));
                    }
                }
                e.Handled = true;
                break;
            case Key.Enter or Key.F2 when source is PropertyGridItem rowItem:
                e.Handled = rowItem.FocusEditor();
                break;
            case Key.Enter or Key.Space when item is PropertyGridCategory category:
                category.IsExpanded = !category.IsExpanded;
                e.Handled = true;
                break;
        }
    }

    /// <summary>Scrolls an item into view and gives it keyboard focus.</summary>
    internal bool FocusItem(int index)
    {
        if (_host is null || index < 0 || index >= _items.Count)
        {
            return false;
        }
        _host.ScrollIntoView(index);
        _host.UpdateLayout();
        if (_items[index] is PropertyGridRow row)
        {
            SelectedRow = row;
        }
        return _host.ContainerFromIndex(index) is { } container && container.Focus(NavigationMethod.Directional);
    }

    #endregion
}
