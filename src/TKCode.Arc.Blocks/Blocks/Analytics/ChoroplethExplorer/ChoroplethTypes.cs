using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>How a <see cref="ChoroplethMetric"/>'s values are written.</summary>
public enum ChoroplethValueFormat
{
    /// <summary>Money in compact form: "$9.8M", "$903K", legend ticks with two significant digits ("$120K").</summary>
    Currency,

    /// <summary>A signed change in percent units (4.3 is "+4.3%", −4.8 is "−4.8%", with a true minus sign).</summary>
    Change,

    /// <summary>A share in percent units (17.5 is "17.5%").</summary>
    Percent,

    /// <summary>A plain number with grouping ("1,250").</summary>
    Number,
}

/// <summary>How a <see cref="ChoroplethMetric"/> is binned into colour steps.</summary>
public enum ChoroplethScale
{
    /// <summary>One hue from light to dark in <see cref="ChoroplethMetric.BinCount"/> quantile steps.</summary>
    Sequential,

    /// <summary>
    /// Split at zero: negative values in the negative hue, zero and up in the positive hue, each side binned by its own
    /// quantiles into <see cref="ChoroplethMetric.BinCount"/> + 1 steps shared in proportion to how many values each side has.
    /// </summary>
    Diverging,
}

/// <summary>How the headline number of a whole area is made from its regions.</summary>
public enum ChoroplethAggregate
{
    /// <summary>The sum of every region's value (revenue, accounts).</summary>
    Sum,

    /// <summary>The average of the regions' values weighted by the area's first metric (growth, coverage).</summary>
    WeightedAverage,
}

/// <summary>
/// One measure a <see cref="ChoroplethExplorer"/> can shade the map by: a segment in the metric switch, how its values are
/// written and binned, and the words around it ("Ranked by revenue", "Shaded by annual recurring revenue").
/// </summary>
public sealed class ChoroplethMetric : AvaloniaObject
{
    /// <summary>The key regions store their values under (<see cref="ChoroplethRegion.SetMetricValue"/>).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string>(nameof(Key), string.Empty);

    /// <summary>The segment's text in the metric switch and the row's label in the hover card ("Revenue").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string>(nameof(Label), string.Empty);

    /// <summary>The lower-case name used inside sentences ("Ranked by revenue"); null uses <see cref="Label"/> in lower case.</summary>
    public static readonly StyledProperty<string?> NounProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string?>(nameof(Noun));

    /// <summary>The small text next to the headline number ("Annual recurring revenue").</summary>
    public static readonly StyledProperty<string?> HeadlineLabelProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string?>(nameof(HeadlineLabel));

    /// <summary>The line under the legend while no step is pointed at ("Shaded by annual recurring revenue").</summary>
    public static readonly StyledProperty<string?> LegendCaptionProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string?>(nameof(LegendCaption));

    /// <summary>How values are written.</summary>
    public static readonly StyledProperty<ChoroplethValueFormat> FormatProperty =
        AvaloniaProperty.Register<ChoroplethMetric, ChoroplethValueFormat>(nameof(Format));

    /// <summary>The currency symbol of <see cref="ChoroplethValueFormat.Currency"/> ("$", "€", "C$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ChoroplethMetric, string>(nameof(CurrencySymbol), "$");

    /// <summary>Writes a value (list, hover card, headline, announcements); null uses <see cref="Format"/>.</summary>
    public static readonly StyledProperty<Func<double, string>?> FormatterProperty =
        AvaloniaProperty.Register<ChoroplethMetric, Func<double, string>?>(nameof(Formatter));

    /// <summary>Writes a legend tick (a bin edge rounded to two significant digits); null uses <see cref="Format"/>.</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<ChoroplethMetric, Func<double, string>?>(nameof(TickFormatter));

    /// <summary>Sequential (one hue) or diverging (split at zero).</summary>
    public static readonly StyledProperty<ChoroplethScale> ScaleProperty =
        AvaloniaProperty.Register<ChoroplethMetric, ChoroplethScale>(nameof(Scale));

    /// <summary>How the headline number of a whole area is made from its regions.</summary>
    public static readonly StyledProperty<ChoroplethAggregate> AggregateProperty =
        AvaloniaProperty.Register<ChoroplethMetric, ChoroplethAggregate>(nameof(Aggregate));

    /// <summary>How many colour steps a sequential scale has (5; a diverging scale has one more). 2 to 9.</summary>
    public static readonly StyledProperty<int> BinCountProperty =
        AvaloniaProperty.Register<ChoroplethMetric, int>(nameof(BinCount), 5);

    /// <summary>Creates an empty metric (XAML).</summary>
    public ChoroplethMetric()
    {
    }

    /// <summary>Creates a metric.</summary>
    /// <param name="key">The key regions store their values under.</param>
    /// <param name="label">The segment's text.</param>
    /// <param name="format">How values are written.</param>
    /// <param name="scale">Sequential or diverging.</param>
    public ChoroplethMetric(string key, string label, ChoroplethValueFormat format = ChoroplethValueFormat.Currency,
        ChoroplethScale scale = ChoroplethScale.Sequential)
    {
        Key = key;
        Label = label;
        Format = format;
        Scale = scale;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="NounProperty"/>
    public string? Noun { get => GetValue(NounProperty); set => SetValue(NounProperty, value); }

    /// <inheritdoc cref="HeadlineLabelProperty"/>
    public string? HeadlineLabel { get => GetValue(HeadlineLabelProperty); set => SetValue(HeadlineLabelProperty, value); }

    /// <inheritdoc cref="LegendCaptionProperty"/>
    public string? LegendCaption { get => GetValue(LegendCaptionProperty); set => SetValue(LegendCaptionProperty, value); }

    /// <inheritdoc cref="FormatProperty"/>
    public ChoroplethValueFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="FormatterProperty"/>
    public Func<double, string>? Formatter { get => GetValue(FormatterProperty); set => SetValue(FormatterProperty, value); }

    /// <inheritdoc cref="TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc cref="ScaleProperty"/>
    public ChoroplethScale Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    /// <inheritdoc cref="AggregateProperty"/>
    public ChoroplethAggregate Aggregate { get => GetValue(AggregateProperty); set => SetValue(AggregateProperty, value); }

    /// <inheritdoc cref="BinCountProperty"/>
    public int BinCount { get => GetValue(BinCountProperty); set => SetValue(BinCountProperty, value); }

    /// <summary>The noun used in sentences.</summary>
    internal string EffectiveNoun => Noun ?? Label.ToLowerInvariant();

    /// <summary>Writes <paramref name="value"/> for the list, the card and the headline.</summary>
    public string FormatValue(double value) => Formatter?.Invoke(value) ?? ChoroplethMath.FormatValue(value, Format, CurrencySymbol);

    /// <summary>Writes a legend tick.</summary>
    public string FormatTick(double value) => TickFormatter?.Invoke(value) ?? ChoroplethMath.FormatTick(value, Format, CurrencySymbol);

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// One shape on a <see cref="ChoroplethExplorer"/> map: a state, province or region (or, inside one, a county or
/// department). It carries its outline as SVG path data in the area's view box, a value per metric (null = no data), and
/// optionally the subregions shown when it is opened. The block contains no map data of its own: every outline comes from here.
/// </summary>
public sealed class ChoroplethRegion : AvaloniaObject
{
    /// <summary>A stable id (postal code, FIPS code); used by <see cref="ChoroplethExplorer.OpenRegionKey"/> and the events.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ChoroplethRegion, string>(nameof(Key), string.Empty);

    /// <summary>A short code to show or match by ("CA"); optional.</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<ChoroplethRegion, string?>(nameof(Code));

    /// <summary>The display name ("California").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ChoroplethRegion, string>(nameof(Name), string.Empty);

    /// <summary>
    /// The outline as SVG path data in the area's view box (for subregions, in the parent's view box transformed by its
    /// <see cref="SubregionOrigin"/> and <see cref="SubregionScale"/>). Parsed once, when it is first drawn.
    /// </summary>
    public static readonly StyledProperty<string?> PathDataProperty =
        AvaloniaProperty.Register<ChoroplethRegion, string?>(nameof(PathData));

    /// <summary>The outline as a ready geometry; wins over <see cref="PathData"/>.</summary>
    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<ChoroplethRegion, Geometry?>(nameof(Geometry));

    /// <summary>
    /// The outline's bounding box in view-box units. It places the camera and times the west-to-east sweep without parsing
    /// the outline; an empty rect (the default) uses the parsed geometry's bounds.
    /// </summary>
    public static readonly StyledProperty<Rect> BoundsProperty =
        AvaloniaProperty.Register<ChoroplethRegion, Rect>(nameof(Bounds));

    /// <summary>Where the subregions' local grid starts, in this region's view box (0, 0 when they share it).</summary>
    public static readonly StyledProperty<Point> SubregionOriginProperty =
        AvaloniaProperty.Register<ChoroplethRegion, Point>(nameof(SubregionOrigin));

    /// <summary>How many view-box units one unit of the subregions' local grid is (1 when they share the view box).</summary>
    public static readonly StyledProperty<double> SubregionScaleProperty =
        AvaloniaProperty.Register<ChoroplethRegion, double>(nameof(SubregionScale), 1.0);

    /// <summary>The sentence shown when the region is opened and has no subregions; null uses the area's.</summary>
    public static readonly StyledProperty<string?> EmptyDetailTextProperty =
        AvaloniaProperty.Register<ChoroplethRegion, string?>(nameof(EmptyDetailText));

    private readonly Dictionary<string, double?> _values = new(StringComparer.Ordinal);
    private Geometry? _parsed;
    private string? _parsedFrom;

    /// <summary>Creates an empty region (XAML).</summary>
    public ChoroplethRegion()
    {
    }

    /// <summary>Creates a region.</summary>
    /// <param name="key">A stable id.</param>
    /// <param name="name">The display name.</param>
    /// <param name="pathData">The outline as SVG path data.</param>
    /// <param name="bounds">The outline's bounding box (empty to measure the outline).</param>
    public ChoroplethRegion(string key, string name, string? pathData, Rect bounds = default)
    {
        Key = key;
        Name = name;
        PathData = pathData;
        Bounds = bounds;
    }

    /// <summary>Raised when a value or the subregions change (the block re-bins and redraws).</summary>
    internal event Action<ChoroplethRegion>? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="CodeProperty"/>
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="PathDataProperty"/>
    public string? PathData { get => GetValue(PathDataProperty); set => SetValue(PathDataProperty, value); }

    /// <inheritdoc cref="GeometryProperty"/>
    public Geometry? Geometry { get => GetValue(GeometryProperty); set => SetValue(GeometryProperty, value); }

    /// <inheritdoc cref="BoundsProperty"/>
    public Rect Bounds { get => GetValue(BoundsProperty); set => SetValue(BoundsProperty, value); }

    /// <inheritdoc cref="SubregionOriginProperty"/>
    public Point SubregionOrigin { get => GetValue(SubregionOriginProperty); set => SetValue(SubregionOriginProperty, value); }

    /// <inheritdoc cref="SubregionScaleProperty"/>
    public double SubregionScale { get => GetValue(SubregionScaleProperty); set => SetValue(SubregionScaleProperty, value); }

    /// <inheritdoc cref="EmptyDetailTextProperty"/>
    public string? EmptyDetailText { get => GetValue(EmptyDetailTextProperty); set => SetValue(EmptyDetailTextProperty, value); }

    /// <summary>The regions inside this one (counties of a state), shown when it is opened; empty when there are none.</summary>
    public AvaloniaList<ChoroplethRegion> Subregions { get; } = new();

    /// <summary>The values by metric key; null means no data.</summary>
    public IReadOnlyDictionary<string, double?> Values => _values;

    /// <summary>Sets the value of <paramref name="metricKey"/> (null = no data); the block re-bins on the next frame.</summary>
    public void SetMetricValue(string metricKey, double? value)
    {
        _values[metricKey] = value is { } v && double.IsFinite(v) ? v : null;
        Changed?.Invoke(this);
    }

    /// <summary>The value of <paramref name="metricKey"/>, or null when there is none.</summary>
    public double? GetMetricValue(string metricKey) => _values.TryGetValue(metricKey, out var v) ? v : null;

    /// <summary>The outline, parsed from <see cref="PathData"/> once and cached (null when there is none or it does not parse).</summary>
    internal Geometry? ResolveGeometry()
    {
        if (Geometry is { } given)
        {
            return given;
        }
        var data = PathData;
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }
        if (!ReferenceEquals(data, _parsedFrom))
        {
            _parsedFrom = data;
            try
            {
                _parsed = StreamGeometry.Parse(data);
            }
            catch (FormatException)
            {
                _parsed = null;
            }
        }
        return _parsed;
    }

    /// <summary>The bounding box: <see cref="Bounds"/>, else the parsed outline's.</summary>
    internal Rect ResolveBounds()
    {
        var given = Bounds;
        if (given.Width > 0 || given.Height > 0)
        {
            return given;
        }
        return ResolveGeometry()?.Bounds ?? default;
    }

    /// <summary>True when opening the region shows subregions.</summary>
    internal bool HasSubregions => Subregions.Count > 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PathDataProperty || change.Property == GeometryProperty || change.Property == BoundsProperty ||
            change.Property == NameProperty)
        {
            Changed?.Invoke(this);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// One map a <see cref="ChoroplethExplorer"/> can show (a country): its regions, the words for them, and optionally its own
/// metrics (local currency). With more than one area the block shows a country switch.
/// </summary>
public sealed class ChoroplethArea : AvaloniaObject
{
    /// <summary>A stable id (<see cref="ChoroplethExplorer.SelectedAreaKey"/>, events).</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(Key), string.Empty);

    /// <summary>The display name ("United States"): the switch, the summary, the map's accessible name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(Name), string.Empty);

    /// <summary>The plural word for its regions ("states").</summary>
    public static readonly StyledProperty<string> RegionNounProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(RegionNoun), "regions");

    /// <summary>The singular word for one region ("state").</summary>
    public static readonly StyledProperty<string> RegionNounSingularProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(RegionNounSingular), "region");

    /// <summary>The plural word for subregions ("counties").</summary>
    public static readonly StyledProperty<string> SubregionNounProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(SubregionNoun), "subregions");

    /// <summary>The singular word for one subregion ("county").</summary>
    public static readonly StyledProperty<string> SubregionNounSingularProperty =
        AvaloniaProperty.Register<ChoroplethArea, string>(nameof(SubregionNounSingular), "subregion");

    /// <summary>The block's title while this area shows; null uses the block's <see cref="ChoroplethExplorer.Title"/> or the generated one.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChoroplethArea, string?>(nameof(Title));

    /// <summary>The size of the coordinate system the outlines are drawn in (975 × 610).</summary>
    public static readonly StyledProperty<Size> ViewBoxProperty =
        AvaloniaProperty.Register<ChoroplethArea, Size>(nameof(ViewBox), new Size(975, 610));

    /// <summary>The first sentence of the empty panel of a region without subregions ("County data covers ten states.").</summary>
    public static readonly StyledProperty<string?> EmptyDetailTextProperty =
        AvaloniaProperty.Register<ChoroplethArea, string?>(nameof(EmptyDetailText));

    /// <summary>Creates an empty area (XAML).</summary>
    public ChoroplethArea()
    {
    }

    /// <summary>Creates an area.</summary>
    /// <param name="key">A stable id.</param>
    /// <param name="name">The display name.</param>
    /// <param name="regionNoun">The plural word for its regions.</param>
    /// <param name="regionNounSingular">The singular word.</param>
    public ChoroplethArea(string key, string name, string regionNoun = "regions", string regionNounSingular = "region")
    {
        Key = key;
        Name = name;
        RegionNoun = regionNoun;
        RegionNounSingular = regionNounSingular;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="RegionNounProperty"/>
    public string RegionNoun { get => GetValue(RegionNounProperty); set => SetValue(RegionNounProperty, value); }

    /// <inheritdoc cref="RegionNounSingularProperty"/>
    public string RegionNounSingular { get => GetValue(RegionNounSingularProperty); set => SetValue(RegionNounSingularProperty, value); }

    /// <inheritdoc cref="SubregionNounProperty"/>
    public string SubregionNoun { get => GetValue(SubregionNounProperty); set => SetValue(SubregionNounProperty, value); }

    /// <inheritdoc cref="SubregionNounSingularProperty"/>
    public string SubregionNounSingular { get => GetValue(SubregionNounSingularProperty); set => SetValue(SubregionNounSingularProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ViewBoxProperty"/>
    public Size ViewBox { get => GetValue(ViewBoxProperty); set => SetValue(ViewBoxProperty, value); }

    /// <inheritdoc cref="EmptyDetailTextProperty"/>
    public string? EmptyDetailText { get => GetValue(EmptyDetailTextProperty); set => SetValue(EmptyDetailTextProperty, value); }

    /// <summary>The top-level regions.</summary>
    public AvaloniaList<ChoroplethRegion> Regions { get; } = new();

    /// <summary>This area's own metrics (local currency); empty uses the block's <see cref="ChoroplethExplorer.Metrics"/>.</summary>
    public AvaloniaList<ChoroplethMetric> Metrics { get; } = new();

    /// <summary>The word for <paramref name="count"/> regions ("1 state", "51 states").</summary>
    internal string RegionWord(int count) => count == 1 ? RegionNounSingular : RegionNoun;

    /// <summary>The word for <paramref name="count"/> subregions.</summary>
    internal string SubregionWord(int count) => count == 1 ? SubregionNounSingular : SubregionNoun;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Arguments of <c>ChoroplethExplorer.AreaChanged</c> and <c>MetricChanged</c>: the selected key changed.</summary>
public class ChoroplethSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="oldKey">The key before.</param>
    /// <param name="newKey">The key now.</param>
    public ChoroplethSelectionChangedEventArgs(RoutedEvent routedEvent, string? oldKey, string? newKey) : base(routedEvent)
    {
        OldKey = oldKey;
        NewKey = newKey;
    }

    /// <summary>The key before the change.</summary>
    public string? OldKey { get; }

    /// <summary>The key after the change.</summary>
    public string? NewKey { get; }
}

/// <summary>Arguments of <c>ChoroplethExplorer.RegionOpened</c>: the camera started towards a region, or back out (null).</summary>
public class ChoroplethRegionOpenedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="area">The area shown.</param>
    /// <param name="region">The opened region; null when the map zoomed back out.</param>
    public ChoroplethRegionOpenedEventArgs(RoutedEvent routedEvent, ChoroplethArea? area, ChoroplethRegion? region) : base(routedEvent)
    {
        Area = area;
        Region = region;
    }

    /// <summary>The area shown.</summary>
    public ChoroplethArea? Area { get; }

    /// <summary>The opened region; null when the map zoomed back out.</summary>
    public ChoroplethRegion? Region { get; }

    /// <summary>The opened region's key; null when the map zoomed back out.</summary>
    public string? RegionKey => Region?.Key;
}

/// <summary>Arguments of <c>ChoroplethExplorer.SubregionPinned</c>: a subregion was pinned, or the pin was released (null).</summary>
public class ChoroplethSubregionPinnedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="region">The opened region.</param>
    /// <param name="subregion">The pinned subregion; null when the pin was released.</param>
    public ChoroplethSubregionPinnedEventArgs(RoutedEvent routedEvent, ChoroplethRegion? region, ChoroplethRegion? subregion) : base(routedEvent)
    {
        Region = region;
        Subregion = subregion;
    }

    /// <summary>The opened region.</summary>
    public ChoroplethRegion? Region { get; }

    /// <summary>The pinned subregion; null when the pin was released.</summary>
    public ChoroplethRegion? Subregion { get; }
}
