using AtomUI;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Content;
using TKCode.Arc.Gallery.Shots;
using AtomUI.Desktop.Controls;
using AtomUI.Localization;
using Avalonia;

namespace TKCode.Arc.Gallery.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()
                  .WithArcFonts()
                  .WithGalleryShots()
                  .WithGalleryContent()
                  .UseAtomUI(builder =>
                  {
                      builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
                      builder.UseDesktopControls();
                      builder.UseArc();
                      builder.UseArcGallery();
                  });
}
