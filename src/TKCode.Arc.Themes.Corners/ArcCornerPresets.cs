using System.Globalization;

namespace TKCode.Arc.Themes.Corners;

/// <summary>
/// Corner styles as theme presets (design/architecture/theming/corner-style.md). A corner style writes the final value of
/// every radius token of ArcDesign and of the control packages, computed from the defaults in <see cref="CornerTable"/>;
/// nothing else changes. Overrides of packages the app did not enable are skipped, so there is no scope parameter.
/// </summary>
public static class ArcCornerPresets
{
    /// <summary>The slot of corner styles: a state holds one corner style, the last one added.</summary>
    public const string Slot = "ArcCorners";

    /// <summary>Every corner square, pills and circles included.</summary>
    public static ArcThemePreset Square { get; } = Create(ArcCornerStyle.Square);

    /// <summary>Every radius halved; pills and circles stay round (the Gallery's "half" corners).</summary>
    public static ArcThemePreset Half { get; } = Create(ArcCornerStyle.Scale(0.5));

    /// <summary>
    /// The preset of <paramref name="style"/>, named <c>ArcCornersDefault</c>, <c>ArcCornersSquare</c> or
    /// <c>ArcCornersScale{factor}</c>. The default style gives an empty preset: adding it removes a previous corner style.
    /// </summary>
    /// <param name="style">The corner style.</param>
    public static ArcThemePreset Create(ArcCornerStyle style)
        => ArcThemePreset.Create(NameOf(style), preset => AddTo(preset, style), Slot);

    /// <summary>Makes <see cref="Square"/> and <see cref="Half"/> discoverable (called by the package's <c>UseArcCorners()</c>).</summary>
    /// <param name="package">The package being defined.</param>
    internal static void Register(ArcPackageBuilder package) => package.AddPresets([Square, Half]);

    private static string NameOf(ArcCornerStyle style)
    {
        if (style.IsDefault)
        {
            return "ArcCornersDefault";
        }
        if (style.IsSquare)
        {
            return "ArcCornersSquare";
        }
        return "ArcCornersScale" + style.Factor.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static void AddTo(ArcThemePresetBuilder preset, ArcCornerStyle style)
    {
        if (style.IsDefault)
        {
            return;
        }
        foreach (var entry in CornerTable.ArcDesign)
        {
            preset.Token(entry.Token, CornerValues.Value(entry, style));
        }
        // The scroll bar thumb entries name AtomUI's ScrollViewer identity. Arc's base theme draws scroll bars with fixed
        // sizes and registers no such identity, so presets skip them; they stay so the preset matches
        // tests/Baselines/presets/corners-*.json (ThemePresetPortTests).
        foreach (var table in new[]
                 {
                     CornerTable.Controls, AtomUiCornerTable.ScrollViewer, CornerTable.Blocks, CornerTable.Special,
                     CornerTable.AdvancedCharts, CornerTable.Workflow
                 })
        {
            foreach (var entry in table)
            {
                preset.Token(entry.Control, entry.Token, CornerValues.Value(entry, style));
            }
        }
    }
}
