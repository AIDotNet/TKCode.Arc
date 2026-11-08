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
        Sync();
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
        var i = 0;
        while (i < desired.Count)
        {
            if (i < Items.Count && Equals(Items[i], desired[i]))
            {
                i++;
                continue;
            }
            if (i < Items.Count && !Contains(desired, i, Items[i]))
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

    private static bool Contains(List<object?> list, int from, object? item)
    {
        for (var i = from; i < list.Count; i++)
        {
            if (Equals(list[i], item))
            {
                return true;
            }
        }
        return false;
    }
}
