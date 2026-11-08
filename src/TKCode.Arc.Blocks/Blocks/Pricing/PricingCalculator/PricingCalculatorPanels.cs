using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The plan cards of <see cref="PricingCalculator"/> and the two things that glide between them (Arc pricing-calculator,
/// design reference, notes 4, 6.2 and 6.3). Wide: equal columns 12px apart, every card as tall as the tallest. Stacked:
/// one column. The selection frame is a separate layer (1px accent outline, panel radius, 1px outside the selected card)
/// that chases the selected card's live rectangle on <c>SpringSpec.FromVisual(0.5, 0)</c>: it is re-targeted on every
/// arrange, so while cards open and close in the stacked layout it follows the card's current size, keeping its
/// velocity. The recommended badge lives in its card; when the recommendation moves, the new card's badge starts where
/// the old one was seen and springs home (FLIP, same spring, velocity carried on interruption). A resize snaps the frame.
/// </summary>
internal sealed class PricingCalculatorPlansPanel : Panel
{
    /// <summary>The frame's glide: Arc's measured ω 10.4, critically damped.</summary>
    internal static readonly SpringSpec FrameGlide = SpringSpec.FromVisual(0.5, 0);

    private PricingCalculatorCard? _selected;
    private PricingCalculatorCard? _recommended;
    private Point? _flipFrom;
    private Vector _flipVelocity;
    private bool _flipPending;
    private Size _lastSize;
    private Rect _lastFrame;

    public PricingCalculatorPlansPanel()
    {
        Frame = new GlidingIndicator { ZIndex = 10, StrokeThickness = 1, IsHitTestVisible = false };
        Children.Add(Frame);
    }

    /// <summary>The selection frame.</summary>
    public GlidingIndicator Frame { get; }

    /// <summary>Gap between the cards.</summary>
    public double Gap { get; set; } = 12;

    /// <summary>True: one column.</summary>
    public bool IsStacked { get; set; }

    /// <summary>The cards, in order.</summary>
    public IEnumerable<PricingCalculatorCard> Cards => Children.OfType<PricingCalculatorCard>();

    /// <summary>The selected card (the frame's target).</summary>
    public PricingCalculatorCard? Selected
    {
        get => _selected;
        set
        {
            if (!ReferenceEquals(_selected, value))
            {
                _selected = value;
                InvalidateArrange();
            }
        }
    }

    /// <summary>The recommended card (the badge's owner). A change starts the badge's glide from where it was seen.</summary>
    public PricingCalculatorCard? Recommended
    {
        get => _recommended;
        set
        {
            if (ReferenceEquals(_recommended, value))
            {
                return;
            }
            var old = _recommended;
            _flipFrom = null;
            _flipVelocity = default;
            if (old is not null && IsLoaded && old.BadgeHome(this) is { } home)
            {
                // Where the old badge is seen now: its place plus its current offset (it may still be gliding).
                _flipFrom = home + old.BadgeOffset;
                _flipVelocity = old.BadgeVelocity;
            }
            if (old is not null)
            {
                old.IsRecommended = false;
            }
            _recommended = value;
            if (value is not null)
            {
                value.IsRecommended = true;
            }
            _flipPending = _flipFrom is not null && value is not null;
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var cards = Cards.ToList();
        Frame.Measure(availableSize);
        if (cards.Count == 0)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        if (IsStacked)
        {
            var height = 0.0;
            foreach (var card in cards)
            {
                card.Measure(new Size(width, double.PositiveInfinity));
                height += card.DesiredSize.Height;
            }
            return new Size(width, height + Gap * (cards.Count - 1));
        }
        var column = Math.Max(0, (width - Gap * (cards.Count - 1)) / cards.Count);
        var tallest = 0.0;
        foreach (var card in cards)
        {
            card.Measure(new Size(column, double.PositiveInfinity));
            tallest = Math.Max(tallest, card.DesiredSize.Height);
        }
        return new Size(width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var cards = Cards.ToList();
        if (IsStacked)
        {
            var y = 0.0;
            foreach (var card in cards)
            {
                card.Arrange(new Rect(0, y, finalSize.Width, card.DesiredSize.Height));
                y += card.DesiredSize.Height + Gap;
            }
        }
        else if (cards.Count > 0)
        {
            var column = Math.Max(0, (finalSize.Width - Gap * (cards.Count - 1)) / cards.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].Arrange(new Rect(i * (column + Gap), 0, column, finalSize.Height));
            }
        }
        Frame.Arrange(new Rect(finalSize));
        UpdateFrame(finalSize);
        if (_flipPending && !_waitingForLayout)
        {
            // The badge that just appeared is arranged later in this layout pass: measure its place once the pass is over
            // (LayoutUpdated comes before the frame is rendered, so the badge never shows at its place first).
            _waitingForLayout = true;
            LayoutUpdated += OnLayoutUpdatedOnce;
        }
        return finalSize;
    }

    private void UpdateFrame(Size size)
    {
        var resized = size != _lastSize;
        _lastSize = size;
        if (_selected is null)
        {
            Frame.Hide(animate: false);
            return;
        }
        // 1px outside the card; the 1px stroke is centred half a pixel inside that box.
        var target = _selected.Bounds.Inflate(0.5);
        if (Frame.HasTarget && target == _lastFrame && !resized)
        {
            return;
        }
        _lastFrame = target;
        Frame.MoveTo(target, FrameGlide, animate: !resized && Frame.HasTarget);
    }

    private bool _waitingForLayout;

    private void OnLayoutUpdatedOnce(object? sender, EventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdatedOnce;
        _waitingForLayout = false;
        UpdateBadge();
    }

    private void UpdateBadge()
    {
        if (!_flipPending || _recommended is null || _flipFrom is not { } from)
        {
            return;
        }
        _flipPending = false;
        if (!ArcMotion.IsFull(this) || _recommended.BadgeHome(this) is not { } home)
        {
            return;
        }
        var offset = from - home;
        if (Math.Abs(offset.X) < 0.5 && Math.Abs(offset.Y) < 0.5)
        {
            return;
        }
        _recommended.FlyBadgeFrom(offset, _flipVelocity);
    }
}

/// <summary>
/// The controls row: the seat block takes the room left by the billing switch (bottom aligned, 32px apart); when the
/// seat block would get narrower than its minimum, the switch moves under it (20px below).
/// </summary>
internal sealed class PricingCalculatorControlsPanel : Panel
{
    public double ColumnGap { get; set; } = 32;

    public double RowGap { get; set; } = 20;

    public double SeatsMinWidth { get; set; } = 300;

    /// <summary>True while the billing switch sits under the seat block (tests read it).</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var seats = Children[0];
        var billing = Children[1];
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        billing.Measure(new Size(width, double.PositiveInfinity));
        var billingWidth = billing.DesiredSize.Width;
        IsWrapped = width - billingWidth - ColumnGap < SeatsMinWidth;
        if (IsWrapped)
        {
            seats.Measure(new Size(width, double.PositiveInfinity));
            billing.Measure(new Size(width, double.PositiveInfinity));
            return new Size(width, seats.DesiredSize.Height + RowGap + billing.DesiredSize.Height);
        }
        seats.Measure(new Size(Math.Max(0, width - billingWidth - ColumnGap), double.PositiveInfinity));
        return new Size(width, Math.Max(seats.DesiredSize.Height, billing.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var seats = Children[0];
        var billing = Children[1];
        if (IsWrapped)
        {
            seats.Arrange(new Rect(0, 0, finalSize.Width, seats.DesiredSize.Height));
            var bw = billing.HorizontalAlignment == HorizontalAlignment.Stretch ? finalSize.Width : billing.DesiredSize.Width;
            billing.Arrange(new Rect(0, seats.DesiredSize.Height + RowGap, bw, billing.DesiredSize.Height));
        }
        else
        {
            var billingWidth = billing.DesiredSize.Width;
            seats.Arrange(new Rect(0, finalSize.Height - seats.DesiredSize.Height, finalSize.Width - billingWidth - ColumnGap, seats.DesiredSize.Height));
            billing.Arrange(new Rect(finalSize.Width - billingWidth, finalSize.Height - billing.DesiredSize.Height, billingWidth, billing.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>How the totals row lays out.</summary>
internal enum PricingCalculatorTotalsMode
{
    /// <summary>Summary, savings and the trial button on one row.</summary>
    Row,

    /// <summary>Summary and savings on a row, the trial button full width under them.</summary>
    TwoRows,

    /// <summary>Everything stacked, the trial button full width.</summary>
    Column
}

/// <summary>The totals row: [summary 1fr | savings | trial] with 24px between columns and 16px between rows.</summary>
internal sealed class PricingCalculatorTotalsPanel : Panel
{
    private PricingCalculatorTotalsMode _mode;

    public double ColumnGap { get; set; } = 24;

    public double RowGap { get; set; } = 16;

    public PricingCalculatorTotalsMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3)
        {
            return default;
        }
        var width = double.IsInfinity(availableSize.Width) ? 900 : availableSize.Width;
        var (main, savings, cta) = (Children[0], Children[1], Children[2]);
        switch (_mode)
        {
            case PricingCalculatorTotalsMode.Row:
                cta.Measure(Size.Infinity);
                savings.Measure(Size.Infinity);
                main.Measure(new Size(Math.Max(0, width - cta.DesiredSize.Width - savings.DesiredSize.Width - 2 * ColumnGap), double.PositiveInfinity));
                return new Size(width, Math.Max(main.DesiredSize.Height, Math.Max(savings.DesiredSize.Height, cta.DesiredSize.Height)));
            case PricingCalculatorTotalsMode.TwoRows:
                savings.Measure(new Size(width / 2, double.PositiveInfinity));
                main.Measure(new Size(Math.Max(0, width - savings.DesiredSize.Width - ColumnGap), double.PositiveInfinity));
                cta.Measure(new Size(width, double.PositiveInfinity));
                return new Size(width, Math.Max(main.DesiredSize.Height, savings.DesiredSize.Height) + RowGap + cta.DesiredSize.Height);
            default:
                main.Measure(new Size(width, double.PositiveInfinity));
                savings.Measure(new Size(width, double.PositiveInfinity));
                cta.Measure(new Size(width, double.PositiveInfinity));
                var gaps = savings.DesiredSize.Height > 0 ? 2 * RowGap : RowGap;
                return new Size(width, main.DesiredSize.Height + savings.DesiredSize.Height + cta.DesiredSize.Height + gaps);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3)
        {
            return finalSize;
        }
        var (main, savings, cta) = (Children[0], Children[1], Children[2]);
        var width = finalSize.Width;
        switch (_mode)
        {
            case PricingCalculatorTotalsMode.Row:
                {
                    var h = finalSize.Height;
                    var ctaW = cta.DesiredSize.Width;
                    var savW = savings.DesiredSize.Width;
                    cta.Arrange(new Rect(width - ctaW, (h - cta.DesiredSize.Height) / 2, ctaW, cta.DesiredSize.Height));
                    savings.Arrange(new Rect(width - ctaW - ColumnGap - savW, (h - savings.DesiredSize.Height) / 2, savW, savings.DesiredSize.Height));
                    main.Arrange(new Rect(0, (h - main.DesiredSize.Height) / 2, Math.Max(0, width - ctaW - savW - 2 * ColumnGap), main.DesiredSize.Height));
                    break;
                }
            case PricingCalculatorTotalsMode.TwoRows:
                {
                    var top = Math.Max(main.DesiredSize.Height, savings.DesiredSize.Height);
                    var savW = savings.DesiredSize.Width;
                    savings.Arrange(new Rect(width - savW, (top - savings.DesiredSize.Height) / 2, savW, savings.DesiredSize.Height));
                    main.Arrange(new Rect(0, (top - main.DesiredSize.Height) / 2, Math.Max(0, width - savW - ColumnGap), main.DesiredSize.Height));
                    cta.Arrange(new Rect(0, top + RowGap, width, cta.DesiredSize.Height));
                    break;
                }
            default:
                {
                    var y = 0.0;
                    main.Arrange(new Rect(0, y, width, main.DesiredSize.Height));
                    y += main.DesiredSize.Height + RowGap;
                    if (savings.DesiredSize.Height > 0)
                    {
                        savings.Arrange(new Rect(0, y, Math.Min(width, savings.DesiredSize.Width), savings.DesiredSize.Height));
                        y += savings.DesiredSize.Height + RowGap;
                    }
                    cta.Arrange(new Rect(0, y, width, cta.DesiredSize.Height));
                    break;
                }
        }
        return finalSize;
    }
}

/// <summary>A round 30px seat step button (surface, 1px border, a 16px glyph that darkens on hover, presses in slightly).</summary>
internal sealed class PricingCalculatorStepButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(PricingCalculatorStepButton);
}

/// <summary>The outlined pill that offers yearly billing in the totals while billed monthly.</summary>
internal sealed class PricingCalculatorPillButton : Avalonia.Controls.Button
{
    protected override Type StyleKeyOverride => typeof(PricingCalculatorPillButton);
}
