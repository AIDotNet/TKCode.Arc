// Ported from Arc's free hero-section block (MIT, Copyright (c) 2026 Elia Kuratli): hero-relay.tsx and hero-relay.module.css. The product,
// copy and sample events are this library's own, and the graph is drawn here (Blocks does not depend on TKCode.Arc.Workflow).

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

internal enum HeroNodeStatus
{
    Idle,
    Running,
    Done,
    Skipped
}

internal enum HeroNodeId
{
    Trigger,
    Lookup,
    Branch,
    Notify,
    Ticket,
    Store
}

/// <summary>One sample event: every event runs the same workflow, and the branch decides which actions it reaches.</summary>
internal sealed record HeroRelayEvent(string Customer, string Amount, string Plan, bool IsRouted, string Ticket, int Total, int Lookup, int Branch, int Notify, int TicketMs, int Store);

/// <summary>The run: phases, their durations and each node's status.</summary>
internal static class HeroRelayRun
{
    /// <summary>Odd phases run a step, even phases carry the data along the edges out of it, the last holds the finished run (ms).</summary>
    public static readonly double[] Phases = [900, 420, 380, 640, 380, 360, 440, 760, 2800];

    public static readonly int Done = Phases.Length - 1;

    public static readonly HeroRelayEvent[] Events =
    [
        new("Marlow & Sons", "$5,200", "Scale, 180 seats", true, "OPS-217", 397, 112, 2, 158, 226, 54),
        new("Ferncliff", "$310", "Starter, 7 seats", false, string.Empty, 168, 94, 1, 0, 0, 63),
        new("Atlas Freight", "$14,900", "Scale, 620 seats", true, "OPS-218", 441, 127, 2, 151, 249, 69)
    ];

    private static (int Start, int End) Steps(HeroNodeId id) => id switch
    {
        HeroNodeId.Trigger => (1, 2),
        HeroNodeId.Lookup => (3, 4),
        HeroNodeId.Branch => (5, 6),
        _ => (7, 8)
    };

    public static HeroNodeStatus StatusOf(HeroNodeId id, int phase, HeroRelayEvent run)
    {
        if (id is HeroNodeId.Notify or HeroNodeId.Ticket && !run.IsRouted && phase >= 6)
        {
            return HeroNodeStatus.Skipped;
        }
        var (start, end) = Steps(id);
        return phase < start ? HeroNodeStatus.Idle : phase < end ? HeroNodeStatus.Running : HeroNodeStatus.Done;
    }
}

/// <summary>A node of the graph: what the step is, what it found and its state. The running step takes the accent.</summary>
[TemplatePart("PART_Layout", typeof(Grid))]
[PseudoClasses(RunningPseudoClass, SkippedPseudoClass)]
internal sealed class HeroRelayNode : TemplatedControl
{
    public const string RunningPseudoClass = ":running";
    public const string SkippedPseudoClass = ":skipped";

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<HeroRelayNode, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<HeroRelayNode, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubProperty =
        AvaloniaProperty.Register<HeroRelayNode, string?>(nameof(Sub));

    private Grid? _layout;
    private Control? _icon;
    private Control? _text;
    private ArcContentSwap? _state;
    private bool _isCompact;
    private HeroNodeStatus _status = HeroNodeStatus.Done;
    private string? _meta;

    public HeroRelayNode(HeroNodeId id)
    {
        Id = id;
    }

    public HeroNodeId Id { get; }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Sub { get => GetValue(SubProperty); set => SetValue(SubProperty, value); }

    public HeroNodeStatus Status => _status;

    public void SetCompact(bool compact)
    {
        _isCompact = compact;
        ApplyLayout();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _layout = e.NameScope.Find<Grid>("PART_Layout");
        _icon = e.NameScope.Find<Control>("PART_Icon");
        _text = e.NameScope.Find<Control>("PART_Text");
        _state = e.NameScope.Find<ArcContentSwap>("PART_State");
        ApplyLayout();
        RefreshState();
    }

    private void ApplyLayout()
    {
        if (_layout is null || _icon is null || _text is null || _state is null)
        {
            return;
        }
        // Compact nodes on a phone: icon and state on top, the name and its result under them.
        if (_isCompact)
        {
            _layout.ColumnDefinitions = new ColumnDefinitions("*,Auto");
            _layout.RowDefinitions = new RowDefinitions("Auto,Auto");
            _layout.ColumnSpacing = 4;
            _layout.RowSpacing = 6;
            Grid.SetColumn(_state, 1);
            Grid.SetRow(_text, 1);
            Grid.SetColumn(_text, 0);
            Grid.SetColumnSpan(_text, 2);
            _icon.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            _layout.ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
            _layout.RowDefinitions = new RowDefinitions("Auto");
            _layout.ColumnSpacing = 12;
            _layout.RowSpacing = 0;
            Grid.SetColumn(_state, 2);
            Grid.SetRow(_text, 0);
            Grid.SetColumn(_text, 1);
            Grid.SetColumnSpan(_text, 1);
        }
    }

    /// <summary>Sets the status (the state slot pops to the new mark) and the time shown when it is done.</summary>
    public void SetStatus(HeroNodeStatus status, string? meta)
    {
        var changed = status != _status || meta != _meta;
        _status = status;
        _meta = meta;
        PseudoClasses.Set(RunningPseudoClass, status == HeroNodeStatus.Running);
        PseudoClasses.Set(SkippedPseudoClass, status == HeroNodeStatus.Skipped);
        if (changed)
        {
            RefreshState();
        }
    }

    private void RefreshState()
    {
        if (_state is not null)
        {
            _state.Content = BuildStateMark();
        }
    }

    private Control BuildStateMark()
    {
        switch (_status)
        {
            case HeroNodeStatus.Running:
                var spinner = new ArcSpinner { Width = 13, Height = 13, Thickness = 1.5 };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.Accent);
                AutomationProperties.SetName(spinner, "Running");
                return spinner;
            case HeroNodeStatus.Done:
                var check = HeroText.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.Success, 2);
                AutomationProperties.SetName(check, "Done");
                if (string.IsNullOrEmpty(_meta))
                {
                    return check;
                }
                var time = HeroText.Line(_meta, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
                time.VerticalAlignment = VerticalAlignment.Center;
                return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { time, check } };
            case HeroNodeStatus.Skipped:
                var minus = HeroText.Glyph(ArcIcons.Minus, 14, ArcDesignTokenKind.TextMuted, 2);
                AutomationProperties.SetName(minus, "Skipped");
                return minus;
            default:
                var ring = new Border { Width = 10, Height = 10, BorderThickness = new Thickness(1.5) }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
                ring.Bind(Border.BorderBrushProperty, ring.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.CanvasWire)));
                AutomationProperties.SetName(ring, "Waiting");
                return ring;
        }
    }
}

/// <summary>The wires of the graph and the data that draws along them.</summary>
internal sealed class HeroRelayEdges : Control
{
    public static readonly StyledProperty<IBrush?> WireBrushProperty =
        AvaloniaProperty.Register<HeroRelayEdges, IBrush?>(nameof(WireBrush));

    public static readonly StyledProperty<IBrush?> FlowBrushProperty =
        AvaloniaProperty.Register<HeroRelayEdges, IBrush?>(nameof(FlowBrush));

    private const int Segments = 32;

    private (Point[] Points, double[] Lengths)[] _edges = [];
    private double[] _progress = [];
    private bool[] _skipped = [];
    private ImmutablePen? _wirePen;
    private ImmutablePen? _dashPen;
    private ImmutablePen? _flowPen;

    static HeroRelayEdges()
    {
        AffectsRender<HeroRelayEdges>(WireBrushProperty, FlowBrushProperty);
    }

    public HeroRelayEdges()
    {
        IsHitTestVisible = false;
    }

    public IBrush? WireBrush { get => GetValue(WireBrushProperty); set => SetValue(WireBrushProperty, value); }

    public IBrush? FlowBrush { get => GetValue(FlowBrushProperty); set => SetValue(FlowBrushProperty, value); }

    internal IReadOnlyList<double> Progress => _progress;

    public void SetEdges(IReadOnlyList<(Rect From, Rect To)> edges)
    {
        _edges = new (Point[], double[])[edges.Count];
        _progress = new double[edges.Count];
        _skipped = new bool[edges.Count];
        for (var e = 0; e < edges.Count; e++)
        {
            var (from, to) = edges[e];
            var start = new Point(from.Center.X, from.Bottom);
            var end = new Point(to.Center.X, to.Top);
            var mid = (end.Y - start.Y) / 2;
            var c1 = new Point(start.X, start.Y + mid);
            var c2 = new Point(end.X, end.Y - mid);
            var points = new Point[Segments + 1];
            var lengths = new double[Segments + 1];
            for (var i = 0; i <= Segments; i++)
            {
                var t = i / (double)Segments;
                var u = 1 - t;
                points[i] = start * (u * u * u) + c1 * (3 * u * u * t) + c2 * (3 * u * t * t) + end * (t * t * t);
                if (i > 0)
                {
                    var delta = points[i] - points[i - 1];
                    lengths[i] = lengths[i - 1] + Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
                }
            }
            _edges[e] = (points, lengths);
        }
        InvalidateVisual();
    }

    public void SetState(int edge, double progress, bool skipped)
    {
        if (Math.Abs(_progress[edge] - progress) > 0.0005 || _skipped[edge] != skipped)
        {
            _progress[edge] = progress;
            _skipped[edge] = skipped;
            InvalidateVisual();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WireBrushProperty && WireBrush is { } wire)
        {
            _wirePen = new ImmutablePen(wire.ToImmutable(), 1.25);
            _dashPen = new ImmutablePen(wire.ToImmutable(), 1.25, new ImmutableDashStyle(new[] { 3 / 1.25, 4 / 1.25 }, 0));
        }
        else if (change.Property == FlowBrushProperty && FlowBrush is { } flow)
        {
            _flowPen = new ImmutablePen(flow.ToImmutable(), 1.75, null, PenLineCap.Round);
        }
    }

    public override void Render(DrawingContext context)
    {
        for (var e = 0; e < _edges.Length; e++)
        {
            var (points, lengths) = _edges[e];
            var pen = _skipped[e] ? _dashPen : _wirePen;
            if (pen is not null)
            {
                using (context.PushOpacity(_skipped[e] ? 0.7 : 1))
                {
                    for (var i = 1; i < points.Length; i++)
                    {
                        context.DrawLine(pen, points[i - 1], points[i]);
                    }
                }
            }
            var progress = _progress[e];
            if (progress <= 0 || _flowPen is null)
            {
                continue;
            }
            var reach = lengths[^1] * progress;
            for (var i = 1; i < points.Length; i++)
            {
                if (lengths[i] <= reach)
                {
                    context.DrawLine(_flowPen, points[i - 1], points[i]);
                    continue;
                }
                var part = (reach - lengths[i - 1]) / Math.Max(0.001, lengths[i] - lengths[i - 1]);
                context.DrawLine(_flowPen, points[i - 1], points[i - 1] + (points[i] - points[i - 1]) * part);
                break;
            }
        }
    }
}

/// <summary>
/// The workflow, drawn at a fixed size and scaled to its column like a screenshot, so it never reflows mid run. Sample events run
/// on their own while the graph is on screen; below full motion each event shows its finished run.
/// </summary>
/// <remarks>The run is a <see cref="LiveLoop"/> that steps phases, because it must pause off screen and drive the wires frame by frame.</remarks>
internal sealed class HeroRelayGraph : Control
{
    private static readonly Geometry s_branch =
        // Lucide "split".
        ArcIcons.Parse("M16 3h5v5 M8 3H3v5 M12 22v-8.3a4 4 0 0 0-1.172-2.872L3 3 M15 9l6-6");

    private static readonly (HeroNodeId From, HeroNodeId To, int Phase)[] s_edges =
    [
        (HeroNodeId.Trigger, HeroNodeId.Lookup, 2),
        (HeroNodeId.Lookup, HeroNodeId.Branch, 4),
        (HeroNodeId.Branch, HeroNodeId.Notify, 6),
        (HeroNodeId.Branch, HeroNodeId.Ticket, 6),
        (HeroNodeId.Branch, HeroNodeId.Store, 6)
    ];

    private static readonly Dictionary<HeroNodeId, Rect> s_wide = new()
    {
        [HeroNodeId.Trigger] = new Rect(170, 0, 300, 60),
        [HeroNodeId.Lookup] = new Rect(170, 112, 300, 60),
        [HeroNodeId.Branch] = new Rect(170, 224, 300, 60),
        [HeroNodeId.Notify] = new Rect(0, 356, 200, 84),
        [HeroNodeId.Ticket] = new Rect(220, 356, 200, 84),
        [HeroNodeId.Store] = new Rect(440, 356, 200, 84)
    };

    private static readonly Dictionary<HeroNodeId, Rect> s_narrow = new()
    {
        [HeroNodeId.Trigger] = new Rect(0, 0, 344, 52),
        [HeroNodeId.Lookup] = new Rect(0, 78, 344, 52),
        [HeroNodeId.Branch] = new Rect(0, 156, 344, 52),
        [HeroNodeId.Notify] = new Rect(0, 244, 108, 72),
        [HeroNodeId.Ticket] = new Rect(118, 244, 108, 72),
        [HeroNodeId.Store] = new Rect(236, 244, 108, 72)
    };

    private readonly Dictionary<HeroNodeId, HeroRelayNode> _nodes = new();
    private readonly HeroRelayEdges _edgesView = new();
    private readonly Grid _summary = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Height = 44 };
    private readonly Border _summaryLine = new() { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(2, 0) };
    private readonly TextBlock _runLabel;
    private readonly ArcContentSwap _result = new() { Mode = ContentSwapMode.Lift, EnterOffset = 6, EnterDuration = ArcMotionPresets.Fast, ExitDuration = ArcMotionPresets.Fast, HorizontalContentAlignment = HorizontalAlignment.Right };
    private readonly ScaleTransform _scale = new();
    private readonly Surface _surface;
    private readonly LiveLoop _loop;
    private bool _isNarrow;
    private double _layoutScale = 1;
    private int _phase;
    private int _eventIndex;
    private int _count = 1283;
    private double _phaseElapsed;
    private int _resultKey = -1;

    public HeroRelayGraph()
    {
        ClipToBounds = false;
        ArcTokens.Bind(_edgesView, HeroRelayEdges.FlowBrushProperty, ArcDesignTokenKind.Accent);
        _edgesView.Bind(HeroRelayEdges.WireBrushProperty, _edgesView.GetResourceObservable(HeroSectionTokens.Key(HeroSectionTokenKind.CanvasWire)));
        _surface = new Surface(this);
        _surface.Children.Add(_edgesView);
        foreach (var id in Enum.GetValues<HeroNodeId>())
        {
            var node = new HeroRelayNode(id);
            _nodes[id] = node;
            _surface.Children.Add(node);
        }
        _nodes[HeroNodeId.Trigger].Icon = ArcIcons.CreditCard;
        _nodes[HeroNodeId.Trigger].Title = "Payment received";
        _nodes[HeroNodeId.Lookup].Icon = ArcIcons.Search;
        _nodes[HeroNodeId.Lookup].Title = "Find the account";
        _nodes[HeroNodeId.Branch].Icon = s_branch;
        _nodes[HeroNodeId.Branch].Title = "Amount over $1,000";
        _nodes[HeroNodeId.Notify].Icon = ArcIcons.MessageCircle;
        _nodes[HeroNodeId.Ticket].Icon = ArcIcons.Layers;
        _nodes[HeroNodeId.Store].Icon = ArcIcons.Database;

        var chip = HeroText.Line("payment.received", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(chip, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        chip.VerticalAlignment = VerticalAlignment.Center;
        var chipBox = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(7, 1), VerticalAlignment = VerticalAlignment.Center, Child = chip }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(chipBox, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(chipBox, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        _runLabel = HeroText.Line(null, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        _runLabel.VerticalAlignment = VerticalAlignment.Center;
        _summary.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { chipBox, _runLabel } });
        Grid.SetColumn(_result, 1);
        _result.VerticalAlignment = VerticalAlignment.Center;
        _summary.Children.Add(_result);
        _summaryLine.Child = _summary;
        ArcTokens.Bind(_summaryLine, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _surface.Children.Add(_summaryLine);
        _surface.RenderTransformOrigin = RelativePoint.TopLeft;
        _surface.RenderTransform = _scale;
        VisualChildren.Add(_surface);
        LogicalChildren.Add(_surface);

        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running => LiveChanged?.Invoke(running);
    }

    /// <summary>The run started or paused.</summary>
    public event Action<bool>? LiveChanged;

    internal LiveLoop Loop => _loop;

    internal int Phase => EffectivePhase;

    internal int EventIndex => _eventIndex;

    internal int RunCount => _count;

    internal bool IsNarrow => _isNarrow;

    internal HeroRelayEdges Edges => _edgesView;

    internal HeroRelayNode NodeOf(HeroNodeId id) => _nodes[id];

    private int EffectivePhase => ArcMotion.IsFull(this) ? _phase : HeroRelayRun.Done;

    private double DesignWidth => _isNarrow ? 344 : 640;

    private double DesignHeight => _isNarrow ? 316 : 524;

    private Dictionary<HeroNodeId, Rect> Boxes => _isNarrow ? s_narrow : s_wide;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyLayoutMode();
        ApplyRun();
        _loop.Start();
    }

    /// <summary>Runs the next sample at once.</summary>
    public void SendTest()
    {
        _eventIndex = (_eventIndex + 1) % HeroRelayRun.Events.Length;
        _count++;
        _phase = 1;
        _phaseElapsed = 0;
        ApplyRun();
    }

    private bool Step(double seconds)
    {
        Advance(seconds * 1000);
        return true;
    }

    /// <summary>Moves the run forward by <paramref name="milliseconds"/> (the loop's frames; tests call it directly).</summary>
    internal void Advance(double milliseconds)
    {
        _phaseElapsed += milliseconds;
        var moved = false;
        while (_phaseElapsed >= HeroRelayRun.Phases[_phase])
        {
            _phaseElapsed -= HeroRelayRun.Phases[_phase];
            moved = true;
            if (_phase < HeroRelayRun.Done)
            {
                _phase++;
            }
            else
            {
                _phase = 0;
                _eventIndex = (_eventIndex + 1) % HeroRelayRun.Events.Length;
                _count++;
            }
        }
        if (moved)
        {
            ApplyRun();
        }
        else
        {
            ApplyFlow();
        }
    }

    private void ApplyRun()
    {
        var phase = EffectivePhase;
        var run = HeroRelayRun.Events[_eventIndex];
        HeroNodeStatus S(HeroNodeId id) => HeroRelayRun.StatusOf(id, phase, run);
        var narrow = _isNarrow;

        var trigger = _nodes[HeroNodeId.Trigger];
        trigger.Sub = $"{run.Customer}, {run.Amount}";
        trigger.SetStatus(S(HeroNodeId.Trigger), null);
        var lookup = _nodes[HeroNodeId.Lookup];
        lookup.Sub = S(HeroNodeId.Lookup) == HeroNodeStatus.Done ? run.Plan : "Plan, seats and owner";
        lookup.SetStatus(S(HeroNodeId.Lookup), $"{run.Lookup} ms");
        var branch = _nodes[HeroNodeId.Branch];
        branch.Sub = S(HeroNodeId.Branch) == HeroNodeStatus.Done ? run.IsRouted ? "Yes, tell the team" : "No, just record it" : "Condition";
        branch.SetStatus(S(HeroNodeId.Branch), $"{run.Branch} ms");

        Action(HeroNodeId.Notify, narrow ? "Chat" : "Post to #sales", "Team chat", $"Sent in {run.Notify} ms", run.Notify);
        Action(HeroNodeId.Ticket, narrow ? "Ticket" : "Open a ticket", "Onboarding queue", $"{run.Ticket} in {run.TicketMs} ms", run.TicketMs);
        Action(HeroNodeId.Store, narrow ? "Store" : "Save the record", "Warehouse", $"Saved in {run.Store} ms", run.Store);

        void Action(HeroNodeId id, string title, string waiting, string result, int ms)
        {
            var node = _nodes[id];
            var status = S(id);
            node.Title = title;
            node.Sub = status switch
            {
                HeroNodeStatus.Skipped => "Skipped",
                HeroNodeStatus.Done => narrow ? $"{ms} ms" : result,
                HeroNodeStatus.Running => "Running",
                _ => narrow ? "Waiting" : waiting
            };
            node.SetStatus(status, null);
        }

        _runLabel.Text = $"Run {_count:N0}";
        AutomationProperties.SetName(this, $"Workflow, sample run for {run.Customer}");
        ApplyFlow();
        UpdateResult(phase, run);
    }

    private void ApplyFlow()
    {
        var phase = EffectivePhase;
        var run = HeroRelayRun.Events[_eventIndex];
        for (var i = 0; i < s_edges.Length; i++)
        {
            var (_, to, edgePhase) = s_edges[i];
            var skipped = HeroRelayRun.StatusOf(to, phase, run) == HeroNodeStatus.Skipped;
            double progress;
            if (skipped || phase < edgePhase)
            {
                progress = 0;
            }
            else if (phase > edgePhase)
            {
                progress = 1;
            }
            else
            {
                progress = ArcEasings.InOut.Ease(Math.Clamp(_phaseElapsed / HeroRelayRun.Phases[edgePhase], 0, 1));
            }
            _edgesView.SetState(i, progress, skipped);
        }
    }

    private void UpdateResult(int phase, HeroRelayEvent run)
    {
        var key = phase == HeroRelayRun.Done ? 2000 + _count : phase == 0 ? 0 : 1;
        if (key == _resultKey)
        {
            return;
        }
        _resultKey = key;
        if (phase == HeroRelayRun.Done)
        {
            var check = HeroText.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.Success, 2);
            var text = HeroText.Line($"Completed in {run.Total} ms", ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeXS);
            text.VerticalAlignment = VerticalAlignment.Center;
            _result.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { check, text } };
        }
        else if (phase == 0)
        {
            _result.Content = HeroText.Line("Waiting for the next event", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
        }
        else
        {
            var spinner = new ArcSpinner { Width = 13, Height = 13, Thickness = 1.5 };
            ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.Accent);
            var text = HeroText.Line("Running", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS);
            text.VerticalAlignment = VerticalAlignment.Center;
            _result.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { spinner, text } };
        }
    }

    private void ApplyLayoutMode()
    {
        var edges = new (Rect, Rect)[s_edges.Length];
        for (var i = 0; i < edges.Length; i++)
        {
            edges[i] = (Boxes[s_edges[i].From], Boxes[s_edges[i].To]);
        }
        _edgesView.SetEdges(edges);
        foreach (var id in new[] { HeroNodeId.Notify, HeroNodeId.Ticket, HeroNodeId.Store })
        {
            _nodes[id].SetCompact(true);
        }
        _summaryLine.IsVisible = !_isNarrow;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : Math.Min(640, availableSize.Width);
        var narrow = width < 520;
        if (narrow != _isNarrow)
        {
            _isNarrow = narrow;
            ApplyLayoutMode();
            ApplyRun();
        }
        _layoutScale = Math.Min(1, width / DesignWidth);
        _surface.Measure(new Size(DesignWidth, DesignHeight));
        return new Size(width, DesignHeight * _layoutScale);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scale.ScaleX = _layoutScale;
        _scale.ScaleY = _layoutScale;
        _surface.Arrange(new Rect((finalSize.Width - DesignWidth * _layoutScale) / 2, 0, DesignWidth, DesignHeight));
        return finalSize;
    }

    /// <summary>The graph at its design size: nodes where the layout puts them, the wires under them, the run summary below.</summary>
    private sealed class Surface(HeroRelayGraph owner) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var (id, node) in owner._nodes)
            {
                node.Measure(owner.Boxes[id].Size);
            }
            owner._edgesView.Measure(availableSize);
            owner._summaryLine.Measure(new Size(availableSize.Width, 44));
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var (id, node) in owner._nodes)
            {
                node.Arrange(owner.Boxes[id]);
            }
            owner._edgesView.Arrange(new Rect(finalSize));
            owner._summaryLine.Arrange(new Rect(0, 480, finalSize.Width, 44));
            return finalSize;
        }
    }
}

/// <summary>The workflow design: the copy beside the graph on a dotted canvas.</summary>
internal sealed class HeroRelayScene : HeroScene
{
    private readonly HeroEntrance _entrance;
    private readonly Grid _grid = new();
    private readonly StackPanel _copy = new() { Spacing = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _canvas = new() { Spacing = 16, VerticalAlignment = VerticalAlignment.Center };
    private readonly CtaBalancedText _title;
    private readonly TextBlock _description;
    private readonly CtaFlowPanel _actions;
    private readonly Border _liveDot;
    private readonly TranslateTransform _canvasRise = new();

    public HeroRelayScene(HeroSection section, HeroAction primary, HeroAction secondary) : base(section)
    {
        _entrance = new HeroEntrance(this);
        _title = HeroText.Title("Automations you can watch run", 12);
        _description = HeroText.Paragraph("Flowline turns payments, forms and database changes into typed workflows with retries, branches and a trace of every run. Write the steps in code, or wire them on a canvas.", 500);
        _actions = CreateActions(primary, secondary, HorizontalAlignment.Left);
        _actions.Margin = new Thickness(0, 12, 0, 0);
        var fine = HeroText.Line("Free up to 5,000 runs a month. No card needed.", ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeSM);
        fine.TextWrapping = TextWrapping.Wrap;
        foreach (var item in new Control[] { _title, _description, _actions, fine })
        {
            _copy.Children.Add(item);
            _entrance.AddRise(item);
        }

        Graph = new HeroRelayGraph { HorizontalAlignment = HorizontalAlignment.Stretch };
        _liveDot = new Border { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(_liveDot, Border.BackgroundProperty, ArcDesignTokenKind.Success);
        var file = HeroText.Line("on-payment-received.flow", ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(file, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        file.FontSize = 13;
        file.VerticalAlignment = VerticalAlignment.Center;
        SendButton = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = "Send test event" };
        SendButton.Click += (_, _) => Graph.SendTest();
        DockPanel.SetDock(SendButton, Dock.Right);
        var fileRow = new ArcShrinkRow { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Children = { _liveDot, file } };
        ArcShrinkRow.SetIsShrinkable(file, true);
        _canvas.Children.Add(new DockPanel { MaxWidth = 640, LastChildFill = true, Children = { SendButton, fileRow } });
        _canvas.Children.Add(Graph);
        _canvas.RenderTransform = _canvasRise;

        _entrance.Add(1280, (time, full) =>
        {
            var eased = ArcEasings.Enter.Ease(StatsBandTiming.Progress(time, 280, 1000));
            _canvas.Opacity = eased;
            _canvasRise.Y = full ? 24 * (1 - eased) : 0;
        });

        _grid.Children.Add(_copy);
        _grid.Children.Add(_canvas);
        Children.Add(_grid);
        _grid.MaxWidth = 1320;
    }

    internal HeroRelayGraph Graph { get; }

    internal Button SendButton { get; }

    public override void PlayEntrance(bool animate) => _entrance.Play(animate);

    public override void UpdateSize(double width, double viewportHeight)
    {
        var stacked = width <= 900;
        var phone = width <= 560;
        _grid.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("0.92*,*");
        _grid.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        _grid.ColumnSpacing = stacked ? 0 : 64;
        _grid.RowSpacing = !stacked ? 0 : phone ? 32 : 40;
        Grid.SetColumn(_canvas, stacked ? 0 : 1);
        Grid.SetRow(_canvas, stacked ? 1 : 0);
        var top = Math.Clamp(viewportHeight * 0.12, 88, 120);
        _grid.Margin = phone ? new Thickness(16, 80, 16, 24) : new Thickness(40, top, 40, 48);
        _grid.VerticalAlignment = VerticalAlignment.Center;
        _copy.Spacing = phone ? 16 : 20;
        _canvas.Spacing = phone ? 12 : 16;
        HeroText.SetTitleSize(_title, Math.Clamp(19.2 + 0.046 * width, 40, ArcTokens.Get<double>(this, ArcDesignTokenKind.Type5XL)));
        _description.FontSize = phone ? ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeBase) : ArcTokens.Get<double>(this, ArcDesignTokenKind.TypeLG);
        _actions.Margin = new Thickness(0, phone ? 4 : 12, 0, 0);
    }
}
