using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Primitives;

/// <summary>How far a surface sits above the page.</summary>
public enum ArcSurfaceElevation
{
    /// <summary>The page or a resting surface (<c>Background</c>, <c>Surface</c>).</summary>
    Base,

    /// <summary>A floating surface filled with <c>SurfaceRaised</c>: dialogs, drawers, sheets, popovers and menus.</summary>
    Raised,
}

/// <summary>
/// Inherited surface elevation. Raised surfaces set <see cref="ElevationProperty"/> in their theme and their content inherits it,
/// so controls whose hover fill (<c>SurfaceMuted</c>) would nearly vanish against <c>SurfaceRaised</c> can use a stronger one:
/// a ghost <c>Button</c> gets the <c>:raised</c> pseudo-class.
/// </summary>
public sealed class ArcSurface
{
    private ArcSurface()
    {
    }

    /// <summary>The elevation of the nearest surface that sets it; <see cref="ArcSurfaceElevation.Base"/> elsewhere.</summary>
    public static readonly AttachedProperty<ArcSurfaceElevation> ElevationProperty =
        AvaloniaProperty.RegisterAttached<ArcSurface, Control, ArcSurfaceElevation>("Elevation", ArcSurfaceElevation.Base, inherits: true);

    public static ArcSurfaceElevation GetElevation(Control element) => element.GetValue(ElevationProperty);

    public static void SetElevation(Control element, ArcSurfaceElevation value) => element.SetValue(ElevationProperty, value);
}
