using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>Evaluates a column binding against row items and writes edits back through it (two-way).</summary>
internal sealed class DataGridBindingProbe : StyledElement
{
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<DataGridBindingProbe, object?>("Value", defaultBindingMode: BindingMode.TwoWay);

    public DataGridBindingProbe(BindingBase binding)
    {
        Bind(ValueProperty, binding);
    }

    public object? Read(object item)
    {
        DataContext = item;
        return GetValue(ValueProperty);
    }

    public void Write(object item, object? value)
    {
        DataContext = item;
        SetCurrentValue(ValueProperty, value);
    }

    public void Release() => DataContext = null;
}

/// <summary>Reads and writes one column of row items: a getter and setter, a binding, a <see cref="DataGridRow"/> field or a dictionary entry.</summary>
internal sealed class DataGridColumnAccessor
{
    private readonly DataGridBindingProbe? _probe;

    public DataGridColumnAccessor(DataGridColumn column)
    {
        Column = column;
        Key = column.Key;
        Type = column.Type;
        DefinitionVersion = column.DefinitionVersion;
        if (column.ValueGetter is null && column.Binding is { } binding)
        {
            _probe = new DataGridBindingProbe(binding);
        }
    }

    public DataGridColumn Column { get; }

    public string Key { get; }

    public DataGridColumnType Type { get; }

    /// <summary>The column's <see cref="DataGridColumn.DefinitionVersion"/> when this accessor was made.</summary>
    public int DefinitionVersion { get; }

    /// <summary>True when the column can write into <paramref name="item"/>.</summary>
    public bool CanWrite(object item)
    {
        if (Column.ValueGetter is not null)
        {
            return Column.ValueSetter is not null;
        }
        return _probe is not null || item is DataGridRow || item is IDictionary<string, object?> || item is IDictionary;
    }

    /// <summary>The type of the last non-empty raw value read (days are written back as that type: DateTime or DateTimeOffset).</summary>
    private Type? _rawType;

    public object? ReadRaw(object item)
    {
        var raw = ReadRawCore(item);
        if (raw is not null)
        {
            _rawType = raw.GetType();
        }
        return raw;
    }

    private object? ReadRawCore(object item)
    {
        if (Column.ValueGetter is { } getter)
        {
            return getter(item);
        }
        if (_probe is not null)
        {
            return _probe.Read(item);
        }
        return item switch
        {
            DataGridRow row => row[Key],
            IDictionary<string, object?> map => map.TryGetValue(Key, out var value) ? value : null,
            IDictionary dictionary => dictionary.Contains(Key) ? dictionary[Key] : null,
            _ => null
        };
    }

    /// <summary>Converts a normalized value back to the kind of value the column holds (days to DateTime, for example).</summary>
    private object? ToRaw(object? value)
    {
        if (value is DateOnly day)
        {
            if (_rawType == typeof(DateTime))
            {
                return day.ToDateTime(TimeOnly.MinValue);
            }
            if (_rawType == typeof(DateTimeOffset))
            {
                return new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue));
            }
            if (_rawType == typeof(string))
            {
                return day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        return value;
    }

    public object? Read(object item) => DataGridValueParser.Normalize(ReadRaw(item), Type);

    /// <summary>Writes a normalized value; null clears (text columns receive an empty string).</summary>
    public void Write(object item, object? value)
    {
        var raw = ToRaw(value) ?? (Type is DataGridColumnType.Text or DataGridColumnType.Select ? string.Empty : null);
        if (Column.ValueGetter is not null)
        {
            Column.ValueSetter?.Invoke(item, raw);
            return;
        }
        if (_probe is not null)
        {
            _probe.Write(item, raw);
            return;
        }
        switch (item)
        {
            case DataGridRow row:
                row[Key] = raw;
                break;
            case IDictionary<string, object?> map:
                map[Key] = raw;
                break;
            case IDictionary dictionary:
                dictionary[Key] = raw;
                break;
        }
    }

    public void Release() => _probe?.Release();
}

/// <summary>
/// The rows behind a <see cref="DataGrid"/>: the items, their keys and a cache of normalized values per column, filled
/// lazily per column. It follows collection changes (any change reloads) and item property changes (the row is re-read)
/// from the moment the source is set until the grid first leaves the visual tree, and after that only while the grid is
/// attached (<see cref="Attach"/> re-reads what was missed), so outside data does not hold a grid that was shown and left.
/// </summary>
internal sealed class DataGridStore
{
    private readonly List<object> _items = new();
    private readonly List<object> _keys = new();
    private readonly Dictionary<object, int> _indexByKey = new();
    private readonly Dictionary<object, int> _indexByItem = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<INotifyPropertyChanged> _watched = new(ReferenceEqualityComparer.Instance);
    private DataGridColumnAccessor[] _accessors = [];
    private object?[]?[] _values = [];
    private int[] _rowVersions = [];
    private INotifyCollectionChanged? _observed;
    private IEnumerable? _source;
    private Func<object, object>? _keySelector;
    private bool _attached;
    // Until the grid is first shown the store follows its source as before (so the first show reads nothing twice); from
    // the first detach on it follows the source only while attached.
    private bool _everAttached;

    private bool Listening => _attached || !_everAttached;

    /// <summary>Raised after the item set changed (reload).</summary>
    public event Action? Reset;

    /// <summary>Raised after an item reported a property change; the argument is its source index.</summary>
    public event Action<int>? RowChanged;

    public int Count => _items.Count;

    /// <summary>Changes whenever the item set is reloaded.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<object> Items => _items;

    public IEnumerable? Source => _source;

    public DataGridColumnAccessor[] Accessors => _accessors;

    public object ItemAt(int index) => _items[index];

    public object KeyAt(int index) => _keys[index];

    public int RowVersion(int index) => _rowVersions[index];

    public int IndexOfKey(object key) => _indexByKey.TryGetValue(key, out var index) ? index : -1;

    public int IndexOfItem(object item) => _indexByItem.TryGetValue(item, out var index) ? index : -1;

    public void SetSource(IEnumerable? source, Func<object, object>? keySelector)
    {
        Unobserve();
        _source = source;
        _keySelector = keySelector;
        if (Listening)
        {
            Observe();
        }
        Reload();
    }

    public void SetColumns(IReadOnlyList<DataGridColumn> columns)
    {
        foreach (var accessor in _accessors)
        {
            accessor.Release();
        }
        _accessors = columns.Select(c => new DataGridColumnAccessor(c)).ToArray();
        _values = new object?[]?[_accessors.Length];
    }

    public int AccessorIndex(DataGridColumn column) => Array.FindIndex(_accessors, a => ReferenceEquals(a.Column, column));

    public int AccessorIndex(string key) => Array.FindIndex(_accessors, a => a.Key == key);

    /// <summary>The normalized values of a column, loaded on first use.</summary>
    public object?[] Column(int accessor)
    {
        if (_values[accessor] is { } values && values.Length == _items.Count)
        {
            return values;
        }
        var column = new object?[_items.Count];
        var access = _accessors[accessor];
        for (var i = 0; i < column.Length; i++)
        {
            column[i] = access.Read(_items[i]);
        }
        access.Release();
        _values[accessor] = column;
        return column;
    }

    public object? Value(int row, int accessor) => Column(accessor)[row];

    /// <summary>
    /// Writes a normalized value into a row and re-reads the row's cached values from the item (the setter may coerce,
    /// and computed columns may depend on the written one).
    /// </summary>
    public object? Write(int row, int accessor, object? value)
    {
        var access = _accessors[accessor];
        var item = _items[row];
        access.Write(item, value);
        var stored = access.Read(item);
        access.Release();
        if (_values[accessor] is { } values && row < values.Length)
        {
            values[row] = stored;
        }
        for (var a = 0; a < _accessors.Length; a++)
        {
            if (a != accessor && _values[a] is { } other && row < other.Length)
            {
                other[row] = _accessors[a].Read(item);
                _accessors[a].Release();
            }
        }
        _rowVersions[row]++;
        return stored;
    }

    public bool CanWrite(int row, int accessor) => _accessors[accessor].CanWrite(_items[row]);

    /// <summary>Re-reads every cached value of a row (an item changed outside the grid).</summary>
    public void Refresh(int row)
    {
        for (var a = 0; a < _accessors.Length; a++)
        {
            if (_values[a] is { } values && row < values.Length)
            {
                values[row] = _accessors[a].Read(_items[row]);
            }
        }
        foreach (var accessor in _accessors)
        {
            accessor.Release();
        }
        _rowVersions[row]++;
    }

    /// <summary>True when the source can take inserted and removed rows (undo of a delete).</summary>
    public bool IsWritableList => _source is IList { IsReadOnly: false, IsFixedSize: false };

    public void Reload()
    {
        UnwatchItems();
        _items.Clear();
        _keys.Clear();
        _indexByKey.Clear();
        _indexByItem.Clear();
        if (_source is not null)
        {
            foreach (var item in _source)
            {
                if (item is null)
                {
                    continue;
                }
                var key = _keySelector?.Invoke(item) ?? (item is DataGridRow row ? row.Key : item);
                if (!_indexByKey.TryAdd(key, _items.Count))
                {
                    // Duplicate keys break selection and motion; fall back to the item itself.
                    key = item;
                    if (!_indexByKey.TryAdd(key, _items.Count))
                    {
                        continue;
                    }
                }
                _indexByItem.TryAdd(item, _items.Count);
                _items.Add(item);
                _keys.Add(key);
                Watch(item);
            }
        }
        _values = new object?[]?[_accessors.Length];
        _rowVersions = new int[_items.Count];
        Version++;
        Reset?.Invoke();
    }

    /// <summary>Stops following the source and its items (the grid left the visual tree).</summary>
    public void Detach()
    {
        _attached = false;
        _everAttached = true;
        Unobserve();
        UnwatchItems();
    }

    /// <summary>
    /// Follows the source and its items again and re-reads them: changes made while detached were missed. The first attach
    /// has nothing to catch up with (the store has followed the source since it was set) and reads nothing.
    /// </summary>
    public void Attach()
    {
        var first = !_everAttached;
        _attached = true;
        _everAttached = true;
        if (first)
        {
            return;
        }
        Observe();
        Reload();
    }

    private void Observe()
    {
        if (_observed is null && _source is INotifyCollectionChanged observable)
        {
            _observed = observable;
            observable.CollectionChanged += OnCollectionChanged;
        }
    }

    private void Unobserve()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCollectionChanged;
            _observed = null;
        }
    }

    private void Watch(object item)
    {
        if (Listening && item is INotifyPropertyChanged notifier && _watched.Add(notifier))
        {
            notifier.PropertyChanged += OnItemPropertyChanged;
        }
    }

    private void UnwatchItems()
    {
        foreach (var watched in _watched)
        {
            watched.PropertyChanged -= OnItemPropertyChanged;
        }
        _watched.Clear();
    }

    private bool _suppress;

    /// <summary>Runs a write without reacting to the item notifications it causes (the caller refreshes).</summary>
    public void Quietly(Action action)
    {
        var previous = _suppress;
        _suppress = true;
        try
        {
            action();
        }
        finally
        {
            _suppress = previous;
        }
    }

    private int _batch;
    private bool _batchChanged;

    /// <summary>Runs several source changes and reloads once at the end.</summary>
    public void Batch(Action action)
    {
        _batch++;
        try
        {
            action();
        }
        finally
        {
            _batch--;
            // Detached, the store hears nothing from an observable source, so it re-reads the changes the grid made itself.
            if (_batch == 0 && (_batchChanged || (_observed is null && _source is INotifyCollectionChanged)))
            {
                _batchChanged = false;
                Reload();
            }
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_batch > 0)
        {
            _batchChanged = true;
            return;
        }
        if (!TryAppend(e))
        {
            Reload();
        }
    }

    /// <summary>
    /// Rows added at the end of the source (a stream, a paged load) keep every cached column and every watched item:
    /// only the new rows are read. Anything else, or any doubt about the source, reloads.
    /// </summary>
    private bool TryAppend(NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is not { Count: > 0 } added
            || e.NewStartingIndex != _items.Count || _source is not ICollection source || source.Count != _items.Count + added.Count)
        {
            return false;
        }
        var first = _items.Count;
        var keys = new List<object>(added.Count);
        foreach (var item in added)
        {
            if (item is null)
            {
                return false;
            }
            var key = _keySelector?.Invoke(item) ?? (item is DataGridRow row ? row.Key : item);
            if (_indexByKey.ContainsKey(key) || keys.Contains(key) || _indexByItem.ContainsKey(item))
            {
                return false;
            }
            keys.Add(key);
        }
        var index = first;
        foreach (var item in added)
        {
            _indexByKey.Add(keys[index - first], index);
            _indexByItem.Add(item!, index);
            _items.Add(item!);
            _keys.Add(keys[index - first]);
            Watch(item!);
            index++;
        }
        for (var a = 0; a < _accessors.Length; a++)
        {
            if (_values[a] is not { } values)
            {
                continue;
            }
            Array.Resize(ref values, _items.Count);
            for (var i = first; i < values.Length; i++)
            {
                values[i] = _accessors[a].Read(_items[i]);
            }
            _accessors[a].Release();
            _values[a] = values;
        }
        Array.Resize(ref _rowVersions, _items.Count);
        Version++;
        Reset?.Invoke();
        return true;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppress || sender is null || !_indexByItem.TryGetValue(sender, out var index))
        {
            return;
        }
        Refresh(index);
        RowChanged?.Invoke(index);
    }
}
