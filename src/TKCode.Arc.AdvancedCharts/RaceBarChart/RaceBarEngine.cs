using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The race itself, free of drawing: values by period, the ranking at any position on the timeline, and one spring per
/// row. A frame reuses its arrays and allocates nothing. Motion is code driven because every row keeps its own position
/// and velocity across retargets (a bar that is overtaken twice must not restart from rest), which declarative
/// transitions cannot do, and because a few hundred <see cref="MotionValue{T}"/> instances would cost more than the bars.
/// </summary>
internal sealed class RaceBarEngine
{
    /// <summary>Each row's slide between ranks: slightly under-damped, so an overtake reads as one continuous move.</summary>
    private static readonly SpringSpec s_row = SpringSpec.FromVisual(0.5, 0.06);

    /// <summary>A glide along the timeline (a step, a seek, a scrub): critically damped, values never overshoot.</summary>
    private static readonly SpringSpec s_glide = SpringSpec.FromVisual(0.6, 0);

    private static readonly SpringSpec s_domain = SpringSpec.FromVisual(0.45, 0);
    private static readonly SpringSpec s_thumb = SpringSpec.FromVisual(0.24, 0);

    private const double FadeSeconds = 0.09;
    private const double DimSeconds = 0.07;
    private const double TickCrossfadeSeconds = 0.24;
    private const double DimmedOpacity = 0.42;

    private double[][] _values = [];

    /// <summary>The items, duplicates removed.</summary>
    public RaceBarItem[] Items { get; private set; } = [];

    /// <summary>The periods in order.</summary>
    public RaceBarFrame[] Frames { get; private set; } = [];

    /// <summary>The groups, duplicates removed.</summary>
    public RaceBarGroup[] Groups { get; private set; } = [];

    /// <summary>Series hue of each item (0 to 3), or -1 for the neutral bar of an item without a colored group.</summary>
    public int[] Hue { get; private set; } = [];

    /// <summary>The value drawn for each item at <see cref="Position"/>.</summary>
    public double[] Shown { get; private set; } = [];

    /// <summary>Rank of each item (0 is the leader).</summary>
    public int[] Rank { get; private set; } = [];

    /// <summary>Item at each rank.</summary>
    public int[] Order { get; private set; } = [];

    /// <summary>Row of each item on screen, in row units (fractional while it slides).</summary>
    public double[] Row { get; private set; } = [];

    /// <summary>Velocity of each row, in rows per second.</summary>
    public double[] RowVelocity { get; private set; } = [];

    /// <summary>Presence of each row: 1 in the top set, 0 below it.</summary>
    public double[] Presence { get; private set; } = [];

    /// <summary>Dimming of each row while another one is being read.</summary>
    public double[] Dim { get; private set; } = [];

    public int ItemCount => Items.Length;

    public int FrameCount => Frames.Length;

    /// <summary>Where the race is: 2.5 is halfway between the third and the fourth period.</summary>
    public double Position { get; set; }

    public double PositionVelocity { get; set; }

    /// <summary>True while <see cref="Position"/> follows the glide spring to <see cref="GlideTarget"/>.</summary>
    public bool IsGliding { get; set; }

    public double GlideTarget { get; set; }

    /// <summary>The value at the right end of the plot.</summary>
    public double Domain { get; private set; } = 1;

    private double DomainVelocity { get; set; }

    /// <summary>Where the timeline's thumb is drawn, on its own critically damped spring.</summary>
    public double Thumb { get; private set; }

    private double ThumbVelocity { get; set; }

    /// <summary>Distance between axis ticks.</summary>
    public double TickStep { get; private set; } = 1;

    /// <summary>The step the axis is fading out of, or 0.</summary>
    public double PreviousTickStep { get; private set; }

    /// <summary>Crossfade from <see cref="PreviousTickStep"/> to <see cref="TickStep"/>, 0 to 1.</summary>
    public double TickMix { get; private set; } = 1;

    /// <summary>Nothing is moving any more.</summary>
    public bool IsSettled { get; private set; } = true;

    /// <summary>Replaces the data. Rows and values that exist in both datasets keep their place on screen.</summary>
    public void Load(IEnumerable<RaceBarItem> items, IEnumerable<RaceBarFrame> frames, IEnumerable<RaceBarGroup> groups)
    {
        var oldIndex = new Dictionary<string, int>(Items.Length);
        for (var i = 0; i < Items.Length; i++)
        {
            oldIndex[Items[i].Id] = i;
        }
        var oldRow = Row;
        var oldVelocity = RowVelocity;
        var oldPresence = Presence;

        var seen = new HashSet<string>();
        Items = items.Where(item => seen.Add(item.Id)).ToArray();
        seen.Clear();
        Groups = groups.Where(group => seen.Add(group.Id)).ToArray();
        Frames = frames.ToArray();

        var count = Items.Length;
        var index = new Dictionary<string, int>(count);
        for (var i = 0; i < count; i++)
        {
            index[Items[i].Id] = i;
        }
        _values = new double[Frames.Length][];
        for (var f = 0; f < Frames.Length; f++)
        {
            var row = new double[count];
            foreach (var value in Frames[f].Values)
            {
                if (index.TryGetValue(value.Id, out var i) && double.IsFinite(value.Value))
                {
                    row[i] = value.Value;
                }
            }
            _values[f] = row;
        }

        Hue = new int[count];
        for (var i = 0; i < count; i++)
        {
            Hue[i] = HueOf(Items[i]);
        }

        Shown = new double[count];
        Rank = new int[count];
        Order = new int[count];
        Row = new double[count];
        RowVelocity = new double[count];
        Presence = new double[count];
        Dim = new double[count];
        for (var i = 0; i < count; i++)
        {
            Order[i] = i;
            Dim[i] = 1;
            if (oldIndex.TryGetValue(Items[i].Id, out var old) && old < oldRow.Length)
            {
                Row[i] = oldRow[old];
                RowVelocity[i] = oldVelocity[old];
                Presence[i] = oldPresence[old];
            }
            else
            {
                Row[i] = double.NaN;
            }
        }
        HasSurvivingRows = oldIndex.Count > 0 && Row.Any(row => !double.IsNaN(row));
        Position = FrameCount == 0 ? 0 : Math.Clamp(Position, 0, FrameCount - 1);
    }

    /// <summary>Some rows of the last load also existed before it, so they can slide from where they were.</summary>
    public bool HasSurvivingRows { get; private set; }

    /// <summary>After a load: values and ranks at <see cref="Position"/>, and rows that are new wait just below the top set.</summary>
    public void Refresh(int barCount)
    {
        Sample();
        Rerank();
        for (var i = 0; i < Row.Length; i++)
        {
            if (double.IsNaN(Row[i]))
            {
                Row[i] = barCount;
            }
        }
    }

    private int HueOf(RaceBarItem item)
    {
        if (Groups.Length == 0)
        {
            return 0;
        }
        for (var g = 0; g < Math.Min(4, Groups.Length); g++)
        {
            if (Groups[g].Id == item.Group)
            {
                return g;
            }
        }
        return -1;
    }

    /// <summary>The value of an item at any position on the timeline (linear between periods).</summary>
    public double ValueAt(int item, double position)
    {
        if (FrameCount == 0)
        {
            return 0;
        }
        var p = Math.Clamp(position, 0, FrameCount - 1);
        var low = (int)Math.Floor(p);
        var high = Math.Min(low + 1, FrameCount - 1);
        var a = _values[low][item];
        return a + (_values[high][item] - a) * (p - low);
    }

    /// <summary>The ranked first item of a period, or -1.</summary>
    public int LeaderOf(int frame)
    {
        if (ItemCount == 0 || frame < 0 || frame >= FrameCount)
        {
            return -1;
        }
        var values = _values[frame];
        var best = 0;
        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] > values[best])
            {
                best = i;
            }
        }
        return best;
    }

    /// <summary>The largest value anywhere in the data; sizes the column kept free for value text.</summary>
    public double LargestValue()
    {
        var largest = 0.0;
        foreach (var frame in _values)
        {
            foreach (var value in frame)
            {
                largest = Math.Max(largest, value);
            }
        }
        return largest;
    }

    private void Sample()
    {
        for (var i = 0; i < Shown.Length; i++)
        {
            Shown[i] = ValueAt(i, Position);
        }
    }

    // Stable by construction: equal values always fall back to the order of Items, whichever way the timeline moved.
    // Insertion sort because the order is almost sorted from the frame before, and it allocates nothing.
    private void Rerank()
    {
        var order = Order;
        var shown = Shown;
        for (var a = 1; a < order.Length; a++)
        {
            var item = order[a];
            var value = shown[item];
            var b = a - 1;
            while (b >= 0 && (shown[order[b]] < value || (shown[order[b]] == value && order[b] > item)))
            {
                order[b + 1] = order[b];
                b--;
            }
            order[b + 1] = item;
        }
        for (var r = 0; r < order.Length; r++)
        {
            Rank[order[r]] = r;
        }
    }

    private double DomainTarget()
    {
        if (Order.Length == 0)
        {
            return 1;
        }
        var top = Shown[Order[0]];
        return top > 0 ? top : 1;
    }

    private void RetargetTicks(int tickCount)
    {
        var step = TickStepFor(Domain, tickCount);
        if (Math.Abs(step - TickStep) > step * 1e-9)
        {
            PreviousTickStep = TickMix > 0.5 ? TickStep : PreviousTickStep;
            TickStep = step;
            TickMix = 0;
        }
    }

    // NiceStep rounds down, which can give half again as many ticks as asked: go up the 1, 2, 5 ladder until they fit.
    private static double TickStepFor(double domain, int tickCount)
    {
        var step = ChartsBMath.NiceStep(domain, tickCount);
        while (domain / step > tickCount)
        {
            var power = Math.Pow(10, Math.Floor(Math.Log10(step) + 1e-9));
            var unit = step / power;
            step = (unit < 1.5 ? 2 : unit < 3 ? 5 : 10) * power;
        }
        return step;
    }

    /// <summary>Puts everything where it belongs at <see cref="Position"/>, without motion.</summary>
    public void Place(int barCount, int reading, int tickCount)
    {
        IsGliding = false;
        PositionVelocity = 0;
        Sample();
        Rerank();
        for (var i = 0; i < Row.Length; i++)
        {
            Row[i] = Math.Min(Rank[i], barCount);
            RowVelocity[i] = 0;
            Presence[i] = Rank[i] < barCount ? 1 : 0;
            Dim[i] = reading >= 0 && reading != i ? DimmedOpacity : 1;
        }
        Domain = DomainTarget();
        DomainVelocity = 0;
        Thumb = Position;
        ThumbVelocity = 0;
        TickStep = TickStepFor(Domain, tickCount);
        PreviousTickStep = 0;
        TickMix = 1;
        IsSettled = true;
    }

    /// <summary>Advances every spring by one frame.</summary>
    public void Update(double dt, int barCount, int reading, int tickCount)
    {
        var moving = false;
        if (IsGliding)
        {
            var (x, v) = SpringSolver.Evaluate(s_glide, Position - GlideTarget, PositionVelocity, dt);
            if (Math.Abs(x) < 0.0008 && Math.Abs(v) < 0.02)
            {
                Position = GlideTarget;
                PositionVelocity = 0;
                IsGliding = false;
            }
            else
            {
                Position = GlideTarget + x;
                PositionVelocity = v;
                moving = true;
            }
        }
        Sample();
        Rerank();

        var fade = 1 - Math.Exp(-dt / FadeSeconds);
        var dim = 1 - Math.Exp(-dt / DimSeconds);
        for (var i = 0; i < Row.Length; i++)
        {
            var target = (double)Math.Min(Rank[i], barCount);
            var (x, v) = SpringSolver.Evaluate(s_row, Row[i] - target, RowVelocity[i], dt);
            if (Math.Abs(x) < 0.002 && Math.Abs(v) < 0.02)
            {
                Row[i] = target;
                RowVelocity[i] = 0;
            }
            else
            {
                Row[i] = target + x;
                RowVelocity[i] = v;
                moving = true;
            }

            var presence = Rank[i] < barCount ? 1.0 : 0.0;
            Presence[i] += (presence - Presence[i]) * fade;
            if (Math.Abs(presence - Presence[i]) < 0.004)
            {
                Presence[i] = presence;
            }
            else
            {
                moving = true;
            }

            var dimTarget = reading >= 0 && reading != i ? DimmedOpacity : 1.0;
            Dim[i] += (dimTarget - Dim[i]) * dim;
            if (Math.Abs(dimTarget - Dim[i]) < 0.004)
            {
                Dim[i] = dimTarget;
            }
            else
            {
                moving = true;
            }
        }

        var domainTarget = DomainTarget();
        var (dx, dv) = SpringSolver.Evaluate(s_domain, Domain - domainTarget, DomainVelocity, dt);
        if (Math.Abs(dx) < domainTarget * 0.0005 && Math.Abs(dv) < domainTarget * 0.002)
        {
            Domain = domainTarget;
            DomainVelocity = 0;
        }
        else
        {
            Domain = domainTarget + dx;
            DomainVelocity = dv;
            moving = true;
        }

        var (tx, tv) = SpringSolver.Evaluate(s_thumb, Thumb - Position, ThumbVelocity, dt);
        if (Math.Abs(tx) < 0.001 && Math.Abs(tv) < 0.02)
        {
            Thumb = Position;
            ThumbVelocity = 0;
        }
        else
        {
            Thumb = Position + tx;
            ThumbVelocity = tv;
            moving = true;
        }

        RetargetTicks(tickCount);
        if (TickMix < 1)
        {
            TickMix = Math.Min(1, TickMix + dt / TickCrossfadeSeconds);
            moving = true;
        }
        IsSettled = !moving;
    }

    /// <summary>Marks that a spring has a new target, so the next frames run.</summary>
    public void Unsettle() => IsSettled = false;

    /// <summary>The ticks now on the axis, for tests.</summary>
    public IReadOnlyList<double> TickValues()
    {
        var ticks = new List<double>();
        for (var k = 0; k * TickStep <= Domain * (1 + 1e-9) && k < 64; k++)
        {
            ticks.Add(k * TickStep);
        }
        return ticks;
    }
}
