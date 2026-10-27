using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class UptimeStatus
{
    private static readonly CultureInfo s_culture = AnalyticsFormat.DefaultCulture;

    private Grid? _header;
    private UptimeStatusIcon? _icon;
    private AnalyticsTextSwap? _title;
    private TextBlock? _pageName;
    private TextBlock? _separator;
    private AnalyticsTextSwap? _summary;
    private ContentPresenter? _action;
    private Grid? _uptimeHead;
    private StackPanel? _legend;
    private Border? _uptimeFrame;
    private Border? _responseFrame;
    private Border? _incidentsFrame;
    private TextBlock? _uptimeHeading;
    private TextBlock? _uptimeHint;
    private TextBlock? _legendOperational;
    private TextBlock? _legendDegraded;
    private TextBlock? _legendOutage;
    private UptimeServiceList? _list;
    private TextBlock? _startDate;
    private TextBlock? _todayText;
    private UptimeTipLayer? _tip;
    private Grid? _lower;
    private TextBlock? _responseHeading;
    private AnalyticsTextSwap? _subtitle;
    private TextBlock? _latest;
    private TextBlock? _p95Name;
    private TextBlock? _p50Name;
    private TextBlock? _targetName;
    private AnalyticsRollingNumber? _p95Value;
    private AnalyticsRollingNumber? _p50Value;
    private AnalyticsRollingNumber? _targetValue;
    private readonly List<TextBlock> _units = [];
    private UptimeLatencyChart? _chart;
    private UptimeDataTable? _table;
    private TextBlock? _incidentsHeading;
    private TextBlock? _countBefore;
    private AnalyticsRollingNumber? _countValue;
    private TextBlock? _countAfter;
    private UptimeIncidentList? _incidentList;
    private TextBlock? _noIncidents;
    private TextBlock? _empty;
    private AnalyticsAnnouncer? _statusAnnouncer;
    private AnalyticsAnnouncer? _inspectAnnouncer;

    private readonly List<UptimeServiceRow> _rows = [];
    private readonly List<UptimeService> _services = [];
    private readonly List<UptimeIncident> _incidents = [];
    private readonly HashSet<AvaloniaObject> _watched = [];
    private readonly Dictionary<UptimeIncident, bool> _known = new();
    private INotifyCollectionChanged? _servicesObserved;
    private INotifyCollectionChanged? _incidentsObserved;
    private UptimeServiceRow? _tipRow;
    private bool _hasShown;
    private bool _templated;
    private bool _refreshing;
    private bool _scheduled;
    private bool? _stacked;
    private bool? _compact;
    private bool? _legendBelow;
    private DateOnly _endDay;
    private ServiceStatus? _shownStatus;
    private string? _shownTitle;

    static UptimeStatus()
    {
        AffectsMeasure<UptimeStatus>(LabelsProperty);
    }

    /// <summary>Creates the block.</summary>
    public UptimeStatus()
    {
        Services.CollectionChanged += OnDataChanged;
        Incidents.CollectionChanged += OnDataChanged;
    }

    /// <summary>The service rows (tests).</summary>
    internal IReadOnlyList<UptimeServiceRow> Rows => _rows;

    internal UptimeStatusIcon? Icon => _icon;

    internal AnalyticsTextSwap? TitleSwap => _title;

    internal AnalyticsTextSwap? SummarySwap => _summary;

    internal UptimeServiceList? ServiceList => _list;

    internal UptimeTipLayer? Tip => _tip;

    internal UptimeLatencyChart? Chart => _chart;

    internal UptimeIncidentList? IncidentList => _incidentList;

    internal AnalyticsAnnouncer? StatusAnnouncer => _statusAnnouncer;

    internal AnalyticsAnnouncer? InspectAnnouncer => _inspectAnnouncer;

    internal AnalyticsRollingNumber? P95Number => _p95Value;

    internal AnalyticsRollingNumber? P50Number => _p50Value;

    internal AnalyticsRollingNumber? TargetNumber => _targetValue;

    internal AnalyticsRollingNumber? CountNumber => _countValue;

    internal TextBlock? LatestText => _latest;

    internal AnalyticsTextSwap? SubtitleSwap => _subtitle;

    internal Grid? LowerGrid => _lower;

    internal Border? IncidentsFrame => _incidentsFrame;

    internal bool IsStacked => _stacked == true;

    internal bool IsCompactLayout => _compact == true;

    internal DateOnly EndDay => _endDay;

    internal IReadOnlyList<UptimeService> ResolvedServices => _services;

    // Public methods ---------------------------------------------------------------------------------------------------------

    /// <summary>Ends every inspection: the day cards close and the chart's cursor goes.</summary>
    public void ClearInspection()
    {
        foreach (var row in _rows)
        {
            row.Strip.ClearInspection();
        }
        _chart?.ClearCursor();
    }

    /// <summary>Speaks a sentence through the block's polite status region (the block announces incidents and resolutions itself).</summary>
    /// <param name="text">What to say.</param>
    public void Announce(string text) => _statusAnnouncer?.Announce(text);

    /// <summary>Recomputes everything from the data now (the block follows list and property changes by itself).</summary>
    public void Refresh() => Recompute(_hasShown);

    // Template ---------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var n = e.NameScope;
        _header = n.Find<Grid>(HeaderPart);
        _icon = n.Find<UptimeStatusIcon>(IconPart);
        _title = n.Find<AnalyticsTextSwap>(TitlePart);
        _pageName = n.Find<TextBlock>(PageNamePart);
        _separator = n.Find<TextBlock>(SeparatorPart);
        _summary = n.Find<AnalyticsTextSwap>(SummaryPart);
        _action = n.Find<ContentPresenter>(ActionPart);
        _uptimeFrame = n.Find<Border>(UptimeFramePart);
        _uptimeHead = n.Find<Grid>("PART_UptimeHead");
        _legend = n.Find<StackPanel>(LegendPart);
        _uptimeHeading = n.Find<TextBlock>(UptimeHeadingPart);
        _uptimeHint = n.Find<TextBlock>(UptimeHintPart);
        _legendOperational = n.Find<TextBlock>("PART_LegendOperational");
        _legendDegraded = n.Find<TextBlock>("PART_LegendDegraded");
        _legendOutage = n.Find<TextBlock>("PART_LegendOutage");
        _list = n.Find<UptimeServiceList>(ServiceListPart);
        _startDate = n.Find<TextBlock>(StartDatePart);
        _todayText = n.Find<TextBlock>(TodayPart);
        _tip = n.Find<UptimeTipLayer>(TipPart);
        _lower = n.Find<Grid>(LowerPart);
        _responseFrame = n.Find<Border>(ResponseFramePart);
        _responseHeading = n.Find<TextBlock>(ResponseHeadingPart);
        _subtitle = n.Find<AnalyticsTextSwap>(SubtitlePart);
        _latest = n.Find<TextBlock>(LatestPart);
        _p95Name = n.Find<TextBlock>(P95NamePart);
        _p50Name = n.Find<TextBlock>(P50NamePart);
        _targetName = n.Find<TextBlock>(TargetNamePart);
        _p95Value = n.Find<AnalyticsRollingNumber>(P95ValuePart);
        _p50Value = n.Find<AnalyticsRollingNumber>(P50ValuePart);
        _targetValue = n.Find<AnalyticsRollingNumber>(TargetValuePart);
        _units.Clear();
        foreach (var name in new[] { "PART_Unit1", "PART_Unit2", "PART_Unit3" })
        {
            if (n.Find<TextBlock>(name) is { } unit)
            {
                _units.Add(unit);
            }
        }
        _chart = n.Find<UptimeLatencyChart>(ChartPart);
        _table = n.Find<UptimeDataTable>(TablePart);
        _incidentsFrame = n.Find<Border>(IncidentsFramePart);
        _incidentsHeading = n.Find<TextBlock>(IncidentsHeadingPart);
        _countBefore = n.Find<TextBlock>(CountBeforePart);
        _countValue = n.Find<AnalyticsRollingNumber>(CountValuePart);
        _countAfter = n.Find<TextBlock>(CountAfterPart);
        _incidentList = n.Find<UptimeIncidentList>(IncidentListPart);
        _noIncidents = n.Find<TextBlock>(NoIncidentsPart);
        _empty = n.Find<TextBlock>(EmptyPart);
        _statusAnnouncer = n.Find<AnalyticsAnnouncer>(StatusAnnouncerPart);
        _inspectAnnouncer = n.Find<AnalyticsAnnouncer>(InspectAnnouncerPart);
        // Title, summary and subtitle swap with a rise (-0.25em out, +0.3em in); reduced motion replaces them at once.
        var swap = AnalyticsSwapStyle.RiseLate with { ReducedDuration = TimeSpan.Zero };
        foreach (var text in new[] { _title, _summary, _subtitle })
        {
            if (text is not null)
            {
                text.Swap = swap;
            }
        }
        if (_chart is not null)
        {
            _chart.CursorMoved += OnCursorMoved;
        }
        _templated = true;
        _stacked = null;
        _compact = null;
        _legendBelow = null;
        _hasShown = false;
        _shownStatus = null;
        _shownTitle = null;
        if (Bounds.Width > 0)
        {
            UpdateLayoutState(Bounds.Width);
        }
        Recompute(false);
    }

    private void Unhook()
    {
        if (_chart is not null)
        {
            _chart.CursorMoved -= OnCursorMoved;
        }
        foreach (var row in _rows)
        {
            UnhookRow(row);
        }
        _rows.Clear();
        _list?.Reset();
        _tipRow = null;
        _templated = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_templated)
        {
            Recompute(false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unwatch();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutState(change.GetNewValue<Rect>().Width);
        }
        else if (change.Property == ServicesSourceProperty)
        {
            ObserveSource(ref _servicesObserved, ServicesSource);
            Recompute(_hasShown);
        }
        else if (change.Property == IncidentsSourceProperty)
        {
            ObserveSource(ref _incidentsObserved, IncidentsSource);
            Recompute(_hasShown);
        }
        else if (change.Property == SelectedServiceProperty)
        {
            if (_templated && !_refreshing)
            {
                ApplySelection(animate: true);
                RaiseEvent(new UptimeServiceEventArgs(SelectedServiceChangedEvent, SelectedService));
            }
        }
        else if (change.Property == ActionContentProperty)
        {
            if (_action is not null)
            {
                _action.Content = ActionContent;
            }
        }
        else if (change.Property == PageNameProperty || change.Property == UpdatedAtProperty || change.Property == StatusSummaryProperty
                 || change.Property == OperationalTitleProperty || change.Property == DegradedTitleProperty || change.Property == OutageTitleProperty)
        {
            if (_templated)
            {
                UpdateHeader(_hasShown);
            }
        }
        else if (change.Property == TodayProperty || change.Property == DayCountProperty || change.Property == ChartHeadroomProperty
                 || change.Property == LabelsProperty || change.Property == LatencyFormatterProperty || change.Property == PercentFormatterProperty
                 || change.Property == DateFormatterProperty)
        {
            Recompute(_hasShown && change.Property != LabelsProperty);
        }
    }

    // Data -------------------------------------------------------------------------------------------------------------------

    // Changes to the data come in bursts (a new incident and its updates, a few replaced samples): one recompute, on the
    // next turn of the dispatcher, shows them together.
    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRecompute();

    private void ScheduleRecompute()
    {
        if (_scheduled || !_templated)
        {
            return;
        }
        _scheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scheduled = false;
            Recompute(_hasShown);
        }, DispatcherPriority.Send);
    }

    private void ObserveSource(ref INotifyCollectionChanged? observed, IEnumerable? source)
    {
        if (observed is not null)
        {
            observed.CollectionChanged -= OnDataChanged;
        }
        observed = source as INotifyCollectionChanged;
        if (observed is not null)
        {
            observed.CollectionChanged += OnDataChanged;
        }
    }

    private void Unwatch()
    {
        foreach (var item in _watched)
        {
            switch (item)
            {
                case UptimeService service:
                    service.PropertyChanged -= OnServicePropertyChanged;
                    service.Latency.CollectionChanged -= OnDataChanged;
                    break;
                case UptimeIncident incident:
                    incident.PropertyChanged -= OnIncidentPropertyChanged;
                    incident.Updates.CollectionChanged -= OnDataChanged;
                    incident.ServiceKeys.CollectionChanged -= OnDataChanged;
                    break;
            }
        }
        _watched.Clear();
    }

    private void Watch()
    {
        Unwatch();
        foreach (var service in _services)
        {
            service.PropertyChanged += OnServicePropertyChanged;
            service.Latency.CollectionChanged += OnDataChanged;
            _watched.Add(service);
        }
        foreach (var incident in _incidents)
        {
            incident.PropertyChanged += OnIncidentPropertyChanged;
            incident.Updates.CollectionChanged += OnDataChanged;
            incident.ServiceKeys.CollectionChanged += OnDataChanged;
            _watched.Add(incident);
        }
    }

    private void OnServicePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == UptimeService.UptimeProperty || e.Property == UptimeService.CurrentStatusProperty)
        {
            return;
        }
        ScheduleRecompute();
    }

    private void OnIncidentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not UptimeIncident incident)
        {
            return;
        }
        if (e.Property == UptimeIncident.IsExpandedProperty)
        {
            _incidentList?.Find(incident)?.SetExpanded(incident.IsExpanded);
            if (_templated)
            {
                RaiseEvent(new UptimeIncidentEventArgs(IncidentExpandedChangedEvent, incident));
            }
            return;
        }
        ScheduleRecompute();
    }

    private void Resolve()
    {
        _services.Clear();
        if (ServicesSource is { } sources)
        {
            _services.AddRange(sources.OfType<UptimeService>());
        }
        else
        {
            _services.AddRange(Services);
        }
        _incidents.Clear();
        if (IncidentsSource is { } incidents)
        {
            _incidents.AddRange(incidents.OfType<UptimeIncident>());
        }
        else
        {
            _incidents.AddRange(Incidents);
        }
    }

    private DateOnly ResolveEndDay()
    {
        if (Today is { } today)
        {
            return DateOnly.FromDateTime(today.UtcDateTime);
        }
        var newest = DateTimeOffset.MinValue;
        foreach (var service in _services)
        {
            foreach (var sample in service.Latency)
            {
                if (sample.Time > newest)
                {
                    newest = sample.Time;
                }
            }
        }
        return DateOnly.FromDateTime((newest == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : newest).UtcDateTime);
    }

    private void Recompute(bool animate)
    {
        if (!_templated || _refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            animate &= _hasShown && this.IsAttachedToVisualTree();
            Resolve();
            Watch();
            _endDay = ResolveEndDay();
            DeriveDays();
            var previous = _overallStatus;
            var worst = ServiceStatus.Operational;
            foreach (var service in _services)
            {
                if (service.CurrentStatus > worst)
                {
                    worst = service.CurrentStatus;
                }
            }
            SetAndRaise(OverallStatusProperty, ref _overallStatus, worst);
            PseudoClasses.Set(OperationalPseudoClass, worst == ServiceStatus.Operational);
            PseudoClasses.Set(DegradedPseudoClass, worst == ServiceStatus.Degraded);
            PseudoClasses.Set(OutagePseudoClass, worst == ServiceStatus.Outage);
            PseudoClasses.Set(EmptyPseudoClass, _services.Count == 0);
            _ = previous;

            var selected = SelectedService;
            if (selected is null || !_services.Contains(selected))
            {
                selected = _services.FirstOrDefault();
                SetCurrentValue(SelectedServiceProperty, selected);
            }

            SyncRows(animate);
            UpdateStaticTexts();
            ApplySelection(animate);
            UpdateHeader(animate);
            SyncIncidents(animate);
            Announcements();
            _hasShown = true;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void DeriveDays()
    {
        var count = DayCount;
        var intervals = new List<UptimeInterval>(_incidents.Count);
        foreach (var incident in _incidents)
        {
            intervals.Add(new UptimeInterval(incident.Start, incident.Duration, incident.Severity, incident.ServiceKeys.ToArray()));
        }
        foreach (var service in _services)
        {
            var buckets = UptimeEngine.Bucket(_endDay, count, service.Key, intervals);
            var days = new UptimeDay[buckets.Length];
            for (var i = 0; i < buckets.Length; i++)
            {
                var b = buckets[i];
                var list = new List<UptimeDayIncident>(b.Intervals.Length);
                foreach (var k in b.Intervals)
                {
                    list.Add(new UptimeDayIncident(_incidents[k], UptimeEngine.MinutesOnDay(_incidents[k].Start, _incidents[k].Duration, b.Date)));
                }
                days[i] = new UptimeDay(b.Date, b.Status, b.Uptime, b.Minutes, list);
            }
            var current = ServiceStatus.Operational;
            foreach (var incident in _incidents)
            {
                if (!incident.IsResolved && incident.ServiceKeys.Contains(service.Key) && incident.Severity > current)
                {
                    current = incident.Severity;
                }
            }
            service.SetDerived(days, UptimeEngine.WindowUptime(buckets), current);
        }
    }

    // Rows --------------------------------------------------------------------------------------------------------------------

    private void SyncRows(bool animate)
    {
        if (_list is null)
        {
            return;
        }
        var same = _rows.Count == _services.Count;
        for (var i = 0; same && i < _rows.Count; i++)
        {
            same = ReferenceEquals(_rows[i].Service, _services[i]);
        }
        if (!same)
        {
            foreach (var row in _rows)
            {
                UnhookRow(row);
            }
            _rows.Clear();
            _list.Reset();
            _tipRow = null;
            foreach (var service in _services)
            {
                var row = new UptimeServiceRow(service);
                var captured = service;
                row.Button.Chosen += () => ChooseService(captured);
                row.Strip.Activated += () => ChooseService(captured);
                row.Strip.Inspected += (index, keyboard) => OnDayInspected(row, index, keyboard);
                row.Button.IsCompact = _compact == true;
                _rows.Add(row);
                _list.Children.Add(row);
            }
            animate = false;
        }
        var labels = Labels;
        foreach (var row in _rows)
        {
            var service = row.Service;
            row.Button.Show(service, service.CurrentStatus == ServiceStatus.Outage ? labels.OutageBadgeText : labels.DegradedBadgeText, labels.UptimeWordText, animate);
            row.Strip.SetDays(service.Days, animate);
            AutomationProperties.SetName(row.Button, string.Format(s_culture, labels.ServiceButtonFormat, service.Name,
                PercentText(service.Uptime), StatusName(service.CurrentStatus)));
            AutomationProperties.SetName(row.Strip, string.Format(s_culture, labels.StripNameFormat, service.Name, DayCount));
            AutomationProperties.SetHelpText(row.Strip, StripHelp(service));
        }
    }

    private static void UnhookRow(UptimeServiceRow row) => row.Strip.ClearInspection();

    private void ChooseService(UptimeService service)
    {
        if (!ReferenceEquals(SelectedService, service))
        {
            SelectedService = service;
        }
    }

    private string StripHelp(UptimeService service)
    {
        var labels = Labels;
        var parts = new List<string>();
        foreach (var day in service.Days)
        {
            foreach (var entry in day.Incidents)
            {
                parts.Add(string.Format(s_culture, labels.StripIncidentFormat, DateText(day.Date), StatusName(entry.Incident.Severity),
                    Math.Round(entry.Minutes, MidpointRounding.AwayFromZero).ToString("0", s_culture)));
            }
        }
        return string.Format(s_culture, labels.StripHelpFormat, PercentText(service.Uptime),
            parts.Count == 0 ? labels.NoneText : string.Join("; ", parts));
    }

    // Texts -------------------------------------------------------------------------------------------------------------------

    private string PercentText(double fraction) => PercentFormatter?.Invoke(fraction) ?? UptimeEngine.PercentText(fraction);

    private string LatencyText(double ms) => LatencyFormatter?.Invoke(ms) ?? AnalyticsFormat.Number(ms);

    private string DateText(DateOnly date) => DateFormatter?.Invoke(date) ?? date.ToString("ddd, MMM d", s_culture);

    private string StatusName(ServiceStatus status) => status switch
    {
        ServiceStatus.Operational => Labels.OperationalText,
        ServiceStatus.Degraded => Labels.DegradedText,
        _ => Labels.OutageText
    };

    private void UpdateStaticTexts()
    {
        var labels = Labels;
        SetText(_uptimeHeading, labels.UptimeText);
        SetText(_uptimeHint, string.Format(s_culture, labels.UptimeHintFormat, DayCount));
        SetText(_legendOperational, labels.OperationalText);
        SetText(_legendDegraded, labels.DegradedText);
        SetText(_legendOutage, labels.OutageText);
        SetText(_todayText, labels.TodayText);
        var first = _services.FirstOrDefault()?.Days.FirstOrDefault();
        SetText(_startDate, first is null ? null : first.Date.ToString("MMM d", s_culture));
        SetText(_responseHeading, labels.ResponseTimeText);
        SetText(_p95Name, labels.P95Text);
        SetText(_p50Name, labels.P50Text);
        SetText(_targetName, labels.TargetText);
        foreach (var unit in _units)
        {
            unit.Text = labels.MillisecondsText;
        }
        SetText(_incidentsHeading, labels.IncidentsText);
        SetText(_empty, labels.NoServicesText);
        if (_chart is not null)
        {
            _chart.P95Name = labels.P95Text;
            _chart.P50Name = labels.P50Text;
            _chart.SetTickFormat(LatencyFormatter);
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
        }
    }

    // Header ------------------------------------------------------------------------------------------------------------------

    private void UpdateHeader(bool animate)
    {
        if (_title is null)
        {
            return;
        }
        var status = _overallStatus;
        var title = status switch
        {
            ServiceStatus.Operational => OperationalTitle,
            ServiceStatus.Degraded => DegradedTitle,
            _ => OutageTitle
        };
        if (_icon is not null)
        {
            _icon.Status = status;
        }
        _title.Text = title;
        _pageName!.Text = PageName;
        var summary = StatusSummary;
        if (summary is null && UpdatedAt is { } updated)
        {
            summary = string.Format(s_culture, Labels.UpdatedFormat, updated.UtcDateTime);
        }
        _summary!.Text = summary ?? string.Empty;
        var hasName = !string.IsNullOrEmpty(PageName);
        _pageName.IsVisible = hasName;
        _separator!.IsVisible = hasName && !string.IsNullOrEmpty(summary);
        if (_action is not null && !ReferenceEquals(_action.Content, ActionContent))
        {
            _action.Content = ActionContent;
        }
        _shownStatus = status;
        _shownTitle = title;
        _ = animate;
    }

    // Selection, chart and numbers --------------------------------------------------------------------------------------------

    private void ApplySelection(bool animate)
    {
        var service = SelectedService;
        if (_list is not null)
        {
            var row = _rows.FirstOrDefault(r => ReferenceEquals(r.Service, service));
            _list.Select(row);
            foreach (var r in _rows)
            {
                r.Button.IsChecked = ReferenceEquals(r, row);
            }
        }
        if (service is null || _chart is null)
        {
            SetText(_latest, null);
            _subtitle!.Text = string.Empty;
            return;
        }
        var labels = Labels;
        _chart.SetTimeFormat("HH:mm");
        _chart.SetData(service.Latency, service.TargetP95, ChartHeadroom, animate);
        _subtitle!.Text = string.Format(s_culture, labels.ResponseSubtitleFormat, service.Name);
        _inspectedPoint = null;
        RaisePropertyChanged(InspectedPointProperty, null, null);
        ShowSample(service.Latency.Count == 0 ? null : service.Latency[^1], service, animate);
        AutomationProperties.SetName(_chart, string.Format(s_culture, labels.ChartNameFormat, service.Name));
        AutomationProperties.SetHelpText(_chart, ChartSummary(service));
        UpdateTable(service);
    }

    private string ChartSummary(UptimeService service)
    {
        if (service.Latency.Count == 0)
        {
            return service.Name;
        }
        var last = service.Latency[^1];
        var high = service.Latency[0];
        foreach (var s in service.Latency)
        {
            if (s.P95 > high.P95)
            {
                high = s;
            }
        }
        return string.Format(s_culture, Labels.ChartSummaryFormat, service.Name, LatencyText(last.P50), LatencyText(last.P95),
            LatencyText(high.P95), SampleTime(high.Time), LatencyText(service.TargetP95));
    }

    private string SampleTime(DateTimeOffset time) =>
        string.Format(s_culture, Labels.SampleTimeFormat, time.UtcDateTime.ToString("MMM d, HH:mm", s_culture));

    private void ShowSample(LatencySample? sample, UptimeService service, bool animate)
    {
        SetNumber(_p95Value, sample?.P95 ?? 0, animate);
        SetNumber(_p50Value, sample?.P50 ?? 0, animate);
        SetNumber(_targetValue, service.TargetP95, animate);
        if (_latest is not null)
        {
            _latest.Text = sample is { } s
                ? string.Format(s_culture, Labels.LatestFormat, string.Format(s_culture, Labels.SampleTimeFormat,
                    _inspectedPoint is null ? s.Time.UtcDateTime.ToString("HH:mm", s_culture) : s.Time.UtcDateTime.ToString("MMM d, HH:mm", s_culture)))
                : string.Empty;
        }
    }

    private static void SetNumber(AnalyticsRollingNumber? number, double value, bool animate)
    {
        if (number is null)
        {
            return;
        }
        number.IsInstant = !animate;
        number.Format = AnalyticsNumberFormat.Integer;
        number.Amount = Math.Round(value);
        number.IsInstant = false;
    }

    private void UpdateTable(UptimeService service)
    {
        if (_table is null)
        {
            return;
        }
        var labels = Labels;
        _table.Update(string.Format(s_culture, labels.TableCaptionFormat, service.Name),
            service.Latency.Select(s => string.Format(s_culture, labels.TableRowFormat, SampleTime(s.Time), LatencyText(s.P50), LatencyText(s.P95))).ToArray());
    }

    private void OnCursorMoved(int index, bool keyboard)
    {
        var service = SelectedService;
        if (service is null)
        {
            return;
        }
        if (index < 0 || index >= service.Latency.Count)
        {
            _inspectedPoint = null;
            RaisePropertyChanged(InspectedPointProperty, default, null);
            ShowSample(service.Latency.Count == 0 ? null : service.Latency[^1], service, false);
            RaiseEvent(new UptimePointEventArgs(PointInspectedEvent, service, -1, null));
            return;
        }
        var sample = service.Latency[index];
        _inspectedPoint = index;
        RaisePropertyChanged(InspectedPointProperty, default, index);
        // The numbers and the time are replaced as text, without rolling.
        ShowSample(sample, service, false);
        if (keyboard)
        {
            _inspectAnnouncer?.Announce(string.Format(s_culture, Labels.PointAnnouncementFormat, SampleTime(sample.Time),
                LatencyText(sample.P50), LatencyText(sample.P95)));
        }
        RaiseEvent(new UptimePointEventArgs(PointInspectedEvent, service, index, sample));
    }

    // Day cards ---------------------------------------------------------------------------------------------------------------

    private void OnDayInspected(UptimeServiceRow row, int index, bool keyboard)
    {
        var service = row.Service;
        if (index < 0 || index >= service.Days.Count)
        {
            if (ReferenceEquals(_tipRow, row))
            {
                _inspectedDay = null;
                RaisePropertyChanged(InspectedDayProperty, default, null);
                // A pointer that moves from one row to the next exits and enters in one go: hide on the next turn
                // unless another row took the card meanwhile (then it follows on its spring).
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_tipRow, row) && row.Strip.InspectedIndex < 0)
                    {
                        _tip?.Hide();
                        _tipRow = null;
                    }
                }, DispatcherPriority.Input);
            }
            RaiseEvent(new UptimeDayEventArgs(DayInspectedEvent, service, null, -1));
            return;
        }
        var day = service.Days[index];
        _tipRow = row;
        _inspectedDay = index;
        RaisePropertyChanged(InspectedDayProperty, default, index);
        if (_tip is not null)
        {
            var strip = row.Strip;
            var top = strip.TranslatePoint(new Point(strip.BarCenter(index), 0), _tip);
            if (top is { } point)
            {
                _tip.Show(TipFor(service, day), point.X, point.Y);
            }
        }
        if (keyboard)
        {
            var labels = Labels;
            var incidents = day.Incidents.Count == 0
                ? labels.NoIncidentsRecordedText
                : string.Join("; ", day.Incidents.Select(i => i.Incident.Title));
            _inspectAnnouncer?.Announce(string.Format(s_culture, labels.DayAnnouncementFormat, DateText(day.Date), StatusName(day.Status),
                PercentText(day.Uptime), incidents));
        }
        RaiseEvent(new UptimeDayEventArgs(DayInspectedEvent, service, day, index));
    }

    private UptimeTipContent TipFor(UptimeService service, UptimeDay day)
    {
        var labels = Labels;
        var rows = day.Incidents.Select(i => (i.Incident.Title,
            string.Format(s_culture, labels.MinutesFormat, Math.Round(i.Minutes, MidpointRounding.AwayFromZero).ToString("0", s_culture)))).ToList();
        return new UptimeTipContent(string.Format(s_culture, labels.DayTitleFormat, service.Name, DateText(day.Date)),
            PercentText(day.Uptime), day.Status, StatusName(day.Status), rows, labels.NoIncidentsRecordedText);
    }

    // Incidents ---------------------------------------------------------------------------------------------------------------

    private void SyncIncidents(bool animate)
    {
        if (_incidentList is null)
        {
            return;
        }
        var ordered = _incidents.OrderByDescending(i => i.Start).ToList();
        _incidentList.Sync(ordered, CreateItem, animate);
        foreach (var item in _incidentList.LiveItems)
        {
            item.Refresh(Labels, IncidentMeta(item.Incident), UpdateTime(item.Incident), animate);
        }
        var windowStart = _endDay.AddDays(1 - DayCount).ToDateTime(TimeOnly.MinValue);
        var count = _incidents.Count(i => i.Start.UtcDateTime >= windowStart || i.Start.UtcDateTime + i.Duration >= windowStart);
        var labels = Labels;
        var format = labels.IncidentsCountFormat.Replace("{1}", DayCount.ToString(s_culture));
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        var before = at < 0 ? string.Empty : format[..at];
        var after = at < 0 ? format : format[(at + 3)..];
        SetText(_countBefore, before);
        SetText(_countAfter, after);
        SetNumber(_countValue, count, animate);
        if (_countValue is not null)
        {
            _countValue.IsVisible = at >= 0;
        }
        if (_noIncidents is not null)
        {
            _noIncidents.Text = string.Format(s_culture, labels.NoIncidentsFormat, DayCount);
            _noIncidents.IsVisible = ordered.Count == 0;
        }
    }

    private UptimeIncidentItem CreateItem(UptimeIncident incident)
    {
        var item = new UptimeIncidentItem(incident);
        item.Refresh(Labels, IncidentMeta(incident), UpdateTime(incident), false);
        item.SetExpanded(incident.IsExpanded);
        return item;
    }

    private Func<DateTimeOffset, string> UpdateTime(UptimeIncident incident) => time =>
        time.UtcDateTime.Date == incident.Start.UtcDateTime.Date
            ? time.UtcDateTime.ToString("HH:mm", s_culture) + " UTC"
            : time.UtcDateTime.ToString("MMM d, HH:mm", s_culture) + " UTC";

    private string IncidentMeta(UptimeIncident incident)
    {
        var labels = Labels;
        var start = incident.Start.UtcDateTime;
        var when = start.Date == DateTime.UtcNow.Date || _endDay == DateOnly.FromDateTime(start)
            ? string.Format(s_culture, labels.TodayAtFormat, start.ToString("HH:mm", s_culture))
            : string.Format(s_culture, labels.DateAtFormat, start.ToString("MMM d, HH:mm", s_culture));
        var duration = string.Format(s_culture, labels.MinutesFormat, Math.Round(incident.Duration.TotalMinutes, MidpointRounding.AwayFromZero).ToString("0", s_culture));
        if (!incident.IsResolved)
        {
            duration = string.Format(s_culture, labels.SoFarFormat, duration);
        }
        var names = incident.ServiceKeys
            .Select(k => _services.FirstOrDefault(s => s.Key == k)?.Name ?? k);
        return string.Format(s_culture, labels.IncidentMetaFormat, incident.Severity == ServiceStatus.Outage ? labels.OutageText : labels.DegradedText,
            when, duration, string.Join(", ", names));
    }

    private void Announcements()
    {
        var labels = Labels;
        foreach (var incident in _incidents)
        {
            if (!_known.TryGetValue(incident, out var resolved))
            {
                if (_hasShown && !incident.IsResolved)
                {
                    Announce(string.Format(s_culture, labels.IncidentStartedFormat, incident.Title));
                }
            }
            else if (!resolved && incident.IsResolved && _hasShown)
            {
                Announce(string.Format(s_culture, labels.IncidentResolvedFormat, incident.Title));
            }
            _known[incident] = incident.IsResolved;
        }
        foreach (var gone in _known.Keys.Where(k => !_incidents.Contains(k)).ToList())
        {
            _known.Remove(gone);
        }
    }

    // Layout states -----------------------------------------------------------------------------------------------------------

    private void UpdateLayoutState(double width)
    {
        if (!_templated || width <= 0)
        {
            return;
        }
        var stacked = width < 860;
        var compact = width < 560;
        if (_stacked != stacked)
        {
            _stacked = stacked;
            PseudoClasses.Set(StackedPseudoClass, stacked);
            if (_lower is not null && _incidentsFrame is not null && _responseFrame is not null)
            {
                _lower.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("1.2*,*");
                _lower.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
                Grid.SetRow(_incidentsFrame, stacked ? 1 : 0);
                Grid.SetColumn(_incidentsFrame, stacked ? 0 : 1);
                _incidentsFrame.BorderThickness = stacked ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);
            }
        }
        var below = width < 640;
        if (_legendBelow != below && _uptimeHead is not null && _legend is not null)
        {
            // Under 640 the legend drops under the hint, left aligned.
            _legendBelow = below;
            _uptimeHead.RowDefinitions = below ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            Grid.SetRow(_legend, below ? 1 : 0);
            Grid.SetColumn(_legend, below ? 0 : 1);
            Grid.SetColumnSpan(_legend, below ? 2 : 1);
            _legend.HorizontalAlignment = below ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
            _legend.Margin = below ? new Thickness(0, 8, 0, 0) : default;
        }
        if (_compact != compact)
        {
            _compact = compact;
            PseudoClasses.Set(CompactPseudoClass, compact);
            foreach (var row in _rows)
            {
                row.Button.IsCompact = compact;
            }
            if (_header is not null && _action is not null)
            {
                _header.ColumnDefinitions = compact ? new ColumnDefinitions("28,*") : new ColumnDefinitions("28,*,Auto");
                _header.RowDefinitions = compact ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
                Grid.SetRow(_action, compact ? 1 : 0);
                Grid.SetColumn(_action, compact ? 1 : 2);
                _action.HorizontalAlignment = compact ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
            }
        }
    }
}
