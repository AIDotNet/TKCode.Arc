using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Which half of a sticker a <see cref="StickerPeelPaper"/> draws.</summary>
internal enum StickerPeelPaperLayer
{
    /// <summary>Under the face: the shadow and the keyboard focus ring.</summary>
    Underlay,

    /// <summary>Over the face: the flap, with its shade at the bend and its shadow.</summary>
    Overlay
}

/// <summary>
/// The painted parts of a <see cref="StickerPeelSticker"/> that are not its content: under the face the contact or lift
/// shadow and the focus ring, over the face the folded-back flap. The flap is the pulled part of the die cut mirrored
/// across the fold line, shown as plain paper backing with one soft shade at the bend and a light shadow that always
/// falls downward on the screen.
/// </summary>
internal sealed class StickerPeelPaper : Control
{
    /// <summary>Which half this instance draws.</summary>
    public static readonly StyledProperty<StickerPeelPaperLayer> LayerProperty =
        AvaloniaProperty.Register<StickerPeelPaper, StickerPeelPaperLayer>(nameof(Layer));

    /// <summary>The backing paper color.</summary>
    public static readonly StyledProperty<IBrush?> PaperBrushProperty =
        AvaloniaProperty.Register<StickerPeelPaper, IBrush?>(nameof(PaperBrush));

    /// <summary>The keyboard focus ring color.</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<StickerPeelPaper, IBrush?>(nameof(FocusRingBrush));

    /// <summary>Whether the focus ring is drawn (keyboard focus only).</summary>
    public static readonly StyledProperty<bool> ShowFocusRingProperty =
        AvaloniaProperty.Register<StickerPeelPaper, bool>(nameof(ShowFocusRing));

    private static readonly IImmutableBrush[] FlapShadows =
    [
        new ImmutableSolidColorBrush(Color.FromArgb(34, 0, 0, 0)),
        new ImmutableSolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
        new ImmutableSolidColorBrush(Color.FromArgb(10, 0, 0, 0))
    ];

    private static readonly double[] FlapShadowOffsets = [1.5, 4, 8];

    private LinearGradientBrush? _paper;
    private Pen? _ringPen;

    public StickerPeelPaper()
    {
        IsHitTestVisible = false;
    }

    public StickerPeelPaperLayer Layer
    {
        get => GetValue(LayerProperty);
        set => SetValue(LayerProperty, value);
    }

    public IBrush? PaperBrush
    {
        get => GetValue(PaperBrushProperty);
        set => SetValue(PaperBrushProperty, value);
    }

    public IBrush? FocusRingBrush
    {
        get => GetValue(FocusRingBrushProperty);
        set => SetValue(FocusRingBrushProperty, value);
    }

    public bool ShowFocusRing
    {
        get => GetValue(ShowFocusRingProperty);
        set => SetValue(ShowFocusRingProperty, value);
    }

    internal StickerPeelSticker? Owner { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PaperBrushProperty)
        {
            // Paper darkens toward the fold: the one soft shade at the bend.
            _paper = PaperBrush is ISolidColorBrush solid ? BendBrush(solid.Color) : null;
            InvalidateVisual();
        }
        else if (change.Property == FocusRingBrushProperty)
        {
            _ringPen = FocusRingBrush is null ? null : new Pen(FocusRingBrush, 2);
            InvalidateVisual();
        }
        else if (change.Property == ShowFocusRingProperty)
        {
            InvalidateVisual();
        }
    }

    private static LinearGradientBrush BendBrush(Color paper)
    {
        var shade = Color.FromArgb(paper.A,
            (byte)(paper.R * 0.78), (byte)(paper.G * 0.78), (byte)(paper.B * 0.78));
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, 26, RelativeUnit.Absolute),
            GradientStops = [new GradientStop(shade, 0), new GradientStop(paper, 1)]
        };
    }

    public override void Render(DrawingContext context)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        if (Layer == StickerPeelPaperLayer.Underlay)
        {
            RenderUnderlay(context, owner);
        }
        else
        {
            RenderFlap(context, owner.Motion);
        }
    }

    private void RenderUnderlay(DrawingContext context, StickerPeelSticker owner)
    {
        var motion = owner.Motion;
        var radius = owner.OutlineRadius;
        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        var s = motion.Shadow;
        // The shadow falls straight down on the screen whatever the sticker's angle.
        var down = StickerPeelGeometry.Rotate(new Vector(0, 1 + 9 * s), -motion.Rotation);
        var shadow = new BoxShadow
        {
            OffsetX = down.X,
            OffsetY = down.Y,
            Blur = 3 + 19 * s,
            Color = Color.FromArgb((byte)(40 + 40 * s), 0, 0, 0)
        };
        // While a part is peeled back, the shadow and the ring stay on the side that is still stuck: the lifted part
        // shows the surface under it, not the outline it left.
        using (motion.HasFold ? context.PushGeometryClip(StuckSide(motion, rect)) : default(DrawingContext.PushedState?))
        {
            context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(rect, radius), new BoxShadows(shadow));
            if (ShowFocusRing && _ringPen is not null)
            {
                context.DrawRectangle(null, _ringPen, new RoundedRect(rect.Inflate(4), radius + 4));
            }
        }
    }

    /// <summary>The half plane on the stuck side of the fold line, large enough to hold the shadow's blur.</summary>
    private static StreamGeometry StuckSide(StickerPeelMotion motion, Rect rect)
    {
        var m = motion.FoldOrigin;
        var t = motion.FoldTangent;
        var n = motion.FoldNormal;
        var l = 4 * (rect.Width + rect.Height) + 200;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(m.X + t.X * l, m.Y + t.Y * l), true);
            path.LineTo(new Point(m.X - t.X * l, m.Y - t.Y * l));
            path.LineTo(new Point(m.X - t.X * l + n.X * l, m.Y - t.Y * l + n.Y * l));
            path.LineTo(new Point(m.X + t.X * l + n.X * l, m.Y + t.Y * l + n.Y * l));
            path.EndFigure(true);
        }
        return geometry;
    }

    private void RenderFlap(DrawingContext context, StickerPeelMotion motion)
    {
        var points = motion.FlapPoints;
        if (!motion.HasFold || points.Length < 3 || _paper is null)
        {
            return;
        }
        // A new geometry every frame (see StickerPeelSticker.Apply): one rewritten in place leaves trails.
        var flap = new StreamGeometry();
        using (var path = flap.Open())
        {
            path.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
            {
                path.LineTo(points[i]);
            }
            path.EndFigure(true);
        }
        var t = motion.FoldTangent;
        var n = motion.FoldNormal;
        var m = motion.FoldOrigin;
        var down = StickerPeelGeometry.Rotate(new Vector(0, 1), -motion.Rotation);
        var dx = StickerPeelGeometry.Dot(down, t);
        var dy = StickerPeelGeometry.Dot(down, n);
        using (context.PushTransform(new Matrix(t.X, t.Y, n.X, n.Y, m.X, m.Y)))
        {
            for (var i = FlapShadows.Length - 1; i >= 0; i--)
            {
                var k = FlapShadowOffsets[i];
                using (context.PushTransform(Matrix.CreateTranslation(dx * k, dy * k)))
                {
                    context.DrawGeometry(FlapShadows[i], null, flap);
                }
            }
            context.DrawGeometry(_paper, null, flap);
        }
    }
}
