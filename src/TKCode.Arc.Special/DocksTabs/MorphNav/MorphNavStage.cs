using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Springs and timings of <see cref="MorphNav"/>, fitted to per-frame samples of Arc MorphNav（设计参考）.
/// </summary>
internal static class MorphNavMotion
{
    /// <summary>The surface growing to a larger panel: a little bounce (visualDuration 0.44, bounce 0.06).</summary>
    public static readonly SpringSpec Grow = SpringSpec.FromVisual(0.44, 0.06);

    /// <summary>The surface shrinking to a smaller panel or back to the bar: no bounce (visualDuration 0.35).</summary>
    public static readonly SpringSpec Shrink = SpringSpec.FromVisual(0.35, 0);

    /// <summary>The highlight gliding between items (visualDuration 0.3, bounce 0.05).</summary>
    public static readonly SpringSpec Glide = SpringSpec.FromVisual(0.3, 0.05);

    /// <summary>Panels sliding and dropping in, the bar and search scaling (visualDuration 0.4, no bounce).</summary>
    public static readonly SpringSpec Slide = SpringSpec.FromVisual(0.4, 0);

    /// <summary>A face fading in.</summary>
    public static readonly TimeSpan EnterFade = TimeSpan.FromMilliseconds(160);

    /// <summary>A panel starts fading in this long after the surface starts to move.</summary>
    public static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>The bar or the search bar starts fading in this long after the other started to leave.</summary>
    public static readonly TimeSpan LayerEnterDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>A panel fading out.</summary>
    public static readonly TimeSpan ExitFade = TimeSpan.FromMilliseconds(60);

    /// <summary>The bar or the search bar fading out.</summary>
    public static readonly TimeSpan LayerExitFade = TimeSpan.FromMilliseconds(90);

    /// <summary>Blur of a face at the start of its entrance and the end of its exit (px).</summary>
    public const double Blur = 4;

    /// <summary>A neighbour's panel slides in from this far on its side.</summary>
    public const double SlideIn = 20;

    /// <summary>The panel being left slides this far the other way.</summary>
    public const double SlideOut = 10;

    /// <summary>A panel opening from the bar drops in from this far above.</summary>
    public const double DropIn = 4;

    /// <summary>A closing panel lifts this far.</summary>
    public const double LiftOut = 2;

    /// <summary>The bar shrinks to this while search grows out of the surface.</summary>
    public const double BarScale = 0.96;

    /// <summary>The search bar starts from and returns to this scale.</summary>
    public const double SearchScale = 0.97;

    /// <summary>Hover intent before a panel opens.</summary>
    public static readonly TimeSpan OpenIntent = TimeSpan.FromMilliseconds(60);

    /// <summary>How long the pointer may be outside the nav before the panel closes.</summary>
    public static readonly TimeSpan CloseIntent = TimeSpan.FromMilliseconds(180);
}

/// <summary>
/// Enters and exits one layer of the surface (the bar, the search bar or a panel): opacity on a short tween, an offset or a
/// scale on a spring, and a blur that follows the opacity. Reduced motion keeps the fade only; Off jumps.
/// </summary>
/// <remarks>
/// Code-driven: a layer can be sent back the way it came in the middle of its exit (the pointer returns to a neighbour, the
/// search closes while it is still growing), and the offset and scale then keep their velocity; a ControlTheme transition
/// restarts from rest and cannot wait for an exit before the layer is removed.
/// </remarks>
internal sealed class MorphNavLayerMotion
{
    private readonly Control _layer;
    private readonly TranslateTransform? _shift;
    private readonly ScaleTransform? _scale;
    private MotionValue<double>? _opacity;
    private MotionValue<Point>? _offset;
    private MotionValue<double>? _scaleValue;
    private int _version;

    public MorphNavLayerMotion(Control layer, bool scales)
    {
        _layer = layer;
        if (scales)
        {
            _scale = new ScaleTransform();
            layer.RenderTransform = _scale;
            layer.RenderTransformOrigin = RelativePoint.Center;
        }
        else
        {
            _shift = new TranslateTransform();
            layer.RenderTransform = _shift;
        }
        IsShown = true;
    }

    /// <summary>Whether the layer was last asked to show.</summary>
    public bool IsShown { get; private set; }

    /// <summary>Current opacity (1 before any motion).</summary>
    public double Opacity => _opacity?.Value ?? 1;

    /// <summary>Current offset of a sliding layer.</summary>
    public Point Offset => _offset?.Value ?? default;

    private bool Animates => TopLevel.GetTopLevel(_layer) is not null && ArcMotion.GetEffectiveLevel(_layer) != ArcMotionLevel.Off;

    /// <summary>
    /// Shows the layer: from <paramref name="from"/> and <paramref name="fromScale"/> when it was hidden, from where it is
    /// when it was leaving. The fade starts after <paramref name="delay"/>; the offset and scale start at once.
    /// </summary>
    public void Show(Point from, double fromScale, TimeSpan delay)
    {
        var wasShown = IsShown && _opacity is null;
        _version++;
        IsShown = true;
        _layer.IsHitTestVisible = true;
        if (wasShown)
        {
            return;
        }
        Ensure();
        if (!Animates)
        {
            _opacity!.Jump(1);
            _offset!.Jump(default);
            _scaleValue!.Jump(1);
            Apply();
            return;
        }
        if (_opacity!.Value <= 0.001 && !_opacity.IsAnimating)
        {
            _offset!.Jump(from);
            _scaleValue!.Jump(fromScale);
        }
        _ = _opacity.AnimateTo(1, MotionSpec.Tween(MorphNavMotion.EnterFade, ArcEasings.Standard, delay));
        _ = _offset!.AnimateTo(default, MotionSpec.Spring(MorphNavMotion.Slide));
        _ = _scaleValue!.AnimateTo(1, MotionSpec.Spring(MorphNavMotion.Slide));
    }

    /// <summary>Puts a new layer at its hidden start without animating (before its first <see cref="Show"/>).</summary>
    public void StartHidden()
    {
        Ensure();
        IsShown = false;
        _opacity!.Jump(0);
        Apply();
    }

    /// <summary>
    /// Hides the layer toward <paramref name="to"/> and <paramref name="toScale"/>, fading over <paramref name="fade"/>.
    /// Completes with true when the layer has faded out and was not shown again meanwhile.
    /// </summary>
    public async Task<bool> Hide(Point to, double toScale, TimeSpan fade)
    {
        var version = ++_version;
        IsShown = false;
        _layer.IsHitTestVisible = false;
        Ensure();
        if (!Animates)
        {
            _opacity!.Jump(0);
            _offset!.Jump(to);
            _scaleValue!.Jump(toScale);
            Apply();
            return true;
        }
        _ = _offset!.AnimateTo(to, MotionSpec.Spring(MorphNavMotion.Slide));
        _ = _scaleValue!.AnimateTo(toScale, MotionSpec.Spring(MorphNavMotion.Slide));
        await _opacity!.AnimateTo(0, MotionSpec.Tween(fade, ArcEasings.Standard));
        return version == _version && !IsShown;
    }

    private void Ensure()
    {
        if (_opacity is not null)
        {
            return;
        }
        _opacity = new MotionValue<double>(_layer, 1, MotionVectors.Double);
        _offset = new MotionValue<Point>(_layer, default, MotionVectors.Point);
        _scaleValue = new MotionValue<double>(_layer, 1, MotionVectors.Double);
        _opacity.Changed += _ => Apply();
        _offset.Changed += _ => Apply();
        _scaleValue.Changed += _ => Apply();
    }

    private void Apply()
    {
        var opacity = Math.Clamp(_opacity?.Value ?? 1, 0, 1);
        _layer.Opacity = opacity;
        // Reduced motion drops the blur and the travel, keeping the fade.
        _layer.Effect = opacity < 0.999 && ArcMotion.IsFull(_layer) ? ArcBlur.Get(MorphNavMotion.Blur * (1 - opacity)) : null;
        if (_shift is not null && _offset is not null)
        {
            var offset = _offset.Value;
            _shift.X = offset.X;
            _shift.Y = offset.Y;
        }
        if (_scale is not null && _scaleValue is not null)
        {
            var scale = _scaleValue.Value;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
        }
    }
}

/// <summary>
/// The one surface of a <see cref="MorphNav"/>: draws its fill, hairline and shadow at the current size, centred in the
/// nav and growing downward over the page, and arranges its clip (the only child) at that rectangle. The size and corner
/// radius spring to each target the nav sets.
/// </summary>
/// <remarks>
/// Code-driven: the target depends on measured panel sizes and picks a spring by whether the surface grows or shrinks, and
/// a new target arrives while the last one is still moving (the pointer crosses several items); the spring keeps its
/// velocity then. A ControlTheme transition can neither choose its spring per change nor keep velocity.
/// </remarks>
internal sealed class MorphNavStage : Decorator
{
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<MorphNavStage, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> OpaqueSurfaceBrushProperty =
        AvaloniaProperty.Register<MorphNavStage, IBrush?>(nameof(OpaqueSurfaceBrush));

    public static readonly StyledProperty<bool> ReduceTransparencyProperty =
        AvaloniaProperty.Register<MorphNavStage, bool>(nameof(ReduceTransparency));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<MorphNavStage, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<MorphNavStage, BoxShadows>(nameof(BoxShadow));

    public static readonly StyledProperty<CornerRadius> OpenCornerRadiusProperty =
        AvaloniaProperty.Register<MorphNavStage, CornerRadius>(nameof(OpenCornerRadius), new CornerRadius(22));

    public static readonly StyledProperty<CornerRadius> ClosedCornerRadiusProperty =
        AvaloniaProperty.Register<MorphNavStage, CornerRadius>(nameof(ClosedCornerRadius), new CornerRadius(9999));

    private MotionValue<Size>? _size;
    private MotionValue<double>? _radius;
    private Size _rest;
    private double _restRadius;
    private Rect _surface;
    private Pen? _edgePen;
    private IBrush? _edgePenBrush;

    static MorphNavStage()
    {
        AffectsRender<MorphNavStage>(SurfaceBrushProperty, OpaqueSurfaceBrushProperty, ReduceTransparencyProperty, EdgeBrushProperty,
            BoxShadowProperty);
    }

    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }

    public IBrush? OpaqueSurfaceBrush { get => GetValue(OpaqueSurfaceBrushProperty); set => SetValue(OpaqueSurfaceBrushProperty, value); }

    public bool ReduceTransparency { get => GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }

    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    public CornerRadius OpenCornerRadius { get => GetValue(OpenCornerRadiusProperty); set => SetValue(OpenCornerRadiusProperty, value); }

    public CornerRadius ClosedCornerRadius { get => GetValue(ClosedCornerRadiusProperty); set => SetValue(ClosedCornerRadiusProperty, value); }

    /// <summary>The surface's current size.</summary>
    public Size CurrentSize => _size?.Value ?? _rest;

    /// <summary>The surface's current corner radius.</summary>
    public double CurrentRadius => _radius?.Value ?? _restRadius;

    /// <summary>The surface's last target size.</summary>
    public Size TargetSize => _size?.Target ?? _rest;

    /// <summary>Whether the surface is still springing to its target.</summary>
    public bool IsMorphing => _size?.IsAnimating == true || _radius?.IsAnimating == true;

    /// <summary>The surface rectangle in the stage's coordinates (centred, from the top).</summary>
    public Rect SurfaceRect => _surface;

    /// <summary>Moves the surface to <paramref name="size"/> and <paramref name="radius"/>, on <paramref name="spring"/> or at once.</summary>
    public void MoveTo(Size size, double radius, SpringSpec spring, bool animate)
    {
        if (!animate || VisualRoot is null)
        {
            _rest = size;
            _restRadius = radius;
            _size?.Jump(size);
            _radius?.Jump(radius);
            InvalidateArrange();
            InvalidateVisual();
            return;
        }
        if (_size is null)
        {
            _size = new MotionValue<Size>(this, _rest, MotionVectors.Size);
            _radius = new MotionValue<double>(this, _restRadius, MotionVectors.Double);
            _size.Changed += _ => OnMoved();
            _radius.Changed += _ => OnMoved();
        }
        _rest = size;
        _restRadius = radius;
        _ = _size.AnimateTo(size, MotionSpec.Spring(spring));
        _ = _radius!.AnimateTo(radius, MotionSpec.Spring(spring));
    }

    private void OnMoved()
    {
        InvalidateArrange();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
        {
            return default;
        }
        Child.Measure(availableSize);
        return Child.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = CurrentSize;
        _surface = new Rect((finalSize.Width - size.Width) / 2, 0, size.Width, size.Height);
        if (Child is Border clip)
        {
            clip.CornerRadius = new CornerRadius(Math.Min(CurrentRadius, Math.Min(size.Width, size.Height) / 2));
        }
        Child?.Arrange(_surface);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var rect = _surface;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var radius = Math.Min(CurrentRadius, Math.Min(rect.Width, rect.Height) / 2);
        var fill = ReduceTransparency ? OpaqueSurfaceBrush ?? SurfaceBrush : SurfaceBrush;
        context.DrawRectangle(fill, null, new RoundedRect(rect, radius), BoxShadow);
        if (EdgeBrush is { } edge)
        {
            if (_edgePen is null || !ReferenceEquals(_edgePenBrush, edge))
            {
                _edgePen = new Pen(edge, 1);
                _edgePenBrush = edge;
            }
            context.DrawRectangle(null, _edgePen, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }
}

/// <summary>
/// The layers on the surface: the bar at the top, the search bar over it, and the panels under the bar. Every layer is
/// centred on the nav; panels are measured against the nav's width less its gutter, the search bar at the search width.
/// </summary>
internal sealed class MorphNavLayers : Panel
{
    /// <summary>The bar; its height places the panels.</summary>
    public Control? Bar { get; set; }

    /// <summary>The search bar, when it was created.</summary>
    public Control? SearchBar { get; set; }

    /// <summary>The widest a panel may be.</summary>
    public double FaceMaxWidth { get; set; } = 700;

    /// <summary>The search surface's width.</summary>
    public double SearchWidth { get; set; } = 544;

    /// <summary>
    /// Raised after every measure, once the bar and the faces know their sizes: the nav points the surface at them.
    /// (A new or resized face does not change the layers' own size, so the nav's measure does not run again.)
    /// </summary>
    public event EventHandler? Measured;

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            if (ReferenceEquals(child, Bar))
            {
                child.Measure(Size.Infinity);
            }
            else if (ReferenceEquals(child, SearchBar) || child is MorphNavFace { FillsSearchWidth: true })
            {
                child.Measure(new Size(SearchWidth, double.PositiveInfinity));
            }
            else
            {
                child.Measure(new Size(FaceMaxWidth, double.PositiveInfinity));
            }
        }
        Measured?.Invoke(this, EventArgs.Empty);
        return Bar?.DesiredSize ?? default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = Bar?.DesiredSize.Height ?? 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            var size = child.DesiredSize;
            var width = ReferenceEquals(child, SearchBar) || child is MorphNavFace { FillsSearchWidth: true } ? SearchWidth : size.Width;
            var y = child is MorphNavFace ? top : 0;
            child.Arrange(new Rect((finalSize.Width - width) / 2, y, width, size.Height));
        }
        return finalSize;
    }
}
