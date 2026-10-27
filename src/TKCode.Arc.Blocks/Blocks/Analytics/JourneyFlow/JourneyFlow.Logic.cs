using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class JourneyFlow : IJourneyFlowChartHost
{
    private const double WideBreakpoint = 860;
    private const double CompactBreakpoint = 560;
    private const double TightBreakpoint = 480;
    private static readonly string[] DefaultTableHeaders = ["From", "To", "Users", "Share of step"];

    private JourneyFlowModel _model = JourneyFlowModel.Empty;
    private string _signature = string.Empty;
    private JourneyStage[] _stageList = [];
    private JourneyNode[] _nodeList = [];
    private JourneySegment[] _segmentList = [];
    private double[][] _segmentValues = [];
    private JourneyLink?[][] _segmentLinks = [];
    private JourneySegment? _segment;
    private int _segmentIndex = -1;
    private double[] _values = [];
    private double[] _totals = [];
    private readonly List<AvaloniaObject> _tracked = new();
    private readonly List<INotifyCollectionChanged> _trackedSources = new();
    private bool _refreshing;

    private JourneyFlowHeaderPanel? _header;
    private AnalyticsSegmentSwitch? _segments;
    private JourneyFlowRollingText? _total;
    private TextBlock? _subtitleBefore;
    private TextBlock? _subtitleAfter;
    private Control? _subtitleRow;
    private JourneyFlowChart? _chart;
    private AnalyticsTextSwap? _panelTitle;
    private ArcContentSwap? _panelNote;
    private AvaloniaButton? _clear;
    private JourneyFlowRollingText? _bigNumber;
    private JourneyFlowRollingText? _captionShare;
    private AnalyticsTextSwap? _captionText;
    private AnalyticsTextSwap? _firstHeader;
    private StackPanel? _firstRows;
    private AnalyticsTextSwap? _secondHeader;
    private StackPanel? _secondRows;
    private JourneyFlowTableButton? _tableButton;
    private JourneyFlowTable? _table;
    private AnalyticsAnnouncer? _announcer;
    private string? _noteText;

    /// <summary>The chart part (tests).</summary>
    internal JourneyFlowChart? Chart => _chart;

    /// <summary>The segment switch part (tests).</summary>
    internal AnalyticsSegmentSwitch? SegmentSwitch => _segments;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The flow table (tests).</summary>
    internal JourneyFlowTable? Table => _table;

    /// <summary>The side panel's rows of the first and second group (tests).</summary>
    internal IReadOnlyList<JourneyFlowRow> FirstRows => _firstRows?.Children.OfType<JourneyFlowRow>().ToList() ?? [];

    /// <inheritdoc cref="FirstRows"/>
    internal IReadOnlyList<JourneyFlowRow> SecondRows => _secondRows?.Children.OfType<JourneyFlowRow>().ToList() ?? [];

    /// <summary>The side panel's title, note, big number and caption as shown (tests).</summary>
    internal (string? Title, string? Note, string? Big, string? Share, string? Caption, string? First, string? Second) PanelTexts =>
        (_panelTitle?.Text, _noteText, _bigNumber?.Text, _captionShare?.Text, _captionText?.Text, _firstHeader?.Text, _secondHeader?.Text);

    /// <summary>The structure shown (tests).</summary>
    internal JourneyFlowModel Model => _model;

    /// <summary>Clears the selected node.</summary>
    public void ClearSelection() => SetCurrentValue(SelectedNodeProperty, null);

    /// <summary>Selects the node with <paramref name="key"/> (its <see cref="JourneyNode.Key"/> or header); false when there is none.</summary>
    public bool SelectNode(string key)
    {
        var node = _nodeList.FirstOrDefault(n => n.EffectiveKey == key) ?? EffectiveNodes().FirstOrDefault(n => n.EffectiveKey == key);
        if (node is null)
        {
            return false;
        }
        SetCurrentValue(SelectedNodeProperty, node);
        return true;
    }

    /// <summary>
    /// The cohort of <paramref name="node"/> in the shown segment: for every link, how many of its people also passed
    /// through the node (what the chart traces).
    /// </summary>
    public IReadOnlyDictionary<JourneyLink, double> GetCohort(JourneyNode node)
    {
        var index = Array.IndexOf(_nodeList, node);
        var cohort = new double[_model.LinkCount];
        JourneyFlowEngine.NodeCohort(_model, _values, index, cohort);
        return ToLinks(cohort);
    }

    /// <summary>The cohort of <paramref name="link"/> (a link of the shown segment) in every link of the shown segment.</summary>
    public IReadOnlyDictionary<JourneyLink, double> GetCohort(JourneyLink link)
    {
        var links = _segmentIndex >= 0 ? _segmentLinks[_segmentIndex] : [];
        var index = Array.IndexOf(links, link);
        var cohort = new double[_model.LinkCount];
        JourneyFlowEngine.LinkCohort(_model, _values, index, cohort);
        return ToLinks(cohort);
    }

    private IReadOnlyDictionary<JourneyLink, double> ToLinks(double[] cohort)
    {
        var result = new Dictionary<JourneyLink, double>();
        if (_segmentIndex < 0)
        {
            return result;
        }
        var links = _segmentLinks[_segmentIndex];
        for (var l = 0; l < cohort.Length; l++)
        {
            if (links[l] is { } link)
            {
                result[link] = cohort[l];
            }
        }
        return result;
    }

    // Template ------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_segments is not null)
        {
            _segments.SelectionChanged -= OnSegmentSwitchChanged;
        }
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        if (_tableButton is not null)
        {
            _tableButton.Click -= OnTableButtonClick;
        }
        if (_chart is not null)
        {
            _chart.Host = null;
        }
        _header = e.NameScope.Find<JourneyFlowHeaderPanel>(HeaderPart);
        _segments = e.NameScope.Find<AnalyticsSegmentSwitch>(SegmentsPart);
        _total = e.NameScope.Find<JourneyFlowRollingText>(TotalPart);
        _subtitleBefore = e.NameScope.Find<TextBlock>("PART_SubtitleBefore");
        _subtitleAfter = e.NameScope.Find<TextBlock>("PART_SubtitleAfter");
        _subtitleRow = e.NameScope.Find<Control>("PART_SubtitleRow");
        _chart = e.NameScope.Find<JourneyFlowChart>(ChartPart);
        _panelTitle = e.NameScope.Find<AnalyticsTextSwap>(PanelTitlePart);
        _panelNote = e.NameScope.Find<ArcContentSwap>(PanelNotePart);
        _clear = e.NameScope.Find<AvaloniaButton>(ClearPart);
        _bigNumber = e.NameScope.Find<JourneyFlowRollingText>(BigNumberPart);
        _captionShare = e.NameScope.Find<JourneyFlowRollingText>(CaptionSharePart);
        _captionText = e.NameScope.Find<AnalyticsTextSwap>(CaptionTextPart);
        _firstHeader = e.NameScope.Find<AnalyticsTextSwap>(FirstGroupHeaderPart);
        _firstRows = e.NameScope.Find<StackPanel>(FirstGroupRowsPart);
        _secondHeader = e.NameScope.Find<AnalyticsTextSwap>(SecondGroupHeaderPart);
        _secondRows = e.NameScope.Find<StackPanel>(SecondGroupRowsPart);
        _tableButton = e.NameScope.Find<JourneyFlowTableButton>(TableButtonPart);
        _table = e.NameScope.Find<JourneyFlowTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_segments is not null)
        {
            _segments.Look = AnalyticsSegmentLook.Flow;
            _segments.SelectionChanged += OnSegmentSwitchChanged;
        }
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
        }
        if (_tableButton is not null)
        {
            _tableButton.Click += OnTableButtonClick;
            _tableButton.IsExpanded = IsTableExpanded;
        }
        if (_chart is not null)
        {
            _chart.Host = this;
        }
        _signature = string.Empty;
        _model = JourneyFlowModel.Empty;
        Refresh(animate: false);
        UpdateBreakpoints(Bounds.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BoundsProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (property == SelectedSegmentProperty)
        {
            RaiseEvent(new JourneySegmentChangedEventArgs(SelectedSegmentChangedEvent,
                change.GetOldValue<JourneySegment?>(), change.GetNewValue<JourneySegment?>()));
            Refresh(animate: true);
            Announce();
        }
        else if (property == SelectedNodeProperty)
        {
            RaiseEvent(new JourneyNodeChangedEventArgs(SelectedNodeChangedEvent,
                change.GetOldValue<JourneyNode?>(), change.GetNewValue<JourneyNode?>()));
            ApplySelection();
        }
        else if (property == StagesSourceProperty || property == NodesSourceProperty || property == SegmentsSourceProperty)
        {
            Refresh(animate: false);
        }
        else if (property == IsTableExpandedProperty)
        {
            if (_tableButton is not null)
            {
                _tableButton.IsExpanded = IsTableExpanded;
            }
        }
        else if (property == ValueFormatterProperty || property == ShareFormatterProperty || property == LabelShareFormatterProperty ||
                 property == CountFormatProperty || property == ShareOfFormatProperty || property == VisitorsShareTextProperty ||
                 property == VisitorsCaptionProperty || property == GoalCaptionProperty || property == OverviewHintProperty ||
                 property == DropOffsHeaderProperty || property == OutcomesHeaderProperty || property == InboundHeaderProperty ||
                 property == OutboundHeaderProperty || property == JourneyStartTextProperty || property == JourneyEndTextProperty ||
                 property == TableCaptionFormatProperty || property == TableColumnHeadersProperty || property == MaxDropOffRowsProperty ||
                 property == SubtitleFormatProperty || property == SegmentsLabelProperty || property == ChartLabelProperty)
        {
            UpdateTexts();
        }
    }

    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        PseudoClasses.Set(WidePseudoClass, width >= WideBreakpoint);
        PseudoClasses.Set(CompactPseudoClass, width < CompactBreakpoint);
        PseudoClasses.Set(TightPseudoClass, width < TightBreakpoint);
    }

    // Data ----------------------------------------------------------------------------------------------------------

    private IEnumerable<JourneyStage> EffectiveStages() => StagesSource ?? Stages;

    private IEnumerable<JourneyNode> EffectiveNodes() => NodesSource ?? Nodes;

    private IEnumerable<JourneySegment> EffectiveSegments() => SegmentsSource ?? Segments;

    private void OnItemsChanged(NotifyCollectionChangedEventArgs e, IEnumerable owner)
    {
        Refresh(animate: false);
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        if (_refreshing)
        {
            return;
        }
        // A count change in the shown segment morphs; anything else (names, keys, other segments) applies at once.
        Refresh(animate: ReferenceEquals(sender, _segment));
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh(animate: false);

    private void Track()
    {
        foreach (var item in _tracked)
        {
            switch (item)
            {
                case JourneyStage stage:
                    stage.Changed -= OnItemChanged;
                    break;
                case JourneyNode node:
                    node.Changed -= OnItemChanged;
                    break;
                case JourneySegment segment:
                    segment.Changed -= OnItemChanged;
                    break;
            }
        }
        _tracked.Clear();
        foreach (var source in _trackedSources)
        {
            source.CollectionChanged -= OnSourceChanged;
        }
        _trackedSources.Clear();
        foreach (var source in new IEnumerable?[] { StagesSource, NodesSource, SegmentsSource })
        {
            if (source is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged += OnSourceChanged;
                _trackedSources.Add(observable);
            }
        }
        foreach (var stage in EffectiveStages())
        {
            stage.Changed += OnItemChanged;
            _tracked.Add(stage);
        }
        foreach (var node in EffectiveNodes())
        {
            node.Changed += OnItemChanged;
            _tracked.Add(node);
        }
        foreach (var segment in EffectiveSegments())
        {
            segment.Changed += OnItemChanged;
            _tracked.Add(segment);
        }
    }

    private void Refresh(bool animate)
    {
        if (_refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            Track();
            var stages = EffectiveStages().ToArray();
            var nodes = EffectiveNodes().ToArray();
            var segments = EffectiveSegments().ToArray();
            var pairs = new List<(string?, string?)>();
            foreach (var segment in segments)
            {
                foreach (var link in segment.Links)
                {
                    pairs.Add((link.SourceKey, link.TargetKey));
                }
            }
            var signature = Signature(stages, nodes, pairs);
            var structureChanged = signature != _signature;
            if (structureChanged)
            {
                _signature = signature;
                _model = JourneyFlowModel.Build(
                    stages.Select(s => s.EffectiveKey).ToList(),
                    nodes.Select(n => (n.EffectiveKey, n.StageKey, n.Kind)).ToList(),
                    pairs, out var nodeIndexOf, out _);
                var modelNodes = new JourneyNode[_model.NodeCount];
                for (var i = 0; i < nodes.Length; i++)
                {
                    if (nodeIndexOf[i] >= 0)
                    {
                        modelNodes[nodeIndexOf[i]] = nodes[i];
                    }
                }
                _nodeList = modelNodes;
            }
            _stageList = stages;
            _segmentList = segments;
            _segmentValues = new double[segments.Length][];
            _segmentLinks = new JourneyLink?[segments.Length][];
            var keyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var n = 0; n < _nodeList.Length; n++)
            {
                if (_nodeList[n].EffectiveKey is { } key)
                {
                    keyIndex.TryAdd(key, n);
                }
            }
            for (var s = 0; s < segments.Length; s++)
            {
                var values = new double[_model.LinkCount];
                var links = new JourneyLink?[_model.LinkCount];
                foreach (var link in segments[s].Links)
                {
                    if (link.SourceKey is null || link.TargetKey is null ||
                        !keyIndex.TryGetValue(link.SourceKey, out var source) || !keyIndex.TryGetValue(link.TargetKey, out var target))
                    {
                        continue;
                    }
                    var l = _model.FindLink(source, target);
                    if (l < 0)
                    {
                        continue;
                    }
                    values[l] += double.IsFinite(link.Value) && link.Value > 0 ? link.Value : 0;
                    links[l] ??= link;
                }
                _segmentValues[s] = values;
                _segmentLinks[s] = links;
            }
            _segmentIndex = SelectedSegment is { } selected ? Array.IndexOf(segments, selected) : -1;
            if (_segmentIndex < 0 && segments.Length > 0)
            {
                _segmentIndex = 0;
            }
            _segment = _segmentIndex >= 0 ? segments[_segmentIndex] : null;
            _values = _segmentIndex >= 0 ? _segmentValues[_segmentIndex] : new double[_model.LinkCount];
            _totals = new double[_model.NodeCount];
            JourneyFlowEngine.NodeTotals(_model, _values, _totals);
            Visitors = JourneyFlowEngine.Visitors(_model, _totals);
            GoalTotal = JourneyFlowEngine.GoalTotal(_model, _totals);
            PseudoClasses.Set(EmptyPseudoClass, _model.NodeCount == 0 || Visitors <= 0);

            if (_chart is not null)
            {
                _chart.SetData(_model, _values, animate && !structureChanged);
                _chart.SelectedNode = SelectedNode is { } node ? Array.IndexOf(_nodeList, node) : -1;
            }
            UpdateSegmentSwitch();
            UpdateTexts();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static string Signature(JourneyStage[] stages, JourneyNode[] nodes, List<(string?, string?)> pairs)
    {
        var builder = new StringBuilder();
        foreach (var stage in stages)
        {
            builder.Append(stage.EffectiveKey).Append('\u001f');
        }
        builder.Append('\u001e');
        foreach (var node in nodes)
        {
            builder.Append(node.EffectiveKey).Append('\u001f').Append(node.StageKey).Append('\u001f').Append((int)node.Kind).Append('\u001e');
        }
        builder.Append('\u001d');
        foreach (var (source, target) in pairs.Distinct())
        {
            builder.Append(source).Append('\u001f').Append(target).Append('\u001e');
        }
        return builder.ToString();
    }

    private void UpdateSegmentSwitch()
    {
        if (_segments is null)
        {
            return;
        }
        var headers = _segmentList.Select(s => s.Header ?? s.EffectiveKey ?? string.Empty).ToArray();
        if (_segments.Items is not { } items || !items.SequenceEqual(headers))
        {
            _segments.Items = headers;
        }
        _segments.IsVisible = headers.Length >= 2;
        AutomationProperties.SetName(_segments, SegmentsLabel);
        if (_segments.SelectedIndex != _segmentIndex)
        {
            _segments.SelectedIndex = _segmentIndex;
        }
    }

    private void OnSegmentSwitchChanged(object? sender, AnalyticsSegmentChangedEventArgs e)
    {
        if (!e.ByUser || e.NewIndex < 0 || e.NewIndex >= _segmentList.Length)
        {
            return;
        }
        var segment = _segmentList[e.NewIndex];
        SetCurrentValue(SelectedSegmentProperty, segment);
        if (SegmentSelectedCommand is { } command && command.CanExecute(segment))
        {
            command.Execute(segment);
        }
    }

    private void Announce()
    {
        if (_announcer is null || _segment is null)
        {
            return;
        }
        _announcer.Announce(string.Format(CultureInfo.CurrentCulture, AnnouncementFormat ?? string.Empty,
            SegmentHeader(), FormatCount(Visitors), FormatCount(GoalTotal)));
    }

    // Selection -----------------------------------------------------------------------------------------------------

    private void ApplySelection()
    {
        var index = SelectedNode is { } node ? Array.IndexOf(_nodeList, node) : -1;
        if (_chart is not null)
        {
            _chart.SelectedNode = index;
        }
        PseudoClasses.Set(HasSelectionPseudoClass, index >= 0);
        UpdateTracingClass();
        UpdatePanel();
    }

    private void UpdateTracingClass() =>
        PseudoClasses.Set(TracingPseudoClass, HoveredNode is not null || HoveredLink is not null || (SelectedNode is not null && Array.IndexOf(_nodeList, SelectedNode) >= 0));

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        var index = SelectedNode is { } node ? Array.IndexOf(_nodeList, node) : -1;
        UserSelect(null);
        // The button disappears with the selection: the focus goes back to the step it cleared.
        if (index >= 0)
        {
            _chart?.FocusNode(index);
        }
    }

    private void OnTableButtonClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsTableExpandedProperty, !IsTableExpanded);

    private void UserSelect(JourneyNode? node)
    {
        SetCurrentValue(SelectedNodeProperty, node);
        if (NodeSelectedCommand is { } command && command.CanExecute(node))
        {
            command.Execute(node);
        }
    }

    void IJourneyFlowChartHost.OnNodeInvoked(int node)
    {
        if (node < 0 || node >= _nodeList.Length)
        {
            return;
        }
        UserSelect(ReferenceEquals(SelectedNode, _nodeList[node]) ? null : _nodeList[node]);
    }

    void IJourneyFlowChartHost.OnClearRequested()
    {
        if (SelectedNode is not null)
        {
            UserSelect(null);
        }
    }

    void IJourneyFlowChartHost.OnTraceChanged(int node, int link)
    {
        HoveredNode = node >= 0 && node < _nodeList.Length ? _nodeList[node] : null;
        HoveredLink = link >= 0 && _segmentIndex >= 0 ? _segmentLinks[_segmentIndex][link] : null;
        UpdateTracingClass();
        RaiseEvent(new JourneyTraceChangedEventArgs(TraceChangedEvent, HoveredNode, HoveredLink));
    }

    // Words and numbers ---------------------------------------------------------------------------------------------

    /// <summary>A count as text (<see cref="ValueFormatter"/> or grouped whole number).</summary>
    internal string FormatCount(double value) =>
        ValueFormatter is { } formatter ? formatter(value) : Math.Round(value).ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>A share (fraction) as text (<see cref="ShareFormatter"/> or one decimal).</summary>
    internal string FormatShare(double share) =>
        ShareFormatter is { } formatter ? formatter(share) : (share * 100).ToString("0.0", CultureInfo.CurrentCulture) + "%";

    private string People(double value) => string.Format(CultureInfo.CurrentCulture, CountFormat ?? "{0}", FormatCount(value));

    private string NodeName(int node) => node >= 0 && node < _nodeList.Length ? _nodeList[node].Header ?? _nodeList[node].EffectiveKey ?? string.Empty : string.Empty;

    private string ShareName(int node)
    {
        if (node < 0 || node >= _nodeList.Length)
        {
            return string.Empty;
        }
        if (_nodeList[node].ShareLabel is { } label)
        {
            return label;
        }
        var name = NodeName(node);
        return name.Length == 0 ? name : char.ToLower(name[0], CultureInfo.CurrentCulture) + name[1..];
    }

    private string SegmentHeader() => _segment?.Header ?? _segment?.EffectiveKey ?? string.Empty;

    private double StageTotal(int stage)
    {
        double sum = 0;
        foreach (var n in _model.StageNodes[stage])
        {
            sum += _totals[n];
        }
        return sum;
    }

    string IJourneyFlowChartHost.NodeHeader(int node) => NodeName(node);

    string IJourneyFlowChartHost.StageHeader(int stage, bool shortForm)
    {
        if (stage < 0 || stage >= _stageList.Length)
        {
            return string.Empty;
        }
        var s = _stageList[stage];
        return (shortForm ? s.ShortHeader : null) ?? s.Header ?? s.EffectiveKey ?? string.Empty;
    }

    string IJourneyFlowChartHost.FormatCount(double value) => FormatCount(value);

    string IJourneyFlowChartHost.FormatLabelShare(double share) =>
        LabelShareFormatter is { } formatter ? formatter(share) : Math.Round(share * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    AnalyticsReadoutContent IJourneyFlowChartHost.NodeTooltip(int node)
    {
        var total = _totals[node];
        var stage = _model.NodeStage[node];
        var rows = new List<AnalyticsReadoutRow>();
        if (stage == 0)
        {
            foreach (var l in _model.Outgoing[node])
            {
                rows.Add(new AnalyticsReadoutRow(NodeName(_model.LinkTarget[l]), FormatShare(total > 0 ? _values[l] / total : 0)));
            }
        }
        else
        {
            var stageTotal = StageTotal(stage);
            var baseLabel = _stageList.Length > stage ? _stageList[stage].ShareBaseLabel : null;
            if (baseLabel is null)
            {
                var baseNode = JourneyFlowEngine.ShareBaseNode(_model, _totals, stage);
                baseLabel = baseNode >= 0 ? ShareName(baseNode) : ((IJourneyFlowChartHost)this).StageHeader(stage, false);
            }
            rows.Add(new AnalyticsReadoutRow(string.Format(CultureInfo.CurrentCulture, ShareOfFormat ?? "{0}", baseLabel),
                FormatShare(stageTotal > 0 ? total / stageTotal : 0)));
            rows.Add(new AnalyticsReadoutRow(VisitorsShareText ?? string.Empty, FormatShare(Visitors > 0 ? total / Visitors : 0)));
        }
        return new AnalyticsReadoutContent(NodeName(node), People(total), rows);
    }

    AnalyticsReadoutContent IJourneyFlowChartHost.LinkTooltip(int link)
    {
        var source = _model.LinkSource[link];
        var target = _model.LinkTarget[link];
        var value = _values[link];
        var sourceTotal = _totals[source];
        var rows = new List<AnalyticsReadoutRow>
        {
            new(string.Format(CultureInfo.CurrentCulture, ShareOfFormat ?? "{0}", ShareName(source)), FormatShare(sourceTotal > 0 ? value / sourceTotal : 0)),
            new(VisitorsShareText ?? string.Empty, FormatShare(Visitors > 0 ? value / Visitors : 0))
        };
        return new AnalyticsReadoutContent($"{NodeName(source)} → {NodeName(target)}", People(value), rows);
    }

    string IJourneyFlowChartHost.NodeAutomationName(int node)
    {
        var total = _totals.Length > node ? _totals[node] : 0;
        var name = $"{NodeName(node)}: {People(total)}, {FormatShare(Visitors > 0 ? total / Visitors : 0)} {VisitorsCaption}.";
        return _nodeList[node].Description is { Length: > 0 } description ? $"{name} {description.TrimEnd('.')}." : name;
    }

    private void UpdateTexts()
    {
        if (_refreshing && _chart is null)
        {
            return;
        }
        UpdateSubtitle();
        UpdatePanel();
        UpdateTable();
        UpdateChartSummary();
        if (_segments is not null)
        {
            AutomationProperties.SetName(_segments, SegmentsLabel);
        }
        _chart?.RefreshTexts();
    }

    private void UpdateSubtitle()
    {
        var (before, after, hasNumber) = SubtitleParts();
        if (_subtitleRow is not null)
        {
            _subtitleRow.IsVisible = SubtitleFormat is not null;
        }
        if (_subtitleBefore is not null)
        {
            _subtitleBefore.Text = before;
            _subtitleBefore.IsVisible = before.Length > 0;
        }
        if (_subtitleAfter is not null)
        {
            _subtitleAfter.Text = after;
            _subtitleAfter.IsVisible = after.Length > 0;
        }
        if (_total is not null)
        {
            _total.IsVisible = hasNumber;
            _total.Text = FormatCount(Visitors);
        }
    }

    /// <summary>Text before and after <c>{0}</c> in <see cref="SubtitleFormat"/> (the template binds them).</summary>
    internal (string Before, string After, bool HasNumber) SubtitleParts()
    {
        var format = SubtitleFormat ?? string.Empty;
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        return at < 0 ? (format, string.Empty, false) : (format[..at], format[(at + 3)..], true);
    }

    private void UpdateChartSummary()
    {
        if (_chart is null)
        {
            return;
        }
        var builder = new StringBuilder();
        builder.Append(SegmentHeader());
        if (builder.Length > 0)
        {
            builder.Append(". ");
        }
        for (var s = 0; s < _model.StageCount; s++)
        {
            builder.Append(((IJourneyFlowChartHost)this).StageHeader(s, false)).Append(": ");
            var nodes = _model.StageNodes[s];
            for (var i = 0; i < nodes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(NodeName(nodes[i])).Append(' ').Append(People(_totals[nodes[i]]));
            }
            builder.Append(". ");
        }
        AutomationProperties.SetName(_chart, ChartLabel);
        AutomationProperties.SetHelpText(_chart, builder.ToString().TrimEnd());
    }

    private void UpdateTable()
    {
        if (_table is null)
        {
            return;
        }
        var rows = new List<string[]>();
        for (var l = 0; l < _model.LinkCount; l++)
        {
            var source = _model.LinkSource[l];
            var total = _totals[source];
            rows.Add([NodeName(source), NodeName(_model.LinkTarget[l]), FormatCount(_values[l]), FormatShare(total > 0 ? _values[l] / total : 0)]);
        }
        var headers = TableColumnHeaders is { Count: >= 4 } custom ? custom : DefaultTableHeaders;
        _table.Update(string.Format(CultureInfo.CurrentCulture, TableCaptionFormat ?? "{0}", SegmentHeader()), headers, rows);
    }

    private int SlotCount()
    {
        var slots = Math.Max(2, MaxDropOffRows);
        if (_model.StageCount > 0)
        {
            slots = Math.Max(slots, _model.StageNodes[_model.StageCount - 1].Length);
        }
        for (var n = 0; n < _model.NodeCount; n++)
        {
            slots = Math.Max(slots, Math.Max(_model.Incoming[n].Length, _model.Outgoing[n].Length));
        }
        return slots;
    }

    private static List<JourneyFlowRow> EnsureRows(StackPanel panel, int count)
    {
        while (panel.Children.Count < count)
        {
            panel.Children.Add(new JourneyFlowRow());
        }
        while (panel.Children.Count > count)
        {
            panel.Children.RemoveAt(panel.Children.Count - 1);
        }
        return panel.Children.OfType<JourneyFlowRow>().ToList();
    }

    private void UpdatePanel()
    {
        if (_panelTitle is null || _firstRows is null || _secondRows is null)
        {
            return;
        }
        var slots = SlotCount();
        var first = EnsureRows(_firstRows, slots);
        var second = EnsureRows(_secondRows, slots);
        var selected = SelectedNode is { } node ? Array.IndexOf(_nodeList, node) : -1;
        string title, note, caption, firstHeader, secondHeader;
        double big, share;
        if (selected >= 0)
        {
            title = NodeName(selected);
            note = _nodeList[selected].Description ?? string.Empty;
            big = _totals[selected];
            share = Visitors > 0 ? big / Visitors : 0;
            caption = VisitorsCaption;
            firstHeader = InboundHeader;
            secondHeader = OutboundHeader;
            double inSum = 0;
            foreach (var l in _model.Incoming[selected])
            {
                inSum += _values[l];
            }
            FillRows(first, _model.Incoming[selected].Select(l => (NodeName(_model.LinkSource[l]), _values[l], inSum > 0 ? _values[l] / inSum : 0)).ToList(), JourneyStartText);
            FillRows(second, _model.Outgoing[selected].Select(l => (NodeName(_model.LinkTarget[l]), _values[l], big > 0 ? _values[l] / big : 0)).ToList(), JourneyEndText);
        }
        else
        {
            title = SegmentHeader();
            note = OverviewHint ?? string.Empty;
            big = GoalTotal;
            share = Visitors > 0 ? big / Visitors : 0;
            caption = GoalCaption;
            firstHeader = DropOffsHeader;
            var lastStage = _model.StageCount - 1;
            secondHeader = OutcomesHeader ?? (lastStage >= 0 ? ((IJourneyFlowChartHost)this).StageHeader(lastStage, false) : string.Empty);
            var drops = JourneyFlowEngine.DropOffs(_model, _values, _totals, Math.Max(0, MaxDropOffRows));
            FillRows(first, drops.Select(d => (_nodeList[d.Node].DropOffLabel ?? NodeName(d.Node), d.Count, d.Share)).ToList(), null);
            var outcomes = new List<(string, double, double)>();
            if (lastStage >= 0)
            {
                var stageTotal = StageTotal(lastStage);
                foreach (var n in _model.StageNodes[lastStage])
                {
                    outcomes.Add((NodeName(n), _totals[n], stageTotal > 0 ? _totals[n] / stageTotal : 0));
                }
            }
            FillRows(second, outcomes, null);
        }
        _panelTitle.Text = title;
        if (_panelNote is not null && note != _noteText)
        {
            _noteText = note;
            _panelNote.Content = new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, LineHeight = 18.2 };
        }
        if (_bigNumber is not null)
        {
            _bigNumber.Text = FormatCount(big);
        }
        if (_captionShare is not null)
        {
            _captionShare.Text = FormatShare(share);
        }
        if (_captionText is not null)
        {
            _captionText.Text = caption;
        }
        if (_firstHeader is not null)
        {
            _firstHeader.Text = firstHeader;
        }
        if (_secondHeader is not null)
        {
            _secondHeader.Text = secondHeader;
        }
        if (_clear is not null)
        {
            _clear.IsVisible = selected >= 0;
            AutomationProperties.SetName(_clear, ClearText);
        }
    }

    private void FillRows(List<JourneyFlowRow> rows, List<(string Label, double Count, double Share)> data, string? placeholder)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (i < data.Count)
            {
                rows[i].ShowData(data[i].Label, FormatCount(data[i].Count), FormatShare(data[i].Share), data[i].Share);
            }
            else if (i == 0 && data.Count == 0 && placeholder is not null)
            {
                rows[i].ShowPlaceholder(placeholder);
            }
            else
            {
                rows[i].ShowEmpty();
            }
        }
    }
}
