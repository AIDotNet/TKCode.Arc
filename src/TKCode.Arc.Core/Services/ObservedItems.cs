using System.Collections;
using System.Collections.Specialized;
using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Follows a list of items for a control: changes to the list, and property changes of every item that is an
/// <see cref="AvaloniaObject"/> (option types whose properties may be bound in XAML, so their values can arrive after the
/// control read them, or change later). The list and its items may belong to a view model, so a control observes them
/// only while it is attached (<c>Observe(null)</c> when it leaves) and re-reads them when it comes back.
/// </summary>
internal sealed class ObservedItems
{
    private readonly Action<AvaloniaObject?> _changed;
    private HashSet<AvaloniaObject> _items = new(ReferenceEqualityComparer.Instance);
    private IEnumerable? _source;
    private INotifyCollectionChanged? _notifier;

    /// <param name="changed">Called with the item whose property changed, or with null when the list itself changed.</param>
    public ObservedItems(Action<AvaloniaObject?> changed) => _changed = changed;

    /// <summary>The list being observed, or null.</summary>
    public IEnumerable? Source => _source;

    /// <summary>
    /// Observes <paramref name="source"/> (null stops). Observing the same list again picks up items added or removed
    /// meanwhile. Does not call back: the control reads the list itself after calling this.
    /// </summary>
    public void Observe(IEnumerable? source)
    {
        var notifier = source as INotifyCollectionChanged;
        if (!ReferenceEquals(notifier, _notifier))
        {
            if (_notifier is not null)
            {
                _notifier.CollectionChanged -= OnCollectionChanged;
            }
            _notifier = notifier;
            if (_notifier is not null)
            {
                _notifier.CollectionChanged += OnCollectionChanged;
            }
        }
        _source = source;
        Resync();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Resync();
        _changed(null);
    }

    private void OnItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is AvaloniaObject item)
        {
            _changed(item);
        }
    }

    private void Resync()
    {
        var current = new HashSet<AvaloniaObject>(ReferenceEqualityComparer.Instance);
        if (_source is not null)
        {
            foreach (var item in _source)
            {
                if (item is AvaloniaObject observable)
                {
                    current.Add(observable);
                }
            }
        }
        foreach (var item in _items)
        {
            if (!current.Contains(item))
            {
                item.PropertyChanged -= OnItemChanged;
            }
        }
        foreach (var item in current)
        {
            if (!_items.Contains(item))
            {
                item.PropertyChanged += OnItemChanged;
            }
        }
        _items = current;
    }
}
