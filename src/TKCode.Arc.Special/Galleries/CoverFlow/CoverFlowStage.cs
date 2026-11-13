using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The covers of a <see cref="CoverFlow"/>, all laid out once on the stage's centre line and placed every frame by a
/// perspective transform computed from one continuous position (the index at the centre). A drag moves the position with
/// the hand; letting go projects the flick's momentum to the cover it would coast to, and a spring that keeps the release
/// velocity settles there. The wheel steps a cover per notch; a trackpad scrolls sideways and snaps when it rests.
/// </summary>
/// <remarks>
/// Code-driven motion: the position is shared by every cover and handed the gesture's velocity, which a ControlTheme
/// transition cannot express. A frame writes one matrix, an opacity and a shade per visible cover; nothing is laid out.
/// </remarks>
internal sealed class CoverFlowStage : Control
{
    public static readonly StyledProperty<double> PerspectiveProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(Perspective), 900);

    public static readonly StyledProperty<double> SideSpacingProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(SideSpacing), 52);

    public static readonly StyledProperty<double> CenterGapProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(CenterGap), 0.62);

    public static readonly StyledProperty<double> SideScaleProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(SideScale), 0.86);

    public static readonly StyledProperty<double> ReflectionHeightProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(ReflectionHeight), 0.32);

    public static readonly StyledProperty<double> ReflectionGapProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(ReflectionGap), 3);

    public static readonly StyledProperty<double> ReflectionOpacityProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(ReflectionOpacity), 0.3);

    public static readonly StyledProperty<double> MaxShadeProperty =
        AvaloniaProperty.Register<CoverFlowStage, double>(nameof(MaxShade), 0.35);

    public static readonly StyledProperty<IBrush?> ShadeBrushProperty =
        AvaloniaProperty.Register<CoverFlowStage, IBrush?>(nameof(ShadeBrush));

    /// <summary>The settle after a change of cover: critically damped, 0.5s.</summary>
    internal static readonly SpringSpec SettleSpring = SpringSpec.FromVisual(0.5, 0);

    /// <summary>Momentum: the decay of a flick (0.995 per ms coasts about 0.2s).</summary>
    internal const double MomentumDecay = 0.995;

    private readonly List<CoverFlowCover> _covers = new();
    private readonly DragTracker _drag;
    private readonly WheelRouter _wheel = new() { Mode = WheelMode.Pan };
    private readonly DispatcherTimer _wheelRest;
    private double[] _snapPoints = [];
    private MotionValue<double>? _position;
    private double _dragStart;
    private int _centre = -1;
    private Size _coverSize;

    static CoverFlowStage()
    {
        AffectsMeasure<CoverFlowStage>(ReflectionHeightProperty, ReflectionGapProperty);
    }

    public CoverFlowStage()
    {
        _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Horizontal });
        _drag.Started += (_, _) => OnDragStarted();
        _drag.Moved += (_, e) => OnDragMoved(e.Offset.X);
        _drag.Completed += (_, e) => OnDragEnded(e.Velocity.X);
        _drag.Cancelled += (_, _) => OnDragEnded(0);
        _drag.Released += (_, e) => OnTap(e);
        _wheelRest = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(140) };
        _wheelRest.Tick += (_, _) => OnWheelRest();
    }

    internal CoverFlow? Owner { get; set; }

    public double Perspective { get => GetValue(PerspectiveProperty); set => SetValue(PerspectiveProperty, value); }

    public double SideSpacing { get => GetValue(SideSpacingProperty); set => SetValue(SideSpacingProperty, value); }

    public double CenterGap { get => GetValue(CenterGapProperty); set => SetValue(CenterGapProperty, value); }

    public double SideScale { get => GetValue(SideScaleProperty); set => SetValue(SideScaleProperty, value); }

    public double ReflectionHeight { get => GetValue(ReflectionHeightProperty); set => SetValue(ReflectionHeightProperty, value); }

    public double ReflectionGap { get => GetValue(ReflectionGapProperty); set => SetValue(ReflectionGapProperty, value); }

    public double ReflectionOpacity { get => GetValue(ReflectionOpacityProperty); set => SetValue(ReflectionOpacityProperty, value); }

    public double MaxShade { get => GetValue(MaxShadeProperty); set => SetValue(MaxShadeProperty, value); }

    public IBrush? ShadeBrush { get => GetValue(ShadeBrushProperty); set => SetValue(ShadeBrushProperty, value); }

    internal IReadOnlyList<CoverFlowCover> Covers => _covers;

    /// <summary>The continuous position: the index of the cover at the centre (fractional while moving).</summary>
    internal double Position => _position?.Value ?? 0;

    internal bool IsDragging => _drag.IsDragging;

    private MotionValue<double> PositionValue
    {
        get
        {
            if (_position is null)
            {
                _position = new MotionValue<double>(this, 0, MotionVectors.Double);
                _position.Changed += _ => Apply();
            }
            return _position;
        }
    }

    /// <summary>Pixels the hand travels to move one cover.</summary>
    private double Step => Math.Max(16, SideSpacing * 1.4);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PerspectiveProperty || change.Property == SideSpacingProperty || change.Property == CenterGapProperty ||
            change.Property == SideScaleProperty || change.Property == MaxShadeProperty || change.Property == ReflectionOpacityProperty)
        {
            Apply();
        }
        else if (change.Property == ShadeBrushProperty)
        {
            foreach (var cover in _covers)
            {
                cover.SetShadeBrush(change.GetNewValue<IBrush?>());
            }
        }
    }

    /// <summary>Makes one cover per picture, reusing the covers there are.</summary>
    internal void SetCovers(IReadOnlyList<CoverFlowItem> items)
    {
        while (_covers.Count > items.Count)
        {
            var last = _covers[^1];
            _covers.RemoveAt(_covers.Count - 1);
            VisualChildren.Remove(last);
            LogicalChildren.Remove(last);
        }
        while (_covers.Count < items.Count)
        {
            var cover = new CoverFlowCover { Index = _covers.Count };
            cover.SetShadeBrush(ShadeBrush);
            _covers.Add(cover);
            VisualChildren.Add(cover);
            LogicalChildren.Add(cover);
        }
        for (var i = 0; i < items.Count; i++)
        {
            _covers[i].Source = items[i].Source;
        }
        if (_snapPoints.Length != items.Count)
        {
            _snapPoints = new double[items.Count];
            for (var i = 0; i < _snapPoints.Length; i++)
            {
                _snapPoints[i] = i;
            }
        }
        _centre = -1;
        InvalidateMeasure();
        Apply();
    }

    /// <summary>Moves to <paramref name="index"/>: a spring that keeps <paramref name="velocity"/> (covers per second), or a jump.</summary>
    internal void GoTo(int index, bool animate, double velocity = 0)
    {
        if (animate && ArcMotion.IsFull(this) && TopLevel.GetTopLevel(this) is not null)
        {
            _ = MomentumProjector.SettleTo(PositionValue, index, velocity, SettleSpring);
        }
        else
        {
            PositionValue.Jump(index);
        }
    }

    // Input --------------------------------------------------------------------------------------------------------------

    private void OnDragStarted()
    {
        _wheelRest.Stop();
        _position?.Stop();
        _dragStart = Position;
    }

    private void OnDragMoved(double dx)
    {
        var last = Math.Max(0, _covers.Count - 1);
        var raw = _dragStart - dx / Step;
        // Past the first or the last cover the row resists.
        PositionValue.Jump(RubberBand.Clamp(raw, 0, last, 3));
    }

    private void OnDragEnded(double velocityX)
    {
        if (_covers.Count == 0)
        {
            return;
        }
        var velocity = -velocityX / Step;
        var target = MomentumProjector.SelectSnapPoint(_snapPoints, Position, velocity,
            SnapProjection.Decay(MomentumDecay, fromIndex: (int)Math.Round(_dragStart), flickVelocity: 3));
        Owner?.CommitFromGesture(target, velocity);
    }

    private void OnTap(DragGestureEventArgs e)
    {
        if (Owner is not { } owner || TopLevel.GetTopLevel(this) is not { } root)
        {
            return;
        }
        var point = root.TranslatePoint(e.Position, this) ?? default;
        var hit = HitCover(point);
        if (hit < 0)
        {
            return;
        }
        if (hit == owner.SelectedIndex && Math.Abs(Position - hit) < 0.25)
        {
            owner.Invoke(hit);
        }
        else
        {
            owner.Select(hit);
        }
    }

    /// <summary>The cover drawn at <paramref name="point"/> (stage coordinates), front-most first; -1 for none.</summary>
    internal int HitCover(Point point)
    {
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        foreach (var cover in _covers)
        {
            if (cover.Opacity <= 0.01 || cover.Distance >= bestDistance)
            {
                continue;
            }
            var local = point - cover.Bounds.Position;
            if (CoverFlowProjection.TryUnproject(cover.Projection, new Point(local.X, local.Y), out var inside) &&
                new Rect(cover.Bounds.Size).Contains(inside))
            {
                best = cover.Index;
                bestDistance = cover.Distance;
            }
        }
        return best;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_covers.Count == 0 || _drag.IsDragging)
        {
            return;
        }
        var device = _wheel.ClassifyBurst(e.Delta, TimeSpan.FromMilliseconds(e.Timestamp));
        if (device == WheelDevice.Notched)
        {
            // One cover per notch; Shift or a vertical wheel both step.
            var notch = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            if (notch != 0 && Owner is { } owner)
            {
                owner.Select(Math.Clamp(owner.SelectedIndex - Math.Sign(notch), 0, _covers.Count - 1));
                e.Handled = true;
            }
            return;
        }
        // A trackpad scrolls the row sideways only; vertical scrolling stays with the page.
        if (Math.Abs(e.Delta.X) <= Math.Abs(e.Delta.Y))
        {
            return;
        }
        _position?.Stop();
        var moved = Position - e.Delta.X * WheelRouter.DefaultPixelsPerWheelUnit / Step;
        PositionValue.Jump(RubberBand.Clamp(moved, 0, _covers.Count - 1, 1));
        _wheelRest.Stop();
        _wheelRest.Start();
        e.Handled = true;
    }

    private void OnWheelRest()
    {
        _wheelRest.Stop();
        if (_covers.Count > 0 && !_drag.IsDragging)
        {
            Owner?.CommitFromGesture((int)Math.Clamp(Math.Round(Position), 0, _covers.Count - 1), 0);
        }
    }

    // Layout and placement -----------------------------------------------------------------------------------------------

    /// <summary>The size of a cover for the stage's height.</summary>
    internal Size CoverSize => _coverSize;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = Owner;
        var width = owner?.CoverWidth is > 0 and var w ? w : 180;
        var ratio = owner?.CoverAspectRatio is > 0 and var r ? r : 1;
        _coverSize = new Size(width, width / ratio);
        foreach (var cover in _covers)
        {
            cover.Measure(_coverSize);
        }
        var height = _coverSize.Height * (1 + Math.Max(0, ReflectionHeight)) + Math.Max(0, ReflectionGap);
        var stageWidth = double.IsFinite(availableSize.Width) ? availableSize.Width : width * 3;
        return new Size(stageWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var box = new Rect((finalSize.Width - _coverSize.Width) / 2, 0, _coverSize.Width, _coverSize.Height);
        foreach (var cover in _covers)
        {
            cover.Arrange(box);
        }
        Apply();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // The whole stage takes the pointer, so a drag can start between covers.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    /// <summary>Places every cover for the current position.</summary>
    internal void Apply()
    {
        if (_covers.Count == 0 || _coverSize.Width <= 0)
        {
            return;
        }
        var owner = Owner;
        var p = Position;
        var tilt = owner?.TiltAngle ?? 55;
        var reflection = owner?.ShowsReflection ?? true ? ReflectionOpacity : 0;
        var centerGap = Math.Max(0, CenterGap);
        var spacing = Math.Max(1, SideSpacing);
        // Covers beyond the stage's edge are not drawn; the last ones fade out as they reach it.
        var half = Bounds.Width / 2;
        var reach = Math.Max(1, (half - centerGap * _coverSize.Width) / spacing + 1);
        for (var i = 0; i < _covers.Count; i++)
        {
            var cover = _covers[i];
            var d = i - p;
            var a = Math.Abs(d);
            cover.Distance = a;
            var opacity = a > reach + 1 ? 0 : Math.Clamp(reach + 1 - a, 0, 1);
            if (Math.Abs(cover.Opacity - opacity) > 0.002)
            {
                cover.Opacity = opacity;
            }
            if (opacity <= 0)
            {
                continue;
            }
            var (x, yaw, scale) = CoverFlowProjection.Pose(d, _coverSize.Width, centerGap, spacing, tilt, SideScale);
            var matrix = CoverFlowProjection.CoverMatrix(_coverSize.Width, _coverSize.Height, x, scale, yaw, Perspective);
            cover.Projection = matrix;
            cover.Transform.Matrix = matrix;
            cover.SetShade(Math.Min(1, a) * Math.Clamp(MaxShade, 0, 1));
            cover.SetReflection(ReflectionHeight, ReflectionGap, reflection);
            // Nearer covers draw over farther ones; the order changes only every half cover.
            var z = 10000 - (int)Math.Round(a * 2);
            if (cover.ZIndex != z)
            {
                cover.ZIndex = z;
            }
        }
        var centre = (int)Math.Clamp(Math.Round(p), 0, _covers.Count - 1);
        if (centre != _centre)
        {
            _centre = centre;
            owner?.OnCentreChanged(centre);
        }
    }
}
