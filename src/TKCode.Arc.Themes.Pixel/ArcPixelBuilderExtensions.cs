using AtomUI;
using AtomUI.Generated.TKCodeArcThemesPixel;

namespace TKCode.Arc.Themes.Pixel;

public static class ArcPixelBuilderExtensions
{
    /// <summary>
    /// Registers the compiled themes <c>ArcPixel</c> (light) and <c>ArcPixelDark</c> (dark). They are never the default;
    /// select one with <c>builder.Theme.WithInitialTheme(ArcPixelTheme.LightThemeId)</c> or an
    /// <c>IThemeManager.ApplyThemeAsync</c> request. Call it after <c>UseArc()</c> or <c>UseArcCore()</c>.
    /// </summary>
    public static IAtomUIBuilder UseArcPixelTheme(this IAtomUIBuilder builder)
    {
        GeneratedThemePackageRegistration.Register(builder.Theme);
        return builder;
    }
}
