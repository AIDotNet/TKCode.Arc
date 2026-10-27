using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A choropleth map with a linked ranked list: every region of an area (the states of a country) is shaded by the chosen
/// metric in quantile steps, and the same regions are ranked beside the map with a bar each. Pointing at a region outlines
/// it, shows a card with every metric and finds its row; pointing at a legend step lights that step's regions; choosing a
/// region flies the camera into it and shows its subregions (counties) re-binned and re-ranked, or says that there is no
/// finer detail. Switching the metric sweeps the new colours across the map from west to east while the rows slide to their
/// new ranks; with several areas a switch changes the country.
/// <para>The block contains no map data: each <see cref="ChoroplethRegion"/> brings its outline as SVG path data in the
/// area's view box (975 × 610 by default).</para>
/// <code>
/// &lt;arc:ChoroplethExplorer Description="Fiscal year ending March 31, 2027"&gt;
///     &lt;arc:ChoroplethExplorer.Metrics&gt;
///         &lt;arc:ChoroplethMetric Key="revenue" Label="Revenue" HeadlineLabel="Annual recurring revenue" /&gt;
///         &lt;arc:ChoroplethMetric Key="growth" Label="Growth" Format="Change" Scale="Diverging" Aggregate="WeightedAverage" /&gt;
///     &lt;/arc:ChoroplethExplorer.Metrics&gt;
///     &lt;arc:ChoroplethArea Key="us" Name="United States" RegionNoun="states" RegionNounSingular="state" /&gt;
/// &lt;/arc:ChoroplethExplorer&gt;
/// </code>
/// <para>Keyboard: Tab reaches the area switch, the metric switch, the back button (while zoomed), each legend step and the
/// list (one stop). In the list ↑ / ↓ / Home / End move, Enter or Space opens a region or pins a subregion; on a legend step
/// they pin it. Escape releases a pinned legend step first, then zooms back out.</para>
/// </summary>
[TemplatePart(HeaderPart, typeof(Avalonia.Controls.Grid))]
[TemplatePart(ControlsPart, typeof(Avalonia.Controls.Grid))]
[TemplatePart(AreaSwitchPart, typeof(MorphSelect))]
[TemplatePart(MetricSwitchPart, typeof(SegmentedControl))]
[TemplatePart(BodyPart, typeof(ChoroplethBodyPanel))]
[TemplatePart(MapPart, typeof(ChoroplethMapView))]
[TemplatePart(LegendPart, typeof(ChoroplethLegend))]
[TemplatePart(ListPart, typeof(ChoroplethRankList))]
[PseudoClasses(WidePseudoClass, CompactPseudoClass, ZoomedPseudoClass, CardPseudoClass)]
public partial class ChoroplethExplorer : TemplatedControl
{
    internal const string HeaderPart = "PART_Header";
    internal const string ControlsPart = "PART_Controls";
    internal const string TitlePart = "PART_Title";
    internal const string AreaSwitchPart = "PART_AreaSwitch";
    internal const string MetricSwitchPart = "PART_MetricSwitch";
    internal const string BodyPart = "PART_Body";
    internal const string SummaryNamePart = "PART_SummaryName";
    internal const string HeadlinePart = "PART_Headline";
    internal const string HeadlineLabelPart = "PART_HeadlineLabel";
    internal const string BackPresencePart = "PART_BackPresence";
    internal const string BackPart = "PART_Back";
    internal const string MapPart = "PART_Map";
    internal const string LegendPart = "PART_Legend";
    internal const string LegendCaptionPart = "PART_LegendCaption";
    internal const string SideCountPart = "PART_SideCount";
    internal const string SideNotePart = "PART_SideNote";
    internal const string ScrollerPart = "PART_Scroller";
    internal const string ListPart = "PART_List";
    internal const string EmptyPart = "PART_Empty";
    internal const string EmptyTitlePart = "PART_EmptyTitle";
    internal const string EmptyTextPart = "PART_EmptyText";
    internal const string EmptyBackPart = "PART_EmptyBack";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string WidePseudoClass = ":wide";
    internal const string CompactPseudoClass = ":compact";
    internal const string ZoomedPseudoClass = ":zoomed";
    internal const string CardPseudoClass = ":card";

    /// <summary>The legend step index of subregions without a value (<see cref="PinnedBinIndex"/>).</summary>
    public const int NoDataBinIndex = -1;

    #region Properties

    /// <summary>The areas, when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Areas"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> AreasSourceProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, IEnumerable?>(nameof(AreasSource));

    /// <summary>The key of the area shown (two-way); null or an unknown key shows the first.</summary>
    public static readonly StyledProperty<string?> SelectedAreaKeyProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(SelectedAreaKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the metric the map is shaded by (two-way); null or an unknown key uses the first.</summary>
    public static readonly StyledProperty<string?> SelectedMetricKeyProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(SelectedMetricKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the opened region (two-way): setting it flies the camera in, null zooms back out.</summary>
    public static readonly StyledProperty<string?> OpenRegionKeyProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(OpenRegionKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the pinned subregion of the opened region (two-way): its outline stays and its row stays lit.</summary>
    public static readonly StyledProperty<string?> PinnedSubregionKeyProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(PinnedSubregionKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The pinned legend step (two-way): 0 is the lowest step, <see cref="NoDataBinIndex"/> the no-data step, null none.
    /// While a step is pinned only its regions keep their colour.
    /// </summary>
    public static readonly StyledProperty<int?> PinnedBinIndexProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, int?>(nameof(PinnedBinIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The heading; null uses the area's title, else <see cref="ChoroplethLabels.TitleFormat"/> ("Revenue by state").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(Title));

    /// <summary>The line under the heading (the period); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, string?>(nameof(Description));

    /// <summary>Draws the block as a bordered card (16 radius, surface); false (default) lays it flat on the page.</summary>
    public static readonly StyledProperty<bool> IsCardChromeProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, bool>(nameof(IsCardChrome));

    /// <summary>The hue of values (and of positive values on a diverging scale); null uses the accent, or a blue when the accent is grey.</summary>
    public static readonly StyledProperty<Color?> PositiveColorProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, Color?>(nameof(PositiveColor));

    /// <summary>The hue of negative values on a diverging scale; null uses an amber opposite the positive hue.</summary>
    public static readonly StyledProperty<Color?> NegativeColorProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, Color?>(nameof(NegativeColor));

    /// <summary>The fill of subregions without a value; null uses the border colour.</summary>
    public static readonly StyledProperty<Color?> NoDataColorProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, Color?>(nameof(NoDataColor));

    /// <summary>Every word the block writes or speaks besides the data.</summary>
    public static readonly StyledProperty<ChoroplethLabels> LabelsProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, ChoroplethLabels>(nameof(Labels), new ChoroplethLabels());

    /// <summary>Run when a region opens (parameter: the region) or the map zooms out (parameter: null), after <see cref="RegionOpened"/>.</summary>
    public static readonly StyledProperty<ICommand?> RegionOpenedCommandProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, ICommand?>(nameof(RegionOpenedCommand));

    /// <summary>Run when a subregion is pinned (parameter: the subregion) or released (parameter: null).</summary>
    public static readonly StyledProperty<ICommand?> SubregionPinnedCommandProperty =
        AvaloniaProperty.Register<ChoroplethExplorer, ICommand?>(nameof(SubregionPinnedCommand));

    /// <summary>The area being shown (resolved from <see cref="SelectedAreaKey"/>).</summary>
    public static readonly DirectProperty<ChoroplethExplorer, ChoroplethArea?> CurrentAreaProperty =
        AvaloniaProperty.RegisterDirect<ChoroplethExplorer, ChoroplethArea?>(nameof(CurrentArea), o => o.CurrentArea);

    /// <summary>The metric the map is shaded by (resolved from <see cref="SelectedMetricKey"/>).</summary>
    public static readonly DirectProperty<ChoroplethExplorer, ChoroplethMetric?> CurrentMetricProperty =
        AvaloniaProperty.RegisterDirect<ChoroplethExplorer, ChoroplethMetric?>(nameof(CurrentMetric), o => o.CurrentMetric);

    /// <summary>The opened region (resolved from <see cref="OpenRegionKey"/>); null at the area level.</summary>
    public static readonly DirectProperty<ChoroplethExplorer, ChoroplethRegion?> OpenRegionProperty =
        AvaloniaProperty.RegisterDirect<ChoroplethExplorer, ChoroplethRegion?>(nameof(OpenRegion), o => o.OpenRegion);

    #endregion

    #region Events

    /// <summary>Raised when the shown area changes (by the switch or by code).</summary>
    public static readonly RoutedEvent<ChoroplethSelectionChangedEventArgs> AreaChangedEvent =
        RoutedEvent.Register<ChoroplethExplorer, ChoroplethSelectionChangedEventArgs>(nameof(AreaChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the metric changes (by the switch or by code).</summary>
    public static readonly RoutedEvent<ChoroplethSelectionChangedEventArgs> MetricChangedEvent =
        RoutedEvent.Register<ChoroplethExplorer, ChoroplethSelectionChangedEventArgs>(nameof(MetricChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the camera starts towards a region, or back out (region null).</summary>
    public static readonly RoutedEvent<ChoroplethRegionOpenedEventArgs> RegionOpenedEvent =
        RoutedEvent.Register<ChoroplethExplorer, ChoroplethRegionOpenedEventArgs>(nameof(RegionOpened), RoutingStrategies.Bubble);

    /// <summary>Raised when a subregion is pinned or the pin is released.</summary>
    public static readonly RoutedEvent<ChoroplethSubregionPinnedEventArgs> SubregionPinnedEvent =
        RoutedEvent.Register<ChoroplethExplorer, ChoroplethSubregionPinnedEventArgs>(nameof(SubregionPinned), RoutingStrategies.Bubble);

    /// <summary>Raised when the shown area changes (by the switch or by code).</summary>
    public event EventHandler<ChoroplethSelectionChangedEventArgs>? AreaChanged
    {
        add => AddHandler(AreaChangedEvent, value);
        remove => RemoveHandler(AreaChangedEvent, value);
    }

    /// <summary>Raised when the metric changes (by the switch or by code).</summary>
    public event EventHandler<ChoroplethSelectionChangedEventArgs>? MetricChanged
    {
        add => AddHandler(MetricChangedEvent, value);
        remove => RemoveHandler(MetricChangedEvent, value);
    }

    /// <summary>Raised when the camera starts towards a region, or back out (region null).</summary>
    public event EventHandler<ChoroplethRegionOpenedEventArgs>? RegionOpened
    {
        add => AddHandler(RegionOpenedEvent, value);
        remove => RemoveHandler(RegionOpenedEvent, value);
    }

    /// <summary>Raised when a subregion is pinned or the pin is released.</summary>
    public event EventHandler<ChoroplethSubregionPinnedEventArgs>? SubregionPinned
    {
        add => AddHandler(SubregionPinnedEvent, value);
        remove => RemoveHandler(SubregionPinnedEvent, value);
    }

    #endregion

    private ChoroplethArea? _currentArea;
    private ChoroplethMetric? _currentMetric;
    private ChoroplethRegion? _openRegion;

    /// <summary>The areas (countries), in the switch's order, when no <see cref="AreasSource"/> is set.</summary>
    [Content]
    public AvaloniaList<ChoroplethArea> Areas { get; } = new();

    /// <summary>The metrics of every area that has none of its own, in the metric switch's order.</summary>
    public AvaloniaList<ChoroplethMetric> Metrics { get; } = new();

    /// <inheritdoc cref="AreasSourceProperty"/>
    public IEnumerable? AreasSource { get => GetValue(AreasSourceProperty); set => SetValue(AreasSourceProperty, value); }

    /// <inheritdoc cref="SelectedAreaKeyProperty"/>
    public string? SelectedAreaKey { get => GetValue(SelectedAreaKeyProperty); set => SetValue(SelectedAreaKeyProperty, value); }

    /// <inheritdoc cref="SelectedMetricKeyProperty"/>
    public string? SelectedMetricKey { get => GetValue(SelectedMetricKeyProperty); set => SetValue(SelectedMetricKeyProperty, value); }

    /// <inheritdoc cref="OpenRegionKeyProperty"/>
    public string? OpenRegionKey { get => GetValue(OpenRegionKeyProperty); set => SetValue(OpenRegionKeyProperty, value); }

    /// <inheritdoc cref="PinnedSubregionKeyProperty"/>
    public string? PinnedSubregionKey { get => GetValue(PinnedSubregionKeyProperty); set => SetValue(PinnedSubregionKeyProperty, value); }

    /// <inheritdoc cref="PinnedBinIndexProperty"/>
    public int? PinnedBinIndex { get => GetValue(PinnedBinIndexProperty); set => SetValue(PinnedBinIndexProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsCardChromeProperty"/>
    public bool IsCardChrome { get => GetValue(IsCardChromeProperty); set => SetValue(IsCardChromeProperty, value); }

    /// <inheritdoc cref="PositiveColorProperty"/>
    public Color? PositiveColor { get => GetValue(PositiveColorProperty); set => SetValue(PositiveColorProperty, value); }

    /// <inheritdoc cref="NegativeColorProperty"/>
    public Color? NegativeColor { get => GetValue(NegativeColorProperty); set => SetValue(NegativeColorProperty, value); }

    /// <inheritdoc cref="NoDataColorProperty"/>
    public Color? NoDataColor { get => GetValue(NoDataColorProperty); set => SetValue(NoDataColorProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ChoroplethLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="RegionOpenedCommandProperty"/>
    public ICommand? RegionOpenedCommand { get => GetValue(RegionOpenedCommandProperty); set => SetValue(RegionOpenedCommandProperty, value); }

    /// <inheritdoc cref="SubregionPinnedCommandProperty"/>
    public ICommand? SubregionPinnedCommand { get => GetValue(SubregionPinnedCommandProperty); set => SetValue(SubregionPinnedCommandProperty, value); }

    /// <inheritdoc cref="CurrentAreaProperty"/>
    public ChoroplethArea? CurrentArea { get => _currentArea; private set => SetAndRaise(CurrentAreaProperty, ref _currentArea, value); }

    /// <inheritdoc cref="CurrentMetricProperty"/>
    public ChoroplethMetric? CurrentMetric { get => _currentMetric; private set => SetAndRaise(CurrentMetricProperty, ref _currentMetric, value); }

    /// <inheritdoc cref="OpenRegionProperty"/>
    public ChoroplethRegion? OpenRegion { get => _openRegion; private set => SetAndRaise(OpenRegionProperty, ref _openRegion, value); }
}
