using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
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
    /// <c>WithInterFont()</c> for body text: <c>AppBuilder.Configure&lt;App&gt;().WithInterFont().WithArcFonts()</c>. The same
    /// as <c>UseArcFonts()</c> inside <c>UseArc(...)</c>; calling both is harmless.
    /// </summary>
    public static AppBuilder WithArcFonts(this AppBuilder builder) => builder.ConfigureFonts(ArcFontRegistration.AddGeist);

    /// <summary>Registers the pixel fonts (Silkscreen, VT323) that <c>ArcPixelPresets.PixelFontStack</c> names first. Not part of <see cref="WithArcFonts"/> to keep its size out of apps that do not use the pixel theme.</summary>
    public static AppBuilder WithArcPixelFonts(this AppBuilder builder) => builder.ConfigureFonts(ArcFontRegistration.AddPixel);
}

/// <summary>The <c>UseArc(...)</c> entries of TKCode.Arc.Fonts (#151).</summary>
public static class ArcFontsBuilderExtensions
{
    /// <summary>The package id of <see cref="UseArcFonts"/> in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PackageId = "TKCode.Arc.Fonts";

    /// <summary>The package id of <see cref="UseArcPixelFonts"/> in <c>ArcRuntime.Packages</c>.</summary>
    internal const string PixelPackageId = "TKCode.Arc.Fonts.Pixel";

    /// <summary>
    /// Registers Geist and Geist Mono when the app starts, as <see cref="ArcFontsAppBuilderExtensions.WithArcFonts"/> does;
    /// pair with Avalonia's <c>WithInterFont()</c> for body text.
    /// </summary>
    public static ArcBuilder UseArcFonts(this ArcBuilder builder)
        => builder.UsePackage(PackageId, static package => package.OnCommit(static _ => ArcFontRegistration.AddGeist(FontManager.Current)));

    /// <summary>
    /// Registers the pixel fonts (Silkscreen, VT323) when the app starts, as
    /// <see cref="ArcFontsAppBuilderExtensions.WithArcPixelFonts"/> does.
    /// </summary>
    public static ArcBuilder UseArcPixelFonts(this ArcBuilder builder)
        => builder.UsePackage(PixelPackageId, static package => package.OnCommit(static _ => ArcFontRegistration.AddPixel(FontManager.Current)));
}

/// <summary>Adds each collection once per font manager, so <c>WithArcFonts()</c> and <c>UseArcFonts()</c> can both run.</summary>
internal static class ArcFontRegistration
{
    private static readonly ConditionalWeakTable<FontManager, HashSet<string>> s_added = new();

    public static void AddGeist(FontManager fontManager)
    {
        Add(fontManager, "fonts:Geist", static () => new GeistFontCollection());
        Add(fontManager, "fonts:GeistMono", static () => new GeistMonoFontCollection());
    }

    public static void AddPixel(FontManager fontManager) => Add(fontManager, "fonts:ArcPixel", static () => new ArcPixelFontCollection());

    private static void Add(FontManager fontManager, string key, Func<IFontCollection> create)
    {
        var added = s_added.GetOrCreateValue(fontManager);
        lock (added)
        {
            if (!added.Add(key))
            {
                return;
            }
        }
        fontManager.AddFontCollection(create());
    }
}
