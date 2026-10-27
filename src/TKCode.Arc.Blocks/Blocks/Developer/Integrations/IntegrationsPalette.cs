using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The status colors of the Integrations connect pill, mixed at run time from ArcDesign tokens (Arc writes them as
/// <c>color-mix()</c>; Success and Danger can be re-themed, so no control token holds them). Read only while the control
/// is attached; recompute on <c>ActualThemeVariantChanged</c> / <c>ResourcesChanged</c>.
/// </summary>
internal readonly record struct IntegrationsPalette(
    IBrush Rest,
    IBrush RestBorder,
    IBrush Hover,
    IBrush HoverBorder,
    IBrush ConnectedFill,
    IBrush ConnectedBorder,
    IBrush ErrorFill,
    IBrush ErrorHoverFill,
    IBrush ErrorBorder)
{
    /// <summary>Mixes the palette for <paramref name="owner"/>'s theme, or returns null when it has no theme context yet.</summary>
    public static IntegrationsPalette? TryFor(Control owner)
    {
        try
        {
            var surface = ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface);
            var muted = ArcTokens.GetColor(owner, ArcDesignTokenKind.SurfaceMuted);
            var border = ArcTokens.GetColor(owner, ArcDesignTokenKind.Border);
            var strong = ArcTokens.GetColor(owner, ArcDesignTokenKind.BorderStrong);
            var success = ArcTokens.GetColor(owner, ArcDesignTokenKind.Success);
            var danger = ArcTokens.GetColor(owner, ArcDesignTokenKind.Danger);
            return new IntegrationsPalette(
                new ImmutableSolidColorBrush(surface),
                new ImmutableSolidColorBrush(border),
                new ImmutableSolidColorBrush(muted),
                new ImmutableSolidColorBrush(strong),
                // Connected: Success 8% into Surface, its border Success 30% into Border.
                new ImmutableSolidColorBrush(Oklch.MixOklab(surface, success, 0.08)),
                new ImmutableSolidColorBrush(Oklch.MixOklab(border, success, 0.30)),
                // Error (ours, symmetric with connected): Danger 8% / 12% on hover, border Danger 30%.
                new ImmutableSolidColorBrush(Oklch.MixOklab(surface, danger, 0.08)),
                new ImmutableSolidColorBrush(Oklch.MixOklab(surface, danger, 0.12)),
                new ImmutableSolidColorBrush(Oklch.MixOklab(border, danger, 0.30)));
        }
        catch (Exception)
        {
            return null;
        }
    }
}
