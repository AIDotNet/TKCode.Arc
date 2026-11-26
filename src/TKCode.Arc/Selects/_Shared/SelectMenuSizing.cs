using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Sizing rule shared by the select menus: as wide as the widest option needs, never narrower than the field or
/// <c>MenuMinWidth</c>, and never wider than <c>MenuMaxWidth</c> or the space the window leaves.
/// </summary>
internal static class SelectMenuSizing
{
    /// <summary>Space kept free at the window edges.</summary>
    internal const double WindowMargin = 16;

    /// <summary>Options measured for the widest text; the rest of a long list does not widen the menu.</summary>
    internal const int MeasuredOptions = 200;

    /// <summary>min(max(field, min, content), max, available).</summary>
    public static double Resolve(double field, double min, double max, double content, double available)
    {
        var width = Math.Max(Math.Max(field, double.IsNaN(min) ? 0 : min), content);
        if (!double.IsNaN(max) && max > 0)
        {
            width = Math.Min(width, max);
        }
        if (double.IsFinite(available) && available > 0)
        {
            width = Math.Min(width, available);
        }
        return width;
    }

    /// <summary>Width of the top-level minus the margin, or infinity when unknown.</summary>
    public static double Available(Visual? visual)
    {
        var width = visual is null ? 0 : TopLevel.GetTopLevel(visual)?.ClientSize.Width ?? 0;
        return width > 0 ? Math.Max(0, width - 2 * WindowMargin) : double.PositiveInfinity;
    }

    /// <summary>Horizontal space the surface and its scroll viewer take around the option list.</summary>
    public static double Chrome(Border? surface, ScrollViewer? scroll)
    {
        var chrome = 0.0;
        if (surface is not null)
        {
            chrome += surface.Padding.Left + surface.Padding.Right + surface.BorderThickness.Left + surface.BorderThickness.Right;
        }
        if (scroll is not null)
        {
            chrome += scroll.Padding.Left + scroll.Padding.Right;
        }
        return chrome;
    }
}
