using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The circular track and indicator arc of a <see cref="ProgressRing"/>, drawn directly. In determinate mode the arc
/// starts at 12 o'clock and sweeps clockwise to <see cref="Fraction"/> (0–1), crossfading to
/// <see cref="CompleteBrush"/> after a 200 ms delay at 100%. In indeterminate mode a Core <see cref="LiveLoop"/>
/// rotates a smooth comet arc (full motion), pulses opacity on a resting arc (reduced motion), or rests cleanly (off).
/// Geometries and pens are cached so frames allocate nothing.
/// </summary>
internal sealed class ProgressRingTrack : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ProgressRingTrack, double>(nameof(Fraction));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressRingTrack, bool>(nameof(IsIndeterminate));

    public static readonly StyledProperty<bool> IsCompleteProperty =
        AvaloniaProperty.Register<ProgressRingTrack, bool>(nameof(IsComplete));

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ProgressRingTrack, double>(nameof(Thickness), 3);

    public static readonly StyledProperty<double> DefaultDiameterProperty =
        AvaloniaProperty.Register<ProgressRingTrack, double>(nameof(DefaultDiameter), 40);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<ProgressRingTrack, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> IndicatorBrushProperty =
        AvaloniaProperty.Register<ProgressRingTrack, IBrush?>(nameof(IndicatorBrush));

    public static readonly StyledProperty<IBrush?> CompleteBrushProperty =
        AvaloniaProperty.Register<ProgressRingTrack, IBrush?>(nameof(CompleteBrush));

    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public bool IsIndeterminate { get => GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public bool IsComplete { get => GetValue(IsCompleteProperty); set => SetValue(IsCompleteProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public double DefaultDiameter { get => GetValue(DefaultDiameterProperty); set => SetValue(DefaultDiameterProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? IndicatorBrush { get => GetValue(IndicatorBrushProperty); set => SetValue(IndicatorBrushProperty, value); }
    public IBrush? CompleteBrush { get => GetValue(CompleteBrushProperty); set => SetValue(CompleteBrushProperty, value); }

    /// <summary>One full base rotation in seconds during indeterminate mode.</summary>
    private const double TurnSeconds = 1.1;

    /// <summary>Sweep breathing period in seconds during indeterminate mode.</summary>
    private const double SweepPeriodSeconds = 1.65;

    private const double MinIndeterminateSweep = 0.22;
    private const double MaxIndeterminateSweep = 0.72;
    private const double RestingIndeterminateSweep = 0.75;

    private readonly LiveLoop _loop;
    private MotionValue<double>? _done;
    private double _phase;
    private double _pulse;
    private bool _full = true;

    private Pen? _trackPen;
    private Pen? _indicatorPen;
    private Pen? _completePen;

    private readonly EllipseGeometry _circle = new();
    private readonly PathGeometry _arc = new();
    private readonly PathFigure _figure = new() { IsClosed = false, IsFilled = false };
    private readonly ArcSegment _segment = new() { SweepDirection = SweepDirection.Clockwise };
    private Size _circleSize;
    private double _circleThickness = double.NaN;
    private double _arcStart = double.NaN;
    private double _arcSweep = double.NaN;

    static ProgressRingTrack()
    {
        AffectsMeasure<ProgressRingTrack>(DefaultDiameterProperty);
        AffectsRender<ProgressRingTrack>(FractionProperty, IsIndeterminateProperty, ThicknessProperty, TrackBrushProperty, IndicatorBrushProperty, CompleteBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ProgressRingTrack>(false);
    }

    public ProgressRingTrack()
    {
        _figure.Segments!.Add(_segment);
        _arc.Figures!.Add(_figure);
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += OnRunningChanged;
        ResourcesChanged += (_, _) => RefreshLevel();
    }

    /// <summary>0 = accent arc, 1 = success arc.</summary>
    internal double Doneness => _done?.Value ?? (IsComplete && !IsIndeterminate ? 1 : 0);

    /// <summary>True while the indeterminate ring is turning (full motion, attached, visible, in view).</summary>
    internal bool IsSpinning => _loop.IsRunning && _full;

    /// <summary>True while the resting indeterminate ring breathes in opacity (reduced motion).</summary>
    internal bool IsPulsing => _loop.IsRunning && !_full;

    /// <summary>Current rotation angle in degrees during indeterminate mode.</summary>
    internal double IndeterminateAngle => (_phase / TurnSeconds * 360.0) % 360.0;

    /// <summary>Current sweep fraction (0–1) during indeterminate mode.</summary>
    internal double IndeterminateSweep => IsSpinning
        ? MinIndeterminateSweep + (MaxIndeterminateSweep - MinIndeterminateSweep) * (0.5 - 0.5 * Math.Cos(_phase / SweepPeriodSeconds * Math.PI * 2.0))
        : RestingIndeterminateSweep;

    /// <summary>Opacity multiplier of the indeterminate arc (pulses under reduced motion).</summary>
    internal double PulseOpacity => IsPulsing ? ArcBusyPulse.Opacity(_pulse) : 1.0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsIndeterminateProperty)
        {
            if (IsIndeterminate)
            {
                RefreshLevel();
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
            UpdateArcGeometry();
        }
        else if (change.Property == FractionProperty)
        {
            if (!IsIndeterminate)
            {
                UpdateArcGeometry();
            }
        }
        else if (change.Property == IsCompleteProperty)
        {
            var target = IsComplete && !IsIndeterminate ? 1.0 : 0.0;
            _done ??= CreateDoneValue(1 - target);
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                var delay = target > 0.5 ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero;
                _ = _done.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, delay));
            }
            else
            {
                _done.Jump(target);
            }
        }
        else if (change.Property == ThicknessProperty)
        {
            _trackPen = null;
            _indicatorPen = null;
            _completePen = null;
            UpdateCircleAndArc();
        }
        else if (change.Property == TrackBrushProperty)
        {
            _trackPen = null;
        }
        else if (change.Property == IndicatorBrushProperty)
        {
            _indicatorPen = null;
        }
        else if (change.Property == CompleteBrushProperty)
        {
            _completePen = null;
        }
        else if (change.Property == ArcMotion.LevelProperty || change.Property == ArcMotion.TokenLevelProperty)
        {
            RefreshLevel();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCircleAndArc();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var diameter = Math.Max(0, DefaultDiameter);
        var width = double.IsInfinity(availableSize.Width) ? diameter : Math.Min(availableSize.Width, diameter);
        var height = double.IsInfinity(availableSize.Height) ? diameter : Math.Min(availableSize.Height, diameter);
        var side = Math.Max(0, Math.Min(width, height));
        return new Size(side, side);
    }

    private MotionValue<double> CreateDoneValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private bool Step(double seconds)
    {
        if (!IsIndeterminate)
        {
            return false;
        }
        if (_full)
        {
            _phase += seconds;
            UpdateArcGeometry();
        }
        else
        {
            _pulse = ArcBusyPulse.Advance(_pulse, seconds);
        }
        InvalidateVisual();
        return true;
    }

    private void OnRunningChanged(bool running)
    {
        if (running)
        {
            RefreshLevel();
        }
        UpdateArcGeometry();
        InvalidateVisual();
    }

    private void RefreshLevel()
    {
        var full = ArcMotion.IsFull(this);
        if (full == _full)
        {
            return;
        }
        _full = full;
        UpdateArcGeometry();
        InvalidateVisual();
    }

    private void UpdateCircleAndArc()
    {
        var size = Bounds.Size;
        var thickness = Math.Max(0.5, Thickness);
        var side = Math.Min(size.Width, size.Height);
        if (side <= thickness)
        {
            return;
        }
        var radius = (side - thickness) / 2.0;
        var center = new Point(size.Width / 2.0, size.Height / 2.0);
        if (_circleSize != size || !_circleThickness.Equals(thickness))
        {
            _circle.Center = center;
            _circle.RadiusX = radius;
            _circle.RadiusY = radius;
            _circleSize = size;
            _circleThickness = thickness;
            _arcStart = double.NaN;
            _arcSweep = double.NaN;
        }
        UpdateArcGeometry();
    }

    private void UpdateArcGeometry()
    {
        var size = Bounds.Size;
        var thickness = Math.Max(0.5, Thickness);
        var side = Math.Min(size.Width, size.Height);
        if (side <= thickness)
        {
            return;
        }
        double startDegrees;
        double sweepFraction;
        if (IsIndeterminate)
        {
            startDegrees = -90.0 + IndeterminateAngle;
            sweepFraction = IndeterminateSweep;
        }
        else
        {
            startDegrees = -90.0;
            sweepFraction = Math.Clamp(Fraction, 0.0, 1.0);
        }
        if (sweepFraction <= 0.0001 || sweepFraction >= 0.9999)
        {
            return;
        }
        if (_arcStart.Equals(startDegrees) && _arcSweep.Equals(sweepFraction) && _circleSize == size && _circleThickness.Equals(thickness))
        {
            return;
        }
        var radius = (side - thickness) / 2.0;
        var center = new Point(size.Width / 2.0, size.Height / 2.0);
        var from = startDegrees * Math.PI / 180.0;
        var to = (startDegrees + sweepFraction * 360.0) * Math.PI / 180.0;
        _figure.StartPoint = new Point(center.X + radius * Math.Cos(from), center.Y + radius * Math.Sin(from));
        _segment.Point = new Point(center.X + radius * Math.Cos(to), center.Y + radius * Math.Sin(to));
        _segment.Size = new Size(radius, radius);
        _segment.IsLargeArc = sweepFraction > 0.5;
        _arcStart = startDegrees;
        _arcSweep = sweepFraction;
    }

    public override void Render(DrawingContext context)
    {
        var thickness = Math.Max(0.5, Thickness);
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= thickness)
        {
            return;
        }
        if (_circleSize != Bounds.Size || !_circleThickness.Equals(thickness))
        {
            return;
        }
        if (TrackBrush is { } trackBrush)
        {
            _trackPen ??= new Pen(trackBrush, thickness);
            context.DrawGeometry(null, _trackPen, _circle);
        }

        if (IsIndeterminate)
        {
            if (IndicatorBrush is not { } indicatorBrush)
            {
                return;
            }
            _indicatorPen ??= new Pen(indicatorBrush, thickness, lineCap: PenLineCap.Round);
            var opacity = PulseOpacity;
            using (opacity < 1.0 ? context.PushOpacity(opacity) : default(DrawingContext.PushedState?))
            {
                context.DrawGeometry(null, _indicatorPen, _arc);
            }
            return;
        }

        var fraction = Math.Clamp(Fraction, 0.0, 1.0);
        if (fraction <= 0.0001)
        {
            return;
        }
        Geometry geometry = fraction >= 0.9999 ? _circle : _arc;
        var done = Math.Clamp(Doneness, 0.0, 1.0);
        if (done < 1.0 && IndicatorBrush is { } activeBrush)
        {
            _indicatorPen ??= new Pen(activeBrush, thickness, lineCap: PenLineCap.Round);
            context.DrawGeometry(null, _indicatorPen, geometry);
        }
        if (done > 0.0 && CompleteBrush is { } completeBrush)
        {
            _completePen ??= new Pen(completeBrush, thickness, lineCap: PenLineCap.Round);
            using (context.PushOpacity(done))
            {
                context.DrawGeometry(null, _completePen, geometry);
            }
        }
    }
}
