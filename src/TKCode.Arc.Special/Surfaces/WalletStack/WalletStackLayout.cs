namespace TKCode.Arc;

/// <summary>Where one card sits: its top edge in the stage (below the 12px inset), its scale about the top centre, the
/// opacity of its drop shadow and the card's own opacity.</summary>
internal readonly record struct WalletStackPose(double Y, double Scale, double Shadow, double Opacity);

/// <summary>Sizes that follow the card width: the card is ID-1 (85.60 × 53.98 mm) and the stage is 220px taller.</summary>
internal readonly record struct WalletStackGeometry
{
    /// <summary>ID-1 aspect ratio, width over height.</summary>
    public const double Aspect = 1.5858;

    /// <summary>The stage is the card height plus this.</summary>
    public const double StageExtra = 220;

    /// <summary>The cards are inset this much in the stage (the stage overhangs the wallet by as much on each side).</summary>
    public const double Inset = 12;

    public WalletStackGeometry(double cardWidth)
    {
        CardWidth = Math.Max(1, cardWidth);
        CardHeight = CardWidth / Aspect;
        StageHeight = CardHeight + StageExtra;
        RoundedStage = Math.Round(StageHeight);
    }

    public double CardWidth { get; }

    public double CardHeight { get; }

    public double StageHeight { get; }

    /// <summary>The stage height rounded to whole pixels; the pocket and the tuck positions are measured from it.</summary>
    public double RoundedStage { get; }

    /// <summary>One percent of the card width (Arc's <c>cqw</c>): every detail of the face is a multiple of it.</summary>
    public double Cqw => CardWidth / 100;
}

/// <summary>
/// The one layout function of the wallet stack (Arc Wallet stack, design reference): every card's pose is a pure
/// function of the continuous stack position, the fan amount, the hover lift, the open amount and the card's own raise.
/// Springs, drags and interruptions all move these inputs, so the shape never jumps. Z order never changes (the first
/// card is always on top), so a card is never drawn over one in front of it.
/// </summary>
/// <remarks>
/// Segments by <c>d = i − pos</c>: the stack (<c>d ≥ 0</c>, each card further back sits higher and 5% smaller, the gap
/// growing with the fan), the push (<c>−1 ≤ d &lt; 0</c>, the front card travels into the pocket at the bottom) and the
/// pocket (<c>d &lt; −1</c>, 7px lower and 2% larger per layer, at most three layers deep). Opening raises one card to the
/// top at 0.88 and tucks the others into a row at the bottom; the weights are linear, so the raised card moves
/// <c>y = y_stack·(1 − o)</c> and the others <c>lerp(stack, tuck, o)</c>, as measured.
/// </remarks>
internal static class WalletStackLayout
{
    /// <summary>The vertical centre of the resting stack: the same 83.17px at every card width (the stage is always 220px taller).</summary>
    public const double CenterLine = 83.17;

    /// <summary>Hovered cards behind the front one rise this much.</summary>
    public const double HoverRise = 8;

    /// <summary>At most this many cards show in the stack (deeper ones wait behind the fourth) and in the pocket.</summary>
    public const int VisibleDepth = 4;

    /// <summary>The pocket's first layer sits this far above the bottom of the stage.</summary>
    public const double PocketTop = 42;

    /// <summary>The raised card's scale.</summary>
    public const double RaisedScale = 0.88;

    /// <summary>Gap between cards in the stack: 6% of the card height at rest, 22% when fanned.</summary>
    public static double Step(double cardHeight, double fan) => (0.06 + 0.16 * fan) * cardHeight;

    /// <summary>How many steps above the centre line the front card sits: half the visible depth (1.5 for four cards).</summary>
    public static double Half(int count) => (Math.Clamp(count, 1, VisibleDepth) - 1) / 2.0;

    /// <summary>The top of a card <paramref name="d"/> places behind the front (d ≥ 0).</summary>
    public static double StackY(double d, int count, double cardHeight, double fan)
        => CenterLine + (Half(count) - Math.Min(d, VisibleDepth - 1)) * Step(cardHeight, fan);

    /// <summary>The top of the pocket's first layer.</summary>
    public static double PocketY(in WalletStackGeometry g) => g.RoundedStage - PocketTop;

    /// <summary>
    /// The drag distance of one card: from the front position to the pocket, so the front card follows the pointer 1:1
    /// (220.23px fanned and 265.03px at rest for a 296px card).
    /// </summary>
    public static double CardDistance(in WalletStackGeometry g, int count, double fan)
        => PocketY(g) - StackY(0, count, g.CardHeight, fan);

    /// <summary>
    /// The pull distance that closes an open card: the open card follows at 0.46 px per px (217.2px for a 296px card).
    /// </summary>
    public static double OpenDistance(in WalletStackGeometry g, int count)
        => StackY(0, count, g.CardHeight, 0) / 0.46;

    /// <summary>The pose in the stack and pocket (the closed layout).</summary>
    public static WalletStackPose Stack(double d, int count, in WalletStackGeometry g, double fan, double lift)
    {
        var h = g.CardHeight;
        double y, scale, shadow, opacity = 1;
        if (d >= 0)
        {
            y = StackY(d, count, h, fan);
            scale = 1 - 0.05 * Math.Min(d, VisibleDepth - 1);
            // Front .45, one back .15, continuous in between (Arc jumps from .60 to .45 at the boundary).
            shadow = d < 1 ? 0.45 - 0.30 * d : 0.15;
            if (d > VisibleDepth - 1)
            {
                opacity = Math.Clamp(VisibleDepth - d, 0, 1);
            }
        }
        else if (d >= -1)
        {
            var t = -d;
            var front = StackY(0, count, h, fan);
            y = front + (PocketY(g) - front) * t;
            scale = 1 - 0.1 * t;
            shadow = 0.45 * (1 - t);
        }
        else
        {
            var depth = Math.Min(-d - 1, VisibleDepth - 2);
            y = Math.Min(PocketY(g) + 7 * depth, g.RoundedStage - 28);
            scale = 0.9 + 0.02 * depth;
            shadow = 0;
            if (-d > VisibleDepth - 1)
            {
                opacity = Math.Clamp(VisibleDepth + d, 0, 1);
            }
        }
        if (lift > 0)
        {
            // The front card only deepens its shadow; cards behind (and in the pocket) also rise.
            y -= HoverRise * lift * Math.Min(1, Math.Abs(d));
            shadow += 0.35 * lift * Math.Clamp(1 + d, 0, 1);
        }
        return new WalletStackPose(y, scale, shadow, opacity);
    }

    /// <summary>The tucked pose of the <paramref name="rank"/>-th closed card while another one is open.</summary>
    public static WalletStackPose Tuck(int rank, in WalletStackGeometry g)
    {
        // Three slots (four cards); further cards wait, hidden, in the deepest slot.
        var m = Math.Min(rank, VisibleDepth - 2);
        var opacity = rank > VisibleDepth - 2 ? 0 : 1;
        return new WalletStackPose(g.RoundedStage - 30 - 6 * m, 0.94 - 0.02 * m, 0, opacity);
    }

    /// <summary>
    /// The full pose of card <paramref name="index"/>. <paramref name="open"/> is the summed raise of all cards (0
    /// closed, 1 open; above 1 while pulled up), <paramref name="raise"/> this card's share of it and
    /// <paramref name="tuckRank"/> its slot in the tucked row.
    /// </summary>
    public static WalletStackPose Pose(int index, int count, in WalletStackGeometry g, double position, double fan,
                                       double lift, double open, double raise, int tuckRank)
    {
        var stack = Stack(index - position, count, g, fan, lift);
        if (open == 0 && raise == 0)
        {
            return stack;
        }
        var tuck = Tuck(tuckRank, g);
        var tucked = Math.Max(0, open - raise);
        var y = stack.Y + (tuck.Y - stack.Y) * tucked + (0 - stack.Y) * raise;
        var scale = stack.Scale + (tuck.Scale - stack.Scale) * tucked + (RaisedScale - stack.Scale) * raise;
        var closedWeight = Math.Clamp(1 - open, 0, 1);
        var shadow = Math.Clamp(stack.Shadow * closedWeight + Math.Clamp(raise, 0, 1), 0, 1);
        var tuckedWeight = Math.Clamp(tucked, 0, 1);
        var opacity = stack.Opacity + (tuck.Opacity - stack.Opacity) * tuckedWeight;
        opacity += (1 - opacity) * Math.Clamp(raise, 0, 1);
        return new WalletStackPose(y, scale, shadow, opacity);
    }

    /// <summary>
    /// The tucked-row slot of card <paramref name="index"/>: its rank among the cards, skipping the raised one. The
    /// raised card itself takes the slot it would have next to <paramref name="previousRaised"/>, so switching the open
    /// card hands the slot over (the old card drops in where the new one rose from).
    /// </summary>
    public static int TuckRank(int index, int raised, int previousRaised)
    {
        var skipped = index == raised ? previousRaised : raised;
        return skipped >= 0 && skipped < index ? index - 1 : index;
    }

    /// <summary>The activity panel under the open card: it rises 16px and fades in over the last 60% of the opening.</summary>
    public static (double Offset, double Opacity) Detail(double open)
        => (16 * (1 - Math.Clamp(open, 0, 1)), Math.Clamp((open - 0.4) / 0.6, 0, 1));

    /// <summary>The top of the activity panel: below the raised card plus 14px.</summary>
    public static double DetailTop(in WalletStackGeometry g) => WalletStackGeometry.Inset + RaisedScale * g.CardHeight + 14;
}
