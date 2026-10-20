using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// Reads members of arbitrary data items without reflection of its own: a typed <c>Func</c> selector wins, then an
/// Avalonia member binding (compiled bindings stay AOT safe), then the caller's typed fallback.
/// </summary>
internal sealed class ChartsBMemberReader
{
    private readonly Probe _probe = new();

    public object? Read(object item, BindingBase? binding)
    {
        if (binding is null)
        {
            return null;
        }
        _probe.DataContext = item;
        var expression = _probe.Bind(Probe.ValueProperty, binding);
        var value = _probe.GetValue(Probe.ValueProperty);
        expression.Dispose();
        _probe.ClearValue(Probe.ValueProperty);
        _probe.DataContext = null;
        return value;
    }

    public static double ToDouble(object? value) => value switch
    {
        null => double.NaN,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        decimal m => (double)m,
        string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN,
        IConvertible convertible => convertible.ToDouble(CultureInfo.InvariantCulture),
        _ => double.NaN
    };

    public static string? ToText(object? value) => value switch
    {
        null => null,
        string s => s,
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    public static DateOnly? ToDate(object? value) => value switch
    {
        DateOnly date => date,
        DateTime time => DateOnly.FromDateTime(time),
        DateTimeOffset offset => DateOnly.FromDateTime(offset.Date),
        string s => DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null,
        _ => null
    };

    private sealed class Probe : StyledElement
    {
        public static readonly StyledProperty<object?> ValueProperty = AvaloniaProperty.Register<Probe, object?>("Value");
    }
}

/// <summary>
/// Tracks a chart's effective items (its <c>ItemsSource</c>, else its inline items) and reports collection changes
/// while the chart is attached. Subscriptions are dropped on detach so a long-lived source never holds the chart.
/// </summary>
internal sealed class ChartsBItemsWatcher
{
    private readonly Action _changed;
    private IEnumerable? _source;
    private INotifyCollectionChanged? _subscribed;
    private bool _attached;
    private bool _detachedOnce;

    public ChartsBItemsWatcher(Action changed) => _changed = changed;

    public IEnumerable? Source => _source;

    /// <summary>True while collection changes of the source are being reported.</summary>
    public bool IsSubscribed => _subscribed is not null;

    public void SetSource(IEnumerable? source)
    {
        if (ReferenceEquals(source, _source))
        {
            return;
        }
        Unsubscribe();
        _source = source;
        Subscribe();
        _changed();
    }

    public void Attach()
    {
        _attached = true;
        var observable = _source is INotifyCollectionChanged && _subscribed is null;
        Subscribe();
        if (observable && _detachedOnce)
        {
            // Changes made while detached were not observed: read the items again.
            _changed();
        }
    }

    public void Detach()
    {
        _attached = false;
        _detachedOnce = true;
        Unsubscribe();
    }

    public List<object> Snapshot()
    {
        var list = new List<object>();
        if (_source is not null)
        {
            foreach (var item in _source)
            {
                if (item is not null)
                {
                    list.Add(item);
                }
            }
        }
        return list;
    }

    private void Subscribe()
    {
        if (_attached && _subscribed is null && _source is INotifyCollectionChanged incc)
        {
            _subscribed = incc;
            incc.CollectionChanged += OnCollectionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.CollectionChanged -= OnCollectionChanged;
            _subscribed = null;
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => _changed();
}
