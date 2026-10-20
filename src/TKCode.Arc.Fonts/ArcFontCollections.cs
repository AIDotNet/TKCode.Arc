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
}
