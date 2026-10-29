namespace TKCode.Arc.Themes.Corners;

public static class ArcCornersBuilderExtensions
{
    /// <summary>
    /// Enables the corner style package: registers <see cref="ArcCornerPresets.Square"/> and
    /// <see cref="ArcCornerPresets.Half"/> so they can be listed (<c>ArcRuntime.Presets</c>). Registering does not apply them;
    /// apply a style with <c>ArcTheme.Apply(t => t.Corners(ArcCornerStyle.Square))</c>.
    /// </summary>
    public static ArcBuilder UseArcCorners(this ArcBuilder builder)
        => builder.UsePackage("TKCode.Arc.Themes.Corners", static package =>
        {
            package.DependsOn(static b => b.UseArcCore());
            ArcCornerPresets.Register(package);
        });

    /// <summary>
    /// Applies a corner style (<c>t.Preset(ArcCornerPresets.Create(style))</c>). It fills the
    /// <see cref="ArcCornerPresets.Slot"/> slot, so it replaces the previous corner style;
    /// <see cref="ArcCornerStyle.Default"/> removes it.
    /// </summary>
    public static ArcThemeBuilder Corners(this ArcThemeBuilder theme, ArcCornerStyle style)
        => theme.Preset(ArcCornerPresets.Create(style));
}
