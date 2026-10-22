using System.Threading.Tasks;
using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Localization;
using Avalonia;
using Avalonia.Browser;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Docs;
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
        // A published site has no screenshots assembly (the project file leaves it out) and serves the pictures as files.
        if (AppContext.TryGetSwitch("Arc.Gallery.ShotsOverHttp", out var shotsOverHttp) && shotsOverHttp && args.Length > 1)
        {
            GalleryShots.Source = new HttpShotSource(new Uri(args[1]));
        }
        // Nor the pages' text (the content assembly): a page is fetched when it is shown, in the language it is shown in.
        if (AppContext.TryGetSwitch("Arc.Gallery.TextsOverHttp", out var textsOverHttp) && textsOverHttp && args.Length > 1)
        {
            GalleryTexts.Source = new HttpTextSource(new Uri(args[1]));
        }
        return BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .WithInterFont()
                  .WithArcFonts()
                  .WithCjkFont()
                  .UseAtomUI(builder =>
                  {
                      builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
                      builder.UseDesktopControls();
                      builder.UseArc();
                      builder.UseArcGallery();
                  });
}
