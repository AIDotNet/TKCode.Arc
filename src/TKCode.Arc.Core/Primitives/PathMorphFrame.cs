using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A fixed point layout for morphing: every contour keeps its point count, so two frames with the same layout
/// interpolate point by point without allocating (<see cref="Interpolate"/>).
/// </summary>
public sealed class PathMorphFrame
{
    private readonly Point[][] _contours;
    private readonly bool[] _closed;

    public PathMorphFrame(IReadOnlyList<int> contourSizes, IReadOnlyList<bool> closed)
    {
        if (contourSizes.Count != closed.Count)
        {
            throw new ArgumentException("One closed flag per contour is required.", nameof(closed));
        }
        _contours = new Point[contourSizes.Count][];
        _closed = new bool[contourSizes.Count];
        for (var i = 0; i < contourSizes.Count; i++)
        {
            _contours[i] = new Point[Math.Max(1, contourSizes[i])];
            _closed[i] = closed[i];
        }
    }

    /// <summary>Samples per source segment when the frame was sampled vertex to vertex (null for uniform sampling).</summary>
    internal int[][]? Plan { get; set; }

    /// <summary>The <see cref="PathMorphShape.Signature"/> the <see cref="Plan"/> applies to.</summary>
    internal string? PlanSignature { get; set; }

    public int ContourCount => _contours.Length;

    public Span<Point> GetContour(int index) => _contours[index];

    public bool IsClosed(int index) => _closed[index];

    internal void SetClosed(int index, bool closed) => _closed[index] = closed;

    /// <summary>True when both frames have the same number of contours and points per contour.</summary>
    public bool HasSameLayout(PathMorphFrame other)
    {
        if (other._contours.Length != _contours.Length)
        {
            return false;
        }
        for (var i = 0; i < _contours.Length; i++)
        {
            if (other._contours[i].Length != _contours[i].Length)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>A copy with the same layout, points and plan.</summary>
    public PathMorphFrame Clone()
    {
        var copy = new PathMorphFrame(_contours.Select(c => c.Length).ToArray(), _closed);
        copy.CopyFrom(this);
        return copy;
    }

    /// <summary>Copies points (and plan) from a frame with the same layout.</summary>
    public void CopyFrom(PathMorphFrame source)
    {
        if (!HasSameLayout(source))
        {
            throw new ArgumentException("Frames must share a layout.", nameof(source));
        }
        for (var i = 0; i < _contours.Length; i++)
        {
            source._contours[i].CopyTo(_contours[i], 0);
            _closed[i] = source._closed[i];
        }
        Plan = source.Plan;
        PlanSignature = source.PlanSignature;
    }

    /// <summary>
    /// Writes <c>from + (to − from)·t</c> into <paramref name="destination"/>. <paramref name="t"/> may leave [0, 1]
    /// (spring overshoot extrapolates, like Motion's path interpolation).
    /// </summary>
    public static void Interpolate(PathMorphFrame from, PathMorphFrame to, double t, PathMorphFrame destination)
    {
        for (var c = 0; c < destination._contours.Length; c++)
        {
            var a = from._contours[c];
            var b = to._contours[c];
            var d = destination._contours[c];
            for (var i = 0; i < d.Length; i++)
            {
                d[i] = new Point(a[i].X + (b[i].X - a[i].X) * t, a[i].Y + (b[i].Y - a[i].Y) * t);
            }
        }
    }

    /// <summary>
    /// Progress rate for a new segment <paramref name="newFrom"/> → <paramref name="newTo"/> that best keeps the point velocities
    /// of the interrupted segment <paramref name="oldFrom"/> → <paramref name="oldTo"/> moving at <paramref name="oldRate"/> per second
    /// (least squares over all points). Reversing a morph mid-flight keeps its speed exactly.
    /// </summary>
    internal static double CarryRate(PathMorphFrame oldFrom, PathMorphFrame oldTo, double oldRate, PathMorphFrame newFrom, PathMorphFrame newTo)
    {
        if (Math.Abs(oldRate) < 1e-9 || !oldFrom.HasSameLayout(newFrom))
        {
            return 0;
        }
        double dot = 0, norm = 0;
        for (var c = 0; c < newFrom._contours.Length; c++)
        {
            var a0 = oldFrom._contours[c];
            var b0 = oldTo._contours[c];
            var a1 = newFrom._contours[c];
            var b1 = newTo._contours[c];
            for (var i = 0; i < a1.Length; i++)
            {
                var vx = (b0[i].X - a0[i].X) * oldRate;
                var vy = (b0[i].Y - a0[i].Y) * oldRate;
                var dx = b1[i].X - a1[i].X;
                var dy = b1[i].Y - a1[i].Y;
                dot += vx * dx + vy * dy;
                norm += dx * dx + dy * dy;
            }
        }
        return norm < 1e-12 ? 0 : dot / norm;
    }

    /// <summary>Builds the outline as a geometry (one figure per contour; collapsed contours are skipped).</summary>
    public Geometry ToGeometry()
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            WriteTo(context);
        }
        return geometry;
    }

    /// <summary>Writes every contour as a polyline figure.</summary>
    public void WriteTo(StreamGeometryContext context)
    {
        for (var c = 0; c < _contours.Length; c++)
        {
            var points = _contours[c];
            if (IsCollapsed(points))
            {
                continue;
            }
            var closed = _closed[c];
            context.BeginFigure(points[0], closed);
            var count = closed && points.Length > 1 ? points.Length - 1 : points.Length;
            for (var i = 1; i < count; i++)
            {
                context.LineTo(points[i]);
            }
            context.EndFigure(closed);
        }
    }

    private static bool IsCollapsed(Point[] points)
    {
        if (points.Length < 2)
        {
            return true;
        }
        var first = points[0];
        for (var i = 1; i < points.Length; i++)
        {
            if (Math.Abs(points[i].X - first.X) > 0.01 || Math.Abs(points[i].Y - first.Y) > 0.01)
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Resamples outlines into shared point layouts (P16). Outlines with the same command structure are sampled vertex to
/// vertex, so straight edges stay straight while they move (Motion's path interpolation); any other pair is sampled by
/// arc length, with closed rings rotated and, if needed, reversed to the closest correspondence. Missing contours grow
/// from (or shrink into) their own centre.
/// </summary>
public static class PathMorphSampler
{
    /// <summary>Default points per contour.</summary>
    public const int DefaultSamples = 48;

    /// <summary>Samples every contour uniformly by arc length.</summary>
    public static PathMorphFrame Sample(PathMorphShape shape, int samples = DefaultSamples)
    {
        var contours = shape.Contours;
        var frame = new PathMorphFrame(contours.Select(_ => Math.Max(2, samples)).ToArray(), contours.Select(c => c.IsClosed).ToArray());
        for (var i = 0; i < contours.Count; i++)
        {
            SampleUniform(contours[i], frame.GetContour(i));
        }
        return frame;
    }

    /// <summary>Two frames with one layout, ready for <see cref="PathMorphFrame.Interpolate"/>.</summary>
    public static (PathMorphFrame From, PathMorphFrame To) Align(PathMorphShape from, PathMorphShape to, int samples = DefaultSamples)
    {
        samples = Math.Max(2, samples);
        if (from.IsCompatibleWith(to))
        {
            var plan = new int[from.Contours.Count][];
            var sizes = new int[plan.Length];
            for (var c = 0; c < plan.Length; c++)
            {
                plan[c] = PlanSegments(from.Contours[c], to.Contours[c], samples);
                sizes[c] = 1 + plan[c].Sum();
            }
            var closed = from.Contours.Select(c => c.IsClosed).ToArray();
            var a = new PathMorphFrame(sizes, closed) { Plan = plan, PlanSignature = from.Signature };
            var b = new PathMorphFrame(sizes, closed) { Plan = plan, PlanSignature = to.Signature };
            for (var c = 0; c < plan.Length; c++)
            {
                SamplePlanned(from.Contours[c], plan[c], a.GetContour(c));
                SamplePlanned(to.Contours[c], plan[c], b.GetContour(c));
            }
            return (a, b);
        }
        var count = Math.Max(from.Contours.Count, to.Contours.Count);
        var layout = Enumerable.Repeat(samples, count).ToArray();
        var flags = new bool[count];
        for (var c = 0; c < count; c++)
        {
            var fc = c < from.Contours.Count ? from.Contours[c] : null;
            var tc = c < to.Contours.Count ? to.Contours[c] : null;
            flags[c] = (fc?.IsClosed ?? tc!.IsClosed) && (tc?.IsClosed ?? fc!.IsClosed);
        }
        var fromFrame = new PathMorphFrame(layout, flags);
        var toFrame = new PathMorphFrame(layout, flags);
        for (var c = 0; c < count; c++)
        {
            var fc = c < from.Contours.Count ? from.Contours[c] : null;
            var tc = c < to.Contours.Count ? to.Contours[c] : null;
            var fp = fromFrame.GetContour(c);
            var tp = toFrame.GetContour(c);
            if (fc is null)
            {
                SampleUniform(tc!, tp);
                fp.Fill(tc!.Centroid);
            }
            else if (tc is null)
            {
                SampleUniform(fc, fp);
                tp.Fill(fc.Centroid);
            }
            else
            {
                SampleUniform(fc, fp);
                SampleUniform(tc, tp);
                if (fc.IsClosed && tc.IsClosed)
                {
                    AlignRing(fp, tp);
                }
            }
        }
        return (fromFrame, toFrame);
    }

    /// <summary>
    /// Samples <paramref name="shape"/> into the layout of <paramref name="reference"/> (a frame on screen, possibly
    /// mid-morph), aligned to its points. Returns null when the contour counts differ.
    /// </summary>
    public static PathMorphFrame? SampleInto(PathMorphShape shape, PathMorphFrame reference)
    {
        if (shape.Contours.Count != reference.ContourCount)
        {
            return null;
        }
        var sizes = new int[reference.ContourCount];
        var closed = new bool[reference.ContourCount];
        for (var c = 0; c < sizes.Length; c++)
        {
            sizes[c] = reference.GetContour(c).Length;
            closed[c] = reference.IsClosed(c) && shape.Contours[c].IsClosed;
        }
        var frame = new PathMorphFrame(sizes, closed);
        var plan = reference.Plan;
        var planned = plan is not null && reference.PlanSignature is { } sig && PlanFits(plan, shape, sig);
        for (var c = 0; c < sizes.Length; c++)
        {
            var target = frame.GetContour(c);
            if (planned)
            {
                SamplePlanned(shape.Contours[c], plan![c], target);
            }
            else
            {
                SampleUniform(shape.Contours[c], target);
                if (closed[c])
                {
                    AlignRing(reference.GetContour(c), target);
                }
            }
        }
        if (planned)
        {
            frame.Plan = plan;
            frame.PlanSignature = shape.Signature;
        }
        return frame;
    }

    private static bool PlanFits(int[][] plan, PathMorphShape shape, string signature)
    {
        if (!string.Equals(signature, shape.Signature, StringComparison.Ordinal) || plan.Length != shape.Contours.Count)
        {
            return false;
        }
        for (var c = 0; c < plan.Length; c++)
        {
            if (plan[c].Length != shape.Contours[c].SegmentCount)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Distributes samples − 1 intervals over the segments (at least one each), by the larger relative length.</summary>
    internal static int[] PlanSegments(PathMorphContour a, PathMorphContour b, int samples)
    {
        var n = a.SegmentCount;
        var intervals = Math.Max(samples - 1, n);
        var weights = new double[n];
        double total = 0;
        for (var j = 0; j < n; j++)
        {
            var wa = a.Length > 0 ? a.SegmentLength(j) / a.Length : 1.0 / n;
            var wb = b.Length > 0 ? b.SegmentLength(j) / b.Length : 1.0 / n;
            weights[j] = Math.Max(wa, wb);
            total += weights[j];
        }
        var plan = new int[n];
        var spare = intervals - n;
        var remainders = new (double Remainder, int Index)[n];
        var used = 0;
        for (var j = 0; j < n; j++)
        {
            var share = total > 0 ? spare * weights[j] / total : (double)spare / n;
            plan[j] = 1 + (int)Math.Floor(share);
            used += plan[j] - 1;
            remainders[j] = (share - Math.Floor(share), j);
        }
        Array.Sort(remainders, (x, y) => y.Remainder.CompareTo(x.Remainder));
        for (var k = 0; used < spare; k = (k + 1) % n)
        {
            plan[remainders[k].Index]++;
            used++;
        }
        return plan;
    }

    private static void SamplePlanned(PathMorphContour contour, int[] plan, Span<Point> target)
    {
        target[0] = contour.Points[0];
        var index = 1;
        for (var j = 0; j < plan.Length; j++)
        {
            for (var k = 1; k <= plan[j]; k++)
            {
                target[index++] = contour.PointInSegment(j, (double)k / plan[j]);
            }
        }
    }

    private static void SampleUniform(PathMorphContour contour, Span<Point> target)
    {
        var n = target.Length;
        if (n == 1)
        {
            target[0] = contour.Points[0];
            return;
        }
        var length = contour.Length;
        for (var i = 0; i < n; i++)
        {
            target[i] = contour.PointAt(length * i / (n - 1));
        }
    }

    /// <summary>Rotates (and if closer, reverses) the ring <paramref name="ring"/> to match <paramref name="reference"/>.</summary>
    internal static void AlignRing(ReadOnlySpan<Point> reference, Span<Point> ring)
    {
        var m = ring.Length - 1;
        if (m < 2 || reference.Length != ring.Length)
        {
            return;
        }
        var best = double.MaxValue;
        var bestOffset = 0;
        var bestReversed = false;
        for (var pass = 0; pass < 2; pass++)
        {
            var reversed = pass == 1;
            for (var offset = 0; offset < m; offset++)
            {
                double sum = 0;
                for (var i = 0; i < m && sum < best; i++)
                {
                    var j = reversed ? ((offset - i) % m + m) % m : (offset + i) % m;
                    var dx = reference[i].X - ring[j].X;
                    var dy = reference[i].Y - ring[j].Y;
                    sum += dx * dx + dy * dy;
                }
                if (sum < best)
                {
                    best = sum;
                    bestOffset = offset;
                    bestReversed = reversed;
                }
            }
        }
        if (bestOffset == 0 && !bestReversed)
        {
            return;
        }
        Span<Point> copy = m <= 256 ? stackalloc Point[m] : new Point[m];
        ring[..m].CopyTo(copy);
        for (var i = 0; i < m; i++)
        {
            var j = bestReversed ? ((bestOffset - i) % m + m) % m : (bestOffset + i) % m;
            ring[i] = copy[j];
        }
        ring[m] = ring[0];
    }
}
