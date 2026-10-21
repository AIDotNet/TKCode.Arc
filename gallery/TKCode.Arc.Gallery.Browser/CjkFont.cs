using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// The Chinese font of the WebAssembly host. A browser gives Skia no system fonts, so without a font of its own the app draws
/// every Chinese character as a box. The font is Noto Sans SC Regular (SIL OFL 1.1) cut down to the characters the gallery
/// uses plus the 3755 common ones (<c>tools/fonts/subset-noto-sans-sc.py</c>, about 1 MB, 0.7 MB over the wire). It is fetched
/// the first time a Chinese character is drawn, so a visitor who never sees one never downloads it; the fetch is synchronous
/// because the text layout asks for the glyph in the middle of measuring.
/// </summary>
internal sealed partial class CjkFontCollection : FontCollectionBase
{
    /// <summary>The key of the collection and the family inside it, as a font family string.</summary>
    public const string Family = "fonts:ArcCjk#Noto Sans SC";

    private const string File = "fonts/NotoSansSC-Subset.otf";
    private static readonly Uri s_key = new("fonts:ArcCjk");
    private bool _loadAttempted;

    public override Uri Key => s_key;

    public override bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
                                             [NotNullWhen(true)] out GlyphTypeface? glyphTypeface)
    {
        EnsureLoaded();
        return base.TryGetGlyphTypeface(familyName, style, weight, stretch, out glyphTypeface);
    }

    public override bool TryGetFamilyTypefaces(string familyName, [NotNullWhen(true)] out System.Collections.Generic.IReadOnlyList<Typeface>? familyTypefaces)
    {
        EnsureLoaded();
        return base.TryGetFamilyTypefaces(familyName, out familyTypefaces);
    }

    private void EnsureLoaded()
    {
        if (_loadAttempted)
        {
            return;
        }
        _loadAttempted = true;
        var bytes = Fetch(File);
        if (bytes.Length > 0)
        {
            TryAddGlyphTypeface(new MemoryStream(bytes), out _);
        }
    }

    /// <summary>The bytes of a file of the site, or none when it has no such file.</summary>
    [JSImport("file", "arc-lazy")]
    private static partial byte[] Fetch(string path);
}

public static class CjkFontAppBuilderExtensions
{
    /// <summary>
    /// Adds the Chinese font as the fallback for Han and CJK punctuation: text in Inter or Geist that has no glyph for a
    /// character asks it. Pair it with <c>WithArcFonts()</c>.
    /// </summary>
    public static AppBuilder WithCjkFont(this AppBuilder builder) => builder
        .ConfigureFonts(fontManager => fontManager.AddFontCollection(new CjkFontCollection()))
        .With(new FontManagerOptions
        {
            FontFallbacks =
            [
                new FontFallback
                {
                    FontFamily = new FontFamily(CjkFontCollection.Family),
                    UnicodeRange = UnicodeRange.Parse("U+2010-2027,U+2030-205E,U+2E80-2FDF,U+3000-303F,U+3400-4DBF,U+4E00-9FFF,U+F900-FAFF,U+FF00-FFEF")
                }
            ]
        });
}
