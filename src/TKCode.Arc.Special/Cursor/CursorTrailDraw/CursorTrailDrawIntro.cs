using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The short sketch a <see cref="CursorTrailDraw"/> board plays the first time it is in view: a rough box and a rough
/// circle that snap clean, an accent line that joins them, then a laser sweep under the circle. A timeline of pen moves
/// in board units; the board feeds it through the same smoothing, width and snapping as a real pointer.
/// </summary>
internal sealed class CursorTrailDrawIntro
{
    /// <summary>One part of the sketch: a stroke (pen or laser) or a glide with the pen up.</summary>
    internal sealed record Part(double Start, double Duration, Point[] Path, bool Down, CursorTrailDrawTool Tool, int Ink, bool EaseEachLeg);

    private readonly List<Part> _parts = new();

    private CursorTrailDrawIntro(double boardHeight, bool tall)
    {
        Build(boardHeight, tall);
    }

    public IReadOnlyList<Part> Parts => _parts;

    /// <summary>When the cursor appears.</summary>
    public double CursorIn { get; private set; }

    /// <summary>When the last part ends.</summary>
    public double End => _parts.Count == 0 ? 0 : _parts[^1].Start + _parts[^1].Duration;

    /// <summary>The sketch for a board of this height (board units; 1000 wide). Below 1.1 wide per tall the layout stacks.</summary>
    public static CursorTrailDrawIntro For(double boardHeight) => new(boardHeight, boardHeight > 900);

    /// <summary>Where the pen is at <paramref name="time"/> seconds, and which part it is in (−1 before and after).</summary>
    public (Point Position, int Part) Sample(double time)
    {
        if (_parts.Count == 0)
        {
            return (default, -1);
        }
        if (time < _parts[0].Start)
        {
            return (_parts[0].Path[0], -1);
        }
        for (var i = 0; i < _parts.Count; i++)
        {
            var part = _parts[i];
            if (time <= part.Start + part.Duration)
            {
                var t = Math.Clamp((time - part.Start) / part.Duration, 0, 1);
                return (PointAt(part, t), i);
            }
            if (i + 1 < _parts.Count && time < _parts[i + 1].Start)
            {
                return (part.Path[^1], -1);
            }
        }
        return (_parts[^1].Path[^1], -1);
    }

    /// <summary>The clean shapes the sketch ends with, for boards that skip the animation.</summary>
    public IEnumerable<Part> Strokes => _parts.Where(p => p.Down && p.Tool == CursorTrailDrawTool.Pen);

    private static Point PointAt(Part part, double t)
    {
        var path = part.Path;
        if (part.EaseEachLeg && path.Length > 2)
        {
            // Each leg of a box is its own quick move with a short slow-down at the corner, like a hand.
            var legs = path.Length - 1;
            var lengths = new double[legs];
            double total = 0;
            for (var i = 0; i < legs; i++)
            {
                lengths[i] = CursorTrailDrawInkGeometry.Distance(path[i], path[i + 1]);
                total += lengths[i];
            }
            var at = t * total;
            for (var i = 0; i < legs; i++)
            {
                if (at <= lengths[i] || i == legs - 1)
                {
                    var local = lengths[i] <= 0 ? 1 : Math.Clamp(at / lengths[i], 0, 1);
                    return Along(path[i], path[i + 1], EaseInOut(local));
                }
                at -= lengths[i];
            }
        }
        return AlongPath(path, EaseInOut(t));
    }

    private static Point AlongPath(Point[] path, double fraction)
    {
        var total = CursorTrailDrawInkGeometry.TotalLength(path, path.Length);
        var target = total * fraction;
        for (var i = 1; i < path.Length; i++)
        {
            var length = CursorTrailDrawInkGeometry.Distance(path[i - 1], path[i]);
            if (target <= length || i == path.Length - 1)
            {
                return Along(path[i - 1], path[i], length <= 0 ? 1 : Math.Clamp(target / length, 0, 1));
            }
            target -= length;
        }
        return path[^1];
    }

    private static Point Along(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    // A gentle S curve: quick in the middle, slower at both ends.
    private static double EaseInOut(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    private void Build(double height, bool tall)
    {
        var time = 0.45;
        CursorIn = time;
        Point[] box;
        Point[] circle;
        Point[] join;
        Point[] laser;
        if (tall)
        {
            var dy = (height - 1250) / 2;
            box = RoughBox(new Rect(290, 170 + dy, 420, 240), 3);
            circle = RoughCircle(new Point(500, 770 + dy), 140, 1.06, -Math.PI / 2, 5);
            join = Wobble([new Point(503, 432 + dy), new Point(498, 608 + dy)], 7);
            laser = Curve(new Point(360, 960 + dy), new Point(500, 992 + dy), new Point(645, 950 + dy));
        }
        else
        {
            var dy = (height - 625) / 2;
            box = RoughBox(new Rect(128, 196 + dy, 232, 192), 3);
            circle = RoughCircle(new Point(735, 300 + dy), 124, 1.06, -Math.PI / 2, 5);
            join = Wobble([new Point(381, 294 + dy), new Point(590, 290 + dy)], 7);
            laser = Curve(new Point(620, 470 + dy), new Point(735, 492 + dy), new Point(858, 458 + dy));
        }
        time = Add(time, 1.25, box, true, CursorTrailDrawTool.Pen, 0, true);
        time = Glide(time + 0.12, 0.5, box[^1], circle[0]);
        time = Add(time, 1.05, circle, true, CursorTrailDrawTool.Pen, 0, false);
        time = Glide(time + 0.12, 0.45, circle[^1], join[0]);
        time = Add(time, 0.5, join, true, CursorTrailDrawTool.Pen, 1, false);
        time = Glide(time + 0.12, 0.45, join[^1], laser[0]);
        Add(time + 0.1, 0.8, laser, true, CursorTrailDrawTool.Laser, 0, false);
    }

    private double Add(double start, double duration, Point[] path, bool down, CursorTrailDrawTool tool, int ink, bool legs)
    {
        _parts.Add(new Part(start, duration, path, down, tool, ink, legs));
        return start + duration;
    }

    private double Glide(double start, double duration, Point from, Point to) =>
        Add(start, duration, [from, Along(from, to, 0.5) + new Vector(0, -12), to], false, CursorTrailDrawTool.Pen, 0, false);

    /// <summary>A box drawn as one stroke clockwise from the top left, with slightly off corners and a small overshoot.</summary>
    private static Point[] RoughBox(Rect r, int seed)
    {
        var jitter = Jitter(seed);
        return
        [
            new Point(r.Left + jitter(0), r.Top + jitter(1)),
            new Point(r.Right + jitter(2), r.Top - 4 + jitter(3)),
            new Point(r.Right + 3 + jitter(4), r.Bottom + jitter(5)),
            new Point(r.Left - 2 + jitter(6), r.Bottom + 3 + jitter(7)),
            new Point(r.Left + 3 + jitter(8), r.Top + 14 + jitter(9)),
        ];
    }

    /// <summary>A loop a little past a full turn whose radius breathes, like a quick hand-drawn circle.</summary>
    private static Point[] RoughCircle(Point center, double radius, double turns, double start, int seed)
    {
        var steps = 72;
        var points = new Point[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var a = start + t * turns * Math.PI * 2;
            var r = radius * (1 + 0.045 * Math.Sin(a * 2 + seed) + 0.03 * t);
            points[i] = new Point(center.X + Math.Cos(a) * r * 1.03, center.Y + Math.Sin(a) * r);
        }
        return points;
    }

    private static Point[] Wobble(Point[] ends, double amount)
    {
        var steps = 24;
        var points = new Point[steps + 1];
        var dx = ends[1].X - ends[0].X;
        var dy = ends[1].Y - ends[0].Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var nx = -dy / length;
        var ny = dx / length;
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var off = Math.Sin(t * Math.PI) * amount * 0.4;
            points[i] = new Point(ends[0].X + dx * t + nx * off, ends[0].Y + dy * t + ny * off);
        }
        return points;
    }

    private static Point[] Curve(Point a, Point control, Point b)
    {
        var steps = 32;
        var points = new Point[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var t = (double)i / steps;
            var u = 1 - t;
            points[i] = new Point(u * u * a.X + 2 * u * t * control.X + t * t * b.X, u * u * a.Y + 2 * u * t * control.Y + t * t * b.Y);
        }
        return points;
    }

    private static Func<int, double> Jitter(int seed) => i => Math.Sin(seed * 12.9898 + i * 78.233) * 4;
}
