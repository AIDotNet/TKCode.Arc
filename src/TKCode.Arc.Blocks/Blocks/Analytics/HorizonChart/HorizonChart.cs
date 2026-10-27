using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Horizon chart: many daily series compared by how far each one strays from its own baseline, every series folded into
/// one 28px band strip so a screenful of rows (markets, services, stores) reads at a glance. The distance above the
/// baseline fills in one colour and the distance below in another; the strip is cut into bands that are stacked into one
/// row height, and a darker colour means further away. One synchronized crosshair crosses every row and writes that day's
/// value for each of them; the rows sort by name, by their largest swing or by their latest value, and the band count (1 to
/// 4) folds or unfolds every row on one spring.
/// <code>
/// &lt;arc:HorizonChart Title="Orders against forecast" BaselineLabel="forecast" Days="{Binding Days}"&gt;
///     &lt;arc:HorizonSeries Name="Northgate" Values="{Binding NorthgateValues}" /&gt;
/// &lt;/arc:HorizonChart&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys on the chart: Tab lands on it and puts the crosshair on the latest day; ←/→ move it one day, Shift with an arrow
/// one week, Home / End go to the first and last day, Escape hides it. The two switches move their selection with the arrow
/// keys (wrapping) and select as they move. With the pointer, move over the bands to read a day; touch taps and drags set
/// the crosshair and leave it where you lift.
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(ControlsPart, typeof(Panel))]
[TemplatePart(SortGroupPart, typeof(Control))]
[TemplatePart(BandsGroupPart, typeof(Control))]
[TemplatePart(SortLabelPart, typeof(TextBlock))]
[TemplatePart(BandsLabelPart, typeof(TextBlock))]
[TemplatePart(SortSwitchPart, typeof(SegmentedControl))]
[TemplatePart(BandsSwitchPart, typeof(SegmentedControl))]
[TemplatePart(DatePart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(LegendPart, typeof(Control))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(CompactPseudoClass, MediumPseudoClass, ScrubbingPseudoClass, EmptyPseudoClass)]
public partial class HorizonChart : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string ControlsPart = "PART_Controls";
    public const string SortGroupPart = "PART_SortGroup";
    public const string BandsGroupPart = "PART_BandsGroup";
    public const string SortLabelPart = "PART_SortLabel";
    public const string BandsLabelPart = "PART_BandsLabel";
    public const string SortSwitchPart = "PART_SortSwitch";
    public const string BandsSwitchPart = "PART_BandsSwitch";
    public const string DatePart = "PART_Date";
    public const string StatusPart = "PART_Status";
    public const string LegendPart = "PART_Legend";
    public const string PlotPart = "PART_Plot";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is at most 560 wide: smaller rows, 16px sides, the switches and the legend take whole lines.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is at most 900 wide: narrower name and value columns.</summary>
    public const string MediumPseudoClass = ":medium";

    /// <summary>Set while the crosshair is on a day.</summary>
    public const string ScrubbingPseudoClass = ":scrubbing";

    /// <summary>Set while there are no rows.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<HorizonChart, string?>(nameof(Title));

    /// <summary>The line under the heading (what the rows are and what they are compared with); null or empty leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<HorizonChart, string?>(nameof(Description));

    /// <summary>The rows as a list you own (followed when it raises collection changes); it wins over <see cref="Series"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> SeriesSourceProperty =
        AvaloniaProperty.Register<HorizonChart, IEnumerable?>(nameof(SeriesSource));

    /// <summary>The days of the axis, oldest first; each row's values line up with them. Empty takes the longest row and numbers the days.</summary>
    public static readonly StyledProperty<IReadOnlyList<HorizonDay>?> DaysProperty =
        AvaloniaProperty.Register<HorizonChart, IReadOnlyList<HorizonDay>?>(nameof(Days));

    /// <summary>The labelled ticks of the axis; null puts one on the first day and on every first of a month (days that carry a date).</summary>
    public static readonly StyledProperty<IReadOnlyList<HorizonTick>?> TicksProperty =
        AvaloniaProperty.Register<HorizonChart, IReadOnlyList<HorizonTick>?>(nameof(Ticks));

    /// <summary>What the values are measured against ("winter forecast"), in the caption and the spoken texts; null takes <see cref="HorizonChartLabels.BaselineText"/>.</summary>
    public static readonly StyledProperty<string?> BaselineLabelProperty =
        AvaloniaProperty.Register<HorizonChart, string?>(nameof(BaselineLabel));

    /// <summary>The order of the rows (two-way). Default <see cref="HorizonSort.Peak"/>.</summary>
    public static readonly StyledProperty<HorizonSort> SortProperty =
        AvaloniaProperty.Register<HorizonChart, HorizonSort>(nameof(Sort), HorizonSort.Peak, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many bands each row folds into, 1 to 4 (two-way; other values are coerced). One band is an ordinary area chart. Default 3.</summary>
    public static readonly StyledProperty<int> BandsProperty =
        AvaloniaProperty.Register<HorizonChart, int>(nameof(Bands), 3, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceBands);

    /// <summary>The distance from the baseline that fills every band; NaN (default) takes the largest absolute value rounded up to a multiple of 12.</summary>
    public static readonly StyledProperty<double> ExtentProperty =
        AvaloniaProperty.Register<HorizonChart, double>(nameof(Extent), double.NaN);

    /// <summary>The day the crosshair is on (two-way); null hides it and shows the latest day.</summary>
    public static readonly StyledProperty<int?> SelectedDayIndexProperty =
        AvaloniaProperty.Register<HorizonChart, int?>(nameof(SelectedDayIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Formats a value for the rows, the scale and the spoken texts; null gives a signed whole percent ("+12%", "−8%", "0%").</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<HorizonChart, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>The colour of the distance above the baseline; null takes the Series1 token.</summary>
    public static readonly StyledProperty<Color?> AboveColorProperty =
        AvaloniaProperty.Register<HorizonChart, Color?>(nameof(AboveColor));

    /// <summary>The colour of the distance below the baseline; null takes the Series3 token.</summary>
    public static readonly StyledProperty<Color?> BelowColorProperty =
        AvaloniaProperty.Register<HorizonChart, Color?>(nameof(BelowColor));

    /// <summary>Whether the sort switch is shown.</summary>
    public static readonly StyledProperty<bool> IsSortSwitchVisibleProperty =
        AvaloniaProperty.Register<HorizonChart, bool>(nameof(IsSortSwitchVisible), true);

    /// <summary>Whether the band switch is shown.</summary>
    public static readonly StyledProperty<bool> IsBandSwitchVisibleProperty =
        AvaloniaProperty.Register<HorizonChart, bool>(nameof(IsBandSwitchVisible), true);

    /// <summary>Whether the colour scale and its caption are shown.</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<HorizonChart, bool>(nameof(IsLegendVisible), true);

    /// <summary>The words of the block, for translation or rewording.</summary>
    public static readonly StyledProperty<HorizonChartLabels> LabelsProperty =
        AvaloniaProperty.Register<HorizonChart, HorizonChartLabels>(nameof(Labels), new HorizonChartLabels());

    /// <summary>Run when the order changes, with the new <see cref="HorizonSort"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> SortCommandProperty =
        AvaloniaProperty.Register<HorizonChart, ICommand?>(nameof(SortCommand));

    /// <summary>Run when the band count changes, with the new count as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> BandsCommandProperty =
        AvaloniaProperty.Register<HorizonChart, ICommand?>(nameof(BandsCommand));

    /// <summary>Run when the crosshair moves to a day or is cleared, with the day's index (or null) as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> DayCommandProperty =
        AvaloniaProperty.Register<HorizonChart, ICommand?>(nameof(DayCommand));

    #endregion

    #region Events

    /// <summary>Raised after the order changed.</summary>
    public static readonly RoutedEvent<HorizonSortChangedEventArgs> SortChangedEvent =
        RoutedEvent.Register<HorizonChart, HorizonSortChangedEventArgs>(nameof(SortChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after the band count changed.</summary>
    public static readonly RoutedEvent<HorizonBandsChangedEventArgs> BandsChangedEvent =
        RoutedEvent.Register<HorizonChart, HorizonBandsChangedEventArgs>(nameof(BandsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the crosshair moves to another day or is cleared.</summary>
    public static readonly RoutedEvent<HorizonDayChangedEventArgs> DayChangedEvent =
        RoutedEvent.Register<HorizonChart, HorizonDayChangedEventArgs>(nameof(DayChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SortChangedEvent"/>
    public event EventHandler<HorizonSortChangedEventArgs>? SortChanged
    {
        add => AddHandler(SortChangedEvent, value);
        remove => RemoveHandler(SortChangedEvent, value);
    }

    /// <inheritdoc cref="BandsChangedEvent"/>
    public event EventHandler<HorizonBandsChangedEventArgs>? BandsChanged
    {
        add => AddHandler(BandsChangedEvent, value);
        remove => RemoveHandler(BandsChangedEvent, value);
    }

    /// <inheritdoc cref="DayChangedEvent"/>
    public event EventHandler<HorizonDayChangedEventArgs>? DayChanged
    {
        add => AddHandler(DayChangedEvent, value);
        remove => RemoveHandler(DayChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SeriesSourceProperty"/>
    public IEnumerable? SeriesSource { get => GetValue(SeriesSourceProperty); set => SetValue(SeriesSourceProperty, value); }

    /// <summary>The rows, in data order (ignored while <see cref="SeriesSource"/> is set).</summary>
    [Content]
    public AvaloniaList<HorizonSeries> Series { get; } = new();

    /// <inheritdoc cref="DaysProperty"/>
    public IReadOnlyList<HorizonDay>? Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }

    /// <inheritdoc cref="TicksProperty"/>
    public IReadOnlyList<HorizonTick>? Ticks { get => GetValue(TicksProperty); set => SetValue(TicksProperty, value); }

    /// <inheritdoc cref="BaselineLabelProperty"/>
    public string? BaselineLabel { get => GetValue(BaselineLabelProperty); set => SetValue(BaselineLabelProperty, value); }

    /// <inheritdoc cref="SortProperty"/>
    public HorizonSort Sort { get => GetValue(SortProperty); set => SetValue(SortProperty, value); }

    /// <inheritdoc cref="BandsProperty"/>
    public int Bands { get => GetValue(BandsProperty); set => SetValue(BandsProperty, value); }

    /// <inheritdoc cref="ExtentProperty"/>
    public double Extent { get => GetValue(ExtentProperty); set => SetValue(ExtentProperty, value); }

    /// <inheritdoc cref="SelectedDayIndexProperty"/>
    public int? SelectedDayIndex { get => GetValue(SelectedDayIndexProperty); set => SetValue(SelectedDayIndexProperty, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="AboveColorProperty"/>
    public Color? AboveColor { get => GetValue(AboveColorProperty); set => SetValue(AboveColorProperty, value); }

    /// <inheritdoc cref="BelowColorProperty"/>
    public Color? BelowColor { get => GetValue(BelowColorProperty); set => SetValue(BelowColorProperty, value); }

    /// <inheritdoc cref="IsSortSwitchVisibleProperty"/>
    public bool IsSortSwitchVisible { get => GetValue(IsSortSwitchVisibleProperty); set => SetValue(IsSortSwitchVisibleProperty, value); }

    /// <inheritdoc cref="IsBandSwitchVisibleProperty"/>
    public bool IsBandSwitchVisible { get => GetValue(IsBandSwitchVisibleProperty); set => SetValue(IsBandSwitchVisibleProperty, value); }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public HorizonChartLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SortCommandProperty"/>
    public ICommand? SortCommand { get => GetValue(SortCommandProperty); set => SetValue(SortCommandProperty, value); }

    /// <inheritdoc cref="BandsCommandProperty"/>
    public ICommand? BandsCommand { get => GetValue(BandsCommandProperty); set => SetValue(BandsCommandProperty, value); }

    /// <inheritdoc cref="DayCommandProperty"/>
    public ICommand? DayCommand { get => GetValue(DayCommandProperty); set => SetValue(DayCommandProperty, value); }

    #endregion

    private static int CoerceBands(AvaloniaObject sender, int value) => Math.Clamp(value, 1, HorizonEngine.MaxBands);
}
