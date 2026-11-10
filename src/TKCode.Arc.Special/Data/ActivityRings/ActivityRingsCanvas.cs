using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The drawing of one ring of an <see cref="ActivityRings"/>: its spring, its cached circle and arc, and its pens. The
/// arc is updated in place whenever the spring moves, so a frame allocates nothing.
/// </summary>
internal sealed class ActivityRingsTrack
{
    public ActivityRingsTrack(ActivityRing ring)
    {
        Ring = ring;
        Figure.Segments!.Add(Segment);
        Arc.Figures!.Add(Figure);
    }

    public ActivityRing Ring { get; }

    public MotionValue<double>? Spring { get; set; }

    /// <summary>The progress drawn now (1 = one full ring).</summary>
    public double Shown { get; set; }

    /// <summary>The progress the ring is heading for.</summary>
    public double Target { get; set; }

    public IBrush? Brush { get; set; }

    public Pen? ArcPen { get; set; }

    public Pen? CirclePen { get; set; }

    public Pen? TrackPen { get; set; }

    public Point Center { get; set; }

    public double Radius { get; set; }

    public EllipseGeometry Circle { get; } = new();

    public PathGeometry Arc { get; } = new();

    public PathFigure Figure { get; } = new() { IsClosed = false, IsFilled = false };

    public ArcSegment Segment { get; } = new() { SweepDirection = SweepDirection.Clockwise };

    /// <summary>The fraction of a lap the arc geometry was last built for (NaN: never).</summary>
    public double BuiltFraction { get; set; } = double.NaN;

    public void ResetPens()
    {
        ArcPen = null;
        CirclePen = null;
        TrackPen = null;
    }
}

/// <summary>
/// Draws the concentric rings of an <see cref="ActivityRings"/> in one control: outer ring first, each with a faint track,
/// an arc from 12 o'clock and, past the goal, a full ring with the overlapping lap on top and a shade under its head.
/// </summary>
internal sealed class ActivityRingsCanvas : Control
{
    private const double MinVisible = 0.0005;

    private ActivityRings? _owner;
    private Size _laidOut;

    static ActivityRingsCanvas()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ActivityRingsCanvas>(false);
    }

    internal ActivityRings? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var diameter = _owner?.Diameter ?? 0;
        var side = Math.Min(diameter, Math.Min(availableSize.Width, availableSize.Height));
        return double.IsFinite(side) && side > 0 ? new Size(side, side) : new Size(diameter, diameter);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Relayout();
    }

    /// <summary>Places every ring for the current size, thickness and gap, and rebuilds their arcs.</summary>
    internal void Relayout()
    {
        if (_owner is null)
        {
            return;
        }
        var size = Bounds.Size;
        _laidOut = size;
        var thickness = _owner.RingThickness;
        var gap = _owner.RingGap;
        var center = new Point(size.Width / 2, size.Height / 2);
        var outer = Math.Min(size.Width, size.Height) / 2 - thickness / 2;
        var tracks = _owner.Tracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var radius = outer - i * (thickness + gap);
            track.Center = center;
            track.Radius = radius;
            track.Circle.Center = center;
            track.Circle.RadiusX = Math.Max(0, radius);
            track.Circle.RadiusY = Math.Max(0, radius);
            track.BuiltFraction = double.NaN;
            BuildArc(track);
        }
        InvalidateVisual();
    }

    /// <summary>Moves the end of a ring's arc to its shown progress (never during the render pass).</summary>
    internal void BuildArc(ActivityRingsTrack track)
    {
        var fraction = Fraction(track.Shown);
        if (fraction.Equals(track.BuiltFraction) || track.Radius <= 0)
        {
            return;
        }
        track.BuiltFraction = fraction;
        if (fraction <= MinVisible || fraction >= 1 - MinVisible)
        {
            return;
        }
        var radius = track.Radius;
        var center = track.Center;
        var to = (-90 + fraction * 360) * Math.PI / 180;
        track.Figure.StartPoint = new Point(center.X, center.Y - radius);
        track.Segment.Point = new Point(center.X + radius * Math.Cos(to), center.Y + radius * Math.Sin(to));
        track.Segment.Size = new Size(radius, radius);
        track.Segment.IsLargeArc = fraction > 0.5;
    }

    /// <summary>The part of the current lap a progress has covered: 1.3 is 0.3, and exactly 2 is a full lap.</summary>
    internal static double Fraction(double progress)
    {
        if (progress <= 0)
        {
            return 0;
        }
        var fraction = progress - Math.Floor(progress);
        return fraction < MinVisible && progress >= 1 ? 1 : fraction;
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is not { } owner || _laidOut != Bounds.Size)
        {
            return;
        }
        var thickness = owner.RingThickness;
        var trackOpacity = owner.TrackOpacity;
        var shadow = owner.OverlapShadow;
        var tracks = owner.Tracks;
        for (var i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            if (track.Radius <= thickness / 2 || track.Brush is not { } brush)
            {
                continue;
            }
            track.TrackPen ??= new Pen(brush, thickness);
            using (context.PushOpacity(trackOpacity))
            {
                context.DrawGeometry(null, track.TrackPen, track.Circle);
            }
            var progress = track.Shown;
            if (progress <= MinVisible)
            {
                continue;
            }
            track.ArcPen ??= new Pen(brush, thickness, lineCap: PenLineCap.Round);
            var fraction = Fraction(progress);
            if (progress >= 1 - MinVisible)
            {
                // One full lap under whatever comes next.
                track.CirclePen ??= new Pen(brush, thickness);
                context.DrawGeometry(null, track.CirclePen, track.Circle);
                if (progress < 1 + MinVisible || fraction >= 1)
                {
                    continue;
                }
                // The head of the lap on top casts a shade a little ahead of itself onto the lap below.
                var angle = (-90 + fraction * 360) * Math.PI / 180;
                var lead = thickness * 0.14;
                var head = new Point(
                    track.Center.X + track.Radius * Math.Cos(angle) - Math.Sin(angle) * lead,
                    track.Center.Y + track.Radius * Math.Sin(angle) + Math.Cos(angle) * lead);
                if (shadow is not null)
                {
                    var r = thickness / 2 + 0.5;
                    context.DrawEllipse(shadow, null, head, r, r);
                }
            }
            if (fraction <= MinVisible)
            {
                // A sliver too small for an arc still shows as a dot at 12 o'clock.
                var r = thickness / 2;
                context.DrawEllipse(brush, null, new Point(track.Center.X, track.Center.Y - track.Radius), r, r);
                continue;
            }
            context.DrawGeometry(null, track.ArcPen, track.Arc);
        }
    }
}
