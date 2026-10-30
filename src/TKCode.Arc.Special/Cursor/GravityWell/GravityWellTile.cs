using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// One tile of a <see cref="GravityWell"/>: a square cell that clips its content to rounded corners and carries the
/// accent ring a shockwave flashes. The well moves it with its render transform, opacity and stacking order only, so the
/// tile keeps its grid slot and the reading order never changes.
/// </summary>
[TemplatePart(RingPart, typeof(Border))]
internal sealed class GravityWellTile : ContentControl
{
    public const string RingPart = "PART_Ring";

    /// <summary>
    /// How much rounder the ring's corners are than the tile's: the 2px it sits outside, read from the <c>RadiusStep2</c>
    /// token so the corner styles scale it with the tile (half under a 0.5 scale, nothing under Square).
    /// </summary>
    private static readonly StyledProperty<CornerRadius> RingGrowthProperty =
        AvaloniaProperty.Register<GravityWellTile, CornerRadius>("RingGrowth", new CornerRadius(2));

    private readonly MatrixTransform _transform = new();
    private Border? _ring;
    private double _ringOpacity;

    public GravityWellTile()
    {
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.Center;
        ArcTokens.Bind(this, RingGrowthProperty, ArcDesignTokenKind.RadiusStep2);
    }

    /// <summary>How bright the shockwave ring is (0 to 1).</summary>
    internal double RingOpacity => _ringOpacity;

    internal Matrix Placement => _transform.Matrix;

    /// <summary>Writes the tile's pose for this frame: offset, scale, opacity and stacking order.</summary>
    internal void Pose(double offsetX, double offsetY, double scale, double opacity, int zIndex, double ring)
    {
        _transform.Matrix = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);
        Opacity = opacity;
        if (ZIndex != zIndex)
        {
            ZIndex = zIndex;
        }
        SetRing(ring);
    }

    /// <summary>Puts the tile back in its slot.</summary>
    internal void Reset() => Pose(0, 0, 1, 1, 0, 0);

    private void SetRing(double value)
    {
        _ringOpacity = value;
        if (_ring is not null)
        {
            _ring.Opacity = value;
            _ring.IsVisible = value > 0;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring = e.NameScope.Find<Border>(RingPart);
        SetRing(_ringOpacity);
        UpdateRingRadius();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CornerRadiusProperty || change.Property == RingGrowthProperty)
        {
            UpdateRingRadius();
        }
    }

    // The ring sits 2px outside the tile, so its corners are 2px rounder (the growth follows the corner style, see RingGrowth).
    private void UpdateRingRadius()
    {
        if (_ring is null)
        {
            return;
        }
        var r = CornerRadius;
        var growth = GetValue(RingGrowthProperty).TopLeft;
        _ring.CornerRadius = new CornerRadius(Grow(r.TopLeft, growth), Grow(r.TopRight, growth), Grow(r.BottomRight, growth),
            Grow(r.BottomLeft, growth));
    }

    private static double Grow(double radius, double growth) => radius > 0 ? radius + growth : 0;
}
