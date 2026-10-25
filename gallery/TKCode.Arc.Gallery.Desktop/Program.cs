using System.Globalization;
using AtomUI;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Content;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Gallery.Shots;
using AtomUI.Desktop.Controls;
using Avalonia;

namespace TKCode.Arc.Gallery.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        GalleryLogos.Source = new AssetLogoSource();
        // The language chosen in the top bar is kept for the next start; until there is one, the system's language decides.
        GalleryLanguages.Store = new FileLanguageStore();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .WithArcFonts()
                  .WithArcPixelFonts()
                  .WithGalleryShots()
                  .WithGalleryContent()
                  .UseAtomUI(builder =>
                  {
                      // The system's display language (on macOS the first preferred language with the region).
                      builder.UseLanguages(GalleryLanguages.Initial([CultureInfo.CurrentUICulture.Name]), GalleryLanguages.Tags);
                      builder.UseDesktopControls();
                      builder.UseArc();
                      builder.UseArcGallery();
                  });
}
