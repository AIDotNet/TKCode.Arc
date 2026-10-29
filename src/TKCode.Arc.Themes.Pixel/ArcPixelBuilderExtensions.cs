namespace TKCode.Arc.Themes.Pixel;

public static class ArcPixelBuilderExtensions
{
    /// <summary>
    /// Enables the Pixel style package: registers <see cref="ArcPixelPresets.Pixel"/> so it can be listed
    /// (<c>ArcRuntime.Presets</c>). Registering does not apply it; apply it with
    /// <c>ArcTheme.Apply(t => t.Pixel())</c> and go back with <c>t.RemovePreset(ArcPixelPresets.Slot)</c>. The preset
    /// carries a light and a dark palette; the theme's appearance picks one.
    /// </summary>
    public static ArcBuilder UseArcPixelTheme(this ArcBuilder builder)
        => builder.UsePackage("TKCode.Arc.Themes.Pixel", static package =>
        {
            package.DependsOn(static b => b.UseArcCore());
            ArcPixelPresets.Register(package);
        });

    /// <summary>Applies the Pixel style (<c>t.Preset(ArcPixelPresets.Pixel)</c>).</summary>
    public static ArcThemeBuilder Pixel(this ArcThemeBuilder theme) => theme.Preset(ArcPixelPresets.Pixel);
}
