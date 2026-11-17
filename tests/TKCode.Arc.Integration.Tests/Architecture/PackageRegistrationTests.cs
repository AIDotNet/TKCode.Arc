using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Fonts;

namespace TKCode.Arc.Tests.Architecture;

/// <summary>Packages enabled through <c>UseArc(...)</c> show up in <see cref="ArcRuntime.Packages"/> (#151).</summary>
public class PackageRegistrationTests
{
    [AvaloniaFact]
    public void UseArc_Lists_Every_Enabled_Package()
    {
        var packages = ArcRuntime.Current.ShouldNotBeNull().Packages;
        packages.ShouldContain("TKCode.Arc.Fonts");
        packages.ShouldContain("TKCode.Arc.Themes.Corners");
        packages.ShouldContain("TKCode.Arc.Themes.Pixel");
    }

    [AvaloniaFact]
    public void Fonts_Registered_By_UseArcFonts_Can_Be_Registered_Again()
    {
        // WithArcFonts() and UseArcFonts() in one app: each collection is added once per font manager.
        Should.NotThrow(() => ArcFontRegistration.AddGeist(FontManager.Current));
        Should.NotThrow(() => ArcFontRegistration.AddGeist(FontManager.Current));
    }
}
