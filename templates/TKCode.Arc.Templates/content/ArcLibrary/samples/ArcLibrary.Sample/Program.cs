using Avalonia;
using TKCode.Arc;
using TKCode.Arc.Fonts;

namespace ArcLibrary.Sample;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer of your IDE.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .WithArcFonts()
                  // The library registers Arc's controls too (ArcLibraryIdentifierBuilderExtensions.cs).
                  .UseArc(arc => arc.UseArcLibraryIdentifier()
                                    .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Neutral)))
                  .LogToTrace();
}
