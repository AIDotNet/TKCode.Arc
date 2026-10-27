using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The size classes of the block, read from its content width (the width inside padding and border).</summary>
internal readonly record struct ScatterBreakpoints(double ContentWidth)
{
    /// <summary>Four readout cells in one row from this width; under it 2 × 2 (720).</summary>
    public const double WideReadouts = 720;

    /// <summary>Under this width the scrubber's quarter labels are reduced to the first, the Januaries and the last (560).</summary>
    public const double FullTimelineLabels = 560;

    /// <summary>Under this width: a 300px chart, transport in one column, no notes or target labels (480).</summary>
    public const double Compact = 480;

    /// <summary>Under this width the readout values are 18px (380).</summary>
    public const double Tiny = 380;

    public bool HasFourColumns => ContentWidth >= WideReadouts;

    public bool HasQuarterLabels => ContentWidth >= FullTimelineLabels;

    public bool IsCompact => ContentWidth < Compact;

    public bool IsTiny => ContentWidth < Tiny;
}

/// <summary>One label of the scrubber's month row.</summary>
internal readonly record struct ScatterTimelineLabel(int Index, string Text);

/// <summary>A pending annotation on the plot (a month label or a note) with the side it is written on.</summary>
internal readonly record struct ScatterAnnotation(int Index, string Text, ScatterLabelSide Side, bool IsLast);

/// <summary>
/// The pure parts of <see cref="ConnectedScatter"/>: axis ranges and ticks, the healthy zone and the target line in data
/// space, interpolation of the readout values, which segments get an arrow, which side labels go on, the scrubber's month
/// labels, the keyboard model and the default value formats. Everything here is measured in the design notes.
/// </summary>
internal static class ConnectedScatterEngine
{
    /// <summary>Segments whose chord is shorter than this get no arrow (measured: 22.0 skipped, 26.5 kept).</summary>
    public const double ArrowMinChord = 24;

    /// <summary>The nearest point answers the pointer within this distance (29 hits, 30 does not).</summary>
    public const double HoverRadius = 30;

    /// <summary>The last month's label moves above its point when the plot is narrower than this (334 stays beside, 314 flips).</summary>
    public const double LastLabelInsideWidth = 324;

    // Axes ---------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The range and step of an axis: the axis's own values where set (used exactly), otherwise a nice range over
    /// <paramref name="low"/> .. <paramref name="high"/> (not forced to include zero).
    /// </summary>
    public static (double Min, double Max, double Step) ResolveAxis(double min, double max, double step, double low, double high)
    {
        if (double.IsNaN(low) || double.IsInfinity(low) || double.IsNaN(high) || double.IsInfinity(high))
        {
            low = 0;
            high = 1;
        }
        var nice = AnalyticsTicks.Nice(low, high, 5, includeZero: false);
        var lo = double.IsNaN(min) ? nice.Min : min;
        var hi = double.IsNaN(max) ? nice.Max : max;
        if (!(hi > lo))
        {
            hi = lo + 1;
        }
        var s = double.IsNaN(step) || step <= 0 ? (nice.Step > 0 ? nice.Step : (hi - lo) / 4) : step;
        return (lo, hi, s);
    }

    /// <summary>The tick values min, min + step, … up to max (at most 64).</summary>
    public static double[] Ticks(double min, double max, double step)
    {
        if (!(step > 0) || !(max > min))
        {
            return [min];
        }
        var count = (int)Math.Min(64, Math.Floor((max - min) / step + 1e-9) + 1);
        var ticks = new double[count];
        for (var i = 0; i < count; i++)
        {
            ticks[i] = AnalyticsTicks.Clean(min + i * step);
        }
        return ticks;
    }

    /// <summary>
    /// The x ticks to draw at a content width: every tick, or under <see cref="ScatterBreakpoints.Compact"/> every second
    /// one without the two ends (measured $400 $800 $1,200 $1,600 from $200 … $1,800; our threshold is 480, not Arc's 440,
    /// because Arc's labels overlap between 440 and 480).
    /// </summary>
    public static int[] XTickIndexes(int tickCount, bool compact)
    {
        if (!compact || tickCount < 5)
        {
            var all = new int[tickCount];
            for (var i = 0; i < tickCount; i++)
            {
                all[i] = i;
            }
            return all;
        }
        var list = new List<int>();
        for (var i = 1; i < tickCount - 1; i += 2)
        {
            list.Add(i);
        }
        return list.ToArray();
    }

    /// <summary>The default tick text: "$2k" for steps that are multiples of 1,000, the value format otherwise.</summary>
    public static string DefaultTick(double value, double step, Func<double, string> valueFormat)
    {
        if (step >= 1000 && Math.Abs(step % 1000) < 1e-9)
        {
            return AnalyticsFormat.Compact(value, 3, "$", false, trimZeros: true);
        }
        return valueFormat(value);
    }

    // Target line and healthy zone -----------------------------------------------------------------------------------------

    /// <summary>
    /// The part of the line y = ratio · x inside the domain: its entry and exit points in data space; false when the line
    /// misses the domain. Measured: (666.7, 2000) to (1800, 5400) for a 3x line over $200–$1,800 × $2k–$6k.
    /// </summary>
    public static bool TryTargetSegment(double ratio, double xMin, double xMax, double yMin, double yMax, out Point from, out Point to)
    {
        from = default;
        to = default;
        if (!(ratio > 0) || double.IsInfinity(ratio))
        {
            return false;
        }
        var a = Math.Max(xMin, yMin / ratio);
        var b = Math.Min(xMax, yMax / ratio);
        if (!(b > a))
        {
            return false;
        }
        from = new Point(a, ratio * a);
        to = new Point(b, ratio * b);
        return true;
    }

    /// <summary>
    /// The healthy zone (y ≥ ratio · x) clipped to the domain, as a polygon in data space (3 to 5 corners; empty when the
    /// whole domain is below the line). For the measured axes it has five corners.
    /// </summary>
    public static Point[] HealthyPolygon(double ratio, double xMin, double xMax, double yMin, double yMax)
    {
        if (!(ratio > 0) || double.IsInfinity(ratio))
        {
            return [];
        }
        // Sutherland–Hodgman against the half-plane y − ratio·x ≥ 0, starting from the domain's corners (top-left first).
        Point[] corners = [new(xMin, yMax), new(xMax, yMax), new(xMax, yMin), new(xMin, yMin)];
        var result = new List<Point>(6);
        for (var i = 0; i < corners.Length; i++)
        {
            var current = corners[i];
            var next = corners[(i + 1) % corners.Length];
            var currentInside = current.Y - ratio * current.X >= 0;
            var nextInside = next.Y - ratio * next.X >= 0;
            if (currentInside)
            {
                result.Add(current);
            }
            if (currentInside != nextInside)
            {
                var fc = current.Y - ratio * current.X;
                var fn = next.Y - ratio * next.X;
                var t = fc / (fc - fn);
                result.Add(new Point(current.X + (next.X - current.X) * t, current.Y + (next.Y - current.Y) * t));
            }
        }
        return result.ToArray();
    }

    /// <summary>True when the point is on or above the target line, judged on the unrounded values.</summary>
    public static bool IsHealthy(double x, double y, double ratio) => !double.IsNaN(ratio) && y >= ratio * x - 1e-9;

    /// <summary>The ratio y / x (NaN when x is 0).</summary>
    public static double Ratio(double x, double y) => Math.Abs(x) < 1e-12 ? double.NaN : y / x;

    // Positions --------------------------------------------------------------------------------------------------------------

    /// <summary>The value at month position <paramref name="u"/>: linear between the two neighbouring months.</summary>
    public static double Interpolate(ReadOnlySpan<double> values, double u)
    {
        if (values.Length == 0)
        {
            return double.NaN;
        }
        if (values.Length == 1)
        {
            return values[0];
        }
        var clamped = Math.Clamp(double.IsNaN(u) ? 0 : u, 0, values.Length - 1);
        var i = Math.Min((int)Math.Floor(clamped), values.Length - 2);
        var t = clamped - i;
        return values[i] + (values[i + 1] - values[i]) * t;
    }

    /// <summary>The month a position belongs to (rounded half up).</summary>
    public static int MonthOf(double u, int count) => count <= 0 ? 0 : Math.Clamp((int)Math.Floor(u + 0.5), 0, count - 1);

    /// <summary>The pointer's x on the scrubber track as a month position (continuous, clamped).</summary>
    public static double PositionAt(double x, double trackLeft, double trackWidth, int count)
    {
        if (count < 2 || trackWidth <= 0)
        {
            return 0;
        }
        return Math.Clamp((x - trackLeft) / trackWidth, 0, 1) * (count - 1);
    }

    /// <summary>
    /// The scrubber's keys: ← and ↓ one month back, → and ↑ one month on, PageUp three months on and PageDown three back, Home and End the ends. A fractional position steps to the next whole month in the
    /// direction pressed (8.3 + → = 9, 8.3 − ← = 8); the ends clamp. Null for other keys.
    /// </summary>
    public static int? KeyTarget(Key key, double u, int count)
    {
        if (count <= 0)
        {
            return null;
        }
        var last = count - 1;
        var floor = Math.Floor(u + 1e-9);
        var ceil = Math.Ceiling(u - 1e-9);
        int? target = key switch
        {
            Key.Left or Key.Down => (int)floor - (Math.Abs(u - floor) < 1e-9 ? 1 : 0),
            Key.Right or Key.Up => (int)ceil + (Math.Abs(u - ceil) < 1e-9 ? 1 : 0),
            Key.PageUp => (int)ceil + (Math.Abs(u - ceil) < 1e-9 ? 3 : 2),
            Key.PageDown => (int)floor - (Math.Abs(u - floor) < 1e-9 ? 3 : 2),
            Key.Home => 0,
            Key.End => last,
            _ => null
        };
        return target is { } t ? Math.Clamp(t, 0, last) : null;
    }

    /// <summary>The index of the point nearest to (x, y) within <see cref="HoverRadius"/>, or −1.</summary>
    public static int NearestPoint(ReadOnlySpan<Point> points, Point pointer, double radius = HoverRadius)
    {
        var best = -1;
        var bestDistance = radius * radius;
        for (var i = 0; i < points.Length; i++)
        {
            var dx = points[i].X - pointer.X;
            var dy = points[i].Y - pointer.Y;
            var d = dx * dx + dy * dy;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = i;
            }
        }
        return best;
    }

    // Arrows and labels -------------------------------------------------------------------------------------------------------

    /// <summary>True when a segment of this chord length (pixels) is long enough for an arrow.</summary>
    public static bool HasArrow(double chord) => chord >= ArrowMinChord;

    /// <summary>
    /// The side the first month's label goes on: against the direction of the first segment (on the outer end of the path).
    /// A mostly horizontal segment gives left / right, a mostly vertical one top / bottom.
    /// </summary>
    public static ScatterLabelSide FirstSide(Point first, Point second)
        => Against(second.X - first.X, second.Y - first.Y);

    /// <summary>The side the last month's label goes on: the direction of the last segment; above the point in a narrow plot.</summary>
    public static ScatterLabelSide LastSide(Point beforeLast, Point last, double plotWidth)
    {
        if (plotWidth < LastLabelInsideWidth)
        {
            return ScatterLabelSide.Top;
        }
        var side = Against(last.X - beforeLast.X, last.Y - beforeLast.Y);
        return side switch
        {
            ScatterLabelSide.Left => ScatterLabelSide.Right,
            ScatterLabelSide.Right => ScatterLabelSide.Left,
            ScatterLabelSide.Top => ScatterLabelSide.Bottom,
            _ => ScatterLabelSide.Top
        };
    }

    // The side opposite to a travel vector (dx, dy).
    private static ScatterLabelSide Against(double dx, double dy)
    {
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            return dx > 0 ? ScatterLabelSide.Left : ScatterLabelSide.Right;
        }
        return dy > 0 ? ScatterLabelSide.Top : ScatterLabelSide.Bottom;
    }

    /// <summary>
    /// The scrubber's month row: the first month, every quarter's first month (Jan, Apr, Jul, Oct) and the last month; a
    /// quarter label within two months of the last one is dropped, as is anything but the first, the Januaries and the last
    /// when <paramref name="quarters"/> is false. January and the ends carry the year. Months that do not parse as
    /// "MMM yyyy" fall back to about six evenly spaced labels.
    /// </summary>
    public static List<ScatterTimelineLabel> TimelineLabels(IReadOnlyList<ScatterPoint> points, bool quarters)
    {
        var labels = new List<ScatterTimelineLabel>();
        var n = points.Count;
        if (n == 0)
        {
            return labels;
        }
        var parsed = new DateTime?[n];
        var allParsed = true;
        for (var i = 0; i < n; i++)
        {
            parsed[i] = ParseMonth(points[i].Label);
            allParsed &= parsed[i] is not null;
        }
        var last = n - 1;
        for (var i = 0; i < n; i++)
        {
            var point = points[i];
            bool show;
            bool withYear;
            string text;
            if (allParsed)
            {
                var date = parsed[i]!.Value;
                var january = date.Month == 1;
                var quarter = date.Month is 1 or 4 or 7 or 10;
                show = i == last || (i == 0 && (quarter || n < 3)) || (quarters ? quarter : january);
                if (i != last && last - i <= 2 && i != 0)
                {
                    show = false;
                }
                withYear = january || i == 0 || i == last;
                text = point.ShortLabel ?? date.ToString(withYear ? "MMM yyyy" : "MMM", AnalyticsFormat.DefaultCulture);
            }
            else
            {
                var every = Math.Max(1, (int)Math.Ceiling(n / 6.0));
                show = i == last || (i % every == 0 && last - i >= every / 2 + 1);
                text = point.ShortLabel ?? point.Label;
            }
            if (show)
            {
                labels.Add(new ScatterTimelineLabel(i, text));
            }
        }
        return labels;
    }

    private static readonly string[] s_monthFormats = ["MMM yyyy", "MMMM yyyy", "MMM ’yy", "MMM d, yyyy"];

    /// <summary>Parses "Jan 2025" style month names (English, invariant of the thread culture).</summary>
    public static DateTime? ParseMonth(string label)
    {
        return DateTime.TryParseExact(label.Trim(), s_monthFormats, AnalyticsFormat.DefaultCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // Formats ---------------------------------------------------------------------------------------------------------------

    /// <summary>The default value format: whole dollars with grouping ("$1,234").</summary>
    public static string DefaultValue(double value) => AnalyticsFormat.Currency(value);

    /// <summary>The default ratio format: one decimal and an x ("4.8x").</summary>
    public static string DefaultRatio(double ratio) => double.IsNaN(ratio) ? AnalyticsFormat.Missing : ratio.ToString("0.0", AnalyticsFormat.DefaultCulture) + "x";

    /// <summary>The signed difference text for the "since" note: U+2212 for minus, "+" for plus.</summary>
    public static string SignedDifference(double difference, Func<double, string> format)
    {
        var text = format(Math.Abs(difference));
        return (difference < 0 ? AnalyticsFormat.Minus.ToString() : "+") + text;
    }
}

/// <summary>How the head point's position moves.</summary>
internal enum ScatterTimelineMode
{
    /// <summary>At rest.</summary>
    Idle,

    /// <summary>The draw-in: a tween with an easing.</summary>
    Tween,

    /// <summary>Playback: constant speed.</summary>
    Linear,

    /// <summary>A spring to a month (key, release, tap).</summary>
    Glide,

    /// <summary>Pressed on the scrubber: a spring chasing the pointer.</summary>
    Follow,

    /// <summary>Caught up with the pointer: written 1:1.</summary>
    Direct
}

/// <summary>
/// The head point's month position <c>u</c> over time, as one pure model: the 1350ms draw-in (an easing), playback at
/// constant speed, the Smooth spring to a month (keeps its velocity when retargeted), and the press-and-drag chase that
/// switches to 1:1 once the head has caught up with the pointer.
/// </summary>
internal sealed class ConnectedScatterTimeline
{
    /// <summary>Within this many months of the pointer the chase becomes a direct write (measured switch).</summary>
    public const double CatchUp = 0.01;

    private AnalyticsSpringChannel _spring = new(0, 0.0005, 0.005);
    private double _from;
    private double _to;
    private double _duration;
    private double _elapsed;
    private double _monthSeconds = 0.24;
    private Easing _easing = ArcEasings.InOut;
    private bool _arrived;

    /// <summary>The month position now.</summary>
    public double U { get; private set; }

    /// <summary>How it moves.</summary>
    public ScatterTimelineMode Mode { get; private set; }

    /// <summary>The position it is heading to (the pointer while following).</summary>
    public double Target { get; private set; }

    /// <summary>True while a frame loop has something to advance.</summary>
    public bool IsMoving => Mode is ScatterTimelineMode.Tween or ScatterTimelineMode.Linear or ScatterTimelineMode.Glide or ScatterTimelineMode.Follow;

    /// <summary>Sets the position at once and rests.</summary>
    public void Jump(double u)
    {
        U = u;
        Target = u;
        Mode = ScatterTimelineMode.Idle;
        _spring.Jump(u);
        _arrived = false;
    }

    /// <summary>Stops where it is (a pause lands between months).</summary>
    public void Stop()
    {
        Target = U;
        Mode = ScatterTimelineMode.Idle;
        _spring.Stop();
        _arrived = false;
    }

    /// <summary>The draw-in: from <paramref name="from"/> to <paramref name="to"/> in <paramref name="seconds"/> with the easing.</summary>
    public void StartTween(double from, double to, double seconds, Easing easing)
    {
        U = from;
        _from = from;
        _to = to;
        Target = to;
        _duration = Math.Max(1e-6, seconds);
        _elapsed = 0;
        _easing = easing;
        Mode = ScatterTimelineMode.Tween;
        _arrived = false;
    }

    /// <summary>Playback from the current position to <paramref name="to"/> at one month per <paramref name="monthSeconds"/>.</summary>
    public void StartLinear(double to, double monthSeconds)
    {
        Target = to;
        _to = to;
        _monthSeconds = Math.Max(1e-6, monthSeconds);
        Mode = U < to ? ScatterTimelineMode.Linear : ScatterTimelineMode.Idle;
        _arrived = false;
    }

    /// <summary>A spring to <paramref name="target"/> (the current velocity carries over from a spring or a chase).</summary>
    public void GlideTo(double target, in SpringSpec spec)
    {
        BeginSpring();
        Target = target;
        _spring.SpringTo(target, spec);
        Mode = _spring.IsMoving ? ScatterTimelineMode.Glide : ScatterTimelineMode.Idle;
        if (Mode == ScatterTimelineMode.Idle)
        {
            U = target;
            _arrived = true;
        }
        else
        {
            _arrived = false;
        }
    }

    /// <summary>
    /// The pointer is at <paramref name="target"/> on the scrubber: a spring chases it, and once the head is within
    /// <see cref="CatchUp"/> months it follows 1:1 (later calls write the position directly).
    /// </summary>
    public void Follow(double target, in SpringSpec spec)
    {
        if (Mode == ScatterTimelineMode.Direct)
        {
            U = target;
            Target = target;
            _spring.Jump(target);
            return;
        }
        BeginSpring();
        Target = target;
        _spring.SpringTo(target, spec);
        Mode = ScatterTimelineMode.Follow;
        if (Math.Abs(U - target) < CatchUp)
        {
            Mode = ScatterTimelineMode.Direct;
            U = target;
            _spring.Jump(target);
        }
    }

    /// <summary>Ends a chase or a direct drag without moving (the caller then glides to a month).</summary>
    public void EndFollow()
    {
        if (Mode == ScatterTimelineMode.Direct)
        {
            Mode = ScatterTimelineMode.Idle;
            _spring.Jump(U);
        }
    }

    /// <summary>True once after a tween, a playback run or a glide reached its target; reading clears it.</summary>
    public bool TakeArrived()
    {
        var arrived = _arrived;
        _arrived = false;
        return arrived;
    }

    /// <summary>Advances by <paramref name="dt"/> seconds; true while still moving.</summary>
    public bool Advance(double dt)
    {
        switch (Mode)
        {
            case ScatterTimelineMode.Tween:
                _elapsed += dt;
                var t = Math.Clamp(_elapsed / _duration, 0, 1);
                U = _from + (_to - _from) * _easing.Ease(t);
                if (t >= 1)
                {
                    U = _to;
                    Mode = ScatterTimelineMode.Idle;
                    _arrived = true;
                }
                break;
            case ScatterTimelineMode.Linear:
                U = Math.Min(_to, U + dt / _monthSeconds);
                if (U >= _to - 1e-9)
                {
                    U = _to;
                    Mode = ScatterTimelineMode.Idle;
                    _arrived = true;
                }
                break;
            case ScatterTimelineMode.Glide:
                _spring.Step(dt);
                U = _spring.Value;
                if (!_spring.IsMoving)
                {
                    U = Target;
                    Mode = ScatterTimelineMode.Idle;
                    _arrived = true;
                }
                break;
            case ScatterTimelineMode.Follow:
                _spring.Step(dt);
                U = _spring.Value;
                if (Math.Abs(U - Target) < CatchUp)
                {
                    U = Target;
                    _spring.Jump(Target);
                    Mode = ScatterTimelineMode.Direct;
                }
                break;
        }
        return IsMoving;
    }

    // A spring begins at the current position; the velocity only carries over when a spring was already running.
    private void BeginSpring()
    {
        if (Mode is not (ScatterTimelineMode.Glide or ScatterTimelineMode.Follow))
        {
            _spring.Jump(U);
        }
    }
}
