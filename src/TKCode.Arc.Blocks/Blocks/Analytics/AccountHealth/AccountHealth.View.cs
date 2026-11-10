using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(AnalyticsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass)]
public partial class AccountHealth
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (720) wide: tiles and lists stack.</summary>
    public const string CompactPseudoClass = ":compact";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AccountHealth, double>(nameof(CompactWidth), 720);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private double[]? _sampleHistory;
    private AccountHealthUsageMetric[]? _sampleUsage;
    private AccountHealthTicket[]? _sampleTickets;
    private AccountHealthRisk[]? _sampleRisks;
    private AccountHealthAction[]? _sampleActions;
    private bool _compact;
    private bool _syncing;

    private Decorator? _body;
    private AnalyticsAnnouncer? _announcer;
    private TextBlock? _name;
    private TextBlock? _subtitle;
    private Grid? _tiles;
    private TextBlock? _scoreText;
    private Badge? _bandBadge;
    private TextBlock? _changeText;
    private AccountHealthTrend? _trend;
    private TextBlock? _renewalDate;
    private TextBlock? _renewalLeft;
    private TextBlock? _ticketCount;
    private TextBlock? _urgentText;
    private SegmentedControl? _metricSwitch;
    private LineChart? _chart;
    private TextBlock? _usageLine;
    private Grid? _lists;
    private StackPanel? _ticketRows;
    private StackPanel? _riskRows;
    private StackPanel? _actionRows;
    private TextBlock? _projection;
    private Control? _leftList;
    private Control? _rightList;

    static AccountHealth()
    {
        FocusableProperty.OverrideDefaultValue<AccountHealth>(false);
    }

    public AccountHealth()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        HealthHistory.CollectionChanged += OnDataChanged;
        Usage.CollectionChanged += OnDataChanged;
        Tickets.CollectionChanged += OnDataChanged;
        Risks.CollectionChanged += OnDataChanged;
        Actions.CollectionChanged += OnDataChanged;
        UpdateNumbers();
    }

    /// <summary>True while the block shows its built-in invented account (no <see cref="HealthHistory"/> was given).</summary>
    public bool IsShowingSamples => HealthHistory.Count == 0;

    private IReadOnlyList<double> EffectiveHistory => IsShowingSamples ? _sampleHistory ??= AccountHealthSamples.History() : HealthHistory;

    /// <summary>The usage metrics in use (yours, or the samples').</summary>
    public IReadOnlyList<AccountHealthUsageMetric> EffectiveUsage => IsShowingSamples ? _sampleUsage ??= AccountHealthSamples.Usage() : Usage;

    /// <summary>The tickets in use.</summary>
    public IReadOnlyList<AccountHealthTicket> EffectiveTickets => IsShowingSamples ? _sampleTickets ??= AccountHealthSamples.Tickets() : Tickets;

    /// <summary>The risks in use.</summary>
    public IReadOnlyList<AccountHealthRisk> EffectiveRisks => IsShowingSamples ? _sampleRisks ??= AccountHealthSamples.Risks() : Risks;

    /// <summary>The recommended actions in use.</summary>
    public IReadOnlyList<AccountHealthAction> EffectiveActions => IsShowingSamples ? _sampleActions ??= AccountHealthSamples.Actions() : Actions;

    internal LineChart? Chart => _chart;

    internal SegmentedControl? MetricSwitch => _metricSwitch;

    internal StackPanel? ActionRows => _actionRows;

    internal StackPanel? TicketRows => _ticketRows;

    internal TextBlock? Projection => _projection;

    internal TextBlock? RenewalLeft => _renewalLeft;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal bool IsCompact => _compact;

    /// <summary>The band of a score: 0 healthy (75 and up), 1 watch (50 to 74), 2 at risk.</summary>
    public static int BandOf(int score) => score >= 75 ? 0 : score >= 50 ? 1 : 2;

    /// <summary>Re-reads the data: call it after changing an item in place (the collections are followed on their own).</summary>
    public void Refresh()
    {
        UpdateNumbers();
        RefreshAll();
    }

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private DateTime? EffectiveRenewal => RenewalDate ?? (IsShowingSamples ? AccountHealthSamples.Renewal(EffectiveToday) : null);

    private DateTime EffectiveToday => (Today ?? DateTime.Today).Date;

    private void UpdateNumbers()
    {
        var history = EffectiveHistory;
        Score = history.Count > 0 ? (int)Math.Round(Math.Clamp(history[^1], 0, 100)) : 0;
        ProjectedScore = Math.Min(100, Score + EffectiveActions.Where(a => a.IsDone).Sum(a => Math.Max(0, a.Impact)));
        DaysToRenewal = EffectiveRenewal is { } renewal ? (int)(renewal.Date - EffectiveToday).TotalDays : null;
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_body is not null)
        {
            _body.Child = null;
        }
        _body = e.NameScope.Find<Decorator>(BodyPart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            RefreshAll();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        var root = new StackPanel { Spacing = 20 };

        _name = AccountHealthUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeLG, FontWeight.Medium, 24, wrap: false);
        ArcTokens.Bind(_name, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(_name, 2);
        _subtitle = AccountHealthUi.Text(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var titles = new StackPanel { Spacing = 2 };
        titles.Children.Add(_name);
        titles.Children.Add(_subtitle);
        root.Children.Add(titles);

        // Tiles: health (score, band, change, trend), renewal, tickets.
        _scoreText = AccountHealthUi.Big(null);
        _bandBadge = new Badge { Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center };
        var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        scoreRow.Children.Add(_scoreText);
        scoreRow.Children.Add(_bandBadge);
        _changeText = AccountHealthUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        _trend = new AccountHealthTrend { Height = 40, Margin = new Thickness(0, 6, 0, 0) };
        var health = new StackPanel { Spacing = 4 };
        health.Children.Add(AccountHealthUi.Heading(labels.Health));
        health.Children.Add(scoreRow);
        health.Children.Add(_changeText);
        health.Children.Add(_trend);
        var healthTile = AccountHealthUi.Tile(health);

        _renewalDate = AccountHealthUi.Big(null);
        _renewalLeft = AccountHealthUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var renewal = new StackPanel { Spacing = 4 };
        renewal.Children.Add(AccountHealthUi.Heading(labels.Renewal));
        renewal.Children.Add(_renewalDate);
        renewal.Children.Add(_renewalLeft);
        var renewalTile = AccountHealthUi.Tile(renewal);

        _ticketCount = AccountHealthUi.Big(null);
        _urgentText = AccountHealthUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        var tickets = new StackPanel { Spacing = 4 };
        tickets.Children.Add(AccountHealthUi.Heading(labels.Tickets));
        tickets.Children.Add(_ticketCount);
        tickets.Children.Add(_urgentText);
        var ticketsTile = AccountHealthUi.Tile(tickets);

        _tiles = new Grid();
        _tiles.Children.Add(healthTile);
        _tiles.Children.Add(renewalTile);
        _tiles.Children.Add(ticketsTile);
        root.Children.Add(_tiles);

        // Usage: the metric switch and a line chart of the weeks.
        var usageHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var usageTitles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        usageTitles.Children.Add(AccountHealthUi.Heading(labels.Usage));
        _usageLine = AccountHealthUi.Text(null, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8);
        usageTitles.Children.Add(_usageLine);
        usageHead.Children.Add(usageTitles);
        _metricSwitch = new SegmentedControl { HorizontalAlignment = HorizontalAlignment.Right };
        AutomationProperties.SetName(_metricSwitch, labels.Usage);
        _metricSwitch.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _metricSwitch.SelectedIndex >= 0)
            {
                SetCurrentValue(SelectedMetricProperty, _metricSwitch.SelectedIndex);
            }
        };
        Grid.SetColumn(_metricSwitch, 1);
        usageHead.Children.Add(_metricSwitch);
        _chart = new LineChart { PlotHeight = 160, ShowLegend = false, XSelector = o => o };
        var usage = new StackPanel { Spacing = 10 };
        usage.Children.Add(usageHead);
        usage.Children.Add(_chart);
        root.Children.Add(usage);

        // Lists: tickets and risks on the left, actions on the right.
        _ticketRows = new StackPanel { Spacing = 10 };
        _riskRows = new StackPanel { Spacing = 10 };
        var left = new StackPanel { Spacing = 20 };
        var ticketSection = new StackPanel { Spacing = 10 };
        ticketSection.Children.Add(AccountHealthUi.Heading(labels.TicketList));
        ticketSection.Children.Add(_ticketRows);
        var riskSection = new StackPanel { Spacing = 10 };
        riskSection.Children.Add(AccountHealthUi.Heading(labels.Risks));
        riskSection.Children.Add(_riskRows);
        left.Children.Add(riskSection);
        left.Children.Add(ticketSection);
        _actionRows = new StackPanel { Spacing = 4 };
        _projection = AccountHealthUi.Text(null, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium);
        AutomationProperties.SetLiveSetting(_projection, AutomationLiveSetting.Polite);
        var right = new StackPanel { Spacing = 10 };
        right.Children.Add(AccountHealthUi.Heading(labels.Actions));
        right.Children.Add(_actionRows);
        right.Children.Add(_projection);
        _leftList = left;
        _rightList = right;
        _lists = new Grid();
        _lists.Children.Add(left);
        _lists.Children.Add(right);
        root.Children.Add(_lists);
        ApplyLayout();
        return root;
    }

    private void ApplyLayout()
    {
        if (_tiles is null || _lists is null || _leftList is null || _rightList is null)
        {
            return;
        }
        _tiles.ColumnDefinitions.Clear();
        _tiles.RowDefinitions.Clear();
        _lists.ColumnDefinitions.Clear();
        _lists.RowDefinitions.Clear();
        if (_compact)
        {
            _tiles.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
            _tiles.RowSpacing = 12;
            _tiles.ColumnSpacing = 0;
            _lists.RowDefinitions = new RowDefinitions("Auto,Auto");
            _lists.RowSpacing = 24;
            _lists.ColumnSpacing = 0;
        }
        else
        {
            _tiles.ColumnDefinitions = new ColumnDefinitions("1.4*,*,*");
            _tiles.ColumnSpacing = 12;
            _tiles.RowSpacing = 0;
            _lists.ColumnDefinitions = new ColumnDefinitions("*,*");
            _lists.ColumnSpacing = 28;
            _lists.RowSpacing = 0;
        }
        for (var i = 0; i < _tiles.Children.Count; i++)
        {
            Grid.SetColumn(_tiles.Children[i], _compact ? 0 : i);
            Grid.SetRow(_tiles.Children[i], _compact ? i : 0);
        }
        Grid.SetColumn(_rightList, _compact ? 0 : 1);
        Grid.SetRow(_rightList, _compact ? 1 : 0);
    }

    #endregion

    #region Refresh

    private void RefreshAll()
    {
        if (_body?.Child is null)
        {
            return;
        }
        var labels = Labels;
        _name!.Text = AccountName ?? (IsShowingSamples ? AccountHealthSamples.Name : null);
        _name.IsVisible = !string.IsNullOrEmpty(_name.Text);
        _subtitle!.Text = Subtitle ?? (IsShowingSamples && AccountName is null ? AccountHealthSamples.Subtitle : null);
        _subtitle.IsVisible = !string.IsNullOrEmpty(_subtitle.Text);
        AutomationProperties.SetName(this, _name.Text);

        // Health.
        var history = EffectiveHistory;
        var band = BandOf(Score);
        _scoreText!.Text = Score.ToString(CultureInfo.CurrentCulture);
        _bandBadge!.Content = band < labels.Bands.Count ? labels.Bands[band] : null;
        _bandBadge.Tone = band switch { 0 => BadgeTone.Success, 1 => BadgeTone.Warning, _ => BadgeTone.Danger };
        var change = history.Count > 1 ? (int)Math.Round(history[^1] - history[0]) : 0;
        _changeText!.Text = history.Count > 1
            ? string.Format(CultureInfo.CurrentCulture, labels.ChangeFormat, Signed(change), history.Count - 1)
            : null;
        ArcTokens.Bind(_changeText, TextBlock.ForegroundProperty, change < 0 ? ArcDesignTokenKind.Danger : change > 0 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextSecondary);
        _trend!.Values = history;
        AutomationProperties.SetName(_scoreText, labels.Health + " " + Score.ToString(CultureInfo.CurrentCulture) + ", " + _bandBadge.Content + ", " + _changeText.Text);

        // Renewal.
        if (EffectiveRenewal is { } renewal && DaysToRenewal is { } days)
        {
            _renewalDate!.Text = renewal.ToString("MMM d", CultureInfo.CurrentCulture);
            _renewalLeft!.Text = days >= 0
                ? string.Format(CultureInfo.CurrentCulture, labels.DaysLeftFormat, days)
                : string.Format(CultureInfo.CurrentCulture, labels.OverdueFormat, -days);
            ArcTokens.Bind(_renewalLeft, TextBlock.ForegroundProperty,
                days < 0 ? ArcDesignTokenKind.Danger : days <= RenewalWarningDays ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.TextSecondary);
        }
        else
        {
            _renewalDate!.Text = "—";
            _renewalLeft!.Text = labels.NoRenewal;
        }
        AutomationProperties.SetName(_renewalDate, labels.Renewal + " " + _renewalDate.Text + ", " + _renewalLeft.Text);

        // Tickets.
        var open = EffectiveTickets.Where(t => t.Status != AccountHealthTicketStatus.Solved).ToList();
        _ticketCount!.Text = open.Count.ToString(CultureInfo.CurrentCulture);
        var urgent = open.Count(t => t.Priority == AccountHealthTicketPriority.Urgent);
        _urgentText!.Text = string.Format(CultureInfo.CurrentCulture, labels.UrgentFormat, urgent);
        ArcTokens.Bind(_urgentText, TextBlock.ForegroundProperty, urgent > 0 ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetName(_ticketCount, labels.Tickets + " " + _ticketCount.Text + ", " + _urgentText.Text);

        // Usage.
        var metrics = EffectiveUsage;
        _syncing = true;
        _metricSwitch!.ItemsSource = metrics.Select((m, i) => new SegmentedOption(i, m.Name)).ToArray();
        _metricSwitch.SelectedIndex = metrics.Count == 0 ? -1 : Math.Clamp(SelectedMetric, 0, metrics.Count - 1);
        _syncing = false;
        _metricSwitch.IsVisible = metrics.Count > 1;
        UpdateChart();

        BuildTickets();
        BuildRisks();
        BuildActions();
    }

    private void UpdateChart()
    {
        if (_chart is null || _usageLine is null)
        {
            return;
        }
        var metrics = EffectiveUsage;
        if (metrics.Count == 0)
        {
            _chart.IsVisible = false;
            _usageLine.Text = null;
            return;
        }
        var metric = metrics[Math.Clamp(SelectedMetric, 0, metrics.Count - 1)];
        _chart.IsVisible = true;
        _chart.Label = metric.Name;
        _chart.Unit = metric.Unit ?? string.Empty;
        _chart.YMaximum = metric.Limit;
        var weeks = Enumerable.Range(1, metric.Values.Count).Select(i => Labels.WeekPrefix + i.ToString(CultureInfo.CurrentCulture)).ToArray();
        _chart.Series.Clear();
        _chart.Series.Add(new ChartSeries { Key = "usage", Name = metric.Name, Values = metric.Values.ToArray() });
        _chart.ItemsSource = weeks;
        var now = metric.Values.Count > 0 ? metric.Values[^1] : 0;
        var nowText = ArcFormatting.Compact(now) + (metric.Unit is { Length: > 0 } unit ? " " + unit : string.Empty);
        _usageLine.Text = metric.Limit is { } limit
            ? string.Format(CultureInfo.CurrentCulture, Labels.OfLimitFormat, nowText, ArcFormatting.Compact(limit))
            : nowText;
    }

    private void BuildTickets()
    {
        var labels = Labels;
        _ticketRows!.Children.Clear();
        var list = EffectiveTickets.OrderBy(t => t.Status == AccountHealthTicketStatus.Solved).ThenByDescending(t => t.Priority).ToList();
        if (list.Count == 0)
        {
            _ticketRows.Children.Add(AccountHealthUi.Text(labels.Nothing, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM));
        }
        foreach (var ticket in list)
        {
            var priority = (int)ticket.Priority < labels.Priorities.Count ? labels.Priorities[(int)ticket.Priority] : ticket.Priority.ToString();
            var status = (int)ticket.Status < labels.Statuses.Count ? labels.Statuses[(int)ticket.Status] : ticket.Status.ToString();
            var color = ticket.Priority switch
            {
                AccountHealthTicketPriority.Urgent => ArcDesignTokenKind.Danger,
                AccountHealthTicketPriority.High => ArcDesignTokenKind.Warning,
                AccountHealthTicketPriority.Normal => ArcDesignTokenKind.Accent,
                _ => ArcDesignTokenKind.BorderStrong,
            };
            var solved = ticket.Status == AccountHealthTicketStatus.Solved;
            var copy = new StackPanel();
            copy.Children.Add(AccountHealthUi.Text(ticket.Title, solved ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM));
            var meta = string.Join(" · ", new[] { ticket.Id, priority, ticket.Age }.Where(s => !string.IsNullOrEmpty(s)));
            copy.Children.Add(AccountHealthUi.Text(meta, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
            var badge = new Badge
            {
                Content = status,
                Size = BadgeSize.Small,
                Tone = ticket.Status switch { AccountHealthTicketStatus.Open => BadgeTone.Info, AccountHealthTicketStatus.Pending => BadgeTone.Warning, _ => BadgeTone.Neutral },
                VerticalAlignment = VerticalAlignment.Top,
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(new AccountHealthDot(color));
            Grid.SetColumn(copy, 1);
            row.Children.Add(copy);
            Grid.SetColumn(badge, 2);
            row.Children.Add(badge);
            AutomationProperties.SetName(row, ticket.Title + ", " + meta + ", " + status);
            _ticketRows.Children.Add(row);
        }
    }

    private void BuildRisks()
    {
        var labels = Labels;
        _riskRows!.Children.Clear();
        if (EffectiveRisks.Count == 0)
        {
            _riskRows.Children.Add(AccountHealthUi.Text(labels.Nothing, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM));
        }
        foreach (var risk in EffectiveRisks.OrderByDescending(r => r.Level))
        {
            var level = (int)risk.Level < labels.RiskLevels.Count ? labels.RiskLevels[(int)risk.Level] : risk.Level.ToString();
            var copy = new StackPanel();
            copy.Children.Add(AccountHealthUi.Text(risk.Title, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, FontWeight.Medium));
            if (!string.IsNullOrEmpty(risk.Detail))
            {
                copy.Children.Add(AccountHealthUi.Text(risk.Detail, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, lineHeight: 16.8));
            }
            var badge = new Badge
            {
                Content = level,
                Size = BadgeSize.Small,
                Tone = risk.Level switch { AccountHealthRiskLevel.High => BadgeTone.Danger, AccountHealthRiskLevel.Medium => BadgeTone.Warning, _ => BadgeTone.Neutral },
                VerticalAlignment = VerticalAlignment.Top,
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
            row.Children.Add(copy);
            Grid.SetColumn(badge, 1);
            row.Children.Add(badge);
            AutomationProperties.SetName(row, level + ": " + risk.Title + ". " + risk.Detail);
            _riskRows.Children.Add(row);
        }
    }

    private void BuildActions()
    {
        var labels = Labels;
        _actionRows!.Children.Clear();
        if (EffectiveActions.Count == 0)
        {
            _actionRows.Children.Add(AccountHealthUi.Text(labels.Nothing, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM));
        }
        foreach (var action in EffectiveActions.OrderByDescending(a => a.Impact))
        {
            var impact = string.Format(CultureInfo.CurrentCulture, labels.ImpactFormat, action.Impact);
            var box = new Checkbox
            {
                Content = action.Title + "  ·  " + impact,
                Description = action.Detail,
                IsChecked = action.IsDone,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = action,
            };
            box.IsCheckedChanged += (_, _) =>
            {
                var done = box.IsChecked == true;
                if (action.IsDone == done)
                {
                    return;
                }
                action.IsDone = done;
                UpdateNumbers();
                UpdateProjection();
                RaiseEvent(new AccountHealthActionToggledEventArgs(ActionToggledEvent, action, ProjectedScore));
            };
            _actionRows.Children.Add(box);
        }
        UpdateProjection();
    }

    private void UpdateProjection()
    {
        if (_projection is null)
        {
            return;
        }
        _projection.Text = string.Format(CultureInfo.CurrentCulture, Labels.ProjectionFormat, Score, ProjectedScore);
        _projection.IsVisible = ProjectedScore != Score;
        if (_projection.IsVisible)
        {
            _announcer?.Announce(_projection.Text);
        }
    }

    private static string Signed(int value) => value > 0 ? "+" + value.ToString(CultureInfo.CurrentCulture) : value < 0 ? "−" + (-value).ToString(CultureInfo.CurrentCulture) : "0";

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedMetricProperty)
        {
            if (_metricSwitch is not null && _metricSwitch.SelectedIndex != SelectedMetric && !_syncing)
            {
                _syncing = true;
                _metricSwitch.SelectedIndex = SelectedMetric;
                _syncing = false;
            }
            UpdateChart();
        }
        else if (change.Property == RenewalDateProperty || change.Property == TodayProperty || change.Property == AccountNameProperty
                 || change.Property == SubtitleProperty || change.Property == RenewalWarningDaysProperty)
        {
            Refresh();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_body is not null)
            {
                _body.Child = BuildBody();
                RefreshAll();
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        ApplyLayout();
    }
}
