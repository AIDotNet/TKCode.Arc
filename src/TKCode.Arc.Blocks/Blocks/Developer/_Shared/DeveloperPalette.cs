using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Colors the Developer parts mix at run time from ArcDesign tokens (Arc writes them as <c>color-mix()</c>; no token
/// holds them). Read only while the control is attached (the tokens come from its theme scope); recompute on
/// <c>ActualThemeVariantChanged</c> and <c>ResourcesChanged</c> (the accent can change).
/// </summary>
internal readonly record struct DeveloperPalette(
    IBrush PillRest,
    IBrush PillHover,
    IBrush PillPressed,
    IBrush ToggleHover,
    IBrush IconHover,
    IBrush IconOn,
    IBrush SecretBorder)
{
    /// <summary>Mixes the palette for <paramref name="owner"/>'s theme.</summary>
    public static DeveloperPalette For(Control owner)
    {
        var surface = ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface);
        var foreground = ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground);
        var border = ArcTokens.GetColor(owner, ArcDesignTokenKind.Border);
        var accent = ArcTokens.GetColor(owner, ArcDesignTokenKind.Accent);
        return new DeveloperPalette(
            // The Create key / Add endpoint pill: Foreground 5.5% into Surface (.953 / .256), hover 9%, pressed 11%.
            new ImmutableSolidColorBrush(Oklch.MixOklab(surface, foreground, 0.055)),
            new ImmutableSolidColorBrush(Oklch.MixOklab(surface, foreground, 0.09)),
            new ImmutableSolidColorBrush(Oklch.MixOklab(surface, foreground, 0.11)),
            // The 28px toggle inside the pill: Foreground at 8%.
            new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.08)),
            // Env variables' icon buttons: Foreground 5% on hover, 8% when on (revealed) or keyboard focused.
            new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.05)),
            new ImmutableSolidColorBrush(Oklch.WithAlpha(foreground, 0.08)),
            // The one-time secret box border: Accent 45% into Border (.663 / .520).
            new ImmutableSolidColorBrush(Oklch.MixOklab(border, accent, 0.45)));
    }
}
