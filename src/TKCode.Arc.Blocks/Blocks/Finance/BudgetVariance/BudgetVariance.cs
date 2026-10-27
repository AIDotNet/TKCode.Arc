using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Budget against actual spend by department as bullet charts on one shared scale, so every budget tick lines up down the
/// table. Each row shows three qualitative bands, the actual bar (the overspend past the tick in the danger color) and the
/// variance as a signed amount and share, colored by whether it is favorable; every figure is an expense, so spending under
/// budget is favorable. A period switch springs every bar and counts every number to the new window, a total row sums the
/// table, and a department opens in place to show its budget lines. Department figures and the total are summed from the
/// lines, so they always reconcile.
/// <code>
/// &lt;arc:BudgetVariance SelectedPeriod="month"&gt;
///     &lt;arc:BudgetVariance.Periods&gt;
///         &lt;arc:BudgetPeriod Id="month" Label="Month" Description="March 2026" Phrase=" in March" /&gt;
///     &lt;/arc:BudgetVariance.Periods&gt;
///     &lt;arc:BudgetDepartment Id="eng" Name="Engineering" Owner="Mara Quinn"&gt;
///         &lt;arc:BudgetLine Id="cloud" Name="Cloud hosting"&gt;
///             &lt;arc:BudgetAmount Period="month" Budget="120000" Actual="131500" /&gt;
///         &lt;/arc:BudgetLine&gt;
///     &lt;/arc:BudgetDepartment&gt;
/// &lt;/arc:BudgetVariance&gt;
/// </code>
/// </summary>
[TemplatePart(PeriodsPart, typeof(SegmentedControl))]
[TemplatePart(TablePart, typeof(StackPanel))]
[TemplatePart(LegendPart, typeof(WrapPanel))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(StackedPseudoClass)]
public partial class BudgetVariance : TemplatedControl
{
    public const string PeriodsPart = "PART_Periods";
    public const string TablePart = "PART_Table";
    public const string LegendPart = "PART_Legend";
    public const string DescriptionPart = "PART_Description";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The container is narrower than the table threshold: each row stacks its chart under its figures.</summary>
    public const string StackedPseudoClass = ":stacked";

    #region Properties

    /// <summary>The heading of the block.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<BudgetVariance, string?>(nameof(Title), "Budget vs actual");

    /// <summary>The line under the heading; null shows the selected period's <see cref="BudgetPeriod.Description"/>.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<BudgetVariance, string?>(nameof(Description));

    /// <summary>The <see cref="BudgetPeriod.Id"/> of the window shown (two-way); null or unknown shows the first period.</summary>
    public static readonly StyledProperty<string?> SelectedPeriodProperty =
        AvaloniaProperty.Register<BudgetVariance, string?>(nameof(SelectedPeriod), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The <see cref="BudgetDepartment.Id"/> of the department whose lines are open (two-way); null closes every department.</summary>
    public static readonly StyledProperty<string?> ExpandedDepartmentProperty =
        AvaloniaProperty.Register<BudgetVariance, string?>(nameof(ExpandedDepartment), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Lower edge of the tolerance band as a share of budget (0.9 = 90%).</summary>
    public static readonly StyledProperty<double> RangeLowProperty =
        AvaloniaProperty.Register<BudgetVariance, double>(nameof(RangeLow), 0.9);

    /// <summary>Upper edge of the tolerance band as a share of budget (1.1 = 110%); past it the band steps darker.</summary>
    public static readonly StyledProperty<double> RangeHighProperty =
        AvaloniaProperty.Register<BudgetVariance, double>(nameof(RangeHigh), 1.1);

    /// <summary>A variance smaller than this share of budget reads as on plan and stays neutral (0.005 = half a percent).</summary>
    public static readonly StyledProperty<double> OnPlanThresholdProperty =
        AvaloniaProperty.Register<BudgetVariance, double>(nameof(OnPlanThreshold), 0.005);

    /// <summary>The symbol before every amount; amounts shorten to k and M.</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<BudgetVariance, string>(nameof(Currency), "$");

    /// <summary>The block's built-in text (headers, legend, the words after shares and the screen reader sentences).</summary>
    public static readonly StyledProperty<BudgetVarianceLabels> LabelsProperty =
        AvaloniaProperty.Register<BudgetVariance, BudgetVarianceLabels>(nameof(Labels), new BudgetVarianceLabels());

    /// <summary>Whether the legend under the table is shown.</summary>
    public static readonly StyledProperty<bool> IsLegendVisibleProperty =
        AvaloniaProperty.Register<BudgetVariance, bool>(nameof(IsLegendVisible), true);

    /// <summary>The end of the shared scale as a share of budget: at least 1.2, rounded up to a tenth to fit the largest figure of any period.</summary>
    public static readonly DirectProperty<BudgetVariance, double> ScaleMaximumProperty =
        AvaloniaProperty.RegisterDirect<BudgetVariance, double>(nameof(ScaleMaximum), o => o.ScaleMaximum);

    internal static readonly StyledProperty<double> TableThresholdProperty =
        AvaloniaProperty.Register<BudgetVariance, double>(nameof(TableThreshold), 760);

    #endregion

    #region Events

    /// <summary>Raised when the reporting window changes.</summary>
    public static readonly RoutedEvent<BudgetPeriodChangedEventArgs> PeriodChangedEvent =
        RoutedEvent.Register<BudgetVariance, BudgetPeriodChangedEventArgs>(nameof(PeriodChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a department opens or closes; opening one closes the one that was open, which is reported first.</summary>
    public static readonly RoutedEvent<BudgetDepartmentToggledEventArgs> DepartmentToggledEvent =
        RoutedEvent.Register<BudgetVariance, BudgetDepartmentToggledEventArgs>(nameof(DepartmentToggled), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PeriodChangedEvent"/>
    public event EventHandler<BudgetPeriodChangedEventArgs>? PeriodChanged
    {
        add => AddHandler(PeriodChangedEvent, value);
        remove => RemoveHandler(PeriodChangedEvent, value);
    }

    /// <inheritdoc cref="DepartmentToggledEvent"/>
    public event EventHandler<BudgetDepartmentToggledEventArgs>? DepartmentToggled
    {
        add => AddHandler(DepartmentToggledEvent, value);
        remove => RemoveHandler(DepartmentToggledEvent, value);
    }

    #endregion

    private SegmentedControl? _periods;
    private StackPanel? _table;
    private WrapPanel? _legend;
    private TextBlock? _description;
    private FinanceAnnouncer? _announcer;
    private double _scaleMaximum = 1.2;
    private bool _updatingPeriods;
    private bool _isStacked;
    private string? _shownPeriod;

    public BudgetVariance()
    {
        Departments.CollectionChanged += OnDataChanged;
        Periods.CollectionChanged += OnDataChanged;
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="SelectedPeriodProperty"/>
    public string? SelectedPeriod { get => GetValue(SelectedPeriodProperty); set => SetValue(SelectedPeriodProperty, value); }

    /// <inheritdoc cref="ExpandedDepartmentProperty"/>
    public string? ExpandedDepartment { get => GetValue(ExpandedDepartmentProperty); set => SetValue(ExpandedDepartmentProperty, value); }

    /// <inheritdoc cref="RangeLowProperty"/>
    public double RangeLow { get => GetValue(RangeLowProperty); set => SetValue(RangeLowProperty, value); }

    /// <inheritdoc cref="RangeHighProperty"/>
    public double RangeHigh { get => GetValue(RangeHighProperty); set => SetValue(RangeHighProperty, value); }

    /// <inheritdoc cref="OnPlanThresholdProperty"/>
    public double OnPlanThreshold { get => GetValue(OnPlanThresholdProperty); set => SetValue(OnPlanThresholdProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public BudgetVarianceLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsLegendVisibleProperty"/>
    public bool IsLegendVisible { get => GetValue(IsLegendVisibleProperty); set => SetValue(IsLegendVisibleProperty, value); }

    /// <inheritdoc cref="ScaleMaximumProperty"/>
    public double ScaleMaximum { get => _scaleMaximum; private set => SetAndRaise(ScaleMaximumProperty, ref _scaleMaximum, value); }

    internal double TableThreshold { get => GetValue(TableThresholdProperty); set => SetValue(TableThresholdProperty, value); }

    /// <summary>The departments in table order (a collection change rebuilds the table).</summary>
    [Content]
    public AvaloniaList<BudgetDepartment> Departments { get; } = new();

    /// <summary>The reporting windows offered by the switch, in order.</summary>
    public AvaloniaList<BudgetPeriod> Periods { get; } = new();

    /// <summary>The period shown: <see cref="SelectedPeriod"/> when it names one of <see cref="Periods"/>, else the first.</summary>
    public BudgetPeriod? EffectivePeriod =>
        Periods.FirstOrDefault(p => p.Id == SelectedPeriod) ?? (Periods.Count > 0 ? Periods[0] : null);

    /// <summary>True while the container is narrower than the table threshold (each row stacks its chart under its figures).</summary>
    public bool IsStacked => _isStacked;

    /// <summary>The summed budget and actual spend of every department in <paramref name="period"/>.</summary>
    public (double Budget, double Actual) TotalIn(string period)
    {
        double budget = 0, actual = 0;
        foreach (var department in Departments)
        {
            var (b, a) = department.In(period);
            budget += b;
            actual += a;
        }
        return (budget, actual);
    }

    /// <summary>How <paramref name="actual"/> compares with <paramref name="budget"/> under <see cref="OnPlanThreshold"/>.</summary>
    public BudgetVarianceKind KindOf(double budget, double actual)
    {
        if (budget <= 0)
        {
            return actual > 0 ? BudgetVarianceKind.Over : BudgetVarianceKind.OnPlan;
        }
        var share = (actual - budget) / budget;
        if (Math.Abs(share) < OnPlanThreshold)
        {
            return BudgetVarianceKind.OnPlan;
        }
        return share > 0 ? BudgetVarianceKind.Over : BudgetVarianceKind.Under;
    }

    /// <summary>The screen reader sentence of a figure in the shown period (the same sentence the rows carry).</summary>
    public string SentenceFor(string name, double budget, double actual)
    {
        var labels = Labels;
        var phrase = EffectivePeriod?.Phrase is { Length: > 0 } p ? " " + p.TrimStart() : string.Empty;
        var variance = actual - budget;
        var share = budget > 0 ? variance / budget : 0;
        return labels.Sentence(name, FinanceFormat.Compact(actual, Currency), FinanceFormat.Compact(budget, Currency), phrase,
            FinanceFormat.Compact(Math.Abs(variance), Currency), FinanceFormat.Percent(share), KindOf(budget, actual));
    }

    /// <summary>Opens <paramref name="departmentId"/> (closing the open one) or closes it when it is open.</summary>
    public void ToggleDepartment(string departmentId)
        => SetCurrentValue(ExpandedDepartmentProperty, ExpandedDepartment == departmentId ? null : departmentId);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_periods is not null)
        {
            _periods.SelectionChanged -= OnPeriodsSelectionChanged;
        }
        _table?.Children.Clear();
        _legend?.Children.Clear();
        base.OnApplyTemplate(e);

        var scope = e.NameScope;
        _periods = scope.Find<SegmentedControl>(PeriodsPart);
        _table = scope.Find<StackPanel>(TablePart);
        _legend = scope.Find<WrapPanel>(LegendPart);
        _description = scope.Find<TextBlock>(DescriptionPart);
        _announcer = scope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_periods is not null)
        {
            _periods.SelectionChanged += OnPeriodsSelectionChanged;
        }
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SelectedPeriodProperty)
        {
            OnSelectedPeriodChanged();
        }
        else if (property == ExpandedDepartmentProperty)
        {
            OnExpandedChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (property == CurrencyProperty || property == LabelsProperty || property == OnPlanThresholdProperty)
        {
            Rebuild();
        }
        else if (property == RangeLowProperty || property == RangeHighProperty)
        {
            ApplyFigures(animate: false);
            BuildLegend();
        }
        else if (property == DescriptionProperty)
        {
            UpdateDescription();
        }
        else if (property == IsLegendVisibleProperty && _legend is not null)
        {
            _legend.IsVisible = IsLegendVisible;
        }
        else if (property == BoundsProperty || property == TableThresholdProperty)
        {
            UpdateStacked();
        }
    }

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnPeriodsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingPeriods || _periods?.SelectedValue is not string id)
        {
            return;
        }
        SetCurrentValue(SelectedPeriodProperty, id);
    }

    private void OnSelectedPeriodChanged()
    {
        var period = EffectivePeriod?.Id;
        SyncPeriodSwitch();
        UpdateDescription();
        if (period == _shownPeriod)
        {
            return;
        }
        var old = _shownPeriod;
        _shownPeriod = period;
        ApplyFigures(animate: true);
        if (old is not null && period is not null)
        {
            var (budget, actual) = TotalIn(period);
            _announcer?.Announce(Labels.PeriodAnnouncement(EffectivePeriod!.Label, SentenceFor(Labels.Total, budget, actual)));
            RaiseEvent(new BudgetPeriodChangedEventArgs(PeriodChangedEvent, old, period));
        }
    }

    private void OnExpandedChanged(string? oldId, string? newId)
    {
        var closed = _departments.FirstOrDefault(d => d.Department.Id == oldId);
        var opened = _departments.FirstOrDefault(d => d.Department.Id == newId);
        if (closed is not null)
        {
            SetExpanded(closed, false);
            RaiseEvent(new BudgetDepartmentToggledEventArgs(DepartmentToggledEvent, closed.Department, false));
        }
        if (opened is not null)
        {
            SetExpanded(opened, true);
            RaiseEvent(new BudgetDepartmentToggledEventArgs(DepartmentToggledEvent, opened.Department, true));
        }
    }

    private void SyncPeriodSwitch()
    {
        if (_periods is null)
        {
            return;
        }
        _updatingPeriods = true;
        try
        {
            var current = _periods.ItemsSource as IReadOnlyList<SegmentedOption>;
            if (current is null || current.Count != Periods.Count || !current.Select(o => o.Value as string).SequenceEqual(Periods.Select(p => p.Id))
                || !current.Select(o => o.Label).SequenceEqual(Periods.Select(p => p.Label)))
            {
                _periods.ItemsSource = Periods.Select(p => new SegmentedOption(p.Id, p.Label)).ToList();
            }
            _periods.SelectedValue = EffectivePeriod?.Id;
            _periods.IsVisible = Periods.Count > 1;
            AutomationProperties.SetName(_periods, Labels.PeriodSwitch);
        }
        finally
        {
            _updatingPeriods = false;
        }
    }

    private void UpdateDescription()
    {
        if (_description is null)
        {
            return;
        }
        var text = Description ?? EffectivePeriod?.Description;
        _description.Text = text;
        _description.IsVisible = !string.IsNullOrEmpty(text);
    }

    private void UpdateStacked(bool force = false)
    {
        var width = Bounds.Width;
        var stacked = width > 0 ? width < TableThreshold : _isStacked;
        if (!force && stacked == _isStacked)
        {
            return;
        }
        _isStacked = stacked;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        foreach (var panel in _rowPanels)
        {
            panel.IsTable = !stacked;
        }
        foreach (var figure in AllFigures())
        {
            figure.ApplyStacked(stacked);
        }
        if (_headerLead is not null)
        {
            _headerLead.IsVisible = !stacked;
        }
        foreach (var header in _columnHeaders)
        {
            header.IsVisible = !stacked;
        }
    }

    private void Rebuild()
    {
        if (_table is null)
        {
            return;
        }
        _shownPeriod = EffectivePeriod?.Id;
        SyncPeriodSwitch();
        UpdateDescription();
        ScaleMaximum = ComputeScaleMaximum();
        BuildTable();
        BuildLegend();
        ApplyFigures(animate: false);
        UpdateStacked(force: true);
    }

    /// <summary>At least 120% of budget, rounded up to a tenth so the largest figure of any period fits.</summary>
    private double ComputeScaleMaximum()
    {
        double max = 0;
        void Consider(double budget, double actual)
        {
            if (budget > 0)
            {
                max = Math.Max(max, actual / budget);
            }
        }
        foreach (var period in Periods)
        {
            var total = TotalIn(period.Id);
            Consider(total.Budget, total.Actual);
            foreach (var department in Departments)
            {
                var d = department.In(period.Id);
                Consider(d.Budget, d.Actual);
                foreach (var line in department.Lines)
                {
                    var l = line.In(period.Id);
                    Consider(l.Budget, l.Actual);
                }
            }
        }
        return Math.Max(1.2, Math.Ceiling(max * 10 - 1e-9) / 10);
    }

    private IEnumerable<BudgetFigure> AllFigures()
    {
        if (_total is not null)
        {
            yield return _total;
        }
        foreach (var department in _departments)
        {
            yield return department.Figure;
            foreach (var line in department.Lines)
            {
                yield return line;
            }
        }
    }

    private void ApplyFigures(bool animate)
    {
        var period = EffectivePeriod?.Id;
        if (period is null)
        {
            return;
        }
        var scale = ScaleMaximum;
        foreach (var figure in AllFigures())
        {
            var (budget, actual) = figure.Amounts(period);
            figure.Apply(budget, actual, scale, RangeLow, RangeHigh, KindOf(budget, actual), SentenceFor(figure.Name, budget, actual), animate);
        }
    }

    private void SetExpanded(BudgetDepartmentView view, bool expanded)
    {
        view.Host.Classes.Set("expanded", expanded);
        view.Chevron.Classes.Set("expanded", expanded);
        view.LinesHost.Classes.Set("expanded", expanded);
        view.Disclosure.IsExpanded = expanded;
        view.Button.IsExpanded = expanded;
        view.LinesHost.IsHitTestVisible = expanded;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _departments.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var index = _departments.FindIndex(d => d.Button.IsFocused);
        if (index < 0)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Down => Math.Min(index + 1, _departments.Count - 1),
            Key.Up => Math.Max(index - 1, 0),
            Key.Home => 0,
            Key.End => _departments.Count - 1,
            _ => -1,
        };
        if (target < 0)
        {
            return;
        }
        _departments[target].Button.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }
}
