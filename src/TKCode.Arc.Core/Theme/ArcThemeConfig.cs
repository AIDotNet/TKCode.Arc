using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Arc's additions to <see cref="ArcThemeBuilder"/>. Accent seeds, token overrides (<c>Token(name, value)</c> for ArcDesign,
/// <c>Token(identity, name, value)</c> for a control) and the motion level are on the builder itself. The font family
/// methods set the three family tokens from a <see cref="FontFamily"/> instead of a string.
/// </summary>
public static class ArcThemeBuilderExtensions
{
    /// <summary>Uses one of Arc's accent presets as accent seed.</summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="preset">The preset.</param>
    public static ArcThemeBuilder Accent(this ArcThemeBuilder builder, ArcAccentPreset preset)
        => builder.Accent(ArcPalette.GetSeed(preset));

    /// <summary>
    /// Sets the family of body text (the <c>BodyFontFamily</c> token) that every Arc control uses. Setting
    /// <c>FontFamily</c> on a window or panel does not reach Arc controls, because their themes read this token.
    /// </summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="family">The family or fallback chain, for example <c>new FontFamily("fonts:Inter#Inter, avares://MyApp/Assets/Fonts#My Sans")</c>.</param>
    public static ArcThemeBuilder BodyFontFamily(this ArcThemeBuilder builder, FontFamily family)
        => builder.Token("BodyFontFamily", FamilyText(family));

    /// <summary>Sets the family of display text, headings and large numbers (the <c>DisplayFontFamily</c> token).</summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="family">The family or fallback chain.</param>
    public static ArcThemeBuilder DisplayFontFamily(this ArcThemeBuilder builder, FontFamily family)
        => builder.Token("DisplayFontFamily", FamilyText(family));

    /// <summary>Sets the family of monospaced text such as code and terminals (the <c>MonoFontFamily</c> token).</summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="family">The family or fallback chain.</param>
    public static ArcThemeBuilder MonoFontFamily(this ArcThemeBuilder builder, FontFamily family)
        => builder.Token("MonoFontFamily", FamilyText(family));

    /// <summary>Sets the body and the display family to <paramref name="family"/> at once, the usual way to switch a whole app to one font.</summary>
    /// <param name="builder">The theme builder.</param>
    /// <param name="family">The family or fallback chain.</param>
    public static ArcThemeBuilder FontFamily(this ArcThemeBuilder builder, FontFamily family)
        => builder.BodyFontFamily(family).DisplayFontFamily(family);

    private const string CompositePrefix = "compositefont:";

    private static string FamilyText(FontFamily family)
    {
        ArgumentNullException.ThrowIfNull(family);
        // A chain ("A, B, $Default") is a composite whose key holds the text it was made from; its own ToString() would
        // not parse back to the same chain.
        return family.Key?.ToString() is { } key && key.StartsWith(CompositePrefix, StringComparison.Ordinal)
            ? key[CompositePrefix.Length..]
            : family.ToString();
    }
}
