using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// The element an <see cref="ArcPopup"/> puts between its popup host and its surface. It draws the floating shadow
/// around the surface with the surface's corner radius, carries the popup's motion (its <c>Opacity</c> and
/// <c>RenderTransform</c>, so the shadow fades, travels and scales with the surface) and lays the surface out at the
/// <see cref="ArcZoom"/> scale. In the window's overlay layer the shadow is painted past the frame's bounds; in a native
/// popup window, which clips to its size, the frame's <c>Margin</c> is the room the shadow needs and the popup places
/// the surface (not the window) where the placement puts it, so the shadow spreads into transparent room around it.
/// </summary>
/// <remarks>
/// Code that animates a popup itself (presences that turn <see cref="ArcPopup.IsMotionEnabled"/> off) finds this frame
/// with <c>FindAncestorOfType&lt;ArcPopupFrame&gt;()</c> from the surface and writes its <c>Opacity</c> and
/// <c>RenderTransform</c>; frame coordinates are the surface's (scaled) coordinates.
/// The zoom is a <see cref="LayoutTransformControl"/>, which draws its transform through its child's
/// <c>RenderTransform</c>; Arc surfaces animate their own <c>RenderTransform</c> (enter, press), so the surface sits
/// in a plain slot panel that nothing else touches. At scale 1 there is no transform.
/// </remarks>
public sealed class ArcPopupFrame : Border
{
    private readonly LayoutTransformControl _transform;
    private readonly Panel _slot = new();
    private double _scale = 1.0;

    public ArcPopupFrame()
    {
        _transform = new LayoutTransformControl { Child = _slot };
        Child = _transform;
    }

    /// <summary>The popup surface this frame hosts.</summary>
    public Control? Surface
    {
        get => _slot.Children.Count > 0 ? _slot.Children[0] : null;
        internal set
        {
            if (Surface is { } previous)
            {
                previous.PropertyChanged -= HandleSurfacePropertyChanged;
            }
            _slot.Children.Clear();
            if (value is not null)
            {
                _slot.Children.Add(value);
                value.PropertyChanged += HandleSurfacePropertyChanged;
            }
            SyncCornerRadius();
        }
    }

    /// <summary>Layout scale of the surface (<see cref="ArcZoom"/>).</summary>
    internal double Scale
    {
        get => _scale;
        set
        {
            if (_scale == value)
            {
                return;
            }
            _scale = value;
            _transform.LayoutTransform = value == 1.0 ? null : new ScaleTransform(value, value);
            SyncCornerRadius();
        }
    }

    /// <summary>Room the outer (non-inset) shadows in <paramref name="shadows"/> spread to on each side, in whole pixels.</summary>
    internal static Thickness RoomFor(BoxShadows shadows)
    {
        double left = 0, top = 0, right = 0, bottom = 0;
        foreach (var shadow in shadows)
        {
            if (shadow.IsInset)
            {
                continue;
            }
            // Skia's blur fades out within the blur radius; spread grows the shadow shape itself.
            var reach = Math.Max(0, shadow.Blur) + shadow.Spread;
            left = Math.Max(left, reach - shadow.OffsetX);
            right = Math.Max(right, reach + shadow.OffsetX);
            top = Math.Max(top, reach - shadow.OffsetY);
            bottom = Math.Max(bottom, reach + shadow.OffsetY);
        }
        return new Thickness(Math.Ceiling(left), Math.Ceiling(top), Math.Ceiling(right), Math.Ceiling(bottom));
    }

    private void HandleSurfacePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Border.CornerRadiusProperty || e.Property == TemplatedControl.CornerRadiusProperty)
        {
            SyncCornerRadius();
        }
    }

    private void SyncCornerRadius()
    {
        var radius = Surface switch
        {
            Border border => border.CornerRadius,
            TemplatedControl templated => templated.CornerRadius,
            _ => default
        };
        CornerRadius = new CornerRadius(radius.TopLeft * _scale, radius.TopRight * _scale,
            radius.BottomRight * _scale, radius.BottomLeft * _scale);
    }
}
