using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The selection drop of a <see cref="LiquidTabBar"/>, drawn in one piece behind the tabs. Its two ends move on two
/// springs: the end facing the new tab is quick and the other one lags, so the drop stretches toward the new tab, thins
/// and pinches at its waist while it is long, then gathers itself and settles. A frame only moves the shape's six numbers;
/// the path is built from them by the next render (once, and only if one moved), so a frame lays nothing out and
/// raises no change notifications.
/// </summary>
/// <remarks>
/// Code-driven because the shape depends on two springs at once (the distance between the ends sets the squash and the
/// pinch), which a ControlTheme transition on a single property cannot express.
/// </remarks>
internal sealed class LiquidTabBarBlob : Control
{
    /// <summary>The drop's fill.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<LiquidTabBarBlob, IBrush?>(nameof(Fill));

    /// <summary>Corner radius of the drop at rest; it never exceeds half its height.</summary>
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<LiquidTabBarBlob, CornerRadius>(nameof(CornerRadius));

    /// <summary>How much the drop thins while stretched (share of its height).</summary>
    public static readonly StyledProperty<double> SquashProperty =
        AvaloniaProperty.Register<LiquidTabBarBlob, double>(nameof(Squash), 0.22);

    /// <summary>How deep the waist pinches while stretched (share of its height).</summary>
    public static readonly StyledProperty<double> WaistPinchProperty =
        AvaloniaProperty.Register<LiquidTabBarBlob, double>(nameof(WaistPinch), 0.2);

    // The shape: its box, corner radius and waist pinch. The path is built from them when it is next drawn.
    private double _shapeLeft, _shapeRight, _shapeTop, _shapeBottom, _shapeRadius, _shapePinch;
    private StreamGeometry? _geometry;

    private MotionValue<double>? _leftEdge;
    private MotionValue<double>? _rightEdge;
    private double _l;
    private double _r;
    private double _y;
    private double _h;
    private double _restWidth;
    private bool _placed;

    static LiquidTabBarBlob()
    {
        AffectsRender<LiquidTabBarBlob>(FillProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<LiquidTabBarBlob>(false);
    }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public double Squash { get => GetValue(SquashProperty); set => SetValue(SquashProperty, value); }

    public double WaistPinch { get => GetValue(WaistPinchProperty); set => SetValue(WaistPinchProperty, value); }

    /// <summary>The drop's current left and right edges (tests and diagnostics).</summary>
    internal (double Left, double Right) Edges => (_l, _r);

    /// <summary>How stretched the drop is right now, 0 (at rest) to 1 (fully stretched).</summary>
    internal double Stretch => StretchOf(_r - _l);

    /// <summary>Whether a placement is still moving.</summary>
    internal bool IsMoving => _leftEdge?.IsAnimating == true || _rightEdge?.IsAnimating == true;

    /// <summary>Whether the drop has been placed under a tab at least once.</summary>
    internal bool IsPlaced => _placed;

    /// <summary>
    /// Moves the drop under <paramref name="target"/>. With <paramref name="animate"/> the ends travel on springs (the
    /// trailing one slower by <paramref name="viscosity"/>, 0 to 1); otherwise it jumps.
    /// </summary>
    public void Place(Rect target, bool animate, double viscosity)
    {
        _y = target.Y;
        _h = target.Height;
        if (!_placed || !animate)
        {
            _placed = true;
            _leftEdge?.Jump(target.Left);
            _rightEdge?.Jump(target.Right);
            _l = target.Left;
            _r = target.Right;
            _restWidth = target.Width;
            Refresh();
            return;
        }
        if (_leftEdge is not null && _rightEdge is not null
            && Math.Abs(target.Left - _leftEdge.Target) < 0.01 && Math.Abs(target.Right - _rightEdge.Target) < 0.01)
        {
            return;
        }
        _leftEdge ??= CreateEdge(_l);
        _rightEdge ??= CreateEdge(_r);
        // The stretch is measured against the wider of the two tabs, so changing width alone never reads as stretch.
        _restWidth = Math.Max(_r - _l, target.Width);
        var forward = target.Center.X >= (_l + _r) / 2;
        var v = Math.Clamp(viscosity, 0, 1);
        var lead = MotionSpec.Spring(SpringSpec.FromVisual(0.3, 0.22));
        var trail = MotionSpec.Spring(SpringSpec.FromVisual(0.32 + 0.3 * v, 0.12));
        _ = _leftEdge.AnimateTo(target.Left, forward ? trail : lead);
        _ = _rightEdge.AnimateTo(target.Right, forward ? lead : trail);
    }

    /// <summary>Hides the drop (no tab is selected); the next placement jumps.</summary>
    public void Hide()
    {
        if (!_placed)
        {
            return;
        }
        _placed = false;
        _leftEdge?.Stop();
        _rightEdge?.Stop();
        InvalidateVisual();
    }

    private MotionValue<double> CreateEdge(double value)
    {
        var edge = new MotionValue<double>(this, value, MotionVectors.Double);
        edge.Changed += OnEdgeChanged;
        return edge;
    }

    private void OnEdgeChanged(double _)
    {
        _l = _leftEdge?.Value ?? _l;
        _r = _rightEdge?.Value ?? _r;
        Refresh();
    }

    private double StretchOf(double width)
    {
        var rest = Math.Max(1, _restWidth);
        return Math.Clamp((width - rest) / (rest * 0.6), 0, 1);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty || change.Property == SquashProperty || change.Property == WaistPinchProperty)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!_placed)
        {
            return;
        }
        var left = Math.Min(_l, _r);
        var right = Math.Max(_l, _r);
        var width = right - left;
        var s = StretchOf(width);
        var height = _h * (1 - Math.Clamp(Squash, 0, 0.9) * s);
        var middle = _y + _h / 2;
        var top = middle - height / 2;
        var bottom = middle + height / 2;
        var r = Math.Max(0, Math.Min(CornerRadius.TopLeft, Math.Min(height / 2, width / 2)));
        var pinch = Math.Clamp(WaistPinch, 0, 0.45) * height * s;
        var mid = (left + right) / 2;
        if (_geometry is null || left != _shapeLeft || right != _shapeRight || top != _shapeTop || bottom != _shapeBottom
            || r != _shapeRadius || pinch != _shapePinch)
        {
            _shapeLeft = left;
            _shapeRight = right;
            _shapeTop = top;
            _shapeBottom = bottom;
            _shapeRadius = r;
            _shapePinch = pinch;
            _geometry = null;
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (!_placed || Fill is not { } fill || _r - _l <= 0.5)
        {
            return;
        }
        context.DrawGeometry(fill, null, _geometry ??= BuildShape());
    }

    // A rounded bar whose top and bottom edges bend toward the waist: curve, corner, side, corner, curve, corner, side, corner.
    private StreamGeometry BuildShape()
    {
        var (left, right, top, bottom, r, pinch) = (_shapeLeft, _shapeRight, _shapeTop, _shapeBottom, _shapeRadius, _shapePinch);
        var mid = (left + right) / 2;
        var size = new Size(r, r);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.SetFillRule(FillRule.EvenOdd);
            path.BeginFigure(new Point(left + r, top), true);
            path.QuadraticBezierTo(new Point(mid, top + 2 * pinch), new Point(right - r, top));
            path.ArcTo(new Point(right, top + r), size, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point(right, bottom - r));
            path.ArcTo(new Point(right - r, bottom), size, 0, false, SweepDirection.Clockwise);
            path.QuadraticBezierTo(new Point(mid, bottom - 2 * pinch), new Point(left + r, bottom));
            path.ArcTo(new Point(left, bottom - r), size, 0, false, SweepDirection.Clockwise);
            path.LineTo(new Point(left, top + r));
            path.ArcTo(new Point(left + r, top), size, 0, false, SweepDirection.Clockwise);
            path.EndFigure(true);
        }
        return geometry;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A detached drop lands where it was going; the next placement starts from there.
        if (_leftEdge is not null && _rightEdge is not null)
        {
            _l = _leftEdge.Target;
            _r = _rightEdge.Target;
            _leftEdge.Jump(_l);
            _rightEdge.Jump(_r);
        }
    }
}
