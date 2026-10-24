using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// The workflow's world inside a <see cref="WorkflowCanvas"/>: the edge layer under one card per step, each card at its
/// step's <see cref="WorkflowNode.Position"/>. The canvas moves and scales the whole world with one render transform, so
/// panning and zooming never lay the cards out again.
/// </summary>
internal sealed class WorkflowWorldPanel : Panel
{
    public static readonly StyledProperty<double> RankGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(RankGap), 52);

    public static readonly StyledProperty<double> FanOutRankGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(FanOutRankGap), 72);

    public static readonly StyledProperty<double> NodeGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(NodeGap), 20);

    public static readonly StyledProperty<double> NarrowRankGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(NarrowRankGap), 26);

    public static readonly StyledProperty<double> NarrowFanOutRankGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(NarrowFanOutRankGap), 36);

    public static readonly StyledProperty<double> NarrowNodeGapProperty =
        AvaloniaProperty.Register<WorkflowWorldPanel, double>(nameof(NarrowNodeGap), 10);

    public WorkflowWorldPanel()
    {
        ClipToBounds = false;
    }

    /// <summary>Arranging spacing: between ranks, below a fan-out, between neighbours.</summary>
    public double RankGap
    {
        get => GetValue(RankGapProperty);
        set => SetValue(RankGapProperty, value);
    }

    public double FanOutRankGap
    {
        get => GetValue(FanOutRankGapProperty);
        set => SetValue(FanOutRankGapProperty, value);
    }

    public double NodeGap
    {
        get => GetValue(NodeGapProperty);
        set => SetValue(NodeGapProperty, value);
    }

    /// <summary>The same spacing on a narrow canvas.</summary>
    public double NarrowRankGap
    {
        get => GetValue(NarrowRankGapProperty);
        set => SetValue(NarrowRankGapProperty, value);
    }

    public double NarrowFanOutRankGap
    {
        get => GetValue(NarrowFanOutRankGapProperty);
        set => SetValue(NarrowFanOutRankGapProperty, value);
    }

    public double NarrowNodeGap
    {
        get => GetValue(NarrowNodeGapProperty);
        set => SetValue(NarrowNodeGapProperty, value);
    }

    /// <summary>Where a step's card shows (the canvas: its position, or on its way there); null uses the position.</summary>
    public Func<WorkflowNode, Point>? Locate { get; set; }

    /// <summary>Called when a card's size changed after it was first measured (the world never asks for room itself).</summary>
    public Action? SizesChanged { get; set; }

    /// <summary>
    /// Whether a card is to be measured and arranged now; null realizes every card. A card that was realized stays so.
    /// Cards that are not realized cost nothing: no template, no layout, no drawing.
    /// </summary>
    public Func<WorkflowNodeCard, bool>? ShouldRealize { get; set; }

    /// <summary>Cards to realize at the next measure whatever <see cref="ShouldRealize"/> says (for focus).</summary>
    public HashSet<WorkflowNodeCard> ForceRealize { get; } = new();

    // The cards' sizes at the last measure (measuring is rare: moving only arranges).
    private Dictionary<Control, Size> _sizes = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        var changed = false;
        var sizes = new Dictionary<Control, Size>(_sizes.Count);
        foreach (var child in Children)
        {
            if (child is WorkflowNodeCard unrealized && !unrealized.IsRealized && !ForceRealize.Contains(unrealized) &&
                ShouldRealize?.Invoke(unrealized) == false)
            {
                continue;
            }
            child.Measure(infinite);
            if (child is WorkflowNodeCard card)
            {
                card.IsRealized = true;
                changed |= _sizes.TryGetValue(card, out var before) && before != card.DesiredSize;
                sizes[card] = card.DesiredSize;
            }
        }
        _sizes = sizes;
        // Forced cards are realized now (or gone): do not keep them, or the cards of steps taken out of the workflow.
        ForceRealize.Clear();
        if (changed)
        {
            SizesChanged?.Invoke();
        }
        // The world never asks for room: the canvas sizes itself from the workflow's bounds and the view.
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            switch (child)
            {
                case WorkflowNodeCard { IsRealized: false }:
                    break;
                case WorkflowNodeCard { Node: { } node } card:
                    card.Arrange(new Rect(Locate?.Invoke(node) ?? PositionOf(node), card.DesiredSize));
                    break;
                case WorkflowEdgeLayer edges:
                    edges.Arrange(edges.Extent);
                    break;
                case WorkflowPortLayer ports:
                    ports.Arrange(ports.Extent);
                    break;
                default:
                    child.Arrange(new Rect(child.DesiredSize));
                    break;
            }
        }
        return finalSize;
    }

    /// <summary>A step's top-left corner; (0, 0) until it has been placed.</summary>
    internal static Point PositionOf(WorkflowNode node)
    {
        var position = node.Position;
        return double.IsFinite(position.X) && double.IsFinite(position.Y) ? position : default;
    }
}
