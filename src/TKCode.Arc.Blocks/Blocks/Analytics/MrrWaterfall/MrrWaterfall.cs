using System.Collections;
using System.Windows.Input;
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
/// MRR waterfall: how recurring revenue moved over a period, as a bridge. One bar stands for the revenue at the start, five
/// floating bars add or take away (new business, expansion, reactivation, contraction, churn) and the last bar is the revenue
/// at the end. Above the chart the ending revenue rolls to its new value with the change since the start, beside net revenue
/// retention, the quick ratio and logo churn. Select a bar, with the pointer or the arrow keys, to list the accounts behind
/// it in the side panel; point at a bar to preview its list. Switching the period or the unit (currency or percent of the
/// start) moves bars, axis, labels, rows and numbers together on one spring.
/// <code>
/// &lt;arc:MrrWaterfall Title="Recurring revenue bridge" Scope="All tiers" PeriodsSource="{Binding Periods}" /&gt;
/// </code>
/// </summary>
/// <remarks>
/// Keys on the chart: Tab lands on the selected bar; ← ↑ and → ↓ move the selection and wrap around from the last bar to the
/// first, Home and End jump to the first and last bar. Every move selects. Each switch is one tab stop that moves with the
/// arrow keys.
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(Control))]
[TemplatePart(PeriodSwitchPart, typeof(SegmentedControl))]
[TemplatePart(ValueSwitchPart, typeof(SegmentedControl))]
[TemplatePart(SummaryPart, typeof(Panel))]
[TemplatePart(HeadlinePart, typeof(Control))]
[TemplatePart(StatsPart, typeof(Control))]
[TemplatePart(BodyPart, typeof(Panel))]
[TemplatePart(PlotPart, typeof(Control))]
[TemplatePart(PanelPart, typeof(Control))]
[TemplatePart(TablePart, typeof(Control))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(StackedPseudoClass, CompactSummaryPseudoClass, CompactAxisPseudoClass, TinyAxisPseudoClass, EmptyPseudoClass)]
public partial class MrrWaterfall : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string PeriodSwitchPart = "PART_PeriodSwitch";
    public const string ValueSwitchPart = "PART_ValueSwitch";
    public const string SummaryPart = "PART_Summary";
    public const string HeadlinePart = "PART_Headline";
    public const string StatsPart = "PART_Stats";
    public const string BodyPart = "PART_Body";
    public const string PlotPart = "PART_Plot";
    public const string PanelPart = "PART_Panel";
    public const string TablePart = "PART_Table";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block's content is narrower than 800: the account panel sits under the chart.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the content is narrower than 560: smaller headline and statistics sharing the row.</summary>
    public const string CompactSummaryPseudoClass = ":compact-summary";

    /// <summary>Set while a band is narrower than 72: short bar names and labels without the currency symbol.</summary>
    public const string CompactAxisPseudoClass = ":compact-axis";

    /// <summary>Set while a band is narrower than 48: the bar names are 10px.</summary>
    public const string TinyAxisPseudoClass = ":tiny-axis";

    /// <summary>Set while there is no period to show.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MrrWaterfall, string?>(nameof(Title));

    /// <summary>The first part of the line under the heading (what the numbers cover, "All tiers"); the period's caption follows it.</summary>
    public static readonly StyledProperty<string?> ScopeProperty =
        AvaloniaProperty.Register<MrrWaterfall, string?>(nameof(Scope));

    /// <summary>The periods as a list you own (followed when it raises collection changes); it wins over <see cref="Periods"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> PeriodsSourceProperty =
        AvaloniaProperty.Register<MrrWaterfall, IEnumerable?>(nameof(PeriodsSource));

    /// <summary>The period shown (two-way); null shows the last one.</summary>
    public static readonly StyledProperty<MrrPeriod?> SelectedPeriodProperty =
        AvaloniaProperty.Register<MrrWaterfall, MrrPeriod?>(nameof(SelectedPeriod), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether amounts are currency or percent of the starting revenue (two-way).</summary>
    public static readonly StyledProperty<MrrValueMode> ValueModeProperty =
        AvaloniaProperty.Register<MrrWaterfall, MrrValueMode>(nameof(ValueMode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected bar (two-way): 0 is the start bar, 1 the first movement, and the last bar is the end. Default 1.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<MrrWaterfall, int>(nameof(SelectedIndex), 1, defaultBindingMode: BindingMode.TwoWay,
            coerce: static (_, value) => Math.Max(0, value));

    /// <summary>The bar under the mouse pointer (read-only), or -1. Touch does not preview.</summary>
    public static readonly DirectProperty<MrrWaterfall, int> HoveredIndexProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, int>(nameof(HoveredIndex), o => o.HoveredIndex);

    /// <summary>The currency symbol of every amount ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<MrrWaterfall, string>(nameof(CurrencySymbol), "$");

    /// <summary>Formats a whole currency amount in text (screen reader names, the table); null groups thousands ("$412,380"). Rolling digits use <see cref="CurrencySymbol"/>.</summary>
    public static readonly StyledProperty<Func<double, string>?> CurrencyFormatterProperty =
        AvaloniaProperty.Register<MrrWaterfall, Func<double, string>?>(nameof(CurrencyFormatter));

    /// <summary>Formats a currency amount on the chart's bar labels (signed for steps); null gives one decimal in thousands ("$412.4k", "+$55.8k").</summary>
    public static readonly StyledProperty<Func<double, string>?> CompactFormatterProperty =
        AvaloniaProperty.Register<MrrWaterfall, Func<double, string>?>(nameof(CompactFormatter));

    /// <summary>Formats a percent-of-start value in percent units on the chart and in text (signed for steps); null gives one decimal ("+13.5%").</summary>
    public static readonly StyledProperty<Func<double, string>?> PercentFormatterProperty =
        AvaloniaProperty.Register<MrrWaterfall, Func<double, string>?>(nameof(PercentFormatter));

    /// <summary>Whether the unit switch (currency or percent of start) is shown.</summary>
    public static readonly StyledProperty<bool> IsValueModeSwitchVisibleProperty =
        AvaloniaProperty.Register<MrrWaterfall, bool>(nameof(IsValueModeSwitchVisible), true);

    /// <summary>Whether the period switch is shown (it is hidden for a single period anyway).</summary>
    public static readonly StyledProperty<bool> IsPeriodSwitchVisibleProperty =
        AvaloniaProperty.Register<MrrWaterfall, bool>(nameof(IsPeriodSwitchVisible), true);

    /// <summary>How many accounts the panel names before the rest are grouped in one row (default 5).</summary>
    public static readonly StyledProperty<int> MaxNamedAccountsProperty =
        AvaloniaProperty.Register<MrrWaterfall, int>(nameof(MaxNamedAccounts), 5);

    /// <summary>The words of the block (switch names, headline labels, panel texts, announcements).</summary>
    public static readonly StyledProperty<MrrWaterfallLabels> LabelsProperty =
        AvaloniaProperty.Register<MrrWaterfall, MrrWaterfallLabels>(nameof(Labels), new MrrWaterfallLabels());

    /// <summary>Run when the selected bar changes, with its <see cref="MrrSelectionChangedEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> SelectionCommandProperty =
        AvaloniaProperty.Register<MrrWaterfall, ICommand?>(nameof(SelectionCommand));

    /// <summary>Run when the period changes, with its <see cref="MrrPeriodChangedEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> PeriodCommandProperty =
        AvaloniaProperty.Register<MrrWaterfall, ICommand?>(nameof(PeriodCommand));

    /// <summary>The revenue at the end of the shown period.</summary>
    public static readonly DirectProperty<MrrWaterfall, double> EndingMrrProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double>(nameof(EndingMrr), o => o.EndingMrr);

    /// <summary>The revenue gained or lost over the period (end minus start).</summary>
    public static readonly DirectProperty<MrrWaterfall, double> NetChangeProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double>(nameof(NetChange), o => o.NetChange);

    /// <summary>The net change over the starting revenue (0.129 for +12.9%); null without a starting revenue.</summary>
    public static readonly DirectProperty<MrrWaterfall, double?> NetChangeRatioProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double?>(nameof(NetChangeRatio), o => o.NetChangeRatio);

    /// <summary>Net revenue retention (0.977 for 97.7%): start plus expansion minus contraction and churn, over the start; null without a starting revenue.</summary>
    public static readonly DirectProperty<MrrWaterfall, double?> NetRevenueRetentionProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double?>(nameof(NetRevenueRetention), o => o.NetRevenueRetention);

    /// <summary>The quick ratio: new, expansion and reactivation over contraction and churn; null when nothing was lost.</summary>
    public static readonly DirectProperty<MrrWaterfall, double?> QuickRatioProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double?>(nameof(QuickRatio), o => o.QuickRatio);

    /// <summary>Logo churn (0.095 for 9.5%): accounts lost over accounts at the start; null without any.</summary>
    public static readonly DirectProperty<MrrWaterfall, double?> LogoChurnProperty =
        AvaloniaProperty.RegisterDirect<MrrWaterfall, double?>(nameof(LogoChurn), o => o.LogoChurn);

    #endregion

    #region Events

    /// <summary>Raised when the selected bar changes (pointer, keys or code).</summary>
    public static readonly RoutedEvent<MrrSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<MrrWaterfall, MrrSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the shown period changes.</summary>
    public static readonly RoutedEvent<MrrPeriodChangedEventArgs> PeriodChangedEvent =
        RoutedEvent.Register<MrrWaterfall, MrrPeriodChangedEventArgs>(nameof(PeriodChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="ValueMode"/> changes.</summary>
    public static readonly RoutedEvent<MrrValueModeChangedEventArgs> ValueModeChangedEvent =
        RoutedEvent.Register<MrrWaterfall, MrrValueModeChangedEventArgs>(nameof(ValueModeChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<MrrSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="PeriodChangedEvent"/>
    public event EventHandler<MrrPeriodChangedEventArgs>? PeriodChanged
    {
        add => AddHandler(PeriodChangedEvent, value);
        remove => RemoveHandler(PeriodChangedEvent, value);
    }

    /// <inheritdoc cref="ValueModeChangedEvent"/>
    public event EventHandler<MrrValueModeChangedEventArgs>? ValueModeChanged
    {
        add => AddHandler(ValueModeChangedEvent, value);
        remove => RemoveHandler(ValueModeChangedEvent, value);
    }

    #endregion

    #region Accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="ScopeProperty"/>
    public string? Scope { get => GetValue(ScopeProperty); set => SetValue(ScopeProperty, value); }

    /// <inheritdoc cref="PeriodsSourceProperty"/>
    public IEnumerable? PeriodsSource { get => GetValue(PeriodsSourceProperty); set => SetValue(PeriodsSourceProperty, value); }

    /// <summary>The periods the switch offers, in order (ignored while <see cref="PeriodsSource"/> is set).</summary>
    [Content]
    public AvaloniaList<MrrPeriod> Periods { get; } = new();

    /// <inheritdoc cref="SelectedPeriodProperty"/>
    public MrrPeriod? SelectedPeriod { get => GetValue(SelectedPeriodProperty); set => SetValue(SelectedPeriodProperty, value); }

    /// <inheritdoc cref="ValueModeProperty"/>
    public MrrValueMode ValueMode { get => GetValue(ValueModeProperty); set => SetValue(ValueModeProperty, value); }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <inheritdoc cref="HoveredIndexProperty"/>
    public int HoveredIndex { get => _hoveredIndex; private set => SetAndRaise(HoveredIndexProperty, ref _hoveredIndex, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="CurrencyFormatterProperty"/>
    public Func<double, string>? CurrencyFormatter { get => GetValue(CurrencyFormatterProperty); set => SetValue(CurrencyFormatterProperty, value); }

    /// <inheritdoc cref="CompactFormatterProperty"/>
    public Func<double, string>? CompactFormatter { get => GetValue(CompactFormatterProperty); set => SetValue(CompactFormatterProperty, value); }

    /// <inheritdoc cref="PercentFormatterProperty"/>
    public Func<double, string>? PercentFormatter { get => GetValue(PercentFormatterProperty); set => SetValue(PercentFormatterProperty, value); }

    /// <inheritdoc cref="IsValueModeSwitchVisibleProperty"/>
    public bool IsValueModeSwitchVisible { get => GetValue(IsValueModeSwitchVisibleProperty); set => SetValue(IsValueModeSwitchVisibleProperty, value); }

    /// <inheritdoc cref="IsPeriodSwitchVisibleProperty"/>
    public bool IsPeriodSwitchVisible { get => GetValue(IsPeriodSwitchVisibleProperty); set => SetValue(IsPeriodSwitchVisibleProperty, value); }

    /// <inheritdoc cref="MaxNamedAccountsProperty"/>
    public int MaxNamedAccounts { get => GetValue(MaxNamedAccountsProperty); set => SetValue(MaxNamedAccountsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public MrrWaterfallLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SelectionCommandProperty"/>
    public ICommand? SelectionCommand { get => GetValue(SelectionCommandProperty); set => SetValue(SelectionCommandProperty, value); }

    /// <inheritdoc cref="PeriodCommandProperty"/>
    public ICommand? PeriodCommand { get => GetValue(PeriodCommandProperty); set => SetValue(PeriodCommandProperty, value); }

    /// <inheritdoc cref="EndingMrrProperty"/>
    public double EndingMrr { get => _endingMrr; private set => SetAndRaise(EndingMrrProperty, ref _endingMrr, value); }

    /// <inheritdoc cref="NetChangeProperty"/>
    public double NetChange { get => _netChange; private set => SetAndRaise(NetChangeProperty, ref _netChange, value); }

    /// <inheritdoc cref="NetChangeRatioProperty"/>
    public double? NetChangeRatio { get => _netChangeRatio; private set => SetAndRaise(NetChangeRatioProperty, ref _netChangeRatio, value); }

    /// <inheritdoc cref="NetRevenueRetentionProperty"/>
    public double? NetRevenueRetention { get => _netRevenueRetention; private set => SetAndRaise(NetRevenueRetentionProperty, ref _netRevenueRetention, value); }

    /// <inheritdoc cref="QuickRatioProperty"/>
    public double? QuickRatio { get => _quickRatio; private set => SetAndRaise(QuickRatioProperty, ref _quickRatio, value); }

    /// <inheritdoc cref="LogoChurnProperty"/>
    public double? LogoChurn { get => _logoChurn; private set => SetAndRaise(LogoChurnProperty, ref _logoChurn, value); }

    #endregion

    private int _hoveredIndex = -1;
    private double _endingMrr;
    private double _netChange;
    private double? _netChangeRatio;
    private double? _netRevenueRetention;
    private double? _quickRatio;
    private double? _logoChurn;
}
