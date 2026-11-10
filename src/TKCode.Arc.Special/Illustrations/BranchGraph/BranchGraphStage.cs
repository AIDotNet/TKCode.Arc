using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawn history of a <see cref="BranchGraph"/>: one row per commit with the lanes on the left (straight lines down a
/// lane, curves out to a new branch and back into a merge, dots for commits and rings for merges) and the message, branch
/// and tag pills and, on wide cards, the author and id on the right. Commits the story has not reached are not drawn,
/// but their rows are reserved, so nothing moves as the history grows. The graph is one tab stop: Up / Down / Home / End
/// select commits, Escape clears the selection. Edge paths are built when the size changes; drawing an edge in uses one
/// reused dashed pen per color, so frames allocate nothing.
/// </summary>
internal sealed class BranchGraphStage : Control
{
    internal static readonly StyledProperty<IBrush?> Lane1Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane1));

    internal static readonly StyledProperty<IBrush?> Lane2Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane2));

    internal static readonly StyledProperty<IBrush?> Lane3Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane3));

    internal static readonly StyledProperty<IBrush?> Lane4Property =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(Lane4));

    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SurfaceInk));

    internal static readonly StyledProperty<IBrush?> HoverInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(HoverInk));

    internal static readonly StyledProperty<IBrush?> SelectedInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SelectedInk));

    internal static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(FocusInk));

    internal static readonly StyledProperty<IBrush?> TagInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(TagInk));

    internal static readonly StyledProperty<IBrush?> TextInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(TextInk));

    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(SecondaryInk));

    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<BranchGraphStage, IBrush?>(nameof(MutedInk));

    internal static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(RowHeight), 32);

    internal static readonly StyledProperty<double> LaneGapProperty =
        AvaloniaProperty.Register<BranchGraphStage, double>(nameof(LaneGap), 18);

    internal static readonly StyledProperty<CornerRadius> RowRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(RowRadius), new CornerRadius(10));

    internal static readonly StyledProperty<CornerRadius> PillRadiusProperty =
        AvaloniaProperty.Register<BranchGraphStage, CornerRadius>(nameof(PillRadius), new CornerRadius(999));

    internal static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<BranchGraphStage>();

    /// <summary>The width from which the author and id show on the right.</summary>
    public const double WideWidth = 480;

    private const double DotRadius = 5;
    private const double LineWidth = 2;

    private readonly ExplainerTextCache _text = new(1024);
    private readonly List<ExplainerPath> _paths = new();
    private readonly Dictionary<IBrush, IPen> _pens = new();
    private readonly Dictionary<IBrush, Pen> _drawPens = new();
    private readonly Dictionary<IBrush, IPen> _ringPens = new();
    private readonly Dictionary<IBrush, IPen> _pillPens = new();
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);
    private readonly List<string> _metaTexts = new();
    private BranchGraph? _owner;
    private BranchGraphPlan _plan = BranchGraphPlan.Empty;
    private bool[]? _ancestors;
    private double _layoutWidth = -1;
    private double _graphWidth;
    private int _hover = -1;
    private IPen? _focusPen;
    private IBrush? _focusPenBrush;

    static BranchGraphStage()
    {
        FocusableProperty.OverrideDefaultValue<BranchGraphStage>(true);
        FocusAdornerProperty.OverrideDefaultValue<BranchGraphStage>(null);
        AffectsRender<BranchGraphStage>(Lane1Property, Lane2Property, Lane3Property, Lane4Property, SurfaceInkProperty, HoverInkProperty,
            SelectedInkProperty, FocusInkProperty, TagInkProperty, TextInkProperty, SecondaryInkProperty, MutedInkProperty,
            RowRadiusProperty, PillRadiusProperty);
        AffectsMeasure<BranchGraphStage>(RowHeightProperty, LaneGapProperty, FontFamilyProperty);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<BranchGraphStage>(AutomationControlType.List);
    }

    public BranchGraphStage()
    {
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) =>
        {
            _text.Clear();
            _pens.Clear();
            _drawPens.Clear();
            _ringPens.Clear();
            _pillPens.Clear();
            InvalidateVisual();
        };
    }

    public IBrush? Lane1 { get => GetValue(Lane1Property); set => SetValue(Lane1Property, value); }

    public IBrush? Lane2 { get => GetValue(Lane2Property); set => SetValue(Lane2Property, value); }

    public IBrush? Lane3 { get => GetValue(Lane3Property); set => SetValue(Lane3Property, value); }

    public IBrush? Lane4 { get => GetValue(Lane4Property); set => SetValue(Lane4Property, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? HoverInk { get => GetValue(HoverInkProperty); set => SetValue(HoverInkProperty, value); }

    public IBrush? SelectedInk { get => GetValue(SelectedInkProperty); set => SetValue(SelectedInkProperty, value); }

    public IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }

    public IBrush? TagInk { get => GetValue(TagInkProperty); set => SetValue(TagInkProperty, value); }

    public IBrush? TextInk { get => GetValue(TextInkProperty); set => SetValue(TextInkProperty, value); }

    public IBrush? SecondaryInk { get => GetValue(SecondaryInkProperty); set => SetValue(SecondaryInkProperty, value); }

    public IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double LaneGap { get => GetValue(LaneGapProperty); set => SetValue(LaneGapProperty, value); }

    public CornerRadius RowRadius { get => GetValue(RowRadiusProperty); set => SetValue(RowRadiusProperty, value); }

    public CornerRadius PillRadius { get => GetValue(PillRadiusProperty); set => SetValue(PillRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The lanes' width (tests).</summary>
    public double GraphWidth => _graphWidth;

    /// <summary>The words a screen reader hears for the graph now.</summary>
    public string Summary { get; private set; } = string.Empty;

    public void Attach(BranchGraph owner)
    {
        _owner = owner;
        SetPlan(owner.Plan);
    }

    public void SetPlan(BranchGraphPlan plan)
    {
        _plan = plan;
        _metaTexts.Clear();
        foreach (var node in plan.Nodes)
        {
            var author = node.Commit.Author;
            _metaTexts.Add(string.IsNullOrEmpty(author) ? node.Id : author + " · " + node.Id);
        }
        _layoutWidth = -1;
        OnSelectionChanged();
        InvalidateMeasure();
    }

    public void Relayout()
    {
        _layoutWidth = -1;
        InvalidateArrange();
        InvalidateVisual();
    }

    public void OnReveal(int index) => Changed();

    public void Changed()
    {
        UpdateSummary();
        InvalidateVisual();
    }

    public void Tick() => InvalidateVisual();

    public void OnSelectionChanged()
    {
        var index = SelectedIndex;
        _ancestors = index >= 0 ? _plan.AncestorsOf(index) : null;
        UpdateSummary();
        InvalidateVisual();
    }

    /// <summary>The plan index of the selected commit, or -1.</summary>
    public int SelectedIndex
    {
        get
        {
            if (_owner?.SelectedCommit is not { } selected)
            {
                return -1;
            }
            for (var i = 0; i < _plan.Nodes.Length; i++)
            {
                if (ReferenceEquals(_plan.Nodes[i].Commit, selected))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    private int Revealed => _owner?.RevealedCount ?? 0;

    private void UpdateSummary()
    {
        if (_owner is null)
        {
            return;
        }
        var index = SelectedIndex;
        Summary = index >= 0 && index < _plan.Nodes.Length
            ? _owner.Describe(_plan.Nodes[index])
            : Revealed > 0 ? _owner.Describe(_plan.Nodes[Revealed - 1]) : _owner.Title ?? string.Empty;
        AutomationProperties.SetName(this, Summary);
    }

    /// <summary>The row (top to bottom) of a commit.</summary>
    public int RowOf(int index) => _owner?.IsNewestFirst == true ? _plan.Nodes.Length - 1 - index : index;

    /// <summary>The commit drawn in a row.</summary>
    private int IndexOfRow(int row) => _owner?.IsNewestFirst == true ? _plan.Nodes.Length - 1 - row : row;

    /// <summary>The center of a commit's dot.</summary>
    public Point DotOf(int index)
    {
        var node = _plan.Nodes[index];
        return new Point(10 + node.Lane * LaneGap, RowOf(index) * RowHeight + RowHeight / 2);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 560;
        return new Size(width, Math.Max(1, _plan.Nodes.Length) * RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Math.Abs(finalSize.Width - _layoutWidth) > 0.5)
        {
            Solve(finalSize.Width);
        }
        return finalSize;
    }

    private void Solve(double width)
    {
        _layoutWidth = width;
        _graphWidth = 20 + (_plan.LaneCount - 1) * LaneGap;
        while (_paths.Count < _plan.Edges.Length)
        {
            _paths.Add(new ExplainerPath());
        }
        var r = RowHeight;
        for (var i = 0; i < _plan.Edges.Length; i++)
        {
            var edge = _plan.Edges[i];
            var from = DotOf(edge.Parent);
            var to = DotOf(edge.Child);
            var path = _paths[i].Clear().MoveTo(from);
            var s = Math.Sign(to.Y - from.Y);
            var turn = r * s;
            switch (edge.Kind)
            {
                case BranchGraphEdgeKind.Straight:
                    path.LineTo(to);
                    break;
                case BranchGraphEdgeKind.BranchOff:
                    var bend = new Point(to.X, from.Y + turn);
                    path.CubicTo(new Point(from.X, from.Y + turn * 0.6), new Point(to.X, from.Y + turn * 0.4), bend, 16);
                    if (Math.Abs(to.Y - bend.Y) > 0.5)
                    {
                        path.LineTo(to);
                    }
                    break;
                default:
                    var start = new Point(from.X, to.Y - turn);
                    if (Math.Abs(start.Y - from.Y) > 0.5)
                    {
                        path.LineTo(start);
                    }
                    path.CubicTo(new Point(from.X, to.Y - turn * 0.4), new Point(to.X, to.Y - turn * 0.6), to, 16);
                    break;
            }
        }
    }

    /// <summary>A branch's color: lanes 1–4 by the branch's order.</summary>
    private IBrush? BranchBrush(int branchIndex) => (branchIndex % 4) switch
    {
        0 => Lane1,
        1 => Lane2,
        2 => Lane3,
        _ => Lane4
    };

    private IPen? SolidPen(IBrush? brush)
    {
        if (brush is null)
        {
            return null;
        }
        if (!_pens.TryGetValue(brush, out var pen))
        {
            pen = new ImmutablePen(brush.ToImmutable(), LineWidth, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[brush] = pen;
        }
        return pen;
    }

    private Pen? DrawPen(IBrush? brush)
    {
        if (brush is null)
        {
            return null;
        }
        if (!_drawPens.TryGetValue(brush, out var pen))
        {
            pen = new Pen(brush.ToImmutable(), LineWidth, new DashStyle(), PenLineCap.Flat, PenLineJoin.Round);
            _drawPens[brush] = pen;
        }
        return pen;
    }

    private IPen? RingPen(IBrush? brush)
    {
        if (brush is null)
        {
            return null;
        }
        if (!_ringPens.TryGetValue(brush, out var pen))
        {
            pen = new ImmutablePen(brush.ToImmutable(), 2);
            _ringPens[brush] = pen;
        }
        return pen;
    }

    private IPen? PillPen(IBrush? brush)
    {
        if (brush is null)
        {
            return null;
        }
        if (!_pillPens.TryGetValue(brush, out var pen))
        {
            pen = new ImmutablePen(brush.ToImmutable(), 1);
            _pillPens[brush] = pen;
        }
        return pen;
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (_owner is null || _layoutWidth < 0)
        {
            return;
        }
        var now = _owner.StoryTime;
        var animates = _owner.Animates;
        var revealed = Revealed;
        var at = _owner.RevealedAt;
        var selected = SelectedIndex;
        var typeface = new Typeface(FontFamily);
        var r = RowHeight;
        var radius = Math.Min(RowRadius.TopLeft, r / 2);
        // Row backgrounds: the hovered row, then the selected one.
        if (_hover >= 0 && _hover < revealed && _hover != selected && HoverInk is { } hover)
        {
            context.DrawRectangle(hover, null, new Rect(0, RowOf(_hover) * r + 1, bounds.Width, r - 2), radius, radius);
        }
        if (selected >= 0 && selected < revealed && SelectedInk is { } selectedInk)
        {
            context.DrawRectangle(selectedInk, null, new Rect(0, RowOf(selected) * r + 1, bounds.Width, r - 2), radius, radius);
        }
        // Edges: finished ones whole, the newest commit's drawing in.
        for (var i = 0; i < _plan.Edges.Length && i < _paths.Count; i++)
        {
            var edge = _plan.Edges[i];
            if (edge.Child >= revealed)
            {
                continue;
            }
            var child = _plan.Nodes[edge.Child];
            var owner = edge.Kind == BranchGraphEdgeKind.MergeIn ? _plan.Nodes[edge.Parent] : child;
            var brush = BranchBrush(owner.BranchIndex);
            var dim = _ancestors is not null && !_ancestors[edge.Child] ? 0.3 : 1;
            var age = animates && edge.Child < at.Count ? now - at[edge.Child] : double.PositiveInfinity;
            var path = _paths[i];
            using (context.PushOpacity(dim))
            {
                if (age >= BranchGraph.EdgeSeconds)
                {
                    context.DrawGeometry(null, SolidPen(brush), path.Geometry);
                }
                else if (age > 0 && DrawPen(brush) is { } pen)
                {
                    var length = path.Length * ArcEasings.Standard.Ease(Math.Clamp(age / BranchGraph.EdgeSeconds, 0, 1));
                    if (path.ShowPart(pen, 0, length))
                    {
                        context.DrawGeometry(null, pen, path.Geometry);
                    }
                }
            }
        }
        // Dots and rows.
        var wide = bounds.Width >= WideWidth;
        var textX = _graphWidth + 12;
        for (var i = 0; i < revealed && i < _plan.Nodes.Length; i++)
        {
            var node = _plan.Nodes[i];
            var age = animates && i < at.Count ? now - at[i] : double.PositiveInfinity;
            var dim = _ancestors is not null && !_ancestors[i] ? 0.35 : 1;
            var brush = BranchBrush(node.BranchIndex);
            var center = DotOf(i);
            var pop = Math.Clamp((age - BranchGraph.EdgeSeconds * 0.7) / 0.25, 0, 1);
            if (pop > 0)
            {
                var scale = pop < 1 ? 0.4 + 0.6 * ArcEasings.Enter.Ease(pop) + Math.Sin(pop * Math.PI) * 0.15 : 1;
                var rr = DotRadius * scale;
                using (context.PushOpacity(dim))
                {
                    if (node.Kind == BranchGraphCommitKind.Merge)
                    {
                        context.DrawEllipse(SurfaceInk, RingPen(brush), center, rr, rr);
                    }
                    else
                    {
                        context.DrawEllipse(SurfaceInk, null, center, rr + 2, rr + 2);
                        context.DrawEllipse(brush, null, center, rr, rr);
                    }
                }
            }
            var fade = Math.Clamp((age - 0.4) / 0.3, 0, 1);
            if (fade > 0)
            {
                using (context.PushOpacity(fade * dim))
                {
                    DrawRowText(context, i, node, textX, bounds.Width, wide, typeface);
                }
            }
        }
        if (IsFocused && Classes.Contains(":focus-visible") && FocusInk is { } focus)
        {
            if (!ReferenceEquals(focus, _focusPenBrush))
            {
                _focusPenBrush = focus;
                _focusPen = new ImmutablePen(focus.ToImmutable(), 2);
            }
            var ring = selected >= 0 && selected < revealed ? new Rect(0, RowOf(selected) * r + 1, bounds.Width, r - 2).Inflate(2) : bounds.Inflate(3);
            var rr = Math.Min(RowRadius.TopLeft + 2, ring.Height / 2);
            context.DrawRectangle(null, _focusPen, ring, rr, rr);
        }
    }

    private void DrawRowText(DrawingContext context, int index, BranchGraphNode node, double x, double width, bool wide, Typeface typeface)
    {
        var r = RowHeight;
        var top = RowOf(index) * r;
        var right = width - 10;
        if (wide && index < _metaTexts.Count)
        {
            var meta = _text.Get(_metaTexts[index], typeface, 12, ColorOf(MutedInk));
            meta.Draw(context, new Point(right - meta.Width, top + (r - meta.Height) / 2));
            right -= meta.Width + 16;
        }
        // Pills after the message: the branch label on its newest commit, then the tag.
        var pillsWidth = 0.0;
        var isHead = _plan.HeadOf(node.Branch, Revealed) == index;
        TextLayout? branchPill = null;
        TextLayout? tagPill = null;
        if (isHead)
        {
            branchPill = _text.Get(node.Branch, typeface, 11, ColorOf(BranchBrush(node.BranchIndex)));
            pillsWidth += branchPill.Width + 16 + 6;
        }
        if (!string.IsNullOrEmpty(node.Commit.Tag))
        {
            tagPill = _text.Get(node.Commit.Tag, typeface, 11, ColorOf(TagInk));
            pillsWidth += tagPill.Width + 16 + 6;
        }
        var messageColor = node.Kind == BranchGraphCommitKind.Merge ? ColorOf(SecondaryInk) : ColorOf(TextInk);
        var messageWidth = Math.Max(24, right - x - pillsWidth);
        var message = _text.GetTrimmed(node.Commit.Message ?? node.Id, typeface, 13, messageColor, messageWidth);
        message.Draw(context, new Point(x, top + (r - message.Height) / 2));
        var px = x + message.Width + 8;
        if (branchPill is not null)
        {
            px = DrawPill(context, branchPill, BranchBrush(node.BranchIndex), px, top);
        }
        if (tagPill is not null)
        {
            DrawPill(context, tagPill, TagInk, px, top);
        }
    }

    private double DrawPill(DrawingContext context, TextLayout text, IBrush? ink, double x, double rowTop)
    {
        var height = text.Height + 4;
        var rect = new Rect(x, rowTop + (RowHeight - height) / 2, text.Width + 16, height);
        var radius = Math.Min(PillRadius.TopLeft, height / 2);
        context.DrawRectangle(null, PillPen(ink), rect, radius, radius);
        text.Draw(context, new Point(rect.X + 8, rect.Y + 2));
        return rect.Right + 6;
    }

    // ---- input ------------------------------------------------------------------------------------------------------

    private int RowAt(Point point)
    {
        var row = (int)Math.Floor(point.Y / RowHeight);
        if (row < 0 || row >= _plan.Nodes.Length)
        {
            return -1;
        }
        var index = IndexOfRow(row);
        return index < Revealed ? index : -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = e.Pointer.Type == PointerType.Touch ? -1 : RowAt(e.GetPosition(this));
        if (index != _hover)
        {
            _hover = index;
            Cursor = index >= 0 ? s_hand : null;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover >= 0)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var index = RowAt(e.GetPosition(this));
        if (index >= 0)
        {
            var commit = _plan.Nodes[index].Commit;
            _owner.SetCurrentValue(BranchGraph.SelectedCommitProperty, ReferenceEquals(_owner.SelectedCommit, commit) ? null : commit);
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_owner is null || e.Handled || Revealed == 0)
        {
            return;
        }
        var selected = SelectedIndex;
        var row = selected >= 0 ? RowOf(selected) : -1;
        var rows = new List<int>();
        for (var r = 0; r < _plan.Nodes.Length; r++)
        {
            if (IndexOfRow(r) < Revealed)
            {
                rows.Add(r);
            }
        }
        if (rows.Count == 0)
        {
            return;
        }
        var position = rows.IndexOf(row);
        int? next = e.Key switch
        {
            Key.Down => position < 0 ? 0 : Math.Min(rows.Count - 1, position + 1),
            Key.Up => position < 0 ? rows.Count - 1 : Math.Max(0, position - 1),
            Key.Home => 0,
            Key.End => rows.Count - 1,
            _ => null
        };
        if (next is { } p)
        {
            _owner.SetCurrentValue(BranchGraph.SelectedCommitProperty, _plan.Nodes[IndexOfRow(rows[p])].Commit);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && selected >= 0)
        {
            _owner.SetCurrentValue(BranchGraph.SelectedCommitProperty, null);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
