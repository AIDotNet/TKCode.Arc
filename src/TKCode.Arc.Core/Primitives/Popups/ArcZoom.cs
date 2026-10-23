using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// Whole-UI zoom for apps that scale their window content (an accessibility or density setting). Set
/// <see cref="ScaleProperty"/> on the container that is scaled, or on the window: it is inherited, so every popup
/// opened below it (menus, selects, tooltips, hover cards, popovers) lays out at the same scale. Popups draw in the
/// window's overlay layer, outside the scaled container, so they cannot pick the scale up from a parent transform.
/// </summary>
public sealed class ArcZoom
{
    private ArcZoom()
    {
    }

    /// <summary>Layout scale of popup surfaces; 1 (default) leaves them unscaled.</summary>
    public static readonly AttachedProperty<double> ScaleProperty =
        AvaloniaProperty.RegisterAttached<ArcZoom, AvaloniaObject, double>("Scale", 1.0, inherits: true,
            validate: static value => double.IsFinite(value) && value > 0);

    public static double GetScale(AvaloniaObject element) => element.GetValue(ScaleProperty);

    public static void SetScale(AvaloniaObject element, double value) => element.SetValue(ScaleProperty, value);
}

/// <summary>
/// The element an <see cref="ArcPopup"/> puts between itself and its surface: it lays the surface out at
/// <see cref="Scale"/> and enlarges the result, so text stays vector sharp and the popup is placed by the scaled size.
/// The popup pushes the scale in (it is in the tree and inherits <see cref="ArcZoom.ScaleProperty"/>; a surface that is
/// not attached yet cannot) before the popup measures the surface. At scale 1 there is no transform.
/// </summary>
/// <remarks>
/// AtomUI's popup draws the floating shadow with the corner radius of the popup's direct child when that child is a
/// <see cref="Border"/> or templated control, and square otherwise (a square shadow shows past the surface's rounded
/// corners). The host is that child, so it is a Border that draws nothing and carries the surface's corner radius,
/// scaled with the surface.
/// <see cref="LayoutTransformControl"/> draws its transform through its child's <c>RenderTransform</c>, and Arc surfaces
/// animate their own <c>RenderTransform</c> (enter, press), so the surface sits in a plain slot panel that nothing else
/// touches.
/// </remarks>
internal sealed class ArcZoomHost : Border
{
    private readonly LayoutTransformControl _transform;
    private readonly Panel _slot = new();
    private double _scale = 1.0;

    public ArcZoomHost()
    {
        _transform = new LayoutTransformControl { Child = _slot };
        Child = _transform;
    }

    /// <summary>The popup surface this host scales.</summary>
    public Control? Surface
    {
        get => _slot.Children.Count > 0 ? _slot.Children[0] : null;
        set
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

    public double Scale
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
