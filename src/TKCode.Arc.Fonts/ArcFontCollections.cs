using Avalonia;
using Avalonia.Media.Fonts;

namespace TKCode.Arc.Fonts;

/// <summary>Geist (400, 500) as the font collection <c>fonts:Geist</c>; family name "Geist".</summary>
public sealed class GeistFontCollection : EmbeddedFontCollection
{
    public GeistFontCollection()
        : base(new Uri("fonts:Geist", UriKind.Absolute), new Uri("avares://TKCode.Arc.Fonts/Assets/Geist", UriKind.Absolute))
    {
    }
}

/// <summary>Geist Mono (400, 500) as the font collection <c>fonts:GeistMono</c>; family name "Geist Mono".</summary>
public sealed class GeistMonoFontCollection : EmbeddedFontCollection
{
    public GeistMonoFontCollection()
        : base(new Uri("fonts:GeistMono", UriKind.Absolute), new Uri("avares://TKCode.Arc.Fonts/Assets/GeistMono", UriKind.Absolute))
    {
    }
}

/// <summary>Silkscreen (400, 700) and VT323 (400) as the font collection <c>fonts:ArcPixel</c>; family names "Silkscreen" and "VT323". Used by the pixel theme.</summary>
public sealed class ArcPixelFontCollection : EmbeddedFontCollection
{
    public ArcPixelFontCollection()
        : base(new Uri("fonts:ArcPixel", UriKind.Absolute), new Uri("avares://TKCode.Arc.Fonts/Assets/ArcPixel", UriKind.Absolute))
    {
    }
}

public static class ArcFontsAppBuilderExtensions
{
    /// <summary>
    /// Registers Geist and Geist Mono so Arc's display and code text use them. Pair with Avalonia's
    /// <c>WithInterFont()</c> for body text: <c>AppBuilder.Configure&lt;App&gt;().WithInterFont().WithArcFonts()</c>.
    /// </summary>
    public static AppBuilder WithArcFonts(this AppBuilder builder) => builder.ConfigureFonts(fontManager =>
    {
        fontManager.AddFontCollection(new GeistFontCollection());
        fontManager.AddFontCollection(new GeistMonoFontCollection());
    });

    /// <summary>Registers the pixel fonts (Silkscreen, VT323) that <c>ArcPixelPresets.PixelFontStack</c> names first. Not part of <see cref="WithArcFonts"/> to keep its size out of apps that do not use the pixel theme.</summary>
    public static AppBuilder WithArcPixelFonts(this AppBuilder builder) => builder.ConfigureFonts(fontManager =>
    {
        fontManager.AddFontCollection(new ArcPixelFontCollection());
    });
}
