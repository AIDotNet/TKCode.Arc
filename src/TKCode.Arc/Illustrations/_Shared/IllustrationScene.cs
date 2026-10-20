using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The color family a node, edge, badge or packet is shown in.</summary>
internal enum IllustrationTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>
/// A scalar animated on the scene clock: a tween on an easing or a spring that starts from the current value and
/// velocity. A struct evaluated on demand, so animating allocates nothing and needs no per-value frame callbacks.
/// </summary>
internal struct IllustrationValue
{
    private double _from;
    private double _to;
    private double _velocity;
    private double _start;
    private double _duration;
    private SpringSpec _spring;
    private Easing? _easing;
    private bool _isSpring;

    public IllustrationValue(double value)
    {
        _from = _to = value;
        _duration = 0;
    }

    public double Target => _to;

    public double Value(double now)
    {
        var t = now - _start;
        if (_isSpring)
        {
            if (t <= 0)
            {
                return _from;
            }
            var (x, _) = SpringSolver.Evaluate(_spring, _from - _to, _velocity, t);
            return _to + x;
        }
        if (_duration <= 0 || t >= _duration)
        {
            return _to;
        }
        if (t <= 0)
        {
            return _from;
        }
        var p = t / _duration;
        return _from + (_to - _from) * (_easing?.Ease(p) ?? p);
    }

    private double Velocity(double now)
    {
        if (_isSpring)
        {
            var t = Math.Max(0, now - _start);
            return SpringSolver.Evaluate(_spring, _from - _to, _velocity, t).Velocity;
        }
        return 0;
    }

    /// <summary>True while it still moves at <paramref name="now"/>.</summary>
    public bool IsMoving(double now)
    {
        if (_isSpring)
        {
            var t = now - _start;
            if (t < 0)
            {
                return true;
            }
            var (x, v) = SpringSolver.Evaluate(_spring, _from - _to, _velocity, t);
            return Math.Abs(x) > 0.0005 || Math.Abs(v) > 0.005;
        }
        return now - _start < _duration;
    }

    public void Jump(double value)
    {
        _from = _to = value;
        _velocity = 0;
        _duration = 0;
        _isSpring = false;
    }

    /// <summary>Tweens from the current value to <paramref name="to"/>, starting after <paramref name="delay"/>.</summary>
    public void Tween(double to, double now, double duration, Easing? easing = null, double delay = 0)
    {
        _from = Value(now);
        _to = to;
        _start = now + delay;
        _duration = duration;
        _easing = easing ?? ArcEasings.Standard;
        _velocity = 0;
        _isSpring = false;
    }

    /// <summary>Springs to <paramref name="to"/> keeping the current velocity.</summary>
    public void Spring(double to, double now, in SpringSpec spring, double delay = 0)
    {
        var velocity = Velocity(now);
        _from = Value(now);
        _to = to;
        _start = now + delay;
        _spring = spring;
        _velocity = velocity;
        _isSpring = true;
    }
}

/// <summary>A component of a system drawn as a card: icon tile, title, optional subtitle.</summary>
internal sealed class IllustrationNode
{
    public IllustrationNode(string id, Rect bounds, Geometry? icon, string title, string? subtitle = null)
    {
        Id = id;
        Bounds = bounds;
        Icon = icon;
        Title = title;
        Subtitle = subtitle;
    }

    public string Id { get; }

    public Rect Bounds { get; set; }

    public Geometry? Icon { get; set; }

    public string Title { get; set; }

    public string? Subtitle { get; set; }

    /// <summary>Title and subtitle as last drawn (the static layer redraws when either changes).</summary>
    internal string? DrawnTitle;

    internal string? DrawnSubtitle;

    /// <summary>The tone shown; changes cross-fade from <see cref="PreviousTone"/>.</summary>
    public IllustrationTone Tone { get; private set; }

    public IllustrationTone PreviousTone { get; private set; }

    /// <summary>0 shows <see cref="PreviousTone"/>, 1 shows <see cref="Tone"/>.</summary>
    public IllustrationValue ToneMix = new(1);

    /// <summary>Accent halo (active, hovered).</summary>
    public IllustrationValue Emphasis = new(0);

    /// <summary>Dims the card (out of service, not involved).</summary>
    public IllustrationValue Muted = new(0);

    /// <summary>A fill bar along the bottom (queue depth); hidden while negative.</summary>
    public IllustrationValue Meter = new(-1);

    /// <summary>A ring turning around the icon tile while the node works.</summary>
    public bool IsBusy { get; set; }

    /// <summary>Not drawn and not hoverable: an anchor for edges to a part the illustration draws itself.</summary>
    public bool IsHidden { get; init; }

    public IllustrationBadge? Badge { get; set; }

    public Point Center => Bounds.Center;

    public Point Left => new(Bounds.Left, Bounds.Center.Y);

    public Point Right => new(Bounds.Right, Bounds.Center.Y);

    public Point Top => new(Bounds.Center.X, Bounds.Top);

    public Point Bottom => new(Bounds.Center.X, Bounds.Bottom);

    public void SetTone(IllustrationTone tone, double now, bool animate = true)
    {
        if (tone == Tone)
        {
            return;
        }
        PreviousTone = animate ? Tone : tone;
        Tone = tone;
        if (animate)
        {
            ToneMix.Jump(0);
            ToneMix.Tween(1, now, 0.24);
        }
        else
        {
            ToneMix.Jump(1);
        }
    }

    /// <summary>Back to rest: neutral, no halo, not muted, no badge.</summary>
    public void Reset()
    {
        PreviousTone = Tone = IllustrationTone.Neutral;
        ToneMix.Jump(1);
        Emphasis.Jump(0);
        Muted.Jump(0);
        IsBusy = false;
        Badge?.Visible.Jump(0);
    }
}

/// <summary>A small pill next to a node ("Approved", "Promoted") that pops in on the snappy spring.</summary>
internal sealed class IllustrationBadge
{
    public string Text { get; set; } = string.Empty;

    public Geometry? Icon { get; set; }

    public IllustrationTone Tone { get; set; }

    /// <summary>Above the node (default) or below it.</summary>
    public bool Below { get; set; }

    /// <summary>0 hidden, 1 shown (scale 0.6 → 1 and fade).</summary>
    public IllustrationValue Visible = new(0);

    /// <summary>Text and tone as last drawn.</summary>
    internal string? DrawnText;

    internal IllustrationTone DrawnTone;
}

/// <summary>A dashed frame around related nodes (a region) with a title and a health dot.</summary>
internal sealed class IllustrationGroup
{
    public IllustrationGroup(Rect bounds, string title)
    {
        Bounds = bounds;
        Title = title;
    }

    public Rect Bounds { get; set; }

    public string Title { get; set; }

    /// <summary>A word after the title in the group's tone ("healthy", "down").</summary>
    public string? Status { get; set; }

    /// <summary>Status as last drawn.</summary>
    internal string? DrawnStatus;

    public IllustrationTone Tone { get; private set; } = IllustrationTone.Success;

    public IllustrationTone PreviousTone { get; private set; } = IllustrationTone.Success;

    public IllustrationValue ToneMix = new(1);

    /// <summary>Tints the whole frame while the group is in trouble.</summary>
    public IllustrationValue Alarm = new(0);

    public void SetTone(IllustrationTone tone, double now, bool animate = true)
    {
        if (tone == Tone)
        {
            return;
        }
        PreviousTone = animate ? Tone : tone;
        Tone = tone;
        if (animate)
        {
            ToneMix.Jump(0);
            ToneMix.Tween(1, now, 0.24);
        }
        else
        {
            ToneMix.Jump(1);
        }
    }
}

/// <summary>A connection between two nodes: a cubic curve or a polyline, flattened by arc length for packets.</summary>
internal sealed class IllustrationEdge
{
    private const int CurveSteps = 24;

    public IllustrationEdge(IllustrationNode from, IllustrationNode to, Point[] route, bool curved, bool dashed = false)
    {
        From = from;
        To = to;
        Route = route;
        IsCurved = curved;
        IsDashed = dashed;
        Flatten();
    }

    public IllustrationNode From { get; }

    public IllustrationNode To { get; }

    public Point[] Route { get; private set; }

    public bool IsCurved { get; }

    public bool IsDashed { get; set; }

    /// <summary>Highlight in <see cref="ActiveTone"/> (1.5px) over the resting line.</summary>
    public IllustrationValue Active = new(0);

    public IllustrationTone ActiveTone { get; set; } = IllustrationTone.Accent;

    /// <summary>Fades the line (a path not in use).</summary>
    public IllustrationValue Dim = new(0);

    /// <summary>Highlight while a connected node is hovered.</summary>
    public IllustrationValue Hover = new(0);

    /// <summary>Overall visibility of the line (a path that only exists during a flight).</summary>
    public IllustrationValue Shown = new(1);

    public StreamGeometry Geometry { get; private set; } = null!;

    public double Length { get; private set; }

    private Point[] _points = Array.Empty<Point>();
    private double[] _lengths = Array.Empty<double>();

    /// <summary>Horizontal S-curve from <paramref name="a"/> to <paramref name="b"/> (or vertical when <paramref name="vertical"/>).</summary>
    public static Point[] Curve(Point a, Point b, bool vertical = false)
    {
        if (vertical)
        {
            var dy = (b.Y - a.Y) / 2;
            return [a, new Point(a.X, a.Y + dy), new Point(b.X, b.Y - dy), b];
        }
        var dx = (b.X - a.X) / 2;
        return [a, new Point(a.X + dx, a.Y), new Point(b.X - dx, b.Y), b];
    }

    public void SetRoute(Point[] route)
    {
        Route = route;
        Flatten();
    }

    private void Flatten()
    {
        var points = new List<Point>();
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(Route[0], false);
            if (IsCurved && Route.Length == 4)
            {
                context.CubicBezierTo(Route[1], Route[2], Route[3]);
                for (var i = 0; i <= CurveSteps; i++)
                {
                    points.Add(Bezier(Route[0], Route[1], Route[2], Route[3], i / (double)CurveSteps));
                }
            }
            else
            {
                points.Add(Route[0]);
                for (var i = 1; i < Route.Length; i++)
                {
                    context.LineTo(Route[i]);
                    points.Add(Route[i]);
                }
            }
            context.EndFigure(false);
        }
        Geometry = geometry;
        _points = points.ToArray();
        _lengths = new double[_points.Length];
        for (var i = 1; i < _points.Length; i++)
        {
            var d = _points[i] - _points[i - 1];
            _lengths[i] = _lengths[i - 1] + Math.Sqrt(d.X * d.X + d.Y * d.Y);
        }
        Length = _lengths[^1];
    }

    private static Point Bezier(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        return p0 * (u * u * u) + p1 * (3 * u * u * t) + p2 * (3 * u * t * t) + p3 * (t * t * t);
    }

    /// <summary>The point at fraction <paramref name="t"/> of the length and the unit direction of travel there.</summary>
    public (Point Point, Vector Direction) Sample(double t)
    {
        var target = Math.Clamp(t, 0, 1) * Length;
        var index = Array.BinarySearch(_lengths, target);
        if (index < 0)
        {
            index = ~index;
        }
        index = Math.Clamp(index, 1, _points.Length - 1);
        var a = _points[index - 1];
        var b = _points[index];
        var span = _lengths[index] - _lengths[index - 1];
        var f = span <= 0 ? 0 : (target - _lengths[index - 1]) / span;
        var d = b - a;
        var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        var direction = length <= 0 ? new Vector(1, 0) : d / length;
        return (a + d * f, direction);
    }
}

/// <summary>A dot travelling along an edge (pooled; a struct in a fixed array).</summary>
internal struct IllustrationPacket
{
    public bool Alive;
    public int Edge;
    public double Start;
    public double Duration;
    public bool Reverse;
    public IllustrationTone Tone;

    /// <summary>Sideways offset in px (request and response lanes on one line).</summary>
    public double Lane;

    /// <summary>Owner data handed back on arrival.</summary>
    public int Tag;

    /// <summary>Time it was dropped (fades out in place), or NaN.</summary>
    public double DroppedAt;

    /// <summary>Fraction of the way at which it drops by itself (a filtered event), or NaN.</summary>
    public double DropAt;

    /// <summary>Travels on the in-out curve (a single trip); otherwise linear (a continuous flow).</summary>
    public bool Eased;
}

/// <summary>Text placed in the scene (a readout or a caption next to a part).</summary>
internal sealed class IllustrationLabel
{
    public IllustrationLabel(Point anchor, string text, IllustrationInk ink = IllustrationInk.Muted, bool medium = false)
    {
        Anchor = anchor;
        Text = text;
        Ink = ink;
        IsMedium = medium;
    }

    public Point Anchor { get; set; }

    public string Text { get; set; }

    public IllustrationInk Ink { get; set; }

    public bool IsMedium { get; set; }

    public bool IsMono { get; set; }

    /// <summary>Horizontal alignment relative to <see cref="Anchor"/>: -1 left edge, 0 centre, 1 right edge.</summary>
    public int Align { get; set; } = -1;

    public bool IsSmall { get; set; } = true;

    public IllustrationValue Opacity = new(1);

    /// <summary>Text as last drawn.</summary>
    internal string? DrawnText;
}
