using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;
using TKCode.Arc.Gallery.Demos;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Gallery.Browser;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
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
            // The video demos' clip is served next to the screenshots (wwwroot/media/Video/).
            TKCode.Arc.Gallery.Demos.VideoDemoMedia.HttpRoot = new Uri(args[1]);
            // So are the PDF demos' sample documents (wwwroot/media/Pdf/).
            PdfDemoMedia.HttpRoot = new Uri(args[1]);
        }
        // Nor the pages' text (the content assembly): a page is fetched when it is shown, in the language it is shown in.
        if (AppContext.TryGetSwitch("Arc.Gallery.TextsOverHttp", out var textsOverHttp) && textsOverHttp && args.Length > 1)
        {
            GalleryTexts.Source = new HttpTextSource(new Uri(args[1]));
            ChoroplethExplorerDemoData.ReadMap = new HttpMapSource(new Uri(args[1])).Read;
        }
        // A trimmed publish leaves the Special components out of the boot list (the project file): the first Special page loads them.
        if (AppContext.TryGetSwitch("Arc.Gallery.SpecialOnDemand", out var specialOnDemand) && specialOnDemand)
        {
            s_onDemand = new BrowserAssemblyLoader();
            s_onDemand.InstallFallback();
        }
        // The language chosen in the top bar is kept in local storage; until there is one, the browser's languages decide.
        GalleryLanguages.Store = new BrowserLanguageStore();
        // Media players make sound through an HTML audio element each, and video players draw the frames of one HTML video element
        // each into the canvas; if the modules cannot load they keep time silently and show a test picture. Only the pages with a
        // player import them (the imports held back the first frame of every visit).
        GalleryModules.MediaBackend = static async () =>
        {
            var audio = await ArcAudioBrowser.UseAsync();
            var video = await ArcVideoBrowser.UseAsync();
            return audio && video;
        };
        // The PDF page imports pdf.js (about 110 KB compressed, and a worker of 310 KB when a document opens) before it builds its demos: other visits never fetch it.
        GalleryModules.PdfBackend = static () => ArcPdfBrowser.UseAsync();
        // A visitor who reads Chinese needs the font for the first frame: fetched here, the page keeps breathing meanwhile; asked
        // for by the first text layout, the request would stop it.
        var language = GalleryLanguages.Initial(BrowserLanguageStore.Preferred());
        if (language == LanguageTags.ZhCN || language == LanguageTags.ZhTW)
        {
            await CjkFontCollection.PreloadAsync();
        }
        // The sidebar is built in small steps after the first frame, so the first click on a component does not pay for it.
        GalleryShell.PreparesSidebarWhenIdle = true;
        await BuildAvaloniaApp().StartBrowserAppAsync("out");
    }

    private static BrowserAssemblyLoader? s_onDemand;

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .WithInterFont()
                  .WithArcFonts()
                  .WithArcPixelFonts()
                  .WithCjkFont()
                  .UseArc(arc => arc
                      .UseLanguages(GalleryLanguages.Initial(BrowserLanguageStore.Preferred()), GalleryLanguages.Tags)
                      .UseArcControls()
                      .UseArcGallery(s_onDemand));
}
