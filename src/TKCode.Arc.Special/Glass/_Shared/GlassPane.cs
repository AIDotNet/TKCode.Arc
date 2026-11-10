using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The glass material under a glass control's content, in four layers: the shadow (drawn here, outside the shape only), the
/// frost (<see cref="GlassPaneFrost"/>: the blurred copy of <see cref="GlassBackdrop.SourceProperty"/>, clipped to the
/// shape), then the gloss (<see cref="GlassPaneGloss"/>: tint, top glaze, pointer sheen and edge light). It takes no input;
/// the owner forwards the pointer with <see cref="PointAt"/>. Under <see cref="ReduceTransparency"/> the frost is removed
/// and the gloss paints an opaque surface with a plain border.
/// </summary>
internal sealed class GlassPane : Control
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<GlassPane, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<bool> ReduceTransparencyProperty =
        AvaloniaProperty.Register<GlassPane, bool>(nameof(ReduceTransparency));

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<GlassPane, double>(nameof(BlurRadius), 22);

    public static readonly StyledProperty<IBrush?> TintProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(Tint));

    public static readonly StyledProperty<IBrush?> FallbackTintProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(FallbackTint));

    public static readonly StyledProperty<IBrush?> SolidFillProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(SolidFill));

    public static readonly StyledProperty<IBrush?> SolidStrokeProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(SolidStroke));

    public static readonly StyledProperty<double> EdgeWidthProperty =
        AvaloniaProperty.Register<GlassPane, double>(nameof(EdgeWidth), 1);

    public static readonly StyledProperty<IBrush?> EdgeLightProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(EdgeLight));

    public static readonly StyledProperty<IBrush?> EdgeShadeProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(EdgeShade));

    public static readonly StyledProperty<IBrush?> GlazeProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(Glaze));

    public static readonly StyledProperty<IBrush?> SheenProperty =
        AvaloniaProperty.Register<GlassPane, IBrush?>(nameof(Sheen));

    public static readonly StyledProperty<double> SheenRadiusProperty =
        AvaloniaProperty.Register<GlassPane, double>(nameof(SheenRadius), 180);

    /// <summary>Scales the pointer sheen and the edge glow, 0..1 (0 turns both off).</summary>
    public static readonly StyledProperty<double> SheenIntensityProperty =
        AvaloniaProperty.Register<GlassPane, double>(nameof(SheenIntensity), 1,
            coerce: static (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 1);

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        AvaloniaProperty.Register<GlassPane, BoxShadows>(nameof(BoxShadow));

    private readonly GlassPaneClip _clip;
    private readonly GlassPaneFrost _frost;
    private readonly GlassPaneGloss _gloss;
    private Visual? _source;
    private bool _attached;
    private bool _layoutHooked;
    private Point _sourceOffset;
    private Size _sourceSize;

    static GlassPane()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassPane>(false);
        AffectsRender<GlassPane>(BoxShadowProperty, CornerRadiusProperty, ReduceTransparencyProperty);
    }

    public GlassPane()
    {
        _frost = new GlassPaneFrost();
        _clip = new GlassPaneClip { Child = _frost };
        _gloss = new GlassPaneGloss(this);
        VisualChildren.Add(_clip);
        VisualChildren.Add(_gloss);
        // Logical children too, so the motion level and the theme reach them.
        LogicalChildren.Add(_clip);
        LogicalChildren.Add(_gloss);
        // The copy of the source is a picture: take it again when the theme repaints the source.
        ActualThemeVariantChanged += (_, _) => _frost.InvalidateVisual();
    }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public bool ReduceTransparency { get => GetValue(ReduceTransparencyProperty); set => SetValue(ReduceTransparencyProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public IBrush? Tint { get => GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public Color TintColor => GlassPaneColors.Of(Tint);
    public IBrush? FallbackTint { get => GetValue(FallbackTintProperty); set => SetValue(FallbackTintProperty, value); }
    public Color FallbackTintColor => GlassPaneColors.Of(FallbackTint);
    public IBrush? SolidFill { get => GetValue(SolidFillProperty); set => SetValue(SolidFillProperty, value); }
    public IBrush? SolidStroke { get => GetValue(SolidStrokeProperty); set => SetValue(SolidStrokeProperty, value); }
    public double EdgeWidth { get => GetValue(EdgeWidthProperty); set => SetValue(EdgeWidthProperty, value); }
    public IBrush? EdgeLight { get => GetValue(EdgeLightProperty); set => SetValue(EdgeLightProperty, value); }
    public Color EdgeLightColor => GlassPaneColors.Of(EdgeLight);
    public IBrush? EdgeShade { get => GetValue(EdgeShadeProperty); set => SetValue(EdgeShadeProperty, value); }
    public Color EdgeShadeColor => GlassPaneColors.Of(EdgeShade);
    public IBrush? Glaze { get => GetValue(GlazeProperty); set => SetValue(GlazeProperty, value); }
    public Color GlazeColor => GlassPaneColors.Of(Glaze);
    public IBrush? Sheen { get => GetValue(SheenProperty); set => SetValue(SheenProperty, value); }
    public Color SheenColor => GlassPaneColors.Of(Sheen);
    public double SheenRadius { get => GetValue(SheenRadiusProperty); set => SetValue(SheenRadiusProperty, value); }
    public double SheenIntensity { get => GetValue(SheenIntensityProperty); set => SetValue(SheenIntensityProperty, value); }
    public BoxShadows BoxShadow { get => GetValue(BoxShadowProperty); set => SetValue(BoxShadowProperty, value); }

    /// <summary>True while the blurred backdrop shows through (a usable source and transparency allowed).</summary>
    public bool IsFrosted => _frost.IsVisible;

    /// <summary>True under the ArcDesign <c>ReduceTransparency</c> token: an opaque surface.</summary>
    public bool IsSolid => ReduceTransparency;

    internal GlassPaneFrost Frost => _frost;

    internal GlassPaneGloss Gloss => _gloss;

    /// <summary>
    /// Moves the sheen toward <paramref name="position"/> (in this pane's coordinates), or fades it out when null. Only
    /// at full motion: reduced and no motion show no sheen.
    /// </summary>
    public void PointAt(Point? position) => _gloss.PointAt(position);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == GlassBackdrop.SourceProperty || property == ReduceTransparencyProperty)
        {
            UpdateSource();
        }
        else if (property == GlassBackdrop.IsLiveProperty)
        {
            _frost.SetLive(change.GetNewValue<bool>());
        }
        else if (property == BlurRadiusProperty)
        {
            _frost.SetBlur(BlurRadius);
        }
        else if (property == CornerRadiusProperty)
        {
            _clip.CornerRadius = CornerRadius;
            _gloss.InvalidateBrushes();
        }
        if (property == TintProperty || property == FallbackTintProperty || property == SolidFillProperty ||
            property == SolidStrokeProperty || property == EdgeWidthProperty || property == EdgeLightProperty ||
            property == EdgeShadeProperty || property == GlazeProperty || property == SheenProperty ||
            property == SheenRadiusProperty || property == SheenIntensityProperty || property == ReduceTransparencyProperty)
        {
            _gloss.InvalidateBrushes();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _frost.SetBlur(BlurRadius);
        _frost.SetLive(GlassBackdrop.GetIsLive(this));
        _clip.CornerRadius = CornerRadius;
        UpdateSource();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        UpdateSource();
    }

    /// <summary>
    /// Decides whether the backdrop can show through: a source that is attached to the same window and is not this pane or
    /// one of its ancestors (a copy of an ancestor would contain the pane itself).
    /// </summary>
    private void UpdateSource()
    {
        var source = GlassBackdrop.GetSource(this);
        var usable = _attached && !ReduceTransparency && source is not null && !ReferenceEquals(source, this) &&
                     !source.IsVisualAncestorOf(this) && TopLevel.GetTopLevel(source) is { } root &&
                     ReferenceEquals(root, TopLevel.GetTopLevel(this));
        _source = usable ? source : null;
        _frost.SetSource(_source);
        _frost.IsVisible = _source is not null;
        if (_source is not null && !_layoutHooked)
        {
            LayoutUpdated += OnLayoutUpdated;
            _layoutHooked = true;
        }
        else if (_source is null && _layoutHooked)
        {
            LayoutUpdated -= OnLayoutUpdated;
            _layoutHooked = false;
        }
        _gloss.InvalidateBrushes();
    }

    /// <summary>The copy is a picture taken when the frost draws: redraw it when the pane moves against the source.</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_source is not { } source)
        {
            return;
        }
        var offset = source.TranslatePoint(default, _frost) ?? default;
        var size = source.Bounds.Size;
        if (Math.Abs(offset.X - _sourceOffset.X) > 0.01 || Math.Abs(offset.Y - _sourceOffset.Y) > 0.01 || size != _sourceSize)
        {
            _sourceOffset = offset;
            _sourceSize = size;
            _frost.InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _clip.Measure(availableSize);
        _gloss.Measure(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        _clip.Arrange(rect);
        _gloss.Arrange(rect);
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var shadow = BoxShadow;
        if (shadow.Count == 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        // A box shadow is painted outside the shape only, so it does not darken the translucent glass.
        context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(new Rect(Bounds.Size), CornerRadius), shadow);
    }
}

/// <summary>Reads the color of a solid brush token (ArcDesign and control token colors resolve to brushes).</summary>
internal static class GlassPaneColors
{
    public static Color Of(IBrush? brush) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : Colors.Transparent;
}

/// <summary>Clips the frost (and its blur, which spreads past the edges) to the pane's rounded shape.</summary>
internal sealed class GlassPaneClip : Decorator
{
    private CornerRadius _cornerRadius;
    private RectangleGeometry? _geometry;

    public CornerRadius CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = value;
            UpdateClip(Bounds.Size);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        UpdateClip(finalSize);
        return size;
    }

    private void UpdateClip(Size size)
    {
        var rect = new Rect(size);
        // A rectangle geometry takes one radius; glass shapes use one radius on every corner.
        var radius = Math.Min(_cornerRadius.TopLeft, Math.Min(size.Width, size.Height) / 2);
        if (_geometry is null)
        {
            _geometry = new RectangleGeometry(rect, radius, radius);
            Clip = _geometry;
        }
        else if (_geometry.Rect != rect || _geometry.RadiusX != radius)
        {
            _geometry.Rect = rect;
            _geometry.RadiusX = radius;
            _geometry.RadiusY = radius;
            InvalidateVisual();
        }
    }
}

/// <summary>
/// The blurred backdrop: the source visual painted again with a <see cref="VisualBrush"/> where it lies under the pane, and
/// blurred by an effect on this layer. The brush records the source when this layer renders, so it is redrawn only when
/// the pane moves (<see cref="GlassPane"/> watches the layout), the theme changes, or every frame for a live source.
/// </summary>
internal sealed class GlassPaneFrost : Control
{
    // A glass surface inside the source of another one would paint its own source again while being painted; a frost
    // drawn while another frost is drawing paints nothing, so nesting can never recurse.
    [ThreadStatic]
    private static int t_depth;

    private readonly VisualBrush _brush = new() { Stretch = Stretch.Fill, TileMode = TileMode.None };
    private Visual? _source;
    private LiveLoop? _live;
    private bool _wantsLive;
    private double _blur = -1;

    static GlassPaneFrost()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassPaneFrost>(false);
    }

    /// <summary>The source the frost copies, or null.</summary>
    public Visual? Source => _source;

    /// <summary>How many times the frost painted the source (tests read it).</summary>
    internal int Paints { get; private set; }

    public void SetSource(Visual? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }
        _source = source;
        _brush.Visual = source;
        UpdateLive();
        InvalidateVisual();
    }

    public void SetLive(bool live)
    {
        _wantsLive = live;
        UpdateLive();
    }

    public void SetBlur(double radius)
    {
        radius = double.IsFinite(radius) ? Math.Max(0, radius) : 0;
        if (Math.Abs(radius - _blur) < 0.01)
        {
            return;
        }
        _blur = radius;
        Effect = radius > 0 ? new ImmutableBlurEffect(radius) : null;
    }

    private void UpdateLive()
    {
        if (_wantsLive && _source is not null)
        {
            _live ??= new LiveLoop(this, _ =>
            {
                InvalidateVisual();
                return true;
            })
            {
                // Mirroring the source is not decoration: it keeps running at every motion level.
                MinimumLevel = ArcMotionLevel.Off
            };
            _live.Start();
        }
        else
        {
            _live?.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_source is not { } source || t_depth > 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        if (source.TranslatePoint(default, this) is not { } offset)
        {
            return;
        }
        var size = source.Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        t_depth++;
        try
        {
            // Only the part under the pane plus the blur's reach matters; the clip keeps the blur's input small.
            var reach = Math.Max(2, _blur * 2);
            using (context.PushClip(new Rect(Bounds.Size).Inflate(reach)))
            {
                context.FillRectangle(_brush, new Rect(offset, size));
            }
            Paints++;
        }
        finally
        {
            t_depth--;
        }
    }
}

/// <summary>
/// The surface of the glass: the tint, a glaze fading down from the top edge, the sheen that follows the pointer, the
/// 1px edge light (bright at the top left, a faint shade at the bottom right) and, near the pointer, a brighter stretch of
/// that edge. Under reduced transparency: the opaque fill and a plain border only.
/// </summary>
internal sealed class GlassPaneGloss : Control
{
    private static readonly SpringSpec s_follow = SpringSpec.FromVisual(0.35, 0);

    private readonly GlassPane _pane;
    private MotionValue<Point>? _position;
    private MotionValue<double>? _alpha;
    private bool _brushesValid;
    private IImmutableBrush? _tint;
    private IImmutableBrush? _glaze;
    private IImmutableBrush? _sheen;
    private IImmutableBrush? _glowMask;
    private ImmutablePen? _edgePen;
    private ImmutablePen? _glowPen;
    private ImmutablePen? _solidPen;

    static GlassPaneGloss()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<GlassPaneGloss>(false);
    }

    public GlassPaneGloss(GlassPane pane)
    {
        _pane = pane;
    }

    /// <summary>The sheen's current strength, 0..1 (tests read it).</summary>
    internal double SheenAlpha => _alpha?.Value ?? 0;

    /// <summary>The sheen's current center (tests read it).</summary>
    internal Point SheenPosition => _position?.Value ?? default;

    public void InvalidateBrushes()
    {
        _brushesValid = false;
        InvalidateVisual();
    }

    public void PointAt(Point? position)
    {
        if (position is { } target && _pane.SheenIntensity > 0 && !_pane.IsSolid && ArcMotion.IsFull(this))
        {
            if (_position is null || _alpha is null)
            {
                _position = new MotionValue<Point>(this, target, MotionVectors.Point);
                _alpha = new MotionValue<double>(this, 0, MotionVectors.Double);
                _position.Changed += _ => InvalidateVisual();
                _alpha.Changed += _ => InvalidateVisual();
            }
            if (_alpha.Value <= 0.001)
            {
                // Appearing: start where the pointer is, then fade in.
                _position.Jump(target);
            }
            else
            {
                _ = _position.AnimateTo(target, MotionSpec.Spring(s_follow));
            }
            if (_alpha.Target < 1)
            {
                _ = _alpha.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            }
            return;
        }
        if (_alpha is null)
        {
            return;
        }
        if (position is null && ArcMotion.IsFull(this))
        {
            _ = _alpha.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
        }
        else
        {
            _alpha.Jump(0);
        }
    }

    private void EnsureBrushes()
    {
        if (_brushesValid)
        {
            return;
        }
        _brushesValid = true;
        var pane = _pane;
        var edge = Math.Max(0, pane.EdgeWidth);
        _solidPen = pane.SolidStroke is { } stroke && edge > 0 ? new ImmutablePen(stroke.ToImmutable(), edge) : null;
        _tint = new ImmutableSolidColorBrush(pane.IsFrosted ? pane.TintColor : pane.FallbackTintColor);
        _glaze = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(pane.GlazeColor, 0), new GradientStop(Transparent(pane.GlazeColor), 1) }
        }.ToImmutable();
        _sheen = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(pane.SheenColor, 0),
                new GradientStop(Fade(pane.SheenColor, 0.4), 0.45),
                new GradientStop(Transparent(pane.SheenColor), 1)
            }
        }.ToImmutable();
        _glowMask = new RadialGradientBrush
        {
            GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Transparent, 1) }
        }.ToImmutable();
        var light = pane.EdgeLightColor;
        _edgePen = edge > 0
            ? new ImmutablePen(new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(light, 0),
                    new GradientStop(Fade(light, 0.35), 0.45),
                    new GradientStop(pane.EdgeShadeColor, 1)
                }
            }.ToImmutable(), edge)
            : null;
        _glowPen = edge > 0 ? new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(255, light.R, light.G, light.B)), edge * 1.5) : null;
    }

    private static Color Transparent(Color color) => Color.FromArgb(0, color.R, color.G, color.B);

    private static Color Fade(Color color, double factor) =>
        Color.FromArgb((byte)Math.Round(color.A * factor), color.R, color.G, color.B);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var pane = _pane;
        var radius = pane.CornerRadius;
        var shape = new RoundedRect(bounds, radius);
        var edge = Math.Max(0, pane.EdgeWidth);
        var inner = new RoundedRect(bounds.Deflate(edge / 2), Deflate(radius, edge / 2));
        if (pane.IsSolid)
        {
            context.DrawRectangle(pane.SolidFill, null, shape);
            if (_solidPen is not null)
            {
                context.DrawRectangle(null, _solidPen, inner);
            }
            return;
        }

        context.DrawRectangle(_tint, null, shape);
        var alpha = (_alpha?.Value ?? 0) * pane.SheenIntensity;
        var sheenRadius = Math.Max(1, pane.SheenRadius);
        var center = _position?.Value ?? default;
        using (context.PushClip(shape))
        {
            context.FillRectangle(_glaze!, new Rect(0, 0, bounds.Width, Math.Min(bounds.Height, Math.Max(24, bounds.Height * 0.6))));
            if (alpha > 0.002)
            {
                using (context.PushOpacity(alpha))
                {
                    context.DrawEllipse(_sheen, null, center, sheenRadius, sheenRadius);
                }
            }
        }
        if (_edgePen is not null)
        {
            context.DrawRectangle(null, _edgePen, inner);
        }
        if (alpha > 0.002 && _glowPen is not null)
        {
            // The edge brightens where the light is: the plain edge masked by a soft disc around the sheen.
            var reach = sheenRadius * 0.8;
            using (context.PushOpacityMask(_glowMask!, new Rect(center.X - reach, center.Y - reach, reach * 2, reach * 2)))
            using (context.PushOpacity(alpha * 0.8))
            {
                context.DrawRectangle(null, _glowPen, inner);
            }
        }
    }

    private static CornerRadius Deflate(CornerRadius radius, double by) => new(
        Math.Max(0, radius.TopLeft - by), Math.Max(0, radius.TopRight - by),
        Math.Max(0, radius.BottomRight - by), Math.Max(0, radius.BottomLeft - by));
}
