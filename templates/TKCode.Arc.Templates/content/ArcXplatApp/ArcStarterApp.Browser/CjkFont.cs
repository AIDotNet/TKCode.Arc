using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace ArcStarterApp.Browser;

/// <summary>
/// Chinese text in the browser. The canvas the app draws on has no system fonts, so the app brings Noto Sans SC
/// (Assets/Fonts, SIL Open Font License) and uses it for every character Inter does not have.
/// </summary>
internal static class CjkFont
{
    private const string Family = "fonts:AppCjk#Noto Sans SC";

    public static AppBuilder WithCjkFont(this AppBuilder builder) =>
        builder.ConfigureFonts(fonts => fonts.AddFontCollection(
                   new EmbeddedFontCollection(new Uri("fonts:AppCjk"), new Uri("avares://ArcStarterApp.Browser/Assets/Fonts"))))
               .With(new FontManagerOptions
               {
                   FontFallbacks =
                   [
                       new FontFallback
                       {
                           FontFamily = new FontFamily(Family),
                           // Han characters, CJK and full-width punctuation, general punctuation.
                           UnicodeRange = UnicodeRange.Parse(
                               "U+2010-2027,U+2030-205E,U+2E80-2FDF,U+3000-303F,U+3400-4DBF,U+4E00-9FFF,U+F900-FAFF,U+FF00-FFEF")
                       }
                   ]
               });
}
