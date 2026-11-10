using System.Collections;
using System.Collections.Specialized;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>
/// The pictures a Photos control shows: its own <see cref="Items"/> (written inline in XAML) or, when set, an
/// <see cref="ItemsSource"/>. Elements of the source are turned into the control's item type by a converter (so a list of
/// addresses or <see cref="ImageSource"/>s works too). Collection changes are followed only while the owner is attached
/// (<see cref="Attach"/> / <see cref="Detach"/>), so a long-lived source never holds a page that was closed.
/// </summary>
internal sealed class PhotosItemFeed<T> where T : class
{
    private readonly Func<object?, T?> _convert;
    private readonly Action _changed;
    private readonly List<T> _current = new();
    private Dictionary<object, T> _converted = new(ReferenceEqualityComparer.Instance);
    private Dictionary<object, T> _swap = new(ReferenceEqualityComparer.Instance);
    private IEnumerable? _source;
    private INotifyCollectionChanged? _observed;
    private bool _attached;

    public PhotosItemFeed(Func<object?, T?> convert, Action changed)
    {
        _convert = convert;
        _changed = changed;
        Items = new AvaloniaList<T>();
        Items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>Items written inline; used while <see cref="ItemsSource"/> is null.</summary>
    public AvaloniaList<T> Items { get; }

    /// <summary>The bound collection, which wins over <see cref="Items"/>.</summary>
    public IEnumerable? ItemsSource
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }
            Unobserve();
            _source = value;
            Observe();
            Rebuild();
        }
    }

    /// <summary>The items shown, in order.</summary>
    public IReadOnlyList<T> Current => _current;

    public int Count => _current.Count;

    public T this[int index] => _current[index];

    public int IndexOf(T item) => _current.IndexOf(item);

    public void Attach()
    {
        if (_attached)
        {
            return;
        }
        _attached = true;
        Observe();
        Rebuild();
    }

    public void Detach()
    {
        _attached = false;
        Unobserve();
    }

    private void Observe()
    {
        if (_attached && _source is INotifyCollectionChanged observed)
        {
            _observed = observed;
            observed.CollectionChanged += OnSourceChanged;
        }
    }

    private void Unobserve()
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnSourceChanged;
            _observed = null;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_source is null)
        {
            Rebuild();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        _current.Clear();
        if (_source is null)
        {
            _current.AddRange(Items);
        }
        else
        {
            // Converted elements keep their item between rebuilds, so a control can follow them as the list changes.
            _swap.Clear();
            foreach (var element in _source)
            {
                if (element is null)
                {
                    continue;
                }
                if (!_converted.TryGetValue(element, out var item))
                {
                    item = _convert(element);
                }
                if (item is not null)
                {
                    _swap[element] = item;
                    _current.Add(item);
                }
            }
            (_converted, _swap) = (_swap, _converted);
        }
        _changed();
    }

    /// <summary>The picture behind an element that is a source or an address; null for anything else.</summary>
    public static ImageSource? SourceOf(object? element) => element switch
    {
        ImageSource source => source,
        string text when ImageSource.TryParse(text, out var parsed) => parsed,
        Uri uri when ImageSource.TryParse(uri.OriginalString, out var parsed) => parsed,
        _ => null
    };
}
