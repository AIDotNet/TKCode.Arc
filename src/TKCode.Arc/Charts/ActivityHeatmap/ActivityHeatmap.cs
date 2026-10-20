using System.Collections;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A contribution-style calendar (Arc "Activity heatmap"): one square per day, weeks as columns, four accent tints for
/// how busy a day was. Cells wave in once on first view; a single tooltip glides between cells on hover or keyboard
/// focus; a new range recolors the grid in a diagonal sweep instead of redrawing it; the legend previews (hover, focus)
/// or pins (click) one level. ↑ / ↓ move by day, ← / → by week, Home / End to the ends, Enter / Space select.
/// </summary>
public class ActivityHeatmap : Control
{
    /// <summary>Defines the <see cref="ItemsSource"/> property.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ActivityHeatmap, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Defines the <see cref="DateSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, DateOnly>?> DateSelectorProperty =
        AvaloniaProperty.Register<ActivityHeatmap, Func<object, DateOnly>?>(nameof(DateSelector));

    /// <summary>Defines the <see cref="CountSelector"/> property.</summary>
    public static readonly StyledProperty<Func<object, double>?> CountSelectorProperty =
        AvaloniaProperty.Register<ActivityHeatmap, Func<object, double>?>(nameof(CountSelector));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActivityHeatmap, string?>(nameof(Label));

    /// <summary>Defines the <see cref="Period"/> property.</summary>
    public static readonly StyledProperty<string?> PeriodProperty =
        AvaloniaProperty.Register<ActivityHeatmap, string?>(nameof(Period));

    /// <summary>Defines the <see cref="UnitOne"/> property.</summary>
    public static readonly StyledProperty<string> UnitOneProperty =
        AvaloniaProperty.Register<ActivityHeatmap, string>(nameof(UnitOne), "contribution");

    /// <summary>Defines the <see cref="UnitOther"/> property.</summary>
    public static readonly StyledProperty<string> UnitOtherProperty =
        AvaloniaProperty.Register<ActivityHeatmap, string>(nameof(UnitOther), "contributions");

    /// <summary>Defines the <see cref="Thresholds"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ThresholdsProperty =
        AvaloniaProperty.Register<ActivityHeatmap, IReadOnlyList<double>?>(nameof(Thresholds));

    /// <summary>Defines the <see cref="FirstDayOfWeek"/> property.</summary>
    public static readonly StyledProperty<DayOfWeek> FirstDayOfWeekProperty =
        AvaloniaProperty.Register<ActivityHeatmap, DayOfWeek>(nameof(FirstDayOfWeek), DayOfWeek.Sunday);

    /// <summary>Defines the <see cref="Culture"/> property.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<ActivityHeatmap, CultureInfo?>(nameof(Culture));

    /// <summary>Defines the <see cref="SelectedDate"/> property.</summary>
    public static readonly StyledProperty<DateOnly?> SelectedDateProperty =
        AvaloniaProperty.Register<ActivityHeatmap, DateOnly?>(nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<ActivityHeatmap, object?>(nameof(Actions));

    internal static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(CellGap), 3);

    internal static readonly StyledProperty<double> CellMinProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(CellMin), 9);

    internal static readonly StyledProperty<double> CellMaxProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(CellMax), 15);

    internal static readonly StyledProperty<double> WeekdayColumnProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(WeekdayColumn), 30);

    private readonly ChartsBPalette _palette;
    private readonly ChartsBItemsWatcher _watcher;
    private readonly ChartsBMemberReader _reader = new();
    private readonly AvaloniaList<ActivityDay> _items = new();
    private readonly RollingDigits _total;
    private readonly TextBlock _noun;
    private readonly TextBlock _in;
    private readonly ArcContentSwap _period;
    private readonly ContentPresenter _actions;
    private readonly ActivityHeatmapGrid _grid;
    private readonly ActivityHeatmapLegend _legend;
    private BindingBase? _dateBinding;
    private BindingBase? _countBinding;
    private ActivityHeatmapModel _model = ActivityHeatmapModel.Build([], DayOfWeek.Sunday, null, CultureInfo.GetCultureInfo("en-US"));
    private int? _preview;
    private int? _pinned;

    /// <summary>Creates an activity heatmap.</summary>
    public ActivityHeatmap()
    {
        _palette = new ChartsBPalette(this, OnPaletteChanged);
        _total = new RollingDigits { UseGrouping = true };
        ArcTokens.Bind(_total, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_total, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_total, RollingDigits.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(_total, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _noun = Text(ArcDesignTokenKind.Foreground, ArcDesignTokenKind.WeightMedium);
        _in = Text(ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.WeightRegular);
        _in.Text = "in";
        _period = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap };
        ArcTokens.Bind(_period, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_period, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_period, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _actions = new ContentPresenter();
        _grid = new ActivityHeatmapGrid(this);
        _legend = new ActivityHeatmapLegend(this);
        foreach (var child in new Control[] { _total, _noun, _in, _period, _actions, _grid, _legend })
        {
            VisualChildren.Add(child);
            LogicalChildren.Add(child);
        }
        _watcher = new ChartsBItemsWatcher(Rebuild);
        _items.CollectionChanged += (_, _) =>
        {
            // While attached the watcher reports inline changes itself.
            if (ItemsSource is null && !_watcher.IsSubscribed)
            {
                Rebuild();
            }
        };
        _watcher.SetSource(_items);
    }

    static ActivityHeatmap()
    {
        AffectsMeasure<ActivityHeatmap>(CellGapProperty, CellMinProperty, CellMaxProperty, WeekdayColumnProperty);
    }

    /// <summary>Days: <see cref="ActivityDay"/> instances, or any objects read through the selectors or member bindings. Missing days count as zero.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Inline days, used when <see cref="ItemsSource"/> is not set.</summary>
    [Content]
    public AvaloniaList<ActivityDay> Items => _items;

    /// <summary>Reads the date of a data item (wins over <see cref="DateMemberBinding"/>).</summary>
    public Func<object, DateOnly>? DateSelector
    {
        get => GetValue(DateSelectorProperty);
        set => SetValue(DateSelectorProperty, value);
    }

    /// <summary>Reads the count of a data item.</summary>
    public Func<object, double>? CountSelector
    {
        get => GetValue(CountSelectorProperty);
        set => SetValue(CountSelectorProperty, value);
    }

    /// <summary>Binding (member path) that reads each item's date (<see cref="DateOnly"/>, <see cref="DateTime"/> or "yyyy-MM-dd").</summary>
    [AssignBinding]
    public BindingBase? DateMemberBinding
    {
        get => _dateBinding;
        set
        {
            _dateBinding = value;
            Rebuild();
        }
    }

    /// <summary>Binding that reads each item's count.</summary>
    [AssignBinding]
    public BindingBase? CountMemberBinding
    {
        get => _countBinding;
        set
        {
            _countBinding = value;
            Rebuild();
        }
    }

    /// <summary>Accessible name of the grid, such as "Contributions in 2025".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Finishes the summary line: "1,284 contributions in {Period}".</summary>
    public string? Period
    {
        get => GetValue(PeriodProperty);
        set => SetValue(PeriodProperty, value);
    }

    /// <summary>Singular noun for the count (default "contribution").</summary>
    public string UnitOne
    {
        get => GetValue(UnitOneProperty);
        set => SetValue(UnitOneProperty, value);
    }

    /// <summary>Plural noun for the count (default "contributions").</summary>
    public string UnitOther
    {
        get => GetValue(UnitOtherProperty);
        set => SetValue(UnitOtherProperty, value);
    }

    /// <summary>Upper bounds of levels one to three; above the last is level four. Default: quarters of the busiest day.</summary>
    public IReadOnlyList<double>? Thresholds
    {
        get => GetValue(ThresholdsProperty);
        set => SetValue(ThresholdsProperty, value);
    }

    /// <summary>First row of every week column (default Sunday).</summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    /// <summary>Formatting culture for dates and numbers (default en-US, like Arc).</summary>
    public CultureInfo? Culture
    {
        get => GetValue(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    /// <summary>The selected day (two-way); a ring glides to it.</summary>
    public DateOnly? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    /// <summary>Controls beside the summary, such as a range switch.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Raised when a day is chosen with a click, Enter or Space.</summary>
    public event EventHandler<ActivityHeatmapDateSelectedEventArgs>? DateSelected;

    internal double CellGap
    {
        get => GetValue(CellGapProperty);
        set => SetValue(CellGapProperty, value);
    }

    internal double CellMin
    {
        get => GetValue(CellMinProperty);
        set => SetValue(CellMinProperty, value);
    }

    internal double CellMax
    {
        get => GetValue(CellMaxProperty);
        set => SetValue(CellMaxProperty, value);
    }

    internal double WeekdayColumn
    {
        get => GetValue(WeekdayColumnProperty);
        set => SetValue(WeekdayColumnProperty, value);
    }

    /// <summary>Sum of all counts in the range.</summary>
    public double Total => _model.Total;

    /// <summary>The three upper bounds in use (given or derived from the busiest day).</summary>
    public IReadOnlyList<double> EffectiveThresholds => _model.Thresholds;

    /// <summary>Summary line, such as "1,284 contributions in 2025".</summary>
    public string Summary => $"{Number(_model.Total)} {Noun(_model.Total)} in {Period}";

    internal ActivityHeatmapModel Model => _model;
    internal ChartsBPalette Palette => _palette;
    internal ActivityHeatmapGrid Grid => _grid;
    internal ActivityHeatmapLegend Legend => _legend;
    internal CultureInfo EffectiveCulture => Culture ?? CultureInfo.GetCultureInfo("en-US");

    /// <summary>The level the legend previews (hover, focus) or pins (click).</summary>
    internal int? Highlight => _preview ?? _pinned;

    internal int? PinnedLevel => _pinned;

    internal string Noun(double count) => count == 1 ? UnitOne : UnitOther;

    internal string Number(double value) => value.ToString("#,0.##", EffectiveCulture);

    /// <summary>Level ranges for the legend, such as "1 to 3 contributions".</summary>
    internal string RangeText(int level, bool brief)
    {
        var (a, b, c) = (_model.Thresholds[0], _model.Thresholds[1], _model.Thresholds[2]);
        var to = brief ? "–" : " to ";
        return level switch
        {
            0 => $"no {UnitOther}",
            1 => a == 1 ? $"1 {UnitOne}" : $"1{to}{Number(a)} {UnitOther}",
            2 => $"{Number(a + 1)}{to}{Number(b)} {UnitOther}",
            3 => $"{Number(b + 1)}{to}{Number(c)} {UnitOther}",
            _ => brief ? $"{Number(c + 1)}+ {UnitOther}" : $"{Number(c + 1)} or more {UnitOther}"
        };
    }

    internal string LegendDescription => $"Darker squares mean more {UnitOther}. Levels: {string.Join(", ", Enumerable.Range(0, 5).Select(l => RangeText(l, false)))}.";

    internal Color LevelColor(int level)
    {
        var level0 = ChartsBMath.MixIn(_palette.Foreground, 0.065, _palette.Surface);
        return level switch
        {
            1 => ChartsBMath.MixIn(_palette.Accent, 0.26, level0),
            2 => ChartsBMath.MixIn(_palette.Accent, 0.50, level0),
            3 => ChartsBMath.MixIn(_palette.Accent, 0.76, level0),
            4 => _palette.Accent,
            _ => level0
        };
    }

    internal void SetPreview(int? level)
    {
        if (_preview == level)
        {
            return;
        }
        _preview = level;
        OnHighlightChanged();
    }

    internal void TogglePin(int level)
    {
        _pinned = _pinned == level ? null : level;
        OnHighlightChanged();
    }

    private void OnHighlightChanged()
    {
        _grid.OnHighlightChanged();
        _legend.OnHighlightChanged();
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= _model.Length)
        {
            return;
        }
        var date = _model.DateAt(index);
        SelectedDate = date;
        DateSelected?.Invoke(this, new ActivityHeatmapDateSelectedEventArgs(date, _model.Counts[index], _model.Levels[index]));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _watcher.SetSource(ItemsSource ?? _items);
        }
        else if (change.Property == DateSelectorProperty || change.Property == CountSelectorProperty ||
                 change.Property == ThresholdsProperty || change.Property == FirstDayOfWeekProperty || change.Property == CultureProperty)
        {
            Rebuild();
        }
        else if (change.Property == PeriodProperty || change.Property == UnitOneProperty || change.Property == UnitOtherProperty)
        {
            UpdateHeader();
            _legend.InvalidateText();
            _grid.RefreshTooltip();
        }
        else if (change.Property == SelectedDateProperty)
        {
            _grid.OnSelectionChanged();
        }
        else if (change.Property == ActionsProperty)
        {
            _actions.Content = Actions;
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watcher.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watcher.Detach();
    }

    private TextBlock Text(ArcDesignTokenKind color, ArcDesignTokenKind weight)
    {
        var text = new TextBlock();
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(text, TextBlock.FontWeightProperty, weight);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
        return text;
    }

    private void OnPaletteChanged()
    {
        _grid?.OnPaletteChanged();
        _legend?.InvalidateText();
    }

    private void Rebuild()
    {
        var days = new List<ActivityDay>();
        foreach (var item in _watcher.Snapshot())
        {
            if (Resolve(item) is { } day)
            {
                days.Add(day);
            }
        }
        var previous = _model;
        _model = ActivityHeatmapModel.Build(days, FirstDayOfWeek, Thresholds, EffectiveCulture);
        UpdateHeader();
        _grid?.OnModelChanged(previous);
        _legend?.InvalidateText();
        InvalidateMeasure();
    }

    private ActivityDay? Resolve(object item)
    {
        if (item is ActivityDay day && DateSelector is null && CountSelector is null && _dateBinding is null && _countBinding is null)
        {
            return day;
        }
        var typed = item as ActivityDay;
        DateOnly? date = DateSelector is { } dateSelector ? dateSelector(item) : _dateBinding is null ? typed?.Date : ChartsBMemberReader.ToDate(_reader.Read(item, _dateBinding));
        if (date is null)
        {
            return null;
        }
        var count = CountSelector?.Invoke(item) ?? (_countBinding is null ? typed?.Count ?? 0 : ChartsBMemberReader.ToDouble(_reader.Read(item, _countBinding)));
        return new ActivityDay(date.Value, double.IsFinite(count) ? count : 0);
    }

    private void UpdateHeader()
    {
        if (_total is null)
        {
            return;
        }
        _total.CultureName = EffectiveCulture.Name.Length == 0 ? "en-US" : EffectiveCulture.Name;
        _total.Value = _model.Total;
        _noun.Text = Noun(_model.Total);
        _period.Content = Period;
        var hasPeriod = !string.IsNullOrEmpty(Period);
        _in.IsVisible = hasPeriod;
        _period.IsVisible = hasPeriod;
        AutomationProperties.SetHelpText(this, Summary);
    }

    /// <summary>Square size for a container width (Arc: clamp(9px, (100cqi − label − 53 gaps) / 53, 15px)).</summary>
    internal double CellSizeFor(double width)
        => ChartsBMath.Clamp((width - WeekdayColumn - CellGap * 53) / 53, CellMin, CellMax);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 860 : availableSize.Width;
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _total.Measure(unbounded);
        _noun.Measure(unbounded);
        _in.Measure(unbounded);
        _period.Measure(unbounded);
        _actions.Measure(new Size(width, double.PositiveInfinity));
        _grid.Measure(new Size(width, double.PositiveInfinity));
        _legend.Measure(new Size(width, double.PositiveInfinity));
        var header = HeaderHeight(width);
        return new Size(width, header + 16 + _grid.DesiredSize.Height + 16 + _legend.DesiredSize.Height);
    }

    private double SummaryWidth(double gap) => _total.DesiredSize.Width + gap + _noun.DesiredSize.Width +
                                               (_in.IsVisible ? gap + _in.DesiredSize.Width + gap + _period.DesiredSize.Width : 0);

    private double HeaderHeight(double width)
    {
        var line = Math.Max(Math.Max(_total.DesiredSize.Height, _noun.DesiredSize.Height), _period.DesiredSize.Height);
        var summary = SummaryWidth(_palette.TypeSM * 0.3);
        var actions = _actions.DesiredSize;
        return summary + 16 + actions.Width <= width ? Math.Max(line, actions.Height) : line + 12 + actions.Height;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var width = finalSize.Width;
        var gap = _palette.TypeSM * 0.3;
        var line = Math.Max(Math.Max(_total.DesiredSize.Height, _noun.DesiredSize.Height), _period.DesiredSize.Height);
        var header = HeaderHeight(width);
        var actions = _actions.DesiredSize;
        var inline = SummaryWidth(gap) + 16 + actions.Width <= width;
        var summaryTop = inline ? (header - line) / 2 : 0;
        var x = 0.0;
        // Baselines line up: every piece uses the same 14px body size.
        _total.Arrange(new Rect(x, summaryTop + (line - _total.DesiredSize.Height) / 2, _total.DesiredSize.Width, _total.DesiredSize.Height));
        x += _total.DesiredSize.Width + gap;
        _noun.Arrange(new Rect(x, summaryTop + (line - _noun.DesiredSize.Height) / 2, _noun.DesiredSize.Width, _noun.DesiredSize.Height));
        x += _noun.DesiredSize.Width + gap;
        if (_in.IsVisible)
        {
            _in.Arrange(new Rect(x, summaryTop + (line - _in.DesiredSize.Height) / 2, _in.DesiredSize.Width, _in.DesiredSize.Height));
            x += _in.DesiredSize.Width + gap;
            _period.Arrange(new Rect(x, summaryTop + (line - _period.DesiredSize.Height) / 2, _period.DesiredSize.Width, _period.DesiredSize.Height));
        }
        _actions.Arrange(inline
            ? new Rect(width - actions.Width, (header - actions.Height) / 2, actions.Width, actions.Height)
            : new Rect(0, line + 12, actions.Width, actions.Height));
        var y = header + 16;
        _grid.Arrange(new Rect(0, y, width, _grid.DesiredSize.Height));
        y += _grid.DesiredSize.Height + 16;
        _legend.Arrange(new Rect(0, y, width, _legend.DesiredSize.Height));
        return finalSize;
    }
}
