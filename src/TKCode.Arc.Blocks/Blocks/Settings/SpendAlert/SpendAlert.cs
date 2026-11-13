using System.Globalization;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A spending budget with alerts: what was spent against the budget for the period, the pace the period is on, a meter
/// with a tick for every alert, an editor for the budget and the alerts, and a banner when spending crosses one. Raising
/// <see cref="Spent"/> past a switched-on alert shows the banner, announces it and raises <see cref="ThresholdCrossed"/>.
/// <code>
/// &lt;arc:SpendAlert Title="Dining out" Period="November" Budget="600" Spent="412.80" DaysElapsed="18" DaysInPeriod="30"&gt;
///     &lt;arc:SpendThreshold Share="0.5" /&gt;
///     &lt;arc:SpendThreshold Share="0.8" /&gt;
/// &lt;/arc:SpendAlert&gt;
/// </code>
/// </summary>
[TemplatePart(MeterPart, typeof(SpendAlertMeter))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(BudgetPart, typeof(NumberField))]
[TemplatePart(AddPart, typeof(Button))]
[TemplatePart(BannerPart, typeof(SpendAlertBanner))]
[TemplatePart(DismissPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(AlertingPseudoClass, CompactPseudoClass)]
public class SpendAlert : TemplatedControl
{
    public const string MeterPart = "PART_Meter";
    public const string RowsPart = "PART_Rows";
    public const string BudgetPart = "PART_Budget";
    public const string AddPart = "PART_Add";
    public const string BannerPart = "PART_Banner";
    public const string DismissPart = "PART_Dismiss";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The alert banner is shown.</summary>
    public const string AlertingPseudoClass = ":alerting";

    /// <summary>The block is narrower than 520 px: the budget field and the pace line move under their neighbors.</summary>
    public const string CompactPseudoClass = ":compact";

    private const double CompactWidth = 520;
    private const double ShareFieldWidth = 112;

    // Lucide "bell-ring" and "x" (ISC).
    private static readonly Geometry s_bell = ArcIcons.Parse(
        "M10.268 21a2 2 0 0 0 3.464 0 M22 8c0-2.3-.8-4.3-2-6 M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326 M4 2C2.8 3.7 2 5.7 2 8");

    #region Properties

    /// <summary>What the budget is for ("Dining out").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SpendAlert, string?>(nameof(Title), "Monthly budget");

    /// <summary>The period under the title ("November").</summary>
    public static readonly StyledProperty<string?> PeriodProperty =
        AvaloniaProperty.Register<SpendAlert, string?>(nameof(Period));

    /// <summary>The budget for the period (two-way: the budget field edits it).</summary>
    public static readonly StyledProperty<decimal> BudgetProperty =
        AvaloniaProperty.Register<SpendAlert, decimal>(nameof(Budget), 500m, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => Math.Max(0, v));

    /// <summary>What has been spent in the period so far. A rise past a switched-on alert raises it.</summary>
    public static readonly StyledProperty<decimal> SpentProperty =
        AvaloniaProperty.Register<SpendAlert, decimal>(nameof(Spent), coerce: (_, v) => Math.Max(0, v));

    /// <summary>Days of the period that have passed (for the pace).</summary>
    public static readonly StyledProperty<int> DaysElapsedProperty =
        AvaloniaProperty.Register<SpendAlert, int>(nameof(DaysElapsed));

    /// <summary>Days in the period; 0 hides the pace.</summary>
    public static readonly StyledProperty<int> DaysInPeriodProperty =
        AvaloniaProperty.Register<SpendAlert, int>(nameof(DaysInPeriod));

    /// <summary>The symbol written before amounts ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<SpendAlert, string>(nameof(CurrencySymbol), "$");

    /// <summary>Whether the budget and the alerts can be edited in the block (the editor is hidden when false).</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<SpendAlert, bool>(nameof(IsEditable), true);

    /// <summary>Most alerts the editor lets people add (5).</summary>
    public static readonly StyledProperty<int> MaxThresholdsProperty =
        AvaloniaProperty.Register<SpendAlert, int>(nameof(MaxThresholds), 5, coerce: (_, v) => Math.Max(1, v));

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<SpendAlertLabels> LabelsProperty =
        AvaloniaProperty.Register<SpendAlert, SpendAlertLabels>(nameof(Labels), new SpendAlertLabels());

    /// <summary>How the budget is doing.</summary>
    public static readonly DirectProperty<SpendAlert, SpendAlertStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<SpendAlert, SpendAlertStatus>(nameof(Status), o => o.Status);

    /// <summary>The alert the banner shows, or null when no banner is shown.</summary>
    public static readonly DirectProperty<SpendAlert, SpendThreshold?> RaisedThresholdProperty =
        AvaloniaProperty.RegisterDirect<SpendAlert, SpendThreshold?>(nameof(RaisedThreshold), o => o.RaisedThreshold);

    #endregion

    #region Events

    /// <summary>Raised when <see cref="Spent"/> rises past one or more switched-on alerts; carries the highest.</summary>
    public static readonly RoutedEvent<SpendThresholdCrossedEventArgs> ThresholdCrossedEvent =
        RoutedEvent.Register<SpendAlert, SpendThresholdCrossedEventArgs>(nameof(ThresholdCrossed), RoutingStrategies.Bubble);

    /// <summary>Raised when an alert is added, removed, switched or moved in the editor.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ThresholdsEditedEvent =
        RoutedEvent.Register<SpendAlert, RoutedEventArgs>(nameof(ThresholdsEdited), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ThresholdCrossedEvent"/>
    public event EventHandler<SpendThresholdCrossedEventArgs>? ThresholdCrossed
    {
        add => AddHandler(ThresholdCrossedEvent, value);
        remove => RemoveHandler(ThresholdCrossedEvent, value);
    }

    /// <inheritdoc cref="ThresholdsEditedEvent"/>
    public event EventHandler<RoutedEventArgs>? ThresholdsEdited
    {
        add => AddHandler(ThresholdsEditedEvent, value);
        remove => RemoveHandler(ThresholdsEditedEvent, value);
    }

    #endregion

    private readonly List<Row> _rows = new();
    private INameScope? _scope;
    private SpendAlertMeter? _meter;
    private StackPanel? _rowsHost;
    private NumberField? _budget;
    private Button? _add;
    private SpendAlertBanner? _banner;
    private Button? _dismiss;
    private FinanceAnnouncer? _announcer;
    private SpendAlertStatus _status;
    private SpendThreshold? _raised;
    private bool _syncing;
    private bool _isCompact;

    private sealed class Row
    {
        public required SpendThreshold Threshold { get; init; }
        public required Switch Toggle { get; init; }
        public required NumberField Field { get; init; }
        public required Button Remove { get; init; }
        public required Control Host { get; init; }
    }

    static SpendAlert()
    {
        FocusableProperty.OverrideDefaultValue<SpendAlert>(false);
    }

    public SpendAlert()
    {
        Thresholds.CollectionChanged += OnThresholdsChanged;
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="PeriodProperty"/>
    public string? Period { get => GetValue(PeriodProperty); set => SetValue(PeriodProperty, value); }

    /// <inheritdoc cref="BudgetProperty"/>
    public decimal Budget { get => GetValue(BudgetProperty); set => SetValue(BudgetProperty, value); }

    /// <inheritdoc cref="SpentProperty"/>
    public decimal Spent { get => GetValue(SpentProperty); set => SetValue(SpentProperty, value); }

    /// <inheritdoc cref="DaysElapsedProperty"/>
    public int DaysElapsed { get => GetValue(DaysElapsedProperty); set => SetValue(DaysElapsedProperty, value); }

    /// <inheritdoc cref="DaysInPeriodProperty"/>
    public int DaysInPeriod { get => GetValue(DaysInPeriodProperty); set => SetValue(DaysInPeriodProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="MaxThresholdsProperty"/>
    public int MaxThresholds { get => GetValue(MaxThresholdsProperty); set => SetValue(MaxThresholdsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SpendAlertLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public SpendAlertStatus Status { get => _status; private set => SetAndRaise(StatusProperty, ref _status, value); }

    /// <inheritdoc cref="RaisedThresholdProperty"/>
    public SpendThreshold? RaisedThreshold { get => _raised; private set => SetAndRaise(RaisedThresholdProperty, ref _raised, value); }

    /// <summary>The alerts, in any order (the editor lists them by share).</summary>
    [Content]
    public AvaloniaList<SpendThreshold> Thresholds { get; } = new();

    /// <summary>Where spending lands at the end of the period at the pace so far.</summary>
    public decimal Projected => SpendAlertMath.Projected(Spent, DaysElapsed, DaysInPeriod);

    /// <summary>True while the block is narrower than 520 px.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>Adds <paramref name="amount"/> to <see cref="Spent"/> (a purchase); crossing an alert raises it.</summary>
    public void AddSpending(decimal amount) => SetCurrentValue(SpentProperty, Spent + amount);

    /// <summary>Hides the banner.</summary>
    public void DismissAlert()
    {
        RaisedThreshold = null;
        UpdateBanner();
    }

    /// <summary>Adds an alert 10 points above the highest one (or at 50%), up to <see cref="MaxThresholds"/>; returns it, or null at the limit.</summary>
    public SpendThreshold? AddThreshold()
    {
        if (Thresholds.Count >= MaxThresholds)
        {
            return null;
        }
        var highest = Thresholds.Count == 0 ? 0.4 : Thresholds.Max(t => t.Share);
        var threshold = new SpendThreshold(Math.Min(SpendAlertMath.MaxShare, Math.Round(highest + 0.1, 2)));
        Thresholds.Add(threshold);
        RaiseEvent(new RoutedEventArgs(ThresholdsEditedEvent));
        return threshold;
    }

    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        format.CurrencyNegativePattern = 1;
        return amount.ToString("C2", format);
    }

    private static string Percent(double share) => SpendAlertMeter.Percent(share);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_add is not null)
        {
            _add.Click -= OnAddClick;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click -= OnDismissClick;
        }
        if (_budget is not null)
        {
            _budget.PropertyChanged -= OnBudgetFieldChanged;
        }
        _rowsHost?.Children.Clear();
        _rows.Clear();
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _meter = _scope.Find<SpendAlertMeter>(MeterPart);
        _rowsHost = _scope.Find<StackPanel>(RowsPart);
        _budget = _scope.Find<NumberField>(BudgetPart);
        _add = _scope.Find<Button>(AddPart);
        _banner = _scope.Find<SpendAlertBanner>(BannerPart);
        _dismiss = _scope.Find<Button>(DismissPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_scope.Find<ArcGlyph>("PART_BannerGlyph") is { } glyph)
        {
            glyph.Data = s_bell;
        }
        if (_add is not null)
        {
            _add.Click += OnAddClick;
        }
        if (_dismiss is not null)
        {
            _dismiss.Click += OnDismissClick;
        }
        if (_budget is not null)
        {
            _budget.Minimum = 0;
            _budget.Maximum = 1_000_000_000m;
            _budget.Step = 10;
            _budget.PropertyChanged += OnBudgetFieldChanged;
        }
        UpdateTexts();
        BuildRows();
        UpdateFigures();
        UpdateBanner();
        UpdateCompact(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == SpentProperty)
        {
            var before = change.GetOldValue<decimal>();
            var after = change.GetNewValue<decimal>();
            UpdateFigures();
            CheckCrossing(before, after);
        }
        else if (p == BudgetProperty)
        {
            SyncBudgetField();
            UpdateFigures();
            if (RaisedThreshold is not null && SpendAlertMath.Reached(Thresholds, Spent, Budget) is null)
            {
                DismissAlert();
            }
            else
            {
                UpdateBanner();
            }
        }
        else if (p == DaysElapsedProperty || p == DaysInPeriodProperty || p == CurrencySymbolProperty)
        {
            UpdateFigures();
            UpdateBanner();
        }
        else if (p == TitleProperty || p == PeriodProperty)
        {
            UpdateTexts();
            UpdateBanner();
        }
        else if (p == LabelsProperty)
        {
            UpdateTexts();
            BuildRows();
            UpdateFigures();
            UpdateBanner();
        }
        else if (p == IsEditableProperty || p == MaxThresholdsProperty)
        {
            UpdateEditor();
        }
        else if (p == BoundsProperty)
        {
            UpdateCompact();
        }
    }

    private void OnThresholdsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.OldItems?.OfType<SpendThreshold>() ?? [])
        {
            item.PropertyChanged -= OnThresholdChanged;
        }
        foreach (var item in e.NewItems?.OfType<SpendThreshold>() ?? [])
        {
            item.PropertyChanged += OnThresholdChanged;
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var row in _rows)
            {
                row.Threshold.PropertyChanged -= OnThresholdChanged;
            }
            foreach (var item in Thresholds)
            {
                item.PropertyChanged -= OnThresholdChanged;
                item.PropertyChanged += OnThresholdChanged;
            }
        }
        if (RaisedThreshold is not null && !Thresholds.Contains(RaisedThreshold))
        {
            RaisedThreshold = null;
        }
        BuildRows();
        UpdateFigures();
        UpdateBanner();
    }

    private void OnThresholdChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not SpendThreshold threshold)
        {
            return;
        }
        if (!_syncing && _rows.FirstOrDefault(r => r.Threshold == threshold) is { } row)
        {
            SyncRow(row);
        }
        if (RaisedThreshold == threshold && (!threshold.IsEnabled || Spent < (decimal)threshold.Share * Budget))
        {
            RaisedThreshold = null;
        }
        UpdateFigures();
        UpdateBanner();
    }

    private void OnBudgetFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == NumberField.ValueProperty && !_syncing && _budget is not null)
        {
            SetCurrentValue(BudgetProperty, _budget.Value);
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (AddThreshold() is { } added && _rows.FirstOrDefault(r => r.Threshold == added) is { } row)
        {
            row.Field.Focus();
        }
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => DismissAlert();

    private void CheckCrossing(decimal before, decimal after)
    {
        if (SpendAlertMath.Crossed(Thresholds, before, after, Budget) is not { } crossed)
        {
            if (RaisedThreshold is not null && SpendAlertMath.Reached(Thresholds, after, Budget) is null)
            {
                DismissAlert();
            }
            else
            {
                UpdateBanner();
            }
            return;
        }
        RaisedThreshold = crossed;
        UpdateBanner();
        if (_banner is not null)
        {
            _announcer?.Announce(BannerTitle() + ". " + BannerBody());
        }
        RaiseEvent(new SpendThresholdCrossedEventArgs(ThresholdCrossedEvent, crossed, after, Budget));
    }

    private string BannerTitle() => Labels.BannerTitle(Percent(SpendAlertMath.Share(Spent, Budget)), Title ?? string.Empty);

    private string BannerBody()
    {
        var labels = Labels;
        if (Spent > Budget)
        {
            return labels.BannerOver(Money(Spent - Budget));
        }
        return labels.BannerBody(Percent(RaisedThreshold?.Share ?? 0), Money(Budget - Spent));
    }

    private void UpdateBanner()
    {
        var shown = RaisedThreshold is not null;
        PseudoClasses.Set(AlertingPseudoClass, shown);
        if (_banner is null)
        {
            return;
        }
        _banner.Classes.Set("shown", shown);
        _banner.Classes.Set("danger", Spent > Budget);
        if (shown)
        {
            SetText("PART_BannerTitle", BannerTitle());
            SetText("PART_BannerBody", BannerBody());
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText("PART_Title", Title);
        SetText("PART_Period", Period);
        if (Part<TextBlock>("PART_Period") is { } period)
        {
            period.IsVisible = !string.IsNullOrEmpty(Period);
        }
        SetText("PART_SpentWord", labels.Spent);
        SetText("PART_AlertsHeading", labels.AlertsHeading);
        SetText("PART_AlertsHint", labels.AlertsHint);
        if (_dismiss is not null)
        {
            _dismiss.Content = labels.Dismiss;
        }
        if (_add is not null)
        {
            _add.Content = labels.AddAlert;
        }
        if (_budget is not null)
        {
            _budget.Label = labels.Budget;
            _budget.Prefix = CurrencySymbol;
        }
        UpdateEditor();
    }

    private void UpdateEditor()
    {
        if (_add is not null)
        {
            _add.IsVisible = IsEditable;
            _add.IsEnabled = Thresholds.Count < MaxThresholds;
        }
        if (_budget is not null)
        {
            _budget.IsVisible = IsEditable;
            SyncBudgetField();
        }
        foreach (var row in _rows)
        {
            row.Toggle.IsEnabled = IsEditable;
            row.Field.IsReadOnly = !IsEditable;
            row.Remove.IsVisible = IsEditable;
        }
    }

    private void SyncBudgetField()
    {
        if (_budget is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _budget.Value = Budget;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateFigures()
    {
        var labels = Labels;
        var budget = Budget;
        var spent = Spent;
        var share = SpendAlertMath.Share(spent, budget);
        var projected = Projected;
        var status = SpendAlertMath.Status(Thresholds, spent, budget, projected);
        Status = status;

        SetText("PART_Spent", Money(spent));
        SetText("PART_OfBudget", labels.OfBudget(Money(budget)));
        var daysLeft = DaysInPeriod > 0 ? Math.Max(0, DaysInPeriod - DaysElapsed) : 0;
        SetText("PART_Left", spent > budget ? labels.OverBy(Money(spent - budget)) : labels.Left(Money(budget - spent), daysLeft));
        if (Part<TextBlock>("PART_Pace") is { } pace)
        {
            pace.IsVisible = DaysInPeriod > 0 && DaysElapsed > 0 && DaysElapsed < DaysInPeriod;
            pace.Text = labels.Pace(Money(projected));
        }
        SetText("PART_PillText", labels.Status(status));
        if (Part<SpendAlertPill>("PART_Pill") is { } pill)
        {
            pill.Classes.Set("at-risk", status == SpendAlertStatus.AtRisk);
            pill.Classes.Set("warning", status == SpendAlertStatus.Warning);
            pill.Classes.Set("over", status == SpendAlertStatus.Over);
        }
        if (_meter is not null)
        {
            var projectedShare = DaysInPeriod > 0 && DaysElapsed > 0 ? SpendAlertMath.Share(projected, budget) : share;
            var marks = Thresholds.Where(t => t.IsEnabled).Select(t => (t.Share, Reached: budget > 0 && spent >= (decimal)t.Share * budget)).ToList();
            var top = Math.Max(1, Math.Max(share, projectedShare));
            if (marks.Count > 0)
            {
                top = Math.Max(top, marks.Max(m => m.Share));
            }
            _meter.Scale = Math.Ceiling(top * 10 + 1) / 10;
            _meter.Fraction = share;
            _meter.Projected = projectedShare;
            _meter.Tone = status switch
            {
                SpendAlertStatus.Over => 2,
                SpendAlertStatus.Warning => 1,
                _ => 0,
            };
            _meter.SetMarks(marks);
            AutomationProperties.SetName(_meter, labels.MeterName(Money(spent), Money(budget), Percent(share)));
        }
        foreach (var row in _rows)
        {
            row.Host.Classes.Set("reached", row.Threshold.IsEnabled && budget > 0 && spent >= (decimal)row.Threshold.Share * budget);
        }
    }

    private void UpdateCompact(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactWidth : _isCompact;
        if (!force && compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    private void BuildRows()
    {
        if (_rowsHost is null)
        {
            return;
        }
        foreach (var row in _rows)
        {
            row.Toggle.IsCheckedChanged -= OnToggleChanged;
            row.Field.PropertyChanged -= OnFieldChanged;
            row.Remove.Click -= OnRemoveClick;
        }
        _rows.Clear();
        _rowsHost.Children.Clear();
        var labels = Labels;
        foreach (var threshold in Thresholds.OrderBy(t => t.Share))
        {
            var toggle = new Switch { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            var field = new NumberField
            {
                Minimum = (decimal)(SpendAlertMath.MinShare * 100),
                Maximum = (decimal)(SpendAlertMath.MaxShare * 100),
                Step = 5,
                Suffix = "%",
                IsLabelVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0)
            };
            field.Width = ShareFieldWidth;
            AutomationProperties.SetName(field, labels.ShareFieldName);
            var remove = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = new ArcGlyph { Data = ArcIcons.X, Size = 16 },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            var lead = Muted(new TextBlock { Text = labels.AlertAt, VerticalAlignment = VerticalAlignment.Center });
            var tail = Muted(new TextBlock { Text = labels.OfBudgetSuffix, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var host = new ArcShrinkRow { Children = { toggle, lead, field, tail, remove } };
            ArcShrinkRow.SetIsShrinkable(tail, true);
            var row = new Row { Threshold = threshold, Toggle = toggle, Field = field, Remove = remove, Host = host };
            SyncRow(row);
            toggle.IsCheckedChanged += OnToggleChanged;
            field.PropertyChanged += OnFieldChanged;
            remove.Click += OnRemoveClick;
            _rows.Add(row);
            _rowsHost.Children.Add(host);
        }
        UpdateEditor();
    }

    private void SyncRow(Row row)
    {
        _syncing = true;
        try
        {
            var labels = Labels;
            var percent = Percent(row.Threshold.Share);
            row.Toggle.IsChecked = row.Threshold.IsEnabled;
            row.Field.Value = (decimal)Math.Round(row.Threshold.Share * 100);
            row.Field.Opacity = row.Threshold.IsEnabled ? 1 : 0.55;
            AutomationProperties.SetName(row.Toggle, labels.ToggleName(percent));
            AutomationProperties.SetName(row.Remove, labels.RemoveName(percent));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _rows.FirstOrDefault(r => r.Toggle == sender) is not { } row)
        {
            return;
        }
        row.Threshold.IsEnabled = row.Toggle.IsChecked == true;
        RaiseEvent(new RoutedEventArgs(ThresholdsEditedEvent));
    }

    private void OnFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != NumberField.ValueProperty || _syncing || _rows.FirstOrDefault(r => r.Field == sender) is not { } row)
        {
            return;
        }
        row.Threshold.Share = (double)row.Field.Value / 100;
        RaiseEvent(new RoutedEventArgs(ThresholdsEditedEvent));
    }

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (_rows.FindIndex(r => r.Remove == sender) is var index and >= 0)
        {
            var threshold = _rows[index].Threshold;
            Thresholds.Remove(threshold);
            RaiseEvent(new RoutedEventArgs(ThresholdsEditedEvent));
            // Keep the keyboard in the list: the next row's remove button, else the previous one, else Add.
            if (_rows.Count > 0)
            {
                _rows[Math.Min(index, _rows.Count - 1)].Remove.Focus();
            }
            else
            {
                _add?.Focus();
            }
        }
    }

    private static TextBlock Muted(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return text;
    }

    private T? Part<T>(string name) where T : Control => _scope?.Find<T>(name);

    private void SetText(string part, string? text)
    {
        if (Part<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }
}
