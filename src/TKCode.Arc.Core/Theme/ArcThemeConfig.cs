namespace TKCode.Arc;

/// <summary>
/// Arc's additions to <see cref="ArcThemeBuilder"/>. Accent seeds, token overrides (<c>Token(name, value)</c> for ArcDesign,
/// <c>Token(identity, name, value)</c> for a control) and the motion level are on the builder itself.
/// </summary>
public static class ArcThemeBuilderExtensions
{
    /// <summary>Uses one of Arc's accent presets as accent seed.</summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="preset">The preset.</param>
    public static ArcThemeBuilder Accent(this ArcThemeBuilder builder, ArcAccentPreset preset)
        => builder.Accent(ArcPalette.GetSeed(preset));
}
