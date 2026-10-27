using System.Threading.Tasks;
using AtomUI;
using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Browser;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.Gallery.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        // A publish without trimming leaves assemblies out of the boot list (the project file) and fetches them when the runtime asks.
        if (AppContext.TryGetSwitch("Arc.Gallery.LazyAssemblies", out var lazyAssemblies) && lazyAssemblies)
        {
            LazyAssemblies.Install();
        }
        // The logos of the Friends page are files of the site (wwwroot/friends); a desktop host has none and shows letters.
        if (args.Length > 1)
        {
            GalleryLogos.Source = new HttpLogoSource(new Uri(args[1]));
        }
        // A published site has no screenshots assembly (the project file leaves it out) and serves the pictures as files.
        if (AppContext.TryGetSwitch("Arc.Gallery.ShotsOverHttp", out var shotsOverHttp) && shotsOverHttp && args.Length > 1)
        {
            GalleryShots.Source = new HttpShotSource(new Uri(args[1]));
        }
        // Nor the pages' text (the content assembly): a page is fetched when it is shown, in the language it is shown in.
        if (AppContext.TryGetSwitch("Arc.Gallery.TextsOverHttp", out var textsOverHttp) && textsOverHttp && args.Length > 1)
        {
            GalleryTexts.Source = new HttpTextSource(new Uri(args[1]));
            ChoroplethExplorerDemoData.ReadMap = new HttpMapSource(new Uri(args[1])).Read;
        }
        // The language chosen in the top bar is kept in local storage; until there is one, the browser's languages decide.
        GalleryLanguages.Store = new BrowserLanguageStore();
        return BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .WithInterFont()
                  .WithArcFonts()
                  .WithArcPixelFonts()
                  .WithCjkFont()
                  .UseAtomUI(builder =>
                  {
                      builder.UseLanguages(GalleryLanguages.Initial(BrowserLanguageStore.Preferred()), GalleryLanguages.Tags);
                      builder.UseDesktopControls();
                      builder.UseArc();
                      builder.UseArcGallery();
                  });
}
