using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Many small line charts on one grid, one per group (a market, a plan, a product), all over the same periods, so the
/// grid is read like a single chart. Pointing at one panel moves a crosshair through all of them and the readout shows
/// that period's total; a toggle puts every panel on its own range (compare shapes) or on one range from zero (compare
/// sizes), with the axes springing between them; the order follows the latest value, the growth or the name, and panels
/// fly to their new places; each panel carries a faint dashed outline of the grand total; flat groups can be faded.
/// <code>
/// &lt;arc:SmallMultiples Title="Revenue by market" Description="Monthly net revenue" GrowthLag="12" /&gt;
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="Series"/> (or <see cref="SeriesSource"/>), each with one value per period in <see cref="Periods"/>
/// (or <see cref="PeriodsSource"/>), oldest first. Growth compares the latest value with the one
/// <see cref="GrowthLag"/> periods earlier; below <see cref="FlatThreshold"/> either way it counts as flat.
/// </para>
/// <para>
/// Keys: the panels are one tab stop. Focusing them with the keyboard places the crosshair on the latest period; ←/→
/// move it one period in every panel, Page up / Page down twelve, Home / End jump to the first or latest, Escape clears it.
/// </para>
/// </remarks>
[TemplatePart(SortSwitchPart, typeof(SegmentedControl))]
[TemplatePart(ScaleSwitchPart, typeof(SegmentedControl))]
[TemplatePart(DimSwitchPart, typeof(Switch))]
[TemplatePart(GridHostPart, typeof(Panel))]
[TemplatePart(FocusRingPart, typeof(Control))]
[TemplatePart(PeriodPart, typeof(TextBlock))]
[TemplatePart(StatePart, typeof(TextBlock))]
[TemplatePart(TotalPart, typeof(TextBlock))]
[TemplatePart(LiveRegionPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, PhonePseudoClass, EmptyPseudoClass)]
public partial class SmallMultiples : TemplatedControl
{
    public const string SortSwitchPart = "PART_SortSwitch";
    public const string ScaleSwitchPart = "PART_ScaleSwitch";
    public const string DimSwitchPart = "PART_DimSwitch";
    public const string GridHostPart = "PART_GridHost";
    public const string FocusRingPart = "PART_FocusRing";
    public const string PeriodPart = "PART_Period";
    public const string StatePart = "PART_State";
    public const string TotalPart = "PART_Total";
    public const string LiveRegionPart = "PART_LiveRegion";

    /// <summary>Set while the block is narrower than 640: two columns, the controls under the heading, the legend under the readout.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is narrower than 480: tighter paddings, each panel's value under its name.</summary>
    public const string PhonePseudoClass = ":phone";

    /// <summary>Set while there is no series or no period.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The series as a list to follow (observed when it raises collection changes); it wins over <see cref="Series"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> SeriesSourceProperty =
        AvaloniaProperty.Register<SmallMultiples, IEnumerable?>(nameof(SeriesSource));

    /// <summary>The periods as a list to follow; it wins over <see cref="Periods"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> PeriodsSourceProperty =
        AvaloniaProperty.Register<SmallMultiples, IEnumerable?>(nameof(PeriodsSource));

    /// <summary>The heading of the block; null hides it.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SmallMultiples, string?>(nameof(Title));

    /// <summary>The line under the heading (what is measured, over which periods); null hides it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<SmallMultiples, string?>(nameof(Description));

    /// <summary>The panel order (two-way). Default <see cref="SmallMultiplesSort.Latest"/>.</summary>
    public static readonly StyledProperty<SmallMultiplesSort> SortProperty =
        AvaloniaProperty.Register<SmallMultiples, SmallMultiplesSort>(nameof(Sort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// How the panels' y axes are set (two-way): <see cref="SmallMultiplesScale.Independent"/> (the default) gives each
    /// panel its own range, which makes shapes comparable; <see cref="SmallMultiplesScale.Shared"/> gives them all one
    /// range starting at zero, which makes sizes comparable.
    /// </summary>
    public static readonly StyledProperty<SmallMultiplesScale> ScaleProperty =
        AvaloniaProperty.Register<SmallMultiples, SmallMultiplesScale>(nameof(Scale), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Fades the panels whose growth counts as flat (two-way); on by default.</summary>
    public static readonly StyledProperty<bool> IsFlatDimmedProperty =
        AvaloniaProperty.Register<SmallMultiples, bool>(nameof(IsFlatDimmed), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The flat band: a growth whose size (up or down) is under this fraction is flat. 0.05 means ±5%.</summary>
    public static readonly StyledProperty<double> FlatThresholdProperty =
        AvaloniaProperty.Register<SmallMultiples, double>(nameof(FlatThreshold), 0.05);

    /// <summary>
    /// How many periods back the growth looks: the latest value is compared with the one this many periods before it
    /// (12 by default, a year for monthly data). When the data is shorter, the first period is used.
    /// </summary>
    public static readonly StyledProperty<int> GrowthLagProperty =
        AvaloniaProperty.Register<SmallMultiples, int>(nameof(GrowthLag), 12);

    /// <summary>
    /// Turns a value into text for the panel headings, the total and what screen readers hear. Without one, values are
    /// taken to be thousands of US dollars and written "$412k" or "$1.71M".
    /// </summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<SmallMultiples, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Turns a tick value into the short label at the right of each plot; without one, <see cref="ValueFormatter"/> is used.</summary>
    public static readonly StyledProperty<Func<double, string>?> TickFormatterProperty =
        AvaloniaProperty.Register<SmallMultiples, Func<double, string>?>(nameof(TickFormatter));

    /// <summary>The sort option for <see cref="SmallMultiplesSort.Latest"/>.</summary>
    public static readonly StyledProperty<string> LatestSortTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(LatestSortText), "Latest");

    /// <summary>The sort option for <see cref="SmallMultiplesSort.Growth"/>.</summary>
    public static readonly StyledProperty<string> GrowthSortTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(GrowthSortText), "Growth");

    /// <summary>The sort option for <see cref="SmallMultiplesSort.Name"/>.</summary>
    public static readonly StyledProperty<string> NameSortTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(NameSortText), "Name");

    /// <summary>The scale option for <see cref="SmallMultiplesScale.Independent"/>.</summary>
    public static readonly StyledProperty<string> IndependentScaleTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(IndependentScaleText), "Independent");

    /// <summary>The scale option for <see cref="SmallMultiplesScale.Shared"/>.</summary>
    public static readonly StyledProperty<string> SharedScaleTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(SharedScaleText), "Shared");

    /// <summary>The screen reader name of the sort control.</summary>
    public static readonly StyledProperty<string> SortLabelProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(SortLabel), "Sort panels");

    /// <summary>The screen reader name of the scale control.</summary>
    public static readonly StyledProperty<string> ScaleLabelProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(ScaleLabel), "Y scale");

    /// <summary>The dimming switch's text.</summary>
    public static readonly StyledProperty<string> DimFlatTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(DimFlatText), "Dim flat trends");

    /// <summary>Beside the period in the readout while no crosshair is shown.</summary>
    public static readonly StyledProperty<string> LatestTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(LatestText), "Latest");

    /// <summary>Beside the period in the readout while the crosshair is shown; {0} is its position, {1} the number of periods.</summary>
    public static readonly StyledProperty<string> PositionFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(PositionFormat), "{0} of {1}");

    /// <summary>The label of the total in the readout.</summary>
    public static readonly StyledProperty<string> TotalTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(TotalText), "Total");

    /// <summary>The legend's name for the solid line ("Market").</summary>
    public static readonly StyledProperty<string> SeriesLegendTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(SeriesLegendText), "Series");

    /// <summary>The legend's name for the dashed ghost of the total.</summary>
    public static readonly StyledProperty<string> GhostLegendTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(GhostLegendText), "All series, scaled to fit");

    /// <summary>The trend text of a flat series, before its growth ("Flat +0.6%").</summary>
    public static readonly StyledProperty<string> FlatTextProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(FlatText), "Flat");

    /// <summary>After the growth in each panel; {0} is the comparison period's short label ("vs Sep 2025").</summary>
    public static readonly StyledProperty<string> CompareFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(CompareFormat), "vs {0}");

    /// <summary>Screen reader trend words; {0} is the unsigned percentage and {1} the comparison text ("Up 19.9% vs Sep 2025").</summary>
    public static readonly StyledProperty<string> UpFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(UpFormat), "Up {0} {1}");

    /// <summary>Screen reader trend words for a fall; same placeholders as <see cref="UpFormat"/>.</summary>
    public static readonly StyledProperty<string> DownFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(DownFormat), "Down {0} {1}");

    /// <summary>The screen reader name of the panels.</summary>
    public static readonly StyledProperty<string> PanelsLabelProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(PanelsLabel), "Panels");

    /// <summary>The keyboard hint read when the panels get focus.</summary>
    public static readonly StyledProperty<string> KeyboardHintProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(KeyboardHint),
            "Use the left and right arrow keys to move through the periods, Home and End to jump, and Escape to clear.");

    /// <summary>Announced when the crosshair moves by keyboard; {0} period, {1} total, {2} the highest series, {3} its value.</summary>
    public static readonly StyledProperty<string> CrosshairAnnouncementFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(CrosshairAnnouncementFormat), "{0}. Total {1}. Highest {2}, {3}.");

    /// <summary>Announced after sorting; {0} is the sort option's text.</summary>
    public static readonly StyledProperty<string> SortAnnouncementFormatProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(SortAnnouncementFormat), "Sorted by {0}.");

    /// <summary>Announced after switching to the shared scale.</summary>
    public static readonly StyledProperty<string> SharedAnnouncementProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(SharedAnnouncement), "Shared scale. Panels compare size.");

    /// <summary>Announced after switching to the independent scale.</summary>
    public static readonly StyledProperty<string> IndependentAnnouncementProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(IndependentAnnouncement), "Independent scale. Panels compare shape.");

    /// <summary>Announced when flat trends are dimmed.</summary>
    public static readonly StyledProperty<string> DimOnAnnouncementProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(DimOnAnnouncement), "Flat trends dimmed.");

    /// <summary>Announced when flat trends are shown at full strength again.</summary>
    public static readonly StyledProperty<string> DimOffAnnouncementProperty =
        AvaloniaProperty.Register<SmallMultiples, string>(nameof(DimOffAnnouncement), "Flat trends shown at full strength.");

    /// <summary>The period under the synced crosshair; null while it is hidden.</summary>
    public static readonly DirectProperty<SmallMultiples, int?> CrosshairIndexProperty =
        AvaloniaProperty.RegisterDirect<SmallMultiples, int?>(nameof(CrosshairIndex), o => o.CrosshairIndex);

    #endregion

    #region Events

    /// <summary>Occurs once the panels have been put in a new order.</summary>
    public static readonly RoutedEvent<SmallMultiplesSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<SmallMultiples, SmallMultiplesSortChangedEventArgs>(nameof(SortChanged), RoutingStrategies.Bubble);

    /// <summary>Occurs once the y axes have switched to the other scale.</summary>
    public static readonly RoutedEvent<SmallMultiplesScaleChangedEventArgs> ScaleChangedEvent =
        RoutedEvent.Register<SmallMultiples, SmallMultiplesScaleChangedEventArgs>(nameof(ScaleChanged), RoutingStrategies.Bubble);

    /// <summary>Occurs each time the crosshair lands on another period; the index is null when the crosshair goes away.</summary>
    public static readonly RoutedEvent<SmallMultiplesIndexChangedEventArgs> CrosshairIndexChangedEvent =
        RoutedEvent.Register<SmallMultiples, SmallMultiplesIndexChangedEventArgs>(nameof(CrosshairIndexChanged), RoutingStrategies.Bubble);

    /// <summary>Occurs once the panels have been put in a new order.</summary>
    public event EventHandler<SmallMultiplesSortChangedEventArgs>? SortChanged
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    /// <summary>Occurs once the y axes have switched to the other scale.</summary>
    public event EventHandler<SmallMultiplesScaleChangedEventArgs>? ScaleChanged
    {
        add => AddHandler(ScaleChangedEvent, value);
        remove => RemoveHandler(ScaleChangedEvent, value);
    }

    /// <summary>Occurs each time the crosshair lands on another period; the index is null when the crosshair goes away.</summary>
    public event EventHandler<SmallMultiplesIndexChangedEventArgs>? CrosshairIndexChanged
    {
        add => AddHandler(CrosshairIndexChangedEvent, value);
        remove => RemoveHandler(CrosshairIndexChangedEvent, value);
    }

    #endregion

    private int? _crosshairIndex;

    /// <summary>One panel per series, when no <see cref="SeriesSource"/> is set.</summary>
    [Content]
    public AvaloniaList<SmallMultiplesSeries> Series { get; } = new();

    /// <summary>The periods on the shared x axis, oldest first, when no <see cref="PeriodsSource"/> is set.</summary>
    public AvaloniaList<SmallMultiplesPeriod> Periods { get; } = new();

    /// <inheritdoc cref="SeriesSourceProperty"/>
    public IEnumerable? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }

    /// <inheritdoc cref="PeriodsSourceProperty"/>
    public IEnumerable? PeriodsSource { get => GetValue(PeriodsSourceProperty); set => SetValue(PeriodsSourceProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SortProperty"/>
    public SmallMultiplesSort Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }

    /// <inheritdoc cref="ScaleProperty"/>
    public SmallMultiplesScale Scale { get => GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    /// <inheritdoc cref="IsFlatDimmedProperty"/>
    public bool IsFlatDimmed { get => GetValue(IsFlatDimmedProperty); set => SetValue(IsFlatDimmedProperty, value); }

    /// <inheritdoc cref="FlatThresholdProperty"/>
    public double FlatThreshold { get => GetValue(FlatThresholdProperty); set => SetValue(FlatThresholdProperty, value); }

    /// <inheritdoc cref="GrowthLagProperty"/>
    public int GrowthLag { get => GetValue(GrowthLagProperty); set => SetValue(GrowthLagProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="TickFormatterProperty"/>
    public Func<double, string>? TickFormatter { get => GetValue(TickFormatterProperty); set => SetValue(TickFormatterProperty, value); }

    /// <inheritdoc cref="LatestSortTextProperty"/>
    public string LatestSortText { get => GetValue(LatestSortTextProperty); set => SetValue(LatestSortTextProperty, value); }

    /// <inheritdoc cref="GrowthSortTextProperty"/>
    public string GrowthSortText { get => GetValue(GrowthSortTextProperty); set => SetValue(GrowthSortTextProperty, value); }

    /// <inheritdoc cref="NameSortTextProperty"/>
    public string NameSortText { get => GetValue(NameSortTextProperty); set => SetValue(NameSortTextProperty, value); }

    /// <inheritdoc cref="IndependentScaleTextProperty"/>
    public string IndependentScaleText
    {
        get => GetValue(IndependentScaleTextProperty);
        set => SetValue(IndependentScaleTextProperty, value);
    }

    /// <inheritdoc cref="SharedScaleTextProperty"/>
    public string SharedScaleText { get => GetValue(SharedScaleTextProperty); set => SetValue(SharedScaleTextProperty, value); }

    /// <inheritdoc cref="SortLabelProperty"/>
    public string SortLabel { get => GetValue(SortLabelProperty); set => SetValue(SortLabelProperty, value); }

    /// <inheritdoc cref="ScaleLabelProperty"/>
    public string ScaleLabel { get => GetValue(ScaleLabelProperty); set => SetValue(ScaleLabelProperty, value); }

    /// <inheritdoc cref="DimFlatTextProperty"/>
    public string DimFlatText { get => GetValue(DimFlatTextProperty); set => SetValue(DimFlatTextProperty, value); }

    /// <inheritdoc cref="LatestTextProperty"/>
    public string LatestText { get => GetValue(LatestTextProperty); set => SetValue(LatestTextProperty, value); }

    /// <inheritdoc cref="PositionFormatProperty"/>
    public string PositionFormat { get => GetValue(PositionFormatProperty); set => SetValue(PositionFormatProperty, value); }

    /// <inheritdoc cref="TotalTextProperty"/>
    public string TotalText { get => GetValue(TotalTextProperty); set => SetValue(TotalTextProperty, value); }

    /// <inheritdoc cref="SeriesLegendTextProperty"/>
    public string SeriesLegendText { get => GetValue(SeriesLegendTextProperty); set => SetValue(SeriesLegendTextProperty, value); }

    /// <inheritdoc cref="GhostLegendTextProperty"/>
    public string GhostLegendText { get => GetValue(GhostLegendTextProperty); set => SetValue(GhostLegendTextProperty, value); }

    /// <inheritdoc cref="FlatTextProperty"/>
    public string FlatText { get => GetValue(FlatTextProperty); set => SetValue(FlatTextProperty, value); }

    /// <inheritdoc cref="CompareFormatProperty"/>
    public string CompareFormat { get => GetValue(CompareFormatProperty); set => SetValue(CompareFormatProperty, value); }

    /// <inheritdoc cref="UpFormatProperty"/>
    public string UpFormat { get => GetValue(UpFormatProperty); set => SetValue(UpFormatProperty, value); }

    /// <inheritdoc cref="DownFormatProperty"/>
    public string DownFormat { get => GetValue(DownFormatProperty); set => SetValue(DownFormatProperty, value); }

    /// <inheritdoc cref="PanelsLabelProperty"/>
    public string PanelsLabel { get => GetValue(PanelsLabelProperty); set => SetValue(PanelsLabelProperty, value); }

    /// <inheritdoc cref="KeyboardHintProperty"/>
    public string KeyboardHint { get => GetValue(KeyboardHintProperty); set => SetValue(KeyboardHintProperty, value); }

    /// <inheritdoc cref="CrosshairAnnouncementFormatProperty"/>
    public string CrosshairAnnouncementFormat
    {
        get => GetValue(CrosshairAnnouncementFormatProperty);
        set => SetValue(CrosshairAnnouncementFormatProperty, value);
    }

    /// <inheritdoc cref="SortAnnouncementFormatProperty"/>
    public string SortAnnouncementFormat
    {
        get => GetValue(SortAnnouncementFormatProperty);
        set => SetValue(SortAnnouncementFormatProperty, value);
    }

    /// <inheritdoc cref="SharedAnnouncementProperty"/>
    public string SharedAnnouncement { get => GetValue(SharedAnnouncementProperty); set => SetValue(SharedAnnouncementProperty, value); }

    /// <inheritdoc cref="IndependentAnnouncementProperty"/>
    public string IndependentAnnouncement
    {
        get => GetValue(IndependentAnnouncementProperty);
        set => SetValue(IndependentAnnouncementProperty, value);
    }

    /// <inheritdoc cref="DimOnAnnouncementProperty"/>
    public string DimOnAnnouncement { get => GetValue(DimOnAnnouncementProperty); set => SetValue(DimOnAnnouncementProperty, value); }

    /// <inheritdoc cref="DimOffAnnouncementProperty"/>
    public string DimOffAnnouncement { get => GetValue(DimOffAnnouncementProperty); set => SetValue(DimOffAnnouncementProperty, value); }

    /// <inheritdoc cref="CrosshairIndexProperty"/>
    public int? CrosshairIndex { get => _crosshairIndex; private set => SetAndRaise(CrosshairIndexProperty, ref _crosshairIndex, value); }
}
