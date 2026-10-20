namespace TKCode.Arc;

/// <summary>Pure helpers of <see cref="ResizablePanels"/> (Arc's drag physics and the flex distribution).</summary>
internal static class ResizablePanelsMath
{
    /// <summary>Width in px where a pane starts to resist (Arc: 80).</summary>
    public const double DefaultMin = 80;

    /// <summary>How far in px a pane can be pulled past a limit before it stops giving (Arc: 44).</summary>
    public const double Stretch = 44;

    /// <summary>Keyboard steps in px; Shift takes the larger one (Arc: 16 / 64).</summary>
    public const double Step = 16;

    public const double BigStep = 64;

    /// <summary>Past a limit every pixel costs more, like pulling against elastic.</summary>
    public static double Rubber(double overshoot) => overshoot * Stretch * 0.55 / (Stretch + 0.55 * overshoot);

    public static double Band(double value, double lo, double hi)
        => value < lo ? lo - Rubber(lo - value) : value > hi ? hi + Rubber(value - hi) : value;

    /// <summary>A collapsible pane snaps closed once the pointer is this far below its minimum.</summary>
    public static double CollapseAt(double min) => min - Math.Max(min * 0.5, 40);

    /// <summary>Short momentum projection (decay rate .99), used only to decide whether a flick collapses or restores.</summary>
    public static double Project(double velocity, double rate = 0.99) => velocity / 1000 * rate / (1 - rate);

    /// <summary>Pointer velocity in px/s from samples of the last 90ms; 0 when the pointer rested for 50ms.</summary>
    public static double VelocityOf(IReadOnlyList<(double Time, double X)> samples, double now)
    {
        var first = -1;
        var last = -1;
        for (var i = 0; i < samples.Count; i++)
        {
            if (now - samples[i].Time <= 90)
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        if (first < 0 || last <= first || now - samples[last].Time > 50)
        {
            return 0;
        }
        var dt = samples[last].Time - samples[first].Time;
        return dt > 0 ? (samples[last].X - samples[first].X) / (dt / 1000) : 0;
    }

    /// <summary>Percent shares of <paramref name="values"/>.</summary>
    public static double[] ToShares(IReadOnlyList<double> values)
    {
        var total = 0.0;
        foreach (var value in values)
        {
            total += Math.Max(0, value);
        }
        if (total <= 0)
        {
            total = 1;
        }
        var shares = new double[values.Count];
        for (var i = 0; i < shares.Length; i++)
        {
            shares[i] = Math.Max(0, values[i]) / total * 100;
        }
        return shares;
    }

    /// <summary>
    /// CSS flex distribution (<c>flex: 0 1 0px</c>, grow = size): the room is shared in proportion to the grow
    /// factors, then panes that break their min / max are frozen at the limit and the rest is shared again.
    /// </summary>
    public static void Distribute(double room, ReadOnlySpan<double> grow, ReadOnlySpan<double> min, ReadOnlySpan<double> max, Span<double> result)
    {
        var count = grow.Length;
        Span<bool> frozen = stackalloc bool[count];
        for (var pass = 0; pass <= count; pass++)
        {
            var fixedTotal = 0.0;
            var growTotal = 0.0;
            for (var i = 0; i < count; i++)
            {
                if (frozen[i])
                {
                    fixedTotal += result[i];
                }
                else
                {
                    growTotal += Math.Max(0, grow[i]);
                }
            }
            var free = Math.Max(0, room - fixedTotal);
            var violation = 0.0;
            for (var i = 0; i < count; i++)
            {
                if (frozen[i])
                {
                    continue;
                }
                var size = growTotal > 0 ? free * Math.Max(0, grow[i]) / growTotal : 0;
                var bounded = Math.Min(Math.Max(size, min[i]), max[i]);
                result[i] = size;
                violation += bounded - size;
            }
            if (Math.Abs(violation) < 0.01)
            {
                return;
            }
            // Freeze the panes that violate in the direction of the total violation (CSS flex algorithm).
            var any = false;
            for (var i = 0; i < count; i++)
            {
                if (frozen[i])
                {
                    continue;
                }
                var size = result[i];
                var bounded = Math.Min(Math.Max(size, min[i]), max[i]);
                if ((violation > 0 && bounded > size) || (violation < 0 && bounded < size))
                {
                    result[i] = bounded;
                    frozen[i] = true;
                    any = true;
                }
            }
            if (!any)
            {
                return;
            }
        }
    }
}
