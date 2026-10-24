using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One series of a chart. Values come from <see cref="Values"/>, or from the chart's <c>ItemsSource</c> through
/// <see cref="ValueSelector"/> or <see cref="ValueBinding"/>. The <see cref="Key"/> is the series' identity: a
/// series that keeps its key across data changes morphs instead of redrawing, and keeps its color.
/// </summary>
/// <example><code>&lt;arc:ChartSeries Name="Revenue" Values="{Binding Revenue}" /&gt;</code></example>
public class ChartSeries : AvaloniaObject
{
    /// <summary>Stable identity; defaults to <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ChartSeries, string?>(nameof(Key));

    /// <summary>Display name in the legend, the tooltip and the data table.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<ChartSeries, string?>(nameof(Name));

    /// <summary>The values, one per point, oldest first. Missing values count as zero.</summary>
    public static readonly StyledProperty<IEnumerable<double>?> ValuesProperty =
        AvaloniaProperty.Register<ChartSeries, IEnumerable<double>?>(nameof(Values));

    /// <summary>Reads this series' value from each item of the chart's <c>ItemsSource</c> (AOT safe).</summary>
    public static readonly StyledProperty<Func<object, double>?> ValueSelectorProperty =
        AvaloniaProperty.Register<ChartSeries, Func<object, double>?>(nameof(ValueSelector));

    /// <summary>
    /// Reads this series' value from each item of the chart's <c>ItemsSource</c>, the path form of
    /// <see cref="ValueSelector"/>: <c>ValueBinding="{CompiledBinding Revenue, DataType=local:Day}"</c>.
    /// </summary>
    public static readonly StyledProperty<BindingBase?> ValueBindingProperty =
        AvaloniaProperty.Register<ChartSeries, BindingBase?>(nameof(ValueBinding));

    /// <summary>Overrides the palette color (Series1–4, then neutral steps).</summary>
    public static readonly StyledProperty<Color?> ColorProperty =
        AvaloniaProperty.Register<ChartSeries, Color?>(nameof(Color));

    /// <summary>Draws a dashed stroke, useful for a previous period or a target.</summary>
    public static readonly StyledProperty<bool> IsDashedProperty =
        AvaloniaProperty.Register<ChartSeries, bool>(nameof(IsDashed));

    /// <summary>Fills the area under the line with a faint tint. Null: only the first series has an area.</summary>
    public static readonly StyledProperty<bool?> HasAreaProperty =
        AvaloniaProperty.Register<ChartSeries, bool?>(nameof(HasArea));

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ValuesProperty"/>
    public IEnumerable<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    /// <inheritdoc cref="ValueSelectorProperty"/>
    public Func<object, double>? ValueSelector { get => GetValue(ValueSelectorProperty); set => SetValue(ValueSelectorProperty, value); }

    /// <inheritdoc cref="ValueBindingProperty"/>
    [AssignBinding]
    public BindingBase? ValueBinding { get => GetValue(ValueBindingProperty); set => SetValue(ValueBindingProperty, value); }

    /// <inheritdoc cref="ColorProperty"/>
    public Color? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <inheritdoc cref="IsDashedProperty"/>
    public bool IsDashed { get => GetValue(IsDashedProperty); set => SetValue(IsDashedProperty, value); }

    /// <inheritdoc cref="HasAreaProperty"/>
    public bool? HasArea { get => GetValue(HasAreaProperty); set => SetValue(HasAreaProperty, value); }

    /// <summary>The identity used for hidden-series lists and morphing.</summary>
    public string EffectiveKey => Key ?? Name ?? string.Empty;

    /// <summary>Raised when any property or the <see cref="Values"/> collection changes.</summary>
    internal event EventHandler? Invalidated;

    // Values is often a view-model collection that outlives the chart: it is observed only while a shown chart watches
    // this series (ChartSeriesCollection.Watch), counted because one series may sit in several charts.
    private INotifyCollectionChanged? _observed;
    private int _watchers;

    /// <summary>True when <see cref="Values"/> can change without this series being set again.</summary>
    internal bool HasObservableValues => Values is INotifyCollectionChanged;

    /// <summary>Starts raising <see cref="Invalidated"/> for changes inside <see cref="Values"/>.</summary>
    internal void WatchValues()
    {
        if (_watchers++ == 0)
        {
            ObserveValues(Values as INotifyCollectionChanged);
        }
    }

    /// <summary>Undoes one <see cref="WatchValues"/>; the last one lets go of <see cref="Values"/>.</summary>
    internal void UnwatchValues()
    {
        if (_watchers > 0 && --_watchers == 0)
        {
            ObserveValues(null);
        }
    }

    private void ObserveValues(INotifyCollectionChanged? values)
    {
        if (ReferenceEquals(_observed, values))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnValuesCollectionChanged;
        }
        _observed = values;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnValuesCollectionChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty && _watchers > 0)
        {
            ObserveValues(change.NewValue as INotifyCollectionChanged);
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnValuesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Invalidated?.Invoke(this, EventArgs.Empty);
}

/// <summary>The series of a chart; a chart redraws (and morphs) when the collection or any series changes.</summary>
public sealed class ChartSeriesCollection : AvaloniaList<ChartSeries>
{
    /// <summary>Raised when a series is added, removed or changed.</summary>
    internal event EventHandler? Invalidated;

    // The series whose values this collection watches for its shown charts (counted: a collection may be shared).
    private readonly List<ChartSeries> _watched = new();
    private int _watchers;

    public ChartSeriesCollection()
    {
        ResetBehavior = ResetBehavior.Remove;
        CollectionChanged += OnCollectionChanged;
    }

    /// <summary>
    /// A chart is shown: watch every series' <see cref="ChartSeries.Values"/> until <see cref="Unwatch"/>. Returns true
    /// when any of them can have changed unseen (the chart then rebuilds once).
    /// </summary>
    internal bool Watch()
    {
        if (_watchers++ == 0)
        {
            foreach (var series in this)
            {
                series.WatchValues();
                _watched.Add(series);
            }
        }
        foreach (var series in this)
        {
            if (series.HasObservableValues)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A chart left: the last one lets go of every series' values.</summary>
    internal void Unwatch()
    {
        if (_watchers == 0 || --_watchers > 0)
        {
            return;
        }
        foreach (var series in _watched)
        {
            series.UnwatchValues();
        }
        _watched.Clear();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ChartSeries series in e.OldItems)
            {
                series.Invalidated -= OnSeriesInvalidated;
                if (_watched.Remove(series))
                {
                    series.UnwatchValues();
                }
            }
        }
        if (e.NewItems is not null)
        {
            foreach (ChartSeries series in e.NewItems)
            {
                series.Invalidated += OnSeriesInvalidated;
                if (_watchers > 0)
                {
                    series.WatchValues();
                    _watched.Add(series);
                }
            }
        }
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private void OnSeriesInvalidated(object? sender, EventArgs e) => Invalidated?.Invoke(this, EventArgs.Empty);
}
