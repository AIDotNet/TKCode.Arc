using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A journey as a Sankey chart: stages left to right, the people at each step as bars and the flows between steps as
/// ribbons, with the people who leave below the main path and the goal in green. Point at a step or a flow to trace that
/// cohort through the whole journey (where they came from, where they went) with a tooltip; click a step to keep it
/// traced and read its details in the side panel (where people came from and went next); switch segments (channels,
/// cohorts) and every bar, ribbon and count morphs to the new numbers. Under the chart, a "Flow table" lists every flow.
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="Stages"/> (columns), <see cref="Nodes"/> (each in a stage, stacked in list order) and one or more
/// <see cref="Segments"/> whose <see cref="JourneyLink"/>s give the counts between nodes of neighbouring stages. A node's
/// count is the larger of what flows in and what flows out; the first stage's total is the visitors.
/// </para>
/// <para>
/// Keys: the segment switch takes ←/→ (wrapping; the selection follows), Home / End. The chart is one tab stop: ↑/↓ move
/// within a stage, ←/→ to the nearest node of the next stage, Home / End to the first and last node; a focused node is
/// traced with its tooltip; Enter or Space selects it (again clears); Escape clears the selection.
/// </para>
/// </remarks>
[TemplatePart(HeaderPart, typeof(Panel))]
[TemplatePart(SegmentsPart, typeof(Control))]
[TemplatePart(TotalPart, typeof(Control))]
[TemplatePart(ChartPart, typeof(Control))]
[TemplatePart(PanelTitlePart, typeof(Control))]
[TemplatePart(PanelNotePart, typeof(ArcContentSwap))]
[TemplatePart(ClearPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(BigNumberPart, typeof(Control))]
[TemplatePart(CaptionSharePart, typeof(Control))]
[TemplatePart(CaptionTextPart, typeof(Control))]
[TemplatePart(FirstGroupHeaderPart, typeof(Control))]
[TemplatePart(FirstGroupRowsPart, typeof(StackPanel))]
[TemplatePart(SecondGroupHeaderPart, typeof(Control))]
[TemplatePart(SecondGroupRowsPart, typeof(StackPanel))]
[TemplatePart(TableButtonPart, typeof(Avalonia.Controls.Button))]
[TemplatePart(TablePart, typeof(Grid))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(WidePseudoClass, CompactPseudoClass, TightPseudoClass, TracingPseudoClass, HasSelectionPseudoClass, EmptyPseudoClass)]
public partial class JourneyFlow : TemplatedControl
{
    /// <summary>Template part: the header row.</summary>
    public const string HeaderPart = "PART_Header";

    /// <summary>Template part: the segment switch.</summary>
    public const string SegmentsPart = "PART_Segments";

    /// <summary>Template part: the rolling visitors count in the subtitle.</summary>
    public const string TotalPart = "PART_Total";

    /// <summary>Template part: the chart.</summary>
    public const string ChartPart = "PART_Chart";

    /// <summary>Template part: the side panel's title.</summary>
    public const string PanelTitlePart = "PART_PanelTitle";

    /// <summary>Template part: the side panel's note.</summary>
    public const string PanelNotePart = "PART_PanelNote";

    /// <summary>Template part: the side panel's Clear button.</summary>
    public const string ClearPart = "PART_Clear";

    /// <summary>Template part: the side panel's big number.</summary>
    public const string BigNumberPart = "PART_BigNumber";

    /// <summary>Template part: the share under the big number.</summary>
    public const string CaptionSharePart = "PART_CaptionShare";

    /// <summary>Template part: the words after that share.</summary>
    public const string CaptionTextPart = "PART_CaptionText";

    /// <summary>Template part: the first group's header.</summary>
    public const string FirstGroupHeaderPart = "PART_FirstGroupHeader";

    /// <summary>Template part: the first group's rows.</summary>
    public const string FirstGroupRowsPart = "PART_FirstGroupRows";

    /// <summary>Template part: the second group's header.</summary>
    public const string SecondGroupHeaderPart = "PART_SecondGroupHeader";

    /// <summary>Template part: the second group's rows.</summary>
    public const string SecondGroupRowsPart = "PART_SecondGroupRows";

    /// <summary>Template part: the "Flow table" button.</summary>
    public const string TableButtonPart = "PART_TableButton";

    /// <summary>Template part: the flow table.</summary>
    public const string TablePart = "PART_Table";

    /// <summary>Template part: the hidden live region.</summary>
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is 860 wide or more: the side panel sits beside the chart.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set while the block is 559 wide or less: tighter padding, a full-width switch, one column of groups.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is 479 wide or less: smaller chart labels.</summary>
    public const string TightPseudoClass = ":tight";

    /// <summary>Set while a node or link is traced (pointer, keyboard focus or selection).</summary>
    public const string TracingPseudoClass = ":tracing";

    /// <summary>Set while a node is selected.</summary>
    public const string HasSelectionPseudoClass = ":has-selection";

    /// <summary>Set while there is nothing to draw (no nodes or no counts).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>The heading over the chart; null hides it.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<JourneyFlow, string?>(nameof(Title));

    /// <summary>
    /// The line under the heading; <c>{0}</c> is the visitors count, which rolls when the segment changes. Default
    /// "{0} visits"; null hides the line.
    /// </summary>
    public static readonly StyledProperty<string?> SubtitleFormatProperty =
        AvaloniaProperty.Register<JourneyFlow, string?>(nameof(SubtitleFormat), "{0} visits");

    /// <summary>Stages used instead of <see cref="Stages"/> when set (an observable collection is followed).</summary>
    public static readonly StyledProperty<IEnumerable<JourneyStage>?> StagesSourceProperty =
        AvaloniaProperty.Register<JourneyFlow, IEnumerable<JourneyStage>?>(nameof(StagesSource));

    /// <summary>Nodes used instead of <see cref="Nodes"/> when set (an observable collection is followed).</summary>
    public static readonly StyledProperty<IEnumerable<JourneyNode>?> NodesSourceProperty =
        AvaloniaProperty.Register<JourneyFlow, IEnumerable<JourneyNode>?>(nameof(NodesSource));

    /// <summary>Segments used instead of <see cref="Segments"/> when set (an observable collection is followed).</summary>
    public static readonly StyledProperty<IEnumerable<JourneySegment>?> SegmentsSourceProperty =
        AvaloniaProperty.Register<JourneyFlow, IEnumerable<JourneySegment>?>(nameof(SegmentsSource));

    /// <summary>The segment shown (two-way); null shows the first.</summary>
    public static readonly StyledProperty<JourneySegment?> SelectedSegmentProperty =
        AvaloniaProperty.Register<JourneyFlow, JourneySegment?>(nameof(SelectedSegment), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected node (two-way): kept traced, detailed in the side panel, kept across segments. Null for none.</summary>
    public static readonly StyledProperty<JourneyNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<JourneyFlow, JourneyNode?>(nameof(SelectedNode), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The node previewed now (pointer over it, or keyboard focus on it); null when none.</summary>
    public static readonly DirectProperty<JourneyFlow, JourneyNode?> HoveredNodeProperty =
        AvaloniaProperty.RegisterDirect<JourneyFlow, JourneyNode?>(nameof(HoveredNode), o => o.HoveredNode);

    /// <summary>The link previewed now (pointer over its ribbon); null when none.</summary>
    public static readonly DirectProperty<JourneyFlow, JourneyLink?> HoveredLinkProperty =
        AvaloniaProperty.RegisterDirect<JourneyFlow, JourneyLink?>(nameof(HoveredLink), o => o.HoveredLink);

    /// <summary>Visitors of the shown segment: the first stage's total.</summary>
    public static readonly DirectProperty<JourneyFlow, double> VisitorsProperty =
        AvaloniaProperty.RegisterDirect<JourneyFlow, double>(nameof(Visitors), o => o.Visitors);

    /// <summary>People who reached a goal node in the shown segment.</summary>
    public static readonly DirectProperty<JourneyFlow, double> GoalTotalProperty =
        AvaloniaProperty.RegisterDirect<JourneyFlow, double>(nameof(GoalTotal), o => o.GoalTotal);

    /// <summary>Formats counts (labels, tooltip, side panel, table); null groups whole numbers ("12,345").</summary>
    public static readonly StyledProperty<Func<double, string>?> ValueFormatterProperty =
        AvaloniaProperty.Register<JourneyFlow, Func<double, string>?>(nameof(ValueFormatter));

    /// <summary>Formats shares given as fractions 0–1 (tooltip, side panel, table); null shows one decimal ("31.7%").</summary>
    public static readonly StyledProperty<Func<double, string>?> ShareFormatterProperty =
        AvaloniaProperty.Register<JourneyFlow, Func<double, string>?>(nameof(ShareFormatter));

    /// <summary>Formats the shares on the chart's node labels (fractions 0–1); null rounds to whole percents ("32%").</summary>
    public static readonly StyledProperty<Func<double, string>?> LabelShareFormatterProperty =
        AvaloniaProperty.Register<JourneyFlow, Func<double, string>?>(nameof(LabelShareFormatter));

    /// <summary>Spoken name of the segment switch. Default "Segment".</summary>
    public static readonly StyledProperty<string> SegmentsLabelProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(SegmentsLabel), "Segment");

    /// <summary>Spoken name of the chart (its node buttons form one group). Default "Journey steps".</summary>
    public static readonly StyledProperty<string> ChartLabelProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(ChartLabel), "Journey steps");

    /// <summary>Spoken name of the side panel. Default "Step details".</summary>
    public static readonly StyledProperty<string> PanelLabelProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(PanelLabel), "Step details");

    /// <summary>A count of people in the tooltip and spoken names; <c>{0}</c> is the formatted count. Default "{0} users".</summary>
    public static readonly StyledProperty<string> CountFormatProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(CountFormat), "{0} users");

    /// <summary>The tooltip's share row; <c>{0}</c> is what it is a share of. Default "Share of {0}".</summary>
    public static readonly StyledProperty<string> ShareOfFormatProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(ShareOfFormat), "Share of {0}");

    /// <summary>The tooltip's row for the share of all visitors. Default "Share of visitors".</summary>
    public static readonly StyledProperty<string> VisitorsShareTextProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(VisitorsShareText), "Share of visitors");

    /// <summary>Words after a selected node's share of visitors in the side panel. Default "of visitors".</summary>
    public static readonly StyledProperty<string> VisitorsCaptionProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(VisitorsCaption), "of visitors");

    /// <summary>Words after the goal's share of visitors in the overview. Default "of visitors reached the goal".</summary>
    public static readonly StyledProperty<string> GoalCaptionProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(GoalCaption), "of visitors reached the goal");

    /// <summary>The overview's note under its title.</summary>
    public static readonly StyledProperty<string?> OverviewHintProperty =
        AvaloniaProperty.Register<JourneyFlow, string?>(nameof(OverviewHint), "Choose a step to trace who reached it and where they went next");

    /// <summary>The overview's first group: the biggest exits before the last stage. Default "Biggest drop-offs".</summary>
    public static readonly StyledProperty<string> DropOffsHeaderProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(DropOffsHeader), "Biggest drop-offs");

    /// <summary>The overview's second group: the last stage's nodes. Null uses the last stage's header.</summary>
    public static readonly StyledProperty<string?> OutcomesHeaderProperty =
        AvaloniaProperty.Register<JourneyFlow, string?>(nameof(OutcomesHeader));

    /// <summary>A selected node's first group: where its people came from. Default "Arrived from".</summary>
    public static readonly StyledProperty<string> InboundHeaderProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(InboundHeader), "Arrived from");

    /// <summary>A selected node's second group: where its people went. Default "Moved on to".</summary>
    public static readonly StyledProperty<string> OutboundHeaderProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(OutboundHeader), "Moved on to");

    /// <summary>The line when a selected node has nothing flowing in. Default "The journey begins here".</summary>
    public static readonly StyledProperty<string> JourneyStartTextProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(JourneyStartText), "The journey begins here");

    /// <summary>The line when a selected node has nothing flowing out. Default "The journey stops here".</summary>
    public static readonly StyledProperty<string> JourneyEndTextProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(JourneyEndText), "The journey stops here");

    /// <summary>The label of the button that clears the selection. Default "Clear".</summary>
    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(ClearText), "Clear");

    /// <summary>The label of the button that opens the flow table. Default "Flow table".</summary>
    public static readonly StyledProperty<string> TableHeaderProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(TableHeader), "Flow table");

    /// <summary>The table's caption; <c>{0}</c> is the segment's header. Default "Flows for {0}".</summary>
    public static readonly StyledProperty<string> TableCaptionFormatProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(TableCaptionFormat), "Flows for {0}");

    /// <summary>The table's column headers: from, to, count, share of the source step.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> TableColumnHeadersProperty =
        AvaloniaProperty.Register<JourneyFlow, IReadOnlyList<string>?>(nameof(TableColumnHeaders));

    /// <summary>
    /// What is announced when the segment changes; <c>{0}</c> segment, <c>{1}</c> visitors, <c>{2}</c> goal count.
    /// Default "{0}: {1} visits and {2} reached the goal."
    /// </summary>
    public static readonly StyledProperty<string> AnnouncementFormatProperty =
        AvaloniaProperty.Register<JourneyFlow, string>(nameof(AnnouncementFormat), "{0}: {1} visits and {2} reached the goal.");

    /// <summary>How many drop-offs the overview lists. Default 2.</summary>
    public static readonly StyledProperty<int> MaxDropOffRowsProperty =
        AvaloniaProperty.Register<JourneyFlow, int>(nameof(MaxDropOffRows), 2);

    /// <summary>Shows the side panel. Default true.</summary>
    public static readonly StyledProperty<bool> IsSidePanelVisibleProperty =
        AvaloniaProperty.Register<JourneyFlow, bool>(nameof(IsSidePanelVisible), true);

    /// <summary>Shows the "Flow table" button and table. Default true.</summary>
    public static readonly StyledProperty<bool> IsTableVisibleProperty =
        AvaloniaProperty.Register<JourneyFlow, bool>(nameof(IsTableVisible), true);

    /// <summary>The flow table is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsTableExpandedProperty =
        AvaloniaProperty.Register<JourneyFlow, bool>(nameof(IsTableExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Runs when the user selects a node or clears the selection; the parameter is the node or null.</summary>
    public static readonly StyledProperty<ICommand?> NodeSelectedCommandProperty =
        AvaloniaProperty.Register<JourneyFlow, ICommand?>(nameof(NodeSelectedCommand));

    /// <summary>Runs when the user switches segments; the parameter is the new segment.</summary>
    public static readonly StyledProperty<ICommand?> SegmentSelectedCommandProperty =
        AvaloniaProperty.Register<JourneyFlow, ICommand?>(nameof(SegmentSelectedCommand));

    /// <summary>Raised when <see cref="SelectedNode"/> changes (pointer, keyboard, Clear, or code).</summary>
    public static readonly RoutedEvent<JourneyNodeChangedEventArgs> SelectedNodeChangedEvent =
        RoutedEvent.Register<JourneyFlow, JourneyNodeChangedEventArgs>(nameof(SelectedNodeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="SelectedSegment"/> changes.</summary>
    public static readonly RoutedEvent<JourneySegmentChangedEventArgs> SelectedSegmentChangedEvent =
        RoutedEvent.Register<JourneyFlow, JourneySegmentChangedEventArgs>(nameof(SelectedSegmentChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the previewed node or link changes (pointer over it, keyboard focus on a node, or neither).</summary>
    public static readonly RoutedEvent<JourneyTraceChangedEventArgs> TraceChangedEvent =
        RoutedEvent.Register<JourneyFlow, JourneyTraceChangedEventArgs>(nameof(TraceChanged), RoutingStrategies.Bubble);

    private JourneyNode? _hoveredNode;
    private JourneyLink? _hoveredLink;
    private double _visitors;
    private double _goalTotal;

    static JourneyFlow()
    {
        FocusableProperty.OverrideDefaultValue<JourneyFlow>(false);
    }

    /// <summary>Creates the block.</summary>
    public JourneyFlow()
    {
        Stages.CollectionChanged += (_, e) => OnItemsChanged(e, Stages);
        Nodes.CollectionChanged += (_, e) => OnItemsChanged(e, Nodes);
        Segments.CollectionChanged += (_, e) => OnItemsChanged(e, Segments);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleFormatProperty"/>
    public string? SubtitleFormat { get => GetValue(SubtitleFormatProperty); set => SetValue(SubtitleFormatProperty, value); }

    /// <summary>The stages, left to right.</summary>
    public AvaloniaList<JourneyStage> Stages { get; } = new();

    /// <summary>The nodes; each stacks in its stage in list order (steps first, exits after them).</summary>
    public AvaloniaList<JourneyNode> Nodes { get; } = new();

    /// <summary>The data sets; two or more show the segment switch.</summary>
    [Content]
    public AvaloniaList<JourneySegment> Segments { get; } = new();

    /// <inheritdoc cref="StagesSourceProperty"/>
    public IEnumerable<JourneyStage>? StagesSource { get => GetValue(StagesSourceProperty); set => SetValue(StagesSourceProperty, value); }

    /// <inheritdoc cref="NodesSourceProperty"/>
    public IEnumerable<JourneyNode>? NodesSource { get => GetValue(NodesSourceProperty); set => SetValue(NodesSourceProperty, value); }

    /// <inheritdoc cref="SegmentsSourceProperty"/>
    public IEnumerable<JourneySegment>? SegmentsSource { get => GetValue(SegmentsSourceProperty); set => SetValue(SegmentsSourceProperty, value); }

    /// <inheritdoc cref="SelectedSegmentProperty"/>
    public JourneySegment? SelectedSegment { get => GetValue(SelectedSegmentProperty); set => SetValue(SelectedSegmentProperty, value); }

    /// <inheritdoc cref="SelectedNodeProperty"/>
    public JourneyNode? SelectedNode { get => GetValue(SelectedNodeProperty); set => SetValue(SelectedNodeProperty, value); }

    /// <inheritdoc cref="HoveredNodeProperty"/>
    public JourneyNode? HoveredNode { get => _hoveredNode; private set => SetAndRaise(HoveredNodeProperty, ref _hoveredNode, value); }

    /// <inheritdoc cref="HoveredLinkProperty"/>
    public JourneyLink? HoveredLink { get => _hoveredLink; private set => SetAndRaise(HoveredLinkProperty, ref _hoveredLink, value); }

    /// <inheritdoc cref="VisitorsProperty"/>
    public double Visitors { get => _visitors; private set => SetAndRaise(VisitorsProperty, ref _visitors, value); }

    /// <inheritdoc cref="GoalTotalProperty"/>
    public double GoalTotal { get => _goalTotal; private set => SetAndRaise(GoalTotalProperty, ref _goalTotal, value); }

    /// <inheritdoc cref="ValueFormatterProperty"/>
    public Func<double, string>? ValueFormatter { get => GetValue(ValueFormatterProperty); set => SetValue(ValueFormatterProperty, value); }

    /// <inheritdoc cref="ShareFormatterProperty"/>
    public Func<double, string>? ShareFormatter { get => GetValue(ShareFormatterProperty); set => SetValue(ShareFormatterProperty, value); }

    /// <inheritdoc cref="LabelShareFormatterProperty"/>
    public Func<double, string>? LabelShareFormatter { get => GetValue(LabelShareFormatterProperty); set => SetValue(LabelShareFormatterProperty, value); }

    /// <inheritdoc cref="SegmentsLabelProperty"/>
    public string SegmentsLabel { get => GetValue(SegmentsLabelProperty); set => SetValue(SegmentsLabelProperty, value); }

    /// <inheritdoc cref="ChartLabelProperty"/>
    public string ChartLabel { get => GetValue(ChartLabelProperty); set => SetValue(ChartLabelProperty, value); }

    /// <inheritdoc cref="PanelLabelProperty"/>
    public string PanelLabel { get => GetValue(PanelLabelProperty); set => SetValue(PanelLabelProperty, value); }

    /// <inheritdoc cref="CountFormatProperty"/>
    public string CountFormat { get => GetValue(CountFormatProperty); set => SetValue(CountFormatProperty, value); }

    /// <inheritdoc cref="ShareOfFormatProperty"/>
    public string ShareOfFormat { get => GetValue(ShareOfFormatProperty); set => SetValue(ShareOfFormatProperty, value); }

    /// <inheritdoc cref="VisitorsShareTextProperty"/>
    public string VisitorsShareText { get => GetValue(VisitorsShareTextProperty); set => SetValue(VisitorsShareTextProperty, value); }

    /// <inheritdoc cref="VisitorsCaptionProperty"/>
    public string VisitorsCaption { get => GetValue(VisitorsCaptionProperty); set => SetValue(VisitorsCaptionProperty, value); }

    /// <inheritdoc cref="GoalCaptionProperty"/>
    public string GoalCaption { get => GetValue(GoalCaptionProperty); set => SetValue(GoalCaptionProperty, value); }

    /// <inheritdoc cref="OverviewHintProperty"/>
    public string? OverviewHint { get => GetValue(OverviewHintProperty); set => SetValue(OverviewHintProperty, value); }

    /// <inheritdoc cref="DropOffsHeaderProperty"/>
    public string DropOffsHeader { get => GetValue(DropOffsHeaderProperty); set => SetValue(DropOffsHeaderProperty, value); }

    /// <inheritdoc cref="OutcomesHeaderProperty"/>
    public string? OutcomesHeader { get => GetValue(OutcomesHeaderProperty); set => SetValue(OutcomesHeaderProperty, value); }

    /// <inheritdoc cref="InboundHeaderProperty"/>
    public string InboundHeader { get => GetValue(InboundHeaderProperty); set => SetValue(InboundHeaderProperty, value); }

    /// <inheritdoc cref="OutboundHeaderProperty"/>
    public string OutboundHeader { get => GetValue(OutboundHeaderProperty); set => SetValue(OutboundHeaderProperty, value); }

    /// <inheritdoc cref="JourneyStartTextProperty"/>
    public string JourneyStartText { get => GetValue(JourneyStartTextProperty); set => SetValue(JourneyStartTextProperty, value); }

    /// <inheritdoc cref="JourneyEndTextProperty"/>
    public string JourneyEndText { get => GetValue(JourneyEndTextProperty); set => SetValue(JourneyEndTextProperty, value); }

    /// <inheritdoc cref="ClearTextProperty"/>
    public string ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    /// <inheritdoc cref="TableHeaderProperty"/>
    public string TableHeader { get => GetValue(TableHeaderProperty); set => SetValue(TableHeaderProperty, value); }

    /// <inheritdoc cref="TableCaptionFormatProperty"/>
    public string TableCaptionFormat { get => GetValue(TableCaptionFormatProperty); set => SetValue(TableCaptionFormatProperty, value); }

    /// <inheritdoc cref="TableColumnHeadersProperty"/>
    public IReadOnlyList<string>? TableColumnHeaders { get => GetValue(TableColumnHeadersProperty); set => SetValue(TableColumnHeadersProperty, value); }

    /// <inheritdoc cref="AnnouncementFormatProperty"/>
    public string AnnouncementFormat { get => GetValue(AnnouncementFormatProperty); set => SetValue(AnnouncementFormatProperty, value); }

    /// <inheritdoc cref="MaxDropOffRowsProperty"/>
    public int MaxDropOffRows { get => GetValue(MaxDropOffRowsProperty); set => SetValue(MaxDropOffRowsProperty, value); }

    /// <inheritdoc cref="IsSidePanelVisibleProperty"/>
    public bool IsSidePanelVisible { get => GetValue(IsSidePanelVisibleProperty); set => SetValue(IsSidePanelVisibleProperty, value); }

    /// <inheritdoc cref="IsTableVisibleProperty"/>
    public bool IsTableVisible { get => GetValue(IsTableVisibleProperty); set => SetValue(IsTableVisibleProperty, value); }

    /// <inheritdoc cref="IsTableExpandedProperty"/>
    public bool IsTableExpanded { get => GetValue(IsTableExpandedProperty); set => SetValue(IsTableExpandedProperty, value); }

    /// <inheritdoc cref="NodeSelectedCommandProperty"/>
    public ICommand? NodeSelectedCommand { get => GetValue(NodeSelectedCommandProperty); set => SetValue(NodeSelectedCommandProperty, value); }

    /// <inheritdoc cref="SegmentSelectedCommandProperty"/>
    public ICommand? SegmentSelectedCommand { get => GetValue(SegmentSelectedCommandProperty); set => SetValue(SegmentSelectedCommandProperty, value); }

    /// <inheritdoc cref="SelectedNodeChangedEvent"/>
    public event EventHandler<JourneyNodeChangedEventArgs>? SelectedNodeChanged
    {
        add => AddHandler(SelectedNodeChangedEvent, value);
        remove => RemoveHandler(SelectedNodeChangedEvent, value);
    }

    /// <inheritdoc cref="SelectedSegmentChangedEvent"/>
    public event EventHandler<JourneySegmentChangedEventArgs>? SelectedSegmentChanged
    {
        add => AddHandler(SelectedSegmentChangedEvent, value);
        remove => RemoveHandler(SelectedSegmentChangedEvent, value);
    }

    /// <inheritdoc cref="TraceChangedEvent"/>
    public event EventHandler<JourneyTraceChangedEventArgs>? TraceChanged
    {
        add => AddHandler(TraceChangedEvent, value);
        remove => RemoveHandler(TraceChangedEvent, value);
    }
}
