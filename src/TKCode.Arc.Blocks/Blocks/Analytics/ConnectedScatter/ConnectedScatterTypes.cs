using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>Which side of its point an annotation (or a first / last month label) is written on.</summary>
public enum ScatterLabelSide
{
    /// <summary>Left of the point, text ending 10px before it.</summary>
    Left,

    /// <summary>Right of the point, text starting 10px after it.</summary>
    Right,

    /// <summary>Above the point, centred.</summary>
    Top,

    /// <summary>Below the point, centred.</summary>
    Bottom
}

/// <summary>One month of a <see cref="ScatterSeries"/>: the two values that move together.</summary>
public class ScatterPoint : AvaloniaObject
{
    /// <summary>The month's name ("Mar 2025"); it is also what the scrubber, the readout and the card say.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ScatterPoint, string>(nameof(Label), string.Empty);

    /// <summary>The short name under the scrubber ("Mar"); null derives it from <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> ShortLabelProperty =
        AvaloniaProperty.Register<ScatterPoint, string?>(nameof(ShortLabel));

    /// <summary>The value on the horizontal axis (acquisition cost in the reference).</summary>
    public static readonly StyledProperty<double> XProperty =
        AvaloniaProperty.Register<ScatterPoint, double>(nameof(X));

    /// <summary>The value on the vertical axis (customer value in the reference).</summary>
    public static readonly StyledProperty<double> YProperty =
        AvaloniaProperty.Register<ScatterPoint, double>(nameof(Y));

    /// <summary>Creates an empty point.</summary>
    public ScatterPoint()
    {
    }

    /// <summary>Creates a point with its month and values.</summary>
    public ScatterPoint(string label, double x, double y)
    {
        Label = label;
        X = x;
        Y = y;
    }

    /// <summary>Raised when any property of the point changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ShortLabelProperty"/>
    public string? ShortLabel { get => GetValue(ShortLabelProperty); set => SetValue(ShortLabelProperty, value); }

    /// <inheritdoc cref="XProperty"/>
    public double X { get => GetValue(XProperty); set => SetValue(XProperty, value); }

    /// <inheritdoc cref="YProperty"/>
    public double Y { get => GetValue(YProperty); set => SetValue(YProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>A short text written next to one month of a series ("Pricing change"), shown while the viewport is wide enough.</summary>
public class ScatterNote : AvaloniaObject
{
    /// <summary>The month's index in the series, from 0.</summary>
    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<ScatterNote, int>(nameof(Index));

    /// <summary>The text.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ScatterNote, string>(nameof(Text), string.Empty);

    /// <summary>Which side of the point the text goes on.</summary>
    public static readonly StyledProperty<ScatterLabelSide> SideProperty =
        AvaloniaProperty.Register<ScatterNote, ScatterLabelSide>(nameof(Side), ScatterLabelSide.Top);

    /// <summary>Creates an empty note.</summary>
    public ScatterNote()
    {
    }

    /// <summary>Creates a note.</summary>
    public ScatterNote(int index, string text, ScatterLabelSide side = ScatterLabelSide.Top)
    {
        Index = index;
        Text = text;
        Side = side;
    }

    /// <summary>Raised when any property of the note changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="IndexProperty"/>
    public int Index { get => GetValue(IndexProperty); set => SetValue(IndexProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="SideProperty"/>
    public ScatterLabelSide Side { get => GetValue(SideProperty); set => SetValue(SideProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One channel of a <see cref="ConnectedScatter"/>: a name and one <see cref="ScatterPoint"/> per month in time order.
/// Every series of a block should have the same number of months, so that switching between them can morph point by
/// point; with different counts the block replaces the drawing instead.
/// </summary>
public class ScatterSeries : AvaloniaObject
{
    /// <summary>A stable identifier (selection, events); defaults to the name.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ScatterSeries, string?>(nameof(Key));

    /// <summary>The channel's name, on the switch and in the legend.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ScatterSeries, string>(nameof(Name), string.Empty);

    /// <summary>Anything you want to carry along (it comes back in the events).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ScatterSeries, object?>(nameof(Tag));

    /// <summary>Creates an empty series.</summary>
    public ScatterSeries()
    {
        Points.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Notes.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates a series with its name and months.</summary>
    public ScatterSeries(string name, IEnumerable<ScatterPoint> points, string? key = null) : this()
    {
        Name = name;
        Key = key;
        Points.AddRange(points);
    }

    /// <summary>Raised when the series, its points or its notes change.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The months in time order.</summary>
    [Content]
    public AvaloniaList<ScatterPoint> Points { get; } = new();

    /// <summary>Notes written next to months of this series.</summary>
    public AvaloniaList<ScatterNote> Notes { get; } = new();

    /// <summary>The key, or the name when no key is set.</summary>
    public string EffectiveKey => Key ?? Name;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One axis of a <see cref="ConnectedScatter"/>. The range and the step are used exactly as given (no rounding to nice
/// numbers, not forced to start at zero); leave them unset to derive them from the data of every series.
/// </summary>
public class ScatterAxis : AvaloniaObject
{
    /// <summary>The short name in the readout and the card ("CAC").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ScatterAxis, string>(nameof(Name), string.Empty);

    /// <summary>The title of the axis ("Customer acquisition cost").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ScatterAxis, string?>(nameof(Title));

    /// <summary>The lowest value shown; NaN (default) takes it from the data.</summary>
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<ScatterAxis, double>(nameof(Minimum), double.NaN);

    /// <summary>The highest value shown; NaN (default) takes it from the data.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ScatterAxis, double>(nameof(Maximum), double.NaN);

    /// <summary>The distance between ticks; NaN (default) picks a step that gives four to six ticks.</summary>
    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<ScatterAxis, double>(nameof(Step), double.NaN);

    /// <summary>Formats a tick; null gives "$2k" for steps that are multiples of 1,000 and the block's value format otherwise.</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<ScatterAxis, Func<double, string>?>(nameof(TickFormatter));

    /// <summary>Creates an axis.</summary>
    public ScatterAxis()
    {
    }

    /// <summary>Creates an axis with its names and range.</summary>
    public ScatterAxis(string name, string? title, double minimum, double maximum, double step)
    {
        Name = name;
        Title = title;
        Minimum = minimum;
        Maximum = maximum;
        Step = step;
    }

    /// <summary>Raised when any property of the axis changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="StepProperty"/>
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }

    /// <inheritdoc cref="TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Reports that the head point settled on a month (released, a key, a tap, the end of playback), never while it moves.</summary>
public sealed class ScatterMonthChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ScatterMonthChangedEventArgs(RoutedEvent routedEvent, int index, ScatterSeries? series, ScatterPoint? point) : base(routedEvent)
    {
        Index = index;
        Series = series;
        Point = point;
    }

    /// <summary>The month's index, from 0.</summary>
    public int Index { get; }

    /// <summary>The series shown.</summary>
    public ScatterSeries? Series { get; }

    /// <summary>The month's point.</summary>
    public ScatterPoint? Point { get; }
}

/// <summary>Reports that another series was chosen.</summary>
public sealed class ScatterSeriesChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ScatterSeriesChangedEventArgs(RoutedEvent routedEvent, ScatterSeries? series, int index) : base(routedEvent)
    {
        Series = series;
        Index = index;
    }

    /// <summary>The series now shown.</summary>
    public ScatterSeries? Series { get; }

    /// <summary>Its position in the list.</summary>
    public int Index { get; }
}
