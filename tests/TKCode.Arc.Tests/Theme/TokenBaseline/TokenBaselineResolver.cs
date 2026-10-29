using Avalonia.Controls;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Themes.Pixel;

namespace TKCode.Arc.Tests.Theme.TokenBaseline;

/// <summary>
/// The theme-engine-specific half of the token baseline: how a <see cref="TokenVariant"/> is applied and how a token kind
/// is looked up. This is the only file of the baseline that knows the theme engine (TKCode.Arc.Foundation): the theme is
/// applied as an <see cref="ArcThemeState"/> built from the default one, and a token is found the way
/// <c>{arc:ArcDesignTokenResource X}</c> and <c>{arc:XxxTokenResource X}</c> find it in XAML, through the boxed
/// <c>XxxTokenKind</c> key resolved from a control in a themed window. The Pixel and Corners variants apply
/// <see cref="ArcPixelPresets.Pixel"/> and <see cref="ArcCornerPresets.Create"/> as the Gallery does. It was swapped from
/// AtomUI to Foundation in phase F3 (design/roadmap/atomui-replacement.md); keep <see cref="TokenBaselineTests"/>
/// (variants, JSON format, comparison) and <see cref="TokenValueFormatter"/> unchanged, so the committed baselines prove
/// every token still resolves to the same value.
/// </summary>
internal static class TokenBaselineResolver
{
    /// <summary>Applies the variant's appearance, accent, motion mode, Pixel style and corner style to the application theme.</summary>
    public static async Task ApplyAsync(TokenVariant variant)
    {
        if (!variant.Dark && variant.Preset is null && variant.Seed is null && variant.Motion is null && !variant.Pixel
            && variant.CornerScale is null)
        {
            // The untouched default theme.
            await TestHelpers.ApplyAsync(null);
            return;
        }
        await TestHelpers.ApplyAsync(t =>
        {
            if (variant.Dark)
            {
                t.Appearance(ArcAppearance.Dark);
            }
            if (variant.Preset is { } preset)
            {
                t.Accent(preset);
            }
            if (variant.Seed is { } seed)
            {
                t.Accent(seed);
            }
            if (variant.Motion is { } motion)
            {
                t.MotionLevel(motion);
            }
            if (variant.Pixel)
            {
                t.Preset(ArcPixelPresets.Pixel);
            }
            if (variant.CornerScale is { } scale)
            {
                t.Preset(ArcCornerPresets.Create(scale == 0 ? ArcCornerStyle.Square : ArcCornerStyle.Scale(scale)));
            }
        });
    }

    /// <summary>Restores the default theme so later tests start from it.</summary>
    public static Task ResetAsync() => TestHelpers.ApplyAsync(null);

    /// <summary>
    /// Resolves <paramref name="kind"/> (a value of <c>ArcDesignTokenKind</c> or of a control's <c>XxxTokenKind</c>) as
    /// <paramref name="host"/>, a control shown in a themed window, sees it.
    /// </summary>
    public static bool TryResolve(Control host, Enum kind, out object? value)
        => host.TryFindResource(kind, host.ActualThemeVariant, out value);
}
