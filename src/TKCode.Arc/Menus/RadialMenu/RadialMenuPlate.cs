using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The raised surface under a <see cref="RadialMenu"/>'s sectors. A full circle is one disc drawn with the theme's
/// floating shadow. A sector is a rounded band reaching <see cref="RadialMenu"/>'s plate padding past its sectors plus a
/// small disc at the centre; box shadows only follow rectangles and circles, so that shape takes its shadow from a
/// <see cref="DropShadowEffect"/> built from the first layer of the same shadow token.
/// </summary>
internal sealed class RadialMenuPlate : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<RadialMenuPlate>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<RadialMenuPlate>();

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        Border.BorderThicknessProperty.AddOwner<RadialMenuPlate>();

    public static readonly StyledProperty<BoxShadows> BoxShadowProperty =
        Border.BoxShadowProperty.AddOwner<RadialMenuPlate>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<RadialMenuPlate>();

    private RadialMenu? _menu;
    private StreamGeometry? _band;
    private (double Inner, double Outer, double Start, double Sweep, double Padding, double Gap, double Corner, Size Size) _bandKey;
    private IPen? _pen;
    private bool _sector;

    static RadialMenuPlate()
    {
        AffectsRender<RadialMenuPlate>(BackgroundProperty, BorderBrushProperty, BorderThicknessProperty, BoxShadowProperty, CornerRadiusProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RadialMenuPlate>(false);
    }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public BoxShadows BoxShadow
    {
        get => GetValue(BoxShadowProperty);
        set => SetValue(BoxShadowProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    private RadialMenu? Menu => _menu ??= TemplatedParent as RadialMenu ?? this.FindAncestorOfType<RadialMenu>();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _menu = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoxShadowProperty)
        {
            UpdateEffect();
        }
    }

    /// <summary>Called by the menu when its geometry changes.</summary>
    internal void Refresh()
    {
        var sector = Menu is { } menu && !RadialMenuLayout.IsFullCircle(menu.SweepAngle);
        if (sector != _sector)
        {
            _sector = sector;
            UpdateEffect();
        }
        InvalidateVisual();
    }

    private void UpdateEffect()
    {
        // The full circle draws its shadow exactly; only the sector's free-form shape needs the effect.
        if (!_sector || BoxShadow.Count == 0)
        {
            Effect = null;
            return;
        }
        var shadow = BoxShadow[0];
        Effect = new DropShadowEffect
        {
            OffsetX = shadow.OffsetX,
            OffsetY = shadow.OffsetY,
            BlurRadius = shadow.Blur,
            Color = shadow.Color,
            Opacity = 1
        };
    }

    public override void Render(DrawingContext context)
    {
        if (Menu is not { } menu)
        {
            return;
        }
        var size = Bounds.Size;
        var center = new Point(size.Width / 2, size.Height / 2);
        var padding = menu.PlatePadding;
        var inner = menu.EffectiveInnerRadius;
        var outer = menu.EffectiveOuterRadius;
        var thickness = BorderThickness.Left;
        IPen? pen = null;
        if (BorderBrush is { } stroke && thickness > 0)
        {
            if (_pen is not Pen cached || !ReferenceEquals(cached.Brush, stroke) || cached.Thickness != thickness)
            {
                _pen = new Pen(stroke, thickness);
            }
            pen = _pen;
        }
        if (RadialMenuLayout.IsFullCircle(menu.SweepAngle))
        {
            var radius = outer + padding;
            var disc = new RoundedRect(new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius), radius);
            context.DrawRectangle(Background, pen, disc, BoxShadow);
            return;
        }
        var gap = Math.Max(0, menu.SectorGap);
        var sweep = RadialMenuLayout.ClampSweep(menu.SweepAngle);
        var key = (inner, outer, menu.StartAngle, sweep, padding, gap, CornerRadius.TopLeft, size);
        if (_band is null || key != _bandKey)
        {
            _bandKey = key;
            var bandInner = Math.Max(0, inner - padding);
            _band = RadialMenuLayout.Sector(center, bandInner, outer + padding, new RadialMenuSlot(menu.StartAngle, sweep), -padding,
                                            CornerRadius.TopLeft);
        }
        context.DrawGeometry(Background, pen, _band);
        var discRadius = Math.Max(0, inner - padding - gap);
        if (discRadius > 0)
        {
            context.DrawEllipse(Background, pen, center, discRadius, discRadius);
        }
    }
}
