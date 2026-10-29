using Avalonia;

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
