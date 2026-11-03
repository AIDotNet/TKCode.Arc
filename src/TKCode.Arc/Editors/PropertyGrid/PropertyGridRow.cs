using System.Collections;
using System.ComponentModel;
using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="PropertyGrid"/>: a property of the selected objects, a property of an expanded value, or an
/// item of an expanded list. It is the data context of the row's editor, so a custom <see cref="PropertyGridEditor"/>
/// template binds to <see cref="Value"/>, <see cref="IsReadOnly"/> and <see cref="IsMixed"/>.
/// </summary>
public sealed class PropertyGridRow : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs s_value = new(nameof(Value));
    private static readonly PropertyChangedEventArgs s_mixed = new(nameof(IsMixed));
    private static readonly PropertyChangedEventArgs s_readOnly = new(nameof(IsReadOnly));
    private static readonly PropertyChangedEventArgs s_text = new(nameof(DisplayText));
    private static readonly PropertyChangedEventArgs s_error = new(nameof(ErrorMessage));
    private static readonly PropertyChangedEventArgs s_expandable = new(nameof(IsExpandable));
    private static readonly PropertyChangedEventArgs s_expanded = new(nameof(IsExpanded));
    private static readonly PropertyChangedEventArgs s_count = new(nameof(ItemCount));
    private static readonly PropertyChangedEventArgs s_canAdd = new(nameof(CanAddItem));
    private static readonly PropertyChangedEventArgs s_canRemove = new(nameof(CanRemove));

    private readonly PropertyGridProperty[] _properties;
    private object[] _owners;
    private object?[] _values;
    private bool _isMixed;
    private bool _isReadOnly;
    private string _displayText = string.Empty;
    private string? _errorMessage;
    private bool _isExpandable;
    private bool _isExpanded;
    private int _itemCount = -1;
    private bool _canAddItem;
    private bool _canRemove;
    private List<PropertyGridRow>? _children;
    private object[]? _childShape;

    internal PropertyGridRow(PropertyGrid grid, PropertyGridRow? parent, PropertyGridProperty[] properties, object[] owners, int index)
    {
        Grid = grid;
        Parent = parent;
        _properties = properties;
        _owners = owners;
        _values = new object?[owners.Length];
        Index = index;
        Depth = parent is null ? 0 : parent.Depth + 1;
        Path = parent is null ? properties[0].Name
            : index >= 0 ? parent.Path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"
            : parent.Path + "." + properties[0].Name;
        _isExpanded = grid.IsPathExpanded(Path);
    }

    /// <summary>Raised when a value or state of the row changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The grid that shows the row.</summary>
    public PropertyGrid Grid { get; }

    /// <summary>The row this one expands from; null at the top level.</summary>
    public PropertyGridRow? Parent { get; }

    /// <summary>The property shown (for several selected objects, the one of the first).</summary>
    public PropertyGridProperty Property => _properties[0];

    /// <summary>The property of each owner, in the order of <see cref="Owners"/>.</summary>
    public IReadOnlyList<PropertyGridProperty> Properties => _properties;

    /// <summary>The property name.</summary>
    public string Name => Property.Name;

    /// <summary>The label: the property's display name, or <c>[index]</c> for a list item.</summary>
    public string DisplayName => Index >= 0 ? "[" + Index.ToString(CultureInfo.CurrentCulture) + "]" : Property.DisplayName;

    /// <summary>The property's description.</summary>
    public string? Description => Property.Description;

    /// <summary>The category of the top-level row this one belongs to.</summary>
    public string Category => Parent?.Category ?? Property.Category ?? Grid.MiscCategoryText;

    /// <summary>The declared type of the property.</summary>
    public Type PropertyType => Property.PropertyType;

    /// <summary>Nesting level: 0 for the selected objects' own properties.</summary>
    public int Depth { get; }

    /// <summary>The position in the list for a list item; -1 otherwise.</summary>
    public int Index { get; }

    /// <summary>Where the row sits, such as <c>Start.X</c> or <c>Points[2]</c>; expanded rows are remembered by it.</summary>
    public string Path { get; }

    /// <summary>The objects the property is read from: the selected objects at the top level, the parent's values below.</summary>
    public IReadOnlyList<object> Owners => _owners;

    /// <summary>The value of each owner.</summary>
    public IReadOnlyList<object?> Values => _values;

    /// <summary>The value; null while the owners disagree (<see cref="IsMixed"/>). Setting it writes every owner.</summary>
    public object? Value
    {
        get => _isMixed ? null : _values.Length > 0 ? _values[0] : null;
        set => Grid.Write(this, value);
    }

    /// <summary>Whether the selected objects hold different values.</summary>
    public bool IsMixed => _isMixed;

    /// <summary>Whether the value cannot be written.</summary>
    public bool IsReadOnly => _isReadOnly;

    /// <summary>The value as text (the mixed text, a list's item count or a value's summary where it applies).</summary>
    public string DisplayText => _displayText;

    /// <summary>Why the last write failed (a conversion error or the setter's exception); null after a good write.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        internal set => Set(ref _errorMessage, value, s_error);
    }

    /// <summary>Whether the value has rows of its own (a list, or a type the grid can describe).</summary>
    public bool IsExpandable => _isExpandable;

    /// <summary>Whether the value's rows are shown. Remembered by <see cref="Path"/> while the grid lives.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }
            _isExpanded = value;
            Grid.OnRowExpandedChanged(this);
            PropertyChanged?.Invoke(this, s_expanded);
        }
    }

    /// <summary>The value's rows; empty until the row is expanded.</summary>
    public IReadOnlyList<PropertyGridRow> Children => (IReadOnlyList<PropertyGridRow>?)_children ?? [];

    /// <summary>Whether the value is a list.</summary>
    public bool IsCollection => _itemCount >= 0;

    /// <summary>The number of items of a list value; -1 when the value is not a list.</summary>
    public int ItemCount => _itemCount;

    /// <summary>Whether <see cref="AddItem"/> can append to the list.</summary>
    public bool CanAddItem => _canAddItem;

    /// <summary>Whether <see cref="Remove"/> can take this item out of its list.</summary>
    public bool CanRemove => _canRemove;

    /// <summary>Appends a new item (from <see cref="PropertyGridProperty.CreateElement"/>) to the list of every owner.</summary>
    public void AddItem() => Grid.AddItem(this);

    /// <summary>Removes this item from its list.</summary>
    public void Remove() => Grid.RemoveItem(this);

    /// <inheritdoc />
    public override string ToString() => Path;

    internal object[] OwnersArray => _owners;

    internal List<PropertyGridRow>? ChildRows => _children;

    /// <summary>Writes one owner's value. When the setter returns another object (the changed copy of a struct, or a new
    /// instance of an immutable type) that object is written back to its own owner, up to the selected object.</summary>
    internal void WriteAt(int index, object? value)
    {
        var owner = _owners[index];
        var setter = _properties[index].Setter ?? throw new InvalidOperationException($"'{Path}' is read-only.");
        var updated = setter(owner, value);
        if (!ReferenceEquals(updated, owner))
        {
            _owners[index] = updated;
            if (Parent is { } parent)
            {
                parent.WriteAt(index, updated);
            }
            else
            {
                Grid.ReplaceTarget(index, updated);
            }
        }
    }

    /// <summary>Points the row at new owners (the parent's values changed); the property list is unchanged.</summary>
    internal void SetOwners(object[] owners) => _owners = owners;

    /// <summary>Reads the values again and updates the children; returns whether rows were added or removed.</summary>
    internal bool Reload()
    {
        var count = _owners.Length;
        if (_values.Length != count)
        {
            _values = new object?[count];
        }
        string? readError = null;
        for (var i = 0; i < count; i++)
        {
            try
            {
                _values[i] = _properties[i].Getter(_owners[i]);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _values[i] = null;
                readError = exception.Message;
            }
        }
        var mixed = false;
        for (var i = 1; i < count && !mixed; i++)
        {
            mixed = !PropertyGridValues.Same(_values[0], _values[i]);
        }

        var lists = ListsOf(_values);
        var itemCount = lists is null ? -1 : lists[0].Count;
        if (lists is not null)
        {
            foreach (var list in lists)
            {
                if (list.Count != itemCount)
                {
                    itemCount = -1;
                    break;
                }
            }
        }
        var readOnly = ComputeReadOnly();
        var canAdd = lists is not null && itemCount >= 0 && Property.CreateElement is not null && Grid.IsEnabledForEdits
                     && lists.TrueForAll(static list => !list.IsFixedSize && !list.IsReadOnly);
        var canRemove = Index >= 0 && Parent is not null && Grid.IsEnabledForEdits
                        && _owners.All(static owner => owner is IList { IsFixedSize: false, IsReadOnly: false });

        var shape = ChildShape(lists, itemCount);
        var expandable = shape is not null;
        var oldValue = Value;
        var oldMixed = _isMixed;
        _isMixed = mixed;
        var changedValue = oldMixed != mixed || !PropertyGridValues.Same(oldValue, Value);

        Set(ref _isReadOnly, readOnly, s_readOnly);
        Set(ref _itemCount, itemCount, s_count);
        Set(ref _canAddItem, canAdd, s_canAdd);
        Set(ref _canRemove, canRemove, s_canRemove);
        Set(ref _isExpandable, expandable, s_expandable);
        Set(ref _displayText, TextOf(lists, itemCount), s_text);
        if (readError is not null)
        {
            ErrorMessage = readError;
        }
        if (oldMixed != mixed)
        {
            PropertyChanged?.Invoke(this, s_mixed);
        }
        if (changedValue)
        {
            PropertyChanged?.Invoke(this, s_value);
        }

        var structural = false;
        if (!_isExpanded || shape is null)
        {
            if (_children is not null)
            {
                _children = null;
                _childShape = null;
                structural = true;
            }
            return structural;
        }
        // Children read from this row's values: the values themselves, or for a list the lists (an item row indexes them).
        var childOwners = new object[count];
        for (var i = 0; i < count; i++)
        {
            childOwners[i] = _values[i]!;
        }
        if (_children is null || _childShape is null || !SameShape(_childShape, shape))
        {
            _children = lists is not null ? BuildItems(lists, itemCount) : Grid.BuildRows(this, childOwners);
            _childShape = shape;
            foreach (var child in _children)
            {
                child.Reload();
            }
            return true;
        }
        foreach (var child in _children)
        {
            child.SetOwners((object[])childOwners.Clone());
            structural |= child.Reload();
        }
        return structural;
    }

    private bool ComputeReadOnly()
    {
        if (!Grid.IsEnabledForEdits)
        {
            return true;
        }
        foreach (var property in _properties)
        {
            if (property.IsReadOnly)
            {
                return true;
            }
        }
        // A struct cannot be changed in place: its rows are read-only when it cannot be written back to its owner.
        for (var row = Parent; row is not null && row.Index < 0; row = row.Parent)
        {
            var isStruct = row._values.Length > 0 && row._values[0] is { } value && value.GetType().IsValueType;
            if (!isStruct)
            {
                break;
            }
            if (row.IsReadOnly)
            {
                return true;
            }
        }
        return false;
    }

    private static List<IList>? ListsOf(object?[] values)
    {
        if (values.Length == 0)
        {
            return null;
        }
        var lists = new List<IList>(values.Length);
        foreach (var value in values)
        {
            if (value is not IList list || value is string)
            {
                return null;
            }
            lists.Add(list);
        }
        return lists;
    }

    /// <summary>What the children are made of: the list size, or the description of each owner's type. Null when the value
    /// does not expand.</summary>
    private object[]? ChildShape(List<IList>? lists, int itemCount)
    {
        if (Property.Expand == PropertyGridExpand.Never || _values.Length == 0)
        {
            return null;
        }
        if (lists is not null)
        {
            return itemCount > 0 ? [itemCount] : null;
        }
        var shape = new object[_values.Length];
        for (var i = 0; i < _values.Length; i++)
        {
            if (_values[i] is not { } value || value is string || Grid.Describe(value, Property.Expand) is not { } info)
            {
                return null;
            }
            shape[i] = info;
        }
        return shape;
    }

    private static bool SameShape(object[] a, object[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!Equals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    private List<PropertyGridRow> BuildItems(List<IList> lists, int itemCount)
    {
        var rows = new List<PropertyGridRow>(itemCount);
        var owners = lists.ConvertAll(static list => (object)list).ToArray();
        for (var index = 0; index < itemCount; index++)
        {
            var item = index;
            var properties = new PropertyGridProperty[lists.Count];
            for (var i = 0; i < lists.Count; i++)
            {
                var list = lists[i];
                var elementType = Property.ElementType ?? (list.Count > item ? list[item]?.GetType() : null) ?? typeof(object);
                properties[i] = new PropertyGridProperty("[" + item.ToString(CultureInfo.InvariantCulture) + "]", elementType,
                    owner => ((IList)owner).Count > item ? ((IList)owner)[item] : null,
                    list.IsReadOnly ? null : (owner, value) =>
                    {
                        ((IList)owner)[item] = value;
                        return owner;
                    })
                {
                    Category = Property.Category,
                    Choices = PropertyGridValues.EnumChoices(elementType),
                    FractionDigits = Property.FractionDigits,
                    Minimum = Property.Minimum,
                    Maximum = Property.Maximum,
                    Step = Property.Step,
                    EditorKey = Property.EditorKey
                };
            }
            rows.Add(new PropertyGridRow(Grid, this, properties, (object[])owners.Clone(), item));
        }
        return rows;
    }

    private string TextOf(List<IList>? lists, int itemCount)
    {
        if (_isMixed)
        {
            return Grid.MixedValueText;
        }
        if (lists is not null)
        {
            return itemCount < 0
                ? Grid.MixedValueText
                : string.Format(CultureInfo.CurrentCulture, Grid.CollectionSummaryFormat, itemCount);
        }
        var value = Value;
        if (value is not null && value is not string && PropertyGridTypes.Find(value.GetType()) is { Summary: { } summary })
        {
            return summary(value) ?? string.Empty;
        }
        if (value is not null && Property.Choices is { } choices)
        {
            foreach (var choice in choices)
            {
                if (PropertyGridValues.Same(choice.Value, value))
                {
                    return choice.Text;
                }
            }
        }
        return PropertyGridValues.Format(value, Property);
    }

    private void Set<T>(ref T field, T value, PropertyChangedEventArgs args)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, args);
    }
}

/// <summary>A category of a <see cref="PropertyGrid"/>, shown as a header that collapses its rows.</summary>
public sealed class PropertyGridCategory : INotifyPropertyChanged
{
    private bool _isExpanded;

    internal PropertyGridCategory(PropertyGrid grid, string name)
    {
        Grid = grid;
        Name = name;
        _isExpanded = !grid.IsCategoryCollapsed(name);
    }

    /// <summary>Raised when <see cref="IsExpanded"/> changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The grid.</summary>
    public PropertyGrid Grid { get; }

    /// <summary>The category name.</summary>
    public string Name { get; }

    /// <summary>The top-level rows of the category.</summary>
    public IReadOnlyList<PropertyGridRow> Rows { get; internal set; } = [];

    /// <summary>Whether the rows are shown. Remembered by name while the grid lives.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }
            _isExpanded = value;
            Grid.OnCategoryExpandedChanged(this);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
