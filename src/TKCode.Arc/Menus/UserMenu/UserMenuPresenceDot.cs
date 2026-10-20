using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's presence mark: one dot, three shapes, so status never rests on color alone: solid (available), a bar
/// (busy) and a ring (away). Drawn directly; the color crossfades (standard) while the bar or hole grows on the CSS
/// spring. Code-driven because the hole and bar are drawn shapes inside one glyph, not template parts a ControlTheme
/// transition could reach.
/// </summary>
internal sealed class UserMenuPresenceDot : Control
{
    public static readonly StyledProperty<UserMenuStatus> StatusProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, UserMenuStatus>(nameof(Status));

    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, double>(nameof(DotSize), 10);

    /// <summary>Color behind the dot (Arc's <c>--presence-surface</c>): fills the hole and the bar and draws the ring.</summary>
    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, double>(nameof(RingThickness));

    private static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, IBrush?>("SuccessBrush");

    private static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, IBrush?>("WarningBrush");

    private static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<UserMenuPresenceDot, IBrush?>("DangerBrush");

    private readonly SolidColorBrush _fill = new();
    private MotionValue<Color>? _color;
    private MotionValue<double>? _hole;
    private MotionValue<double>? _bar;

    static UserMenuPresenceDot()
    {
        AffectsMeasure<UserMenuPresenceDot>(DotSizeProperty, RingThicknessProperty);
        AffectsRender<UserMenuPresenceDot>(SurfaceBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<UserMenuPresenceDot>(false);
    }

    public UserMenuPresenceDot()
    {
        ArcTokens.Bind(this, SuccessBrushProperty, ArcDesignTokenKind.Success);
        ArcTokens.Bind(this, WarningBrushProperty, ArcDesignTokenKind.Warning);
        ArcTokens.Bind(this, DangerBrushProperty, ArcDesignTokenKind.Danger);
    }

    public UserMenuStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public double DotSize
    {
        get => GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    public IBrush? SurfaceBrush
    {
        get => GetValue(SurfaceBrushProperty);
        set => SetValue(SurfaceBrushProperty, value);
    }

    public double RingThickness
    {
        get => GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    private Color TargetColor => (Status switch
    {
        UserMenuStatus.Busy => GetValue(DangerBrushProperty),
        UserMenuStatus.Away => GetValue(WarningBrushProperty),
        _ => GetValue(SuccessBrushProperty)
    }) is ISolidColorBrush brush ? brush.Color : Colors.Transparent;

    private double HoleTarget => Status == UserMenuStatus.Away ? 1 : 0;

    private double BarTarget => Status == UserMenuStatus.Busy ? 1 : 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            Morph(animate: true);
        }
        else if (change.Property == SuccessBrushProperty || change.Property == WarningBrushProperty ||
                 change.Property == DangerBrushProperty)
        {
            Morph(animate: false);
        }
    }

    private void Morph(bool animate)
    {
        _color ??= Create(MotionVectors.Color, TargetColor);
        _hole ??= Create(MotionVectors.Double, HoleTarget);
        _bar ??= Create(MotionVectors.Double, BarTarget);
        if (animate && IsLoaded && ArcMotion.IsFull(this))
        {
            _ = _color.AnimateTo(TargetColor, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
            _ = _hole.AnimateTo(HoleTarget, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
            _ = _bar.AnimateTo(BarTarget, MotionSpec.Tween(ArcMotionPresets.CssSpring, ArcEasings.CssSpring));
        }
        else
        {
            _color.Jump(TargetColor);
            _hole.Jump(HoleTarget);
            _bar.Jump(BarTarget);
        }
        InvalidateVisual();
    }

    private MotionValue<T> Create<T>(IMotionVector<T> vector, T initial) where T : struct
    {
        var value = new MotionValue<T>(this, initial, vector);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = DotSize + 2 * RingThickness;
        return new Size(size, size);
    }

    public override void Render(DrawingContext context)
    {
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = DotSize / 2;
        var surface = SurfaceBrush;
        if (RingThickness > 0 && surface is not null)
        {
            context.DrawEllipse(surface, null, center, radius + RingThickness, radius + RingThickness);
        }
        _fill.Color = _color?.Value ?? TargetColor;
        context.DrawEllipse(_fill, null, center, radius, radius);
        if (surface is null)
        {
            return;
        }
        DrawMarks(context, center, DotSize, surface, _hole?.Value ?? HoleTarget, _bar?.Value ?? BarTarget);
    }

    /// <summary>Draws the hole (away) and the bar (busy) cut into a dot of <paramref name="size"/>.</summary>
    internal static void DrawMarks(DrawingContext context, Point center, double size, IBrush surface, double hole, double bar)
    {
        if (hole > 0.001)
        {
            var r = size * 0.23 * hole;
            context.DrawEllipse(surface, null, center, r, r);
        }
        if (bar > 0.001)
        {
            var width = size * 0.58 * bar;
            var height = Math.Max(size * 0.22, 1.5);
            context.DrawRectangle(surface, null, new RoundedRect(
                new Rect(center.X - width / 2, center.Y - height / 2, width, height), 1));
        }
    }
}
