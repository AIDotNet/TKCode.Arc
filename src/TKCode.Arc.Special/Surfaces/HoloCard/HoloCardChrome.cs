using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The floating shadow under a <see cref="HoloCard"/>: a box inset 5 % / 3 % from the card with a 42 px blur and a
/// −14 px spread, offset against the tilt and drawn through the same tilt projection as the card (but not the flip,
/// so it keeps its full size while the card spins). Like a CSS outer shadow it is not painted under its own box.
/// </summary>
internal sealed class HoloCardShadow : Control
{
    private Vector _offset = new(0, HoloCardMath.ShadowRestOffsetY);
    private double _alpha = HoloCardMath.ShadowRestAlpha;

    public HoloCardShadow()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    /// <summary>The shadow's offset in px.</summary>
    public Vector Offset => _offset;

    /// <summary>The shadow's opacity.</summary>
    public double Alpha => _alpha;

    /// <summary>The corner radius of the shadow's box.</summary>
    public double CornerRadius { get; set; } = 22;

    public void Update(Vector offset, double alpha)
    {
        if (offset == _offset && alpha == _alpha)
        {
            return;
        }
        _offset = offset;
        _alpha = alpha;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || _alpha <= 0)
        {
            return;
        }
        var box = new Rect(size).Deflate(new Thickness(size.Width * HoloCardMath.ShadowInsetX, size.Height * HoloCardMath.ShadowInsetY));
        var shadow = new BoxShadow
        {
            OffsetX = _offset.X,
            OffsetY = _offset.Y,
            Blur = HoloCardMath.ShadowBlur,
            Spread = HoloCardMath.ShadowSpread,
            Color = Color.FromArgb((byte)Math.Round(Math.Clamp(_alpha, 0, 1) * 255), 0, 0, 0),
        };
        context.DrawRectangle(null, null, new RoundedRect(box, CornerRadius), new BoxShadows(shadow));
    }
}

/// <summary>
/// The keyboard focus ring of a <see cref="HoloCard"/>: 2 px, 2 px outside the card, its corners following the card's,
/// drawn through the tilt projection so it leans with the card (an outline on the tilted element in Arc).
/// </summary>
internal sealed class HoloCardFocusRing : Control
{
    /// <summary>The ring's colour.</summary>
    public static readonly StyledProperty<IBrush?> BrushProperty =
        AvaloniaProperty.Register<HoloCardFocusRing, IBrush?>(nameof(Brush));

    private Pen? _pen;

    static HoloCardFocusRing()
    {
        AffectsRender<HoloCardFocusRing>(BrushProperty);
    }

    public HoloCardFocusRing()
    {
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public IBrush? Brush
    {
        get => GetValue(BrushProperty);
        set => SetValue(BrushProperty, value);
    }

    /// <summary>The corner radius of the card the ring surrounds.</summary>
    public double CornerRadius { get; set; } = 22;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BrushProperty)
        {
            _pen = Brush is { } brush ? new Pen(brush, 2) : null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_pen is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }
        // Offset 2, width 2: the stroke's centre line is 3 px outside the card.
        context.DrawRectangle(null, _pen, new RoundedRect(new Rect(Bounds.Size).Inflate(3), CornerRadius + 3));
    }
}
