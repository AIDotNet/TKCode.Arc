using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The card network mark of <see cref="CreditTopUp"/>: a 34×22 white plate (radius 4.5, warm grey outline, white in
/// both themes, as the reference) carrying one of our fictional marks, drawn with stream geometry: Nimbus (a 14×8
/// capsule with three slanted stripes), Orbit (a 10px disc crossed by a ring tilted −20°) or a plain card glyph.
/// </summary>
internal sealed class CreditTopUpCardMark : Control
{
    public static readonly StyledProperty<CreditTopUpCardBrand> BrandProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, CreditTopUpCardBrand>(nameof(Brand));

    public static readonly StyledProperty<IBrush?> PlateBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(PlateBrush));

    public static readonly StyledProperty<IBrush?> OutlineBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(OutlineBrush));

    public static readonly StyledProperty<IBrush?> NimbusBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(NimbusBrush));

    public static readonly StyledProperty<IBrush?> OrbitDiscBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(OrbitDiscBrush));

    public static readonly StyledProperty<IBrush?> OrbitRingBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(OrbitRingBrush));

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<CreditTopUpCardMark, IBrush?>(nameof(GlyphBrush));

    public CreditTopUpCardBrand Brand { get => GetValue(BrandProperty); set => SetValue(BrandProperty, value); }
    public IBrush? PlateBrush { get => GetValue(PlateBrushProperty); set => SetValue(PlateBrushProperty, value); }
    public IBrush? OutlineBrush { get => GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }
    public IBrush? NimbusBrush { get => GetValue(NimbusBrushProperty); set => SetValue(NimbusBrushProperty, value); }
    public IBrush? OrbitDiscBrush { get => GetValue(OrbitDiscBrushProperty); set => SetValue(OrbitDiscBrushProperty, value); }
    public IBrush? OrbitRingBrush { get => GetValue(OrbitRingBrushProperty); set => SetValue(OrbitRingBrushProperty, value); }
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }

    private static readonly Geometry s_capsule = Build(ctx =>
    {
        // 14×8 capsule centered on (0, 0).
        ctx.BeginFigure(new Point(-3, -4), true);
        ctx.LineTo(new Point(3, -4));
        ctx.ArcTo(new Point(3, 4), new Size(4, 4), 0, false, SweepDirection.Clockwise);
        ctx.LineTo(new Point(-3, 4));
        ctx.ArcTo(new Point(-3, -4), new Size(4, 4), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(true);
    });

    private static readonly Geometry s_stripes = Build(ctx =>
    {
        for (var i = -1; i <= 1; i++)
        {
            var x = i * 3.6;
            ctx.BeginFigure(new Point(x - 1.6, 3.2), false);
            ctx.LineTo(new Point(x + 1.6, -3.2));
            ctx.EndFigure(false);
        }
    });

    private static readonly Geometry s_card = ArcIcons.CreditCard;

    private IPen? _outlinePen;
    private IPen? _nimbusPen;
    private IPen? _stripePen;
    private IPen? _ringPen;
    private IPen? _glyphPen;

    static CreditTopUpCardMark()
    {
        AffectsRender<CreditTopUpCardMark>(BrandProperty, PlateBrushProperty, OutlineBrushProperty, NimbusBrushProperty,
            OrbitDiscBrushProperty, OrbitRingBrushProperty, GlyphBrushProperty);
    }

    private static Geometry Build(Action<StreamGeometryContext> draw)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            draw(ctx);
        }
        return geometry;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BrandProperty)
        {
            AutomationProperties.SetName(this, Brand switch
            {
                CreditTopUpCardBrand.Nimbus => "Nimbus",
                CreditTopUpCardBrand.Orbit => "Orbit",
                _ => "Card"
            });
        }
        else if (change.Property == OutlineBrushProperty || change.Property == NimbusBrushProperty ||
                 change.Property == OrbitRingBrushProperty || change.Property == GlyphBrushProperty)
        {
            _outlinePen = _nimbusPen = _stripePen = _ringPen = _glyphPen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(34, 22);

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var plate = new RoundedRect(bounds.Deflate(0.5), 4.5);
        _outlinePen ??= new ImmutablePen(OutlineBrush?.ToImmutable() ?? Brushes.LightGray, 1);
        context.DrawRectangle(PlateBrush ?? Brushes.White, _outlinePen, plate);
        var center = bounds.Center;
        switch (Brand)
        {
            case CreditTopUpCardBrand.Nimbus:
            {
                var ink = NimbusBrush ?? Brushes.Teal;
                _nimbusPen ??= new ImmutablePen(ink.ToImmutable(), 1.2);
                _stripePen ??= new ImmutablePen(ink.ToImmutable(), 1.6, lineCap: PenLineCap.Round);
                using (context.PushTransform(Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawGeometry(null, _nimbusPen, s_capsule);
                    using (context.PushGeometryClip(s_capsule))
                    {
                        context.DrawGeometry(null, _stripePen, s_stripes);
                    }
                }
                break;
            }
            case CreditTopUpCardBrand.Orbit:
            {
                _ringPen ??= new ImmutablePen(OrbitRingBrush?.ToImmutable() ?? Brushes.Orange, 1.4);
                var tilt = Matrix.CreateRotation(-20 * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y);
                var ring = new EllipseGeometry(new Rect(-9, -3.5, 18, 7));
                // The far half of the ring passes behind the disc, the near half in front of it.
                using (context.PushTransform(tilt))
                {
                    context.DrawGeometry(null, _ringPen, ring);
                }
                context.DrawEllipse(OrbitDiscBrush ?? Brushes.Navy, null, center, 5, 5);
                using (context.PushTransform(tilt))
                using (context.PushClip(new Rect(-10, 0, 20, 5)))
                {
                    context.DrawGeometry(null, _ringPen, ring);
                }
                break;
            }
            default:
            {
                _glyphPen ??= new ImmutablePen(GlyphBrush?.ToImmutable() ?? Brushes.Gray, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                // The 24-unit Lucide glyph at 14px.
                var scale = 14.0 / 24;
                using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X - 7, center.Y - 7)))
                {
                    context.DrawGeometry(null, _glyphPen, s_card);
                }
                break;
            }
        }
    }
}
