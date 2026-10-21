using AtomUI;
using AtomUI.Desktop.Controls;
using AtomUI.Localization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using TKCode.Arc;
using TKCode.Arc.Fonts;
using TKCode.Arc.Gallery;

namespace TKCode.Arc.DocShots;

/// <summary>
/// Gallery docs tooling (design/engineering/gallery-app.md, 6.8).
/// <code>
///   dotnet tools/docshots/bin/Debug/net10.0/DocShots.dll check &lt;page.md&gt;...   check pages the way GalleryDocsTests does
///   dotnet tools/docshots/bin/Debug/net10.0/DocShots.dll shoot &lt;en-US.md&gt;...  capture every screenshot of the pages
///   dotnet tools/docshots/bin/Debug/net10.0/DocShots.dll page &lt;Class&gt; &lt;lang&gt; &lt;out.png&gt; [dark] [scroll=y|find=Name]
/// </code>
/// A screenshot is a <c>![caption](images/Name.png)</c> line followed by an <c>xml</c> code block: the block is loaded,
/// laid out at its root's <c>Width</c> (640 when it has none) and captured at twice the size in the light and the dark
/// theme, as <c>images/Name.png</c> and <c>images/Name.dark.png</c> next to the page.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var english = new System.Globalization.CultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = english;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = english;
        System.Globalization.CultureInfo.CurrentCulture = english;
        System.Globalization.CultureInfo.CurrentUICulture = english;

        if (args.Length < 2 || args[0] is not ("check" or "shoot" or "page"))
        {
            Console.Error.WriteLine("usage: DocShots check <page.md>... | DocShots shoot <en-US.md>... | DocShots page <Class> <lang> <out.png>");
            return 2;
        }

        AppBuilder.Configure<DocShotsApp>()
                  .UseSkia()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                  .WithInterFont()
                  .WithArcFonts()
                  .UseAtomUI(builder =>
                  {
                      builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN, LanguageTags.ZhTW]);
                      builder.UseDesktopControls();
                      builder.UseArc();
                      builder.UseArcGallery();
                  })
                  .SetupWithoutStarting();

        if (args[0] == "page")
        {
            return PageShot.Run(args.Skip(1).ToArray());
        }
        // The runtime XAML loader resolves namespaces from loaded assemblies; load the Lucide package the guides show.
        _ = typeof(TKCode.Arc.Icons.Lucide.LucideIcon).Assembly;
        var files = args.Skip(1).Select(Path.GetFullPath).ToArray();
        return args[0] == "check" ? DocsCheck.Run(files) : Shooter.Run(files);
    }

    /// <summary>Runs the dispatcher and the render timer for about <paramref name="frames"/> frames.</summary>
    public static void Pump(int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(16);
        }
    }
}

public sealed partial class DocShotsApp : Application
{
    public override void RegisterServices()
    {
        base.RegisterServices();
        ApplicationStartup.Attach(this);
    }
}
