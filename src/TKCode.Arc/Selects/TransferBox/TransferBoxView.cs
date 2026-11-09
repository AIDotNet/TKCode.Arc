using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// The rows one list of a <see cref="TransferBox"/> shows: the items of the bound collection that pass the search
/// filter, in collection order, plus the collection index of every row. The rows are updated in place (inserts and
/// removals, not a reset), so the list keeps its selection, focus and scroll position when items move.
/// </summary>
internal sealed class TransferBoxView
{
    private readonly List<int> _map = new();
    private IList? _source;
    private Func<object?, bool>? _filter;
    private bool _listening;

    /// <summary>The rows, bound to the list's ItemsSource.</summary>
    public AvaloniaList<object?> Items { get; } = new();

    /// <summary>The bound collection.</summary>
    public IList? Source => _source;

    /// <summary>Raised after the rows were brought up to date.</summary>
    public event Action? Changed;

    /// <summary>Index in <see cref="Source"/> of the row at <paramref name="row"/>.</summary>
    public int SourceIndexOf(int row) => _map[row];

    public void SetSource(IList? source)
    {
        if (ReferenceEquals(source, _source))
        {
            return;
        }
        var listening = _listening;
        Detach();
        _source = source;
        Items.Clear();
        if (listening)
        {
            Attach();
        }
        else
        {
            Sync();
        }
    }

    public void SetFilter(Func<object?, bool>? filter)
    {
        _filter = filter;
        Sync();
    }

    /// <summary>Follows the collection's changes (while the box is in a visual tree, so a long-lived view model does not hold it).</summary>
    public void Attach()
    {
        if (_listening)
        {
            return;
        }
        _listening = true;
        if (_source is INotifyCollectionChanged changes)
        {
            changes.CollectionChanged += OnSourceChanged;
        }
        Sync();
    }

    public void Detach()
    {
        if (!_listening)
        {
            return;
        }
        _listening = false;
        if (_source is INotifyCollectionChanged changes)
        {
            changes.CollectionChanged -= OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Items.Clear();
        }
        else if (TryApplyIncrementally(e))
        {
            Changed?.Invoke();
            return;
        }
        Sync();
    }

    /// <summary>
    /// One item added or removed with no filter: the rows are the collection, so only that row changes. Adding items one
    /// by one to a long collection was quadratic through <see cref="Sync"/>.
    /// </summary>
    private bool TryApplyIncrementally(NotifyCollectionChangedEventArgs e)
    {
        if (_filter is not null || _source is null)
        {
            return false;
        }
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: 1 } added &&
            e.NewStartingIndex >= 0 && e.NewStartingIndex <= Items.Count && Items.Count + 1 == _source.Count)
        {
            Items.Insert(e.NewStartingIndex, added[0]);
            _map.Add(_map.Count);
            return true;
        }
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems is { Count: 1 } &&
            e.OldStartingIndex >= 0 && e.OldStartingIndex < Items.Count && Items.Count - 1 == _source.Count)
        {
            Items.RemoveAt(e.OldStartingIndex);
            _map.RemoveAt(_map.Count - 1);
            return true;
        }
        return false;
    }

    /// <summary>Brings the rows up to date with the collection and the filter.</summary>
    public void Sync()
    {
        var desired = new List<object?>();
        _map.Clear();
        if (_source is not null)
        {
            for (var i = 0; i < _source.Count; i++)
            {
                var item = _source[i];
                if (_filter is null || _filter(item))
                {
                    desired.Add(item);
                    _map.Add(i);
                }
            }
        }

        if (Items.Count == 0)
        {
            if (desired.Count > 0)
            {
                Items.AddRange(desired);
            }
        }
        else
        {
            Merge(desired);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Edits <see cref="Items"/> into <paramref name="desired"/> with inserts and removals. Both are subsequences of
    /// the same collection, so walking them once keeps every row that stays (and its selection) in place.
    /// </summary>
    private void Merge(List<object?> desired)
    {
        // Last position of each desired row, built at the first difference: "does this row still come later" in O(1).
        Dictionary<object, int>? last = null;
        var i = 0;
        while (i < desired.Count)
        {
            if (i < Items.Count && Equals(Items[i], desired[i]))
            {
                i++;
                continue;
            }
            if (i < Items.Count && !ComesLater(desired, ref last, i, Items[i]))
            {
                Items.RemoveAt(i);
                continue;
            }
            Items.Insert(i, desired[i]);
            i++;
        }
        if (Items.Count > desired.Count)
        {
            Items.RemoveRange(desired.Count, Items.Count - desired.Count);
        }
    }

    private static readonly object s_null = new();

    private static bool ComesLater(List<object?> list, ref Dictionary<object, int>? last, int from, object? item)
    {
        if (last is null)
        {
            last = new Dictionary<object, int>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                last[list[i] ?? s_null] = i;
            }
        }
        return last.TryGetValue(item ?? s_null, out var position) && position >= from;
    }
}
