// Ported from Arc's free empty-states block (MIT, Copyright (c) 2026 Elia Kuratli): empty-states-art.ts.
// Every scene is drawn from the same ten primitives, in the same paint order: two back tiles, one morphing body, four
// front tiles, two line accents and one two-stroke mark. A scene only moves them. Tiles are rounded rectangles described
// by center, size, radius, rotation and skew, so a circle can become a panel. The body, lines and mark are equal-count
// point lists, sampled clockwise from the top left, so any outline can become any other.

namespace TKCode.Arc;

/// <summary>How a primitive of the illustration is drawn.</summary>
internal enum EmptyStatesPrimKind
{
    /// <summary>geo = [cx, cy, w, h, radius, rotation°, skew°, depth].</summary>
    Tile,

    /// <summary>geo = 176 closed points, then depth.</summary>
    Body,

    /// <summary>geo = 40 open points, then dash gap, depth at the start, depth at the end.</summary>
    Stroke,

    /// <summary>geo = two strokes of 12 open points, then dash gap and depth twice.</summary>
    Mark
}

/// <summary>Arc's <c>data-tone</c> values; <see cref="EmptyStatesArt"/> maps them to ArcDesign colors.</summary>
internal enum EmptyStatesTone
{
    Outline,
    Tuck,
    Slot,
    Query,
    Bar,
    Back,
    Shade,
    Dot,
    Ink,
    Muted,
    Subtle,
    Accent,
    Success,
    Warning
}

/// <summary>One primitive of a frame: its geometry vector, [opacity, drawn fraction], tone and whether it redraws.</summary>
internal sealed class EmptyStatesPrim
{
    public EmptyStatesPrim(EmptyStatesPrimKind kind, double[] geo, double opacity, double drawn, EmptyStatesTone tone, bool redraw)
    {
        Kind = kind;
        Geo = geo;
        Fx = [opacity, drawn];
        RedrawStart = [opacity, 0];
        Tone = tone;
        Redraw = redraw;
    }

    public EmptyStatesPrimKind Kind { get; }

    /// <summary>Springs as one vector (morph).</summary>
    public double[] Geo { get; }

    /// <summary>Opacity and how much of a stroke is drawn (smooth).</summary>
    public double[] Fx { get; }

    /// <summary>[opacity, 0]: where a redrawn stroke starts over.</summary>
    public double[] RedrawStart { get; }

    public EmptyStatesTone Tone { get; }

    /// <summary>A scene change clears the stroke and draws it again.</summary>
    public bool Redraw { get; }
}

/// <summary>One scene × phase: the ten primitives and the accessible description of the drawing.</summary>
internal sealed class EmptyStatesFrame(EmptyStatesPrim[] prims, string label)
{
    public EmptyStatesPrim[] Prims { get; } = prims;

    public string Label { get; } = label;
}

/// <summary>
/// The illustration's frames, computed once when the type is first used with Arc's exact sampling (body 176 points,
/// lines 40, the mark 2 × 12) and centered in the 320 × 216 view box by each scene's idle frame.
/// </summary>
internal static class EmptyStatesArtFrames
{
    public const double ViewWidth = 320;
    public const double ViewHeight = 216;
    public const int BodyPoints = 176;
    public const int StrokePoints = 40;
    public const int MarkPoints = 12;
    public const int PrimCount = 10;

    public static readonly EmptyStatesPrimKind[] Kinds =
    [
        EmptyStatesPrimKind.Tile, EmptyStatesPrimKind.Tile, EmptyStatesPrimKind.Body, EmptyStatesPrimKind.Tile, EmptyStatesPrimKind.Tile,
        EmptyStatesPrimKind.Tile, EmptyStatesPrimKind.Tile, EmptyStatesPrimKind.Stroke, EmptyStatesPrimKind.Stroke, EmptyStatesPrimKind.Mark
    ];

    private const double Tau = Math.PI * 2;
    private const double Rad = Math.PI / 180;

    private static readonly EmptyStatesFrame[,] s_frames = Build();

    /// <summary>The frame of <paramref name="scene"/> in <paramref name="phase"/>.</summary>
    public static EmptyStatesFrame Get(EmptyStatesScene scene, EmptyStatesPhase phase) => s_frames[Index(scene), Index(phase)];

    private static int Index(EmptyStatesScene scene) => scene switch
    {
        EmptyStatesScene.Offline => 1,
        EmptyStatesScene.CaughtUp => 2,
        EmptyStatesScene.NotFound => 3,
        _ => 0
    };

    private static int Index(EmptyStatesPhase phase) => phase switch
    {
        EmptyStatesPhase.Loading => 1,
        EmptyStatesPhase.Done => 2,
        _ => 0
    };

    #region Segments

    private readonly record struct Pt(double X, double Y);

    private sealed class Seg(double length, Func<double, Pt> at)
    {
        public double Length { get; } = length;

        public Pt At(double t) => at(t);
    }

    private static Seg Line(Pt a, Pt b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        return new Seg(Math.Sqrt(dx * dx + dy * dy), t => new Pt(a.X + dx * t, a.Y + dy * t));
    }

    /// <summary>Arcs always run clockwise on screen, from <paramref name="from"/> to <paramref name="to"/> in radians.</summary>
    private static Seg Arc(Pt c, double r, double from, double to)
    {
        var end = to;
        while (end < from - 1e-9)
        {
            end += Tau;
        }
        var span = end - from;
        return new Seg(span * r, t => new Pt(c.X + r * Math.Cos(from + span * t), c.Y + r * Math.Sin(from + span * t)));
    }

    private static Seg ArcDeg(Pt c, double r, double from, double to) => Arc(c, r, from * Rad, to * Rad);

    private static Pt Polar(Pt c, double r, double deg) => new(c.X + r * Math.Cos(deg * Rad), c.Y + r * Math.Sin(deg * Rad));

    private static Seg[] Chain(Pt[] points, bool closed = false)
    {
        var count = closed ? points.Length : points.Length - 1;
        var segs = new Seg[count];
        for (var i = 0; i < count; i++)
        {
            segs[i] = Line(points[i], points[(i + 1) % points.Length]);
        }
        return segs;
    }

    /// <summary>A Catmull-Rom curve through the points, walked by arc length.</summary>
    private static Seg Curve(Pt[] points)
    {
        var dense = new List<Pt>();
        for (var i = 0; i < points.Length - 1; i++)
        {
            Pt p0 = points[Math.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Math.Min(points.Length - 1, i + 2)];
            for (var s = 0; s < 16; s++)
            {
                double t = s / 16.0, t2 = t * t, t3 = t2 * t;
                dense.Add(new Pt(Axis(p0.X, p1.X, p2.X, p3.X, t, t2, t3), Axis(p0.Y, p1.Y, p2.Y, p3.Y, t, t2, t3)));
            }
        }
        dense.Add(points[^1]);
        var cum = new double[dense.Count];
        for (var i = 1; i < dense.Count; i++)
        {
            cum[i] = cum[i - 1] + Math.Sqrt(Square(dense[i].X - dense[i - 1].X) + Square(dense[i].Y - dense[i - 1].Y));
        }
        var length = cum[^1];
        return new Seg(length, t =>
        {
            var goal = t * length;
            var i = 1;
            while (i < cum.Length - 1 && cum[i] < goal)
            {
                i++;
            }
            Pt a = dense[i - 1], b = dense[i];
            var gap = cum[i] - cum[i - 1];
            var u = (goal - cum[i - 1]) / (gap != 0 ? gap : 1);
            return new Pt(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u);
        });

        static double Axis(double p0, double p1, double p2, double p3, double t, double t2, double t3)
            => .5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3);
    }

    private static double Square(double v) => v * v;

    /// <summary>Shares <paramref name="n"/> points between segments by length. Every segment starts on a point, so sharp corners survive the resample.</summary>
    private static int[] Allot(double[] lens, int n)
    {
        var total = 0.0;
        foreach (var len in lens)
        {
            total += len;
        }
        var counts = new int[lens.Length];
        if (total < 1e-6)
        {
            counts[0] = n;
            return counts;
        }
        var exact = new double[lens.Length];
        var sum = 0;
        for (var i = 0; i < lens.Length; i++)
        {
            exact[i] = lens[i] / total * n;
            counts[i] = lens[i] > 1e-6 ? Math.Max(1, (int)Math.Floor(exact[i])) : 0;
            sum += counts[i];
        }
        while (sum < n)
        {
            var best = -1;
            for (var i = 0; i < counts.Length; i++)
            {
                if (lens[i] > 1e-6 && (best < 0 || exact[i] - counts[i] > exact[best] - counts[best]))
                {
                    best = i;
                }
            }
            counts[best]++;
            sum++;
        }
        while (sum > n)
        {
            var best = -1;
            for (var i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 1 && (best < 0 || counts[i] - exact[i] > counts[best] - exact[best]))
                {
                    best = i;
                }
            }
            counts[best]--;
            sum--;
        }
        return counts;
    }

    private static List<double> Sample(Seg[] segs, int n, bool open)
    {
        var lens = new double[segs.Length];
        for (var i = 0; i < segs.Length; i++)
        {
            lens[i] = segs[i].Length;
        }
        var counts = Allot(lens, open ? n - 1 : n);
        var output = new List<double>(n * 2 + 3);
        for (var i = 0; i < segs.Length; i++)
        {
            for (var j = 0; j < counts[i]; j++)
            {
                var p = segs[i].At((double)j / counts[i]);
                output.Add(p.X);
                output.Add(p.Y);
            }
        }
        if (open)
        {
            var p = segs[^1].At(1);
            output.Add(p.X);
            output.Add(p.Y);
        }
        return output;
    }

    private static Seg[] RoundedRect(double x, double y, double w, double h, double tl, double tr, double br, double bl) =>
    [
        Line(new Pt(x + tl, y), new Pt(x + w - tr, y)), ArcDeg(new Pt(x + w - tr, y + tr), tr, -90, 0),
        Line(new Pt(x + w, y + tr), new Pt(x + w, y + h - br)), ArcDeg(new Pt(x + w - br, y + h - br), br, 0, 90),
        Line(new Pt(x + w - br, y + h), new Pt(x + bl, y + h)), ArcDeg(new Pt(x + bl, y + h - bl), bl, 90, 180),
        Line(new Pt(x, y + h - bl), new Pt(x, y + tl)), ArcDeg(new Pt(x + tl, y + tl), tl, 180, 270)
    ];

    private static Seg[] RoundedRect(double x, double y, double w, double h, double radius) => RoundedRect(x, y, w, h, radius, radius, radius, radius);

    private readonly record struct Lobe(Pt C, double R);

    /// <summary>A cloud is a row of overlapping circles sitting on a flat base; the outline walks each circle's exposed top between neighbours.</summary>
    private static Seg[] Cloud(Lobe[] lobes, double baseline)
    {
        static Pt Meet(Lobe a, Lobe b)
        {
            double dx = b.C.X - a.C.X, dy = b.C.Y - a.C.Y, d = Math.Sqrt(dx * dx + dy * dy);
            double along = (a.R * a.R - b.R * b.R + d * d) / (2 * d), h = Math.Sqrt(Math.Max(0, a.R * a.R - along * along));
            double mx = a.C.X + along * dx / d, my = a.C.Y + along * dy / d;
            Pt p = new(mx + h * dy / d, my - h * dx / d), q = new(mx - h * dy / d, my + h * dx / d);
            return p.Y < q.Y ? p : q;
        }
        static double Angle(Pt c, Pt p) => Math.Atan2(p.Y - c.Y, p.X - c.X);
        var joins = new Pt[lobes.Length - 1];
        for (var i = 1; i < lobes.Length; i++)
        {
            joins[i - 1] = Meet(lobes[i - 1], lobes[i]);
        }
        Lobe first = lobes[0], last = lobes[^1];
        var segs = new List<Seg> { Arc(first.C, first.R, -Math.PI / 2, Angle(first.C, joins[0])) };
        for (var i = 1; i < lobes.Length - 1; i++)
        {
            var lobe = lobes[i];
            segs.Add(Arc(lobe.C, lobe.R, Angle(lobe.C, joins[i - 1]), Angle(lobe.C, joins[i])));
        }
        segs.Add(Arc(last.C, last.R, Angle(last.C, joins[^1]), Math.PI / 2));
        segs.Add(Line(new Pt(last.C.X, baseline), new Pt(first.C.X, baseline)));
        segs.Add(Arc(first.C, first.R, Math.PI / 2, Math.PI * 1.5));
        return segs.ToArray();
    }

    #endregion

    #region Primitives

    private static EmptyStatesPrim Tile(double cx, double cy, double w, double h, double r, EmptyStatesTone tone,
                                        double rot = 0, double skew = 0, double z = 1, double o = 1)
        => new(EmptyStatesPrimKind.Tile, [cx, cy, w, h, r, rot, skew, z], o, 1, tone, false);

    private static EmptyStatesPrim Box(double x, double y, double w, double h, double r, EmptyStatesTone tone, double z = 1)
        => Tile(x + w / 2, y + h / 2, w, h, r, tone, z: z);

    private static EmptyStatesPrim Disc(Pt c, double r, EmptyStatesTone tone, double z = 1)
        => Tile(c.X, c.Y, r * 2, r * 2, r, tone, z: z);

    /// <summary>Shapes a scene does not need shrink into a point inside something that stays, and fade on the way.</summary>
    private static EmptyStatesPrim Park(Pt c, double z) => Tile(c.X, c.Y, 4, 4, 2, EmptyStatesTone.Tuck, z: z, o: 0);

    private static EmptyStatesPrim Body(Seg[] segs, double z)
    {
        var geo = Sample(segs, BodyPoints, false);
        geo.Add(z);
        return new EmptyStatesPrim(EmptyStatesPrimKind.Body, geo.ToArray(), 1, 1, EmptyStatesTone.Outline, false);
    }

    private static EmptyStatesPrim Stroke(Seg[] segs, EmptyStatesTone tone, double dash = 0, double z = 1, double? z1 = null,
                                          double o = 1, double draw = 1, bool redraw = false)
    {
        var geo = Sample(segs, StrokePoints, true);
        geo.Add(dash);
        geo.Add(z);
        geo.Add(z1 ?? z);
        return new EmptyStatesPrim(EmptyStatesPrimKind.Stroke, geo.ToArray(), o, draw, tone, redraw);
    }

    private static EmptyStatesPrim Rest(Pt p, double z) => Stroke([Line(p, p)], EmptyStatesTone.Ink, z: z, o: 0, draw: 0);

    private static EmptyStatesPrim Mark((Seg[] A, Seg[] B) strokes, EmptyStatesTone tone, double z = 1, double o = 1, double draw = 1, bool redraw = true)
    {
        var geo = Sample(strokes.A, MarkPoints, true);
        geo.AddRange(Sample(strokes.B, MarkPoints, true));
        geo.Add(0);
        geo.Add(z);
        geo.Add(z);
        return new EmptyStatesPrim(EmptyStatesPrimKind.Mark, geo.ToArray(), o, draw, tone, redraw);
    }

    private static (Seg[], Seg[]) Check(Pt c, double s)
    {
        Pt a = new(c.X - .42 * s, c.Y + .02 * s), b = new(c.X - .12 * s, c.Y + .32 * s), e = new(c.X + .44 * s, c.Y - .3 * s);
        return ([Line(a, b)], [Line(b, e)]);
    }

    private static (Seg[], Seg[]) Cross(Pt c, double h)
        => ([Line(new Pt(c.X - h, c.Y - h), new Pt(c.X + h, c.Y + h))], [Line(new Pt(c.X + h, c.Y - h), new Pt(c.X - h, c.Y + h))]);

    private static (Seg[], Seg[]) Slashes(Pt c)
        => ([Line(new Pt(c.X - 10, c.Y + 5), new Pt(c.X - 2, c.Y - 5))], [Line(new Pt(c.X + 2, c.Y + 5), new Pt(c.X + 10, c.Y - 5))]);

    private static (Seg[], Seg[]) Glare(Pt c, double r) => ([ArcDeg(c, r, 196, 236)], [ArcDeg(c, r, 248, 256)]);

    #endregion

    #region Scenes

    private static EmptyStatesFrame Search(EmptyStatesPhase phase)
    {
        var card = Body(RoundedRect(58, 36, 172, 132, 18), .6);
        EmptyStatesPrim tuck0 = Tile(144, 102, 96, 58, 14, EmptyStatesTone.Tuck, z: .3), tuck1 = Tile(144, 102, 120, 76, 16, EmptyStatesTone.Tuck, z: .3);
        if (phase != EmptyStatesPhase.Done)
        {
            var lens = new Pt(196, 118);
            return new EmptyStatesFrame(
            [
                tuck0, tuck1, card,
                Box(78, 64, 132, 20, 8, EmptyStatesTone.Slot, z: .6), Box(78, 92, 132, 20, 8, EmptyStatesTone.Slot, z: .6),
                Box(78, 120, 132, 20, 8, EmptyStatesTone.Slot, z: .6),
                Disc(lens, 30, EmptyStatesTone.Outline, z: 1.6),
                Stroke([Line(Polar(lens, 30, 45), new Pt(244, 166))], EmptyStatesTone.Ink, z: 1.6),
                Rest(new Pt(244, 166), 1.6),
                Mark(Glare(lens, 20), EmptyStatesTone.Muted, z: 1.6)
            ], "A magnifying glass over three empty result rows");
        }
        // Filters cleared: the magnifier docks into the header beside the query and the rows fill in as results.
        var docked = new Pt(204, 61);
        return new EmptyStatesFrame(
        [
            tuck0, tuck1, card,
            Box(78, 56, 64, 10, 5, EmptyStatesTone.Query, z: .6), Box(78, 100, 128, 10, 5, EmptyStatesTone.Bar, z: .6),
            Box(78, 124, 96, 10, 5, EmptyStatesTone.Bar, z: .6),
            Disc(docked, 9, EmptyStatesTone.Outline, z: 1),
            Stroke([Line(Polar(docked, 9, 45), Polar(docked, 17, 45))], EmptyStatesTone.Ink, z: 1),
            Stroke([Line(new Pt(58, 84), new Pt(230, 84))], EmptyStatesTone.Subtle, z: .6),
            Mark(Glare(docked, 5), EmptyStatesTone.Muted, z: 1, o: 0, draw: 0)
        ], "A results list with a search field and two results");
    }

    private static EmptyStatesFrame Offline(EmptyStatesPhase phase)
    {
        var hub = new Pt(160, 92);
        var (top, bottom) = phase == EmptyStatesPhase.Done ? (146.0, 146.0) : phase == EmptyStatesPhase.Loading ? (141.0, 151.0) : (134.0, 158.0);
        var wire = phase == EmptyStatesPhase.Loading ? EmptyStatesTone.Accent : EmptyStatesTone.Ink;
        var label = phase == EmptyStatesPhase.Done ? "A cloud joined to a device by an unbroken line, with a check on the device"
            : phase == EmptyStatesPhase.Loading ? "The broken line reaching back toward the cloud"
            : "A cloud joined to a device by a broken line";
        var device = phase == EmptyStatesPhase.Done ? EmptyStatesTone.Success : phase == EmptyStatesPhase.Loading ? EmptyStatesTone.Accent : EmptyStatesTone.Outline;
        return new EmptyStatesFrame(
        [
            Tile(160, 92, 80, 30, 15, EmptyStatesTone.Tuck, z: .3), Tile(160, 92, 104, 36, 18, EmptyStatesTone.Tuck, z: .3),
            Body(Cloud([new Lobe(new Pt(100, 96), 22), new Lobe(new Pt(140, 72), 34), new Lobe(new Pt(188, 76), 28), new Lobe(new Pt(220, 98), 20)], 118), 1.3),
            Park(hub, 1.3), Park(hub, 1.3), Park(hub, 1.3),
            Box(134, 168, 52, 34, 9, device, z: .7),
            Stroke([Line(new Pt(160, 118), new Pt(160, top))], wire, z: 1.3, z1: 1),
            Stroke([Line(new Pt(160, bottom), new Pt(160, 168))], wire, z: 1, z1: .7),
            phase == EmptyStatesPhase.Done
                ? Mark(Check(new Pt(160, 185), 18), EmptyStatesTone.Success, z: .7)
                : Mark(Slashes(new Pt(160, 146)), EmptyStatesTone.Warning, z: 1, draw: phase == EmptyStatesPhase.Loading ? 0 : 1)
        ], label);
    }

    private static EmptyStatesFrame Inbox(EmptyStatesPhase phase)
    {
        var lift = phase == EmptyStatesPhase.Loading ? 1 : 0;
        Pt badge = new(226, 84), slot = new(160, 140);
        return new EmptyStatesFrame(
        [
            Box(112, 72 - 8 * lift, 96, 40, 12, EmptyStatesTone.Back, z: .25), Box(100, 88 - 4 * lift, 120, 40, 12, EmptyStatesTone.Back, z: .45),
            Body(RoundedRect(88, 104, 144, 72, 8, 8, 16, 16), .7),
            Park(slot, .7), Park(slot, .7), Park(slot, .7),
            Disc(badge, 17, phase == EmptyStatesPhase.Loading ? EmptyStatesTone.Muted : EmptyStatesTone.Success, z: 1.5),
            Stroke(Chain([new Pt(88, 126), new Pt(124, 126), new Pt(133, 140), new Pt(187, 140), new Pt(196, 126), new Pt(232, 126)]), EmptyStatesTone.Ink, z: .7),
            Rest(slot, .7),
            Mark(Check(badge, 16), EmptyStatesTone.Success, z: 1.5, draw: phase == EmptyStatesPhase.Loading ? 0 : 1)
        ], phase == EmptyStatesPhase.Loading ? "A stacked inbox while new mail is checked" : "A stacked inbox with a check mark");
    }

    private static EmptyStatesFrame Map(EmptyStatesPhase phase)
    {
        Pt start = new(90, 142), pin = new(206, 110);
        var found = phase == EmptyStatesPhase.Done;
        var route = found
            ? Curve([start, new Pt(114, 152), new Pt(140, 140), new Pt(160, 122), new Pt(182, 128), Polar(pin, 10, 135)])
            : Curve([start, new Pt(108, 124), new Pt(132, 130), new Pt(152, 108), new Pt(178, 100), new Pt(200, 84)]);
        var label = found ? "A folded map with a dashed route that ends at a checked destination"
            : phase == EmptyStatesPhase.Loading ? "A folded map while the route is searched again"
            : "A folded map with a dashed route that ends in an x";
        return new EmptyStatesFrame(
        [
            Tile(151, 107, 90, 56, 12, EmptyStatesTone.Tuck, z: .3), Tile(151, 107, 110, 70, 14, EmptyStatesTone.Tuck, z: .3),
            Body(Chain([new Pt(70, 62), new Pt(124, 48), new Pt(178, 62), new Pt(232, 48), new Pt(232, 152), new Pt(178, 166), new Pt(124, 152), new Pt(70, 166)], true), .8),
            Park(start, .8),
            Tile(151, 107, 54, 104, 0, EmptyStatesTone.Shade, skew: Math.Atan2(14, 54) / Rad, z: .8),
            found ? Disc(pin, 10, EmptyStatesTone.Success, z: .8) : Park(start, .8),
            Disc(start, 4, EmptyStatesTone.Dot, z: .8),
            Stroke([route], EmptyStatesTone.Accent, dash: 5, z: .8, draw: phase == EmptyStatesPhase.Loading ? 0 : 1, redraw: true),
            Rest(start, .8),
            found
                ? Mark(Check(pin, 10), EmptyStatesTone.Success, z: .8)
                : Mark(Cross(new Pt(208, 76), 5), EmptyStatesTone.Ink, z: .8, draw: phase == EmptyStatesPhase.Loading ? 0 : 1)
        ], label);
    }

    #endregion

    #region Centering

    /// <summary>Moves every point of a frame; used to center each scene in the view box.</summary>
    private static EmptyStatesFrame Move(EmptyStatesFrame frame, double dx, double dy)
    {
        var prims = new EmptyStatesPrim[frame.Prims.Length];
        for (var p = 0; p < prims.Length; p++)
        {
            var prim = frame.Prims[p];
            var geo = (double[])prim.Geo.Clone();
            if (prim.Kind == EmptyStatesPrimKind.Tile)
            {
                geo[0] += dx;
                geo[1] += dy;
            }
            else
            {
                var end = geo.Length - (prim.Kind == EmptyStatesPrimKind.Body ? 1 : 3);
                for (var i = 0; i < end; i += 2)
                {
                    geo[i] += dx;
                    geo[i + 1] += dy;
                }
            }
            prims[p] = new EmptyStatesPrim(prim.Kind, geo, prim.Fx[0], prim.Fx[1], prim.Tone, prim.Redraw);
        }
        return new EmptyStatesFrame(prims, frame.Label);
    }

    private static (double Dx, double Dy) Center(EmptyStatesFrame frame)
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        void Add(double x, double y)
        {
            x0 = Math.Min(x0, x);
            y0 = Math.Min(y0, y);
            x1 = Math.Max(x1, x);
            y1 = Math.Max(y1, y);
        }
        foreach (var p in frame.Prims)
        {
            if (p.Fx[0] < .01)
            {
                continue;
            }
            if (p.Kind == EmptyStatesPrimKind.Tile)
            {
                var g = p.Geo;
                Add(g[0] - g[2] / 2, g[1] - g[3] / 2);
                Add(g[0] + g[2] / 2, g[1] + g[3] / 2);
                continue;
            }
            if (p.Kind != EmptyStatesPrimKind.Body && p.Fx[1] < .01)
            {
                continue;
            }
            var end = p.Geo.Length - (p.Kind == EmptyStatesPrimKind.Body ? 1 : 3);
            for (var i = 0; i < end; i += 2)
            {
                Add(p.Geo[i], p.Geo[i + 1]);
            }
        }
        return (ViewWidth / 2 - (x0 + x1) / 2, ViewHeight / 2 - (y0 + y1) / 2);
    }

    /// <summary>Every frame is computed once, so the first render and every later one draw the same thing.</summary>
    private static EmptyStatesFrame[,] Build()
    {
        Func<EmptyStatesPhase, EmptyStatesFrame>[] builders = [Search, Offline, Inbox, Map];
        EmptyStatesPhase[] phases = [EmptyStatesPhase.Idle, EmptyStatesPhase.Loading, EmptyStatesPhase.Done];
        var frames = new EmptyStatesFrame[builders.Length, phases.Length];
        for (var s = 0; s < builders.Length; s++)
        {
            var (dx, dy) = Center(builders[s](EmptyStatesPhase.Idle));
            for (var p = 0; p < phases.Length; p++)
            {
                frames[s, p] = Move(builders[s](phases[p]), dx, dy);
            }
        }
        return frames;
    }

    #endregion
}
