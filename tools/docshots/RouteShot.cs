using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.DemoApp;
using TKCode.Arc.Gallery.Shell;

namespace TKCode.Arc.DocShots;

/// <summary>
/// <c>route &lt;address&gt; &lt;out.png&gt; [dark] [width=&lt;px&gt;] [height=&lt;px&gt;] [scroll=&lt;y&gt;] [signedout]</c>: the gallery at an address,
/// such as <c>/demo/overview</c> or <c>/friends</c>, as people see it; <c>scroll</c> moves the open demo page down and
/// <c>signedout</c> signs out of the demo first (its sign-in page).
/// </summary>
internal static class RouteShot
{
    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: DocShots route <address> <out.png> [dark] [width=<px>] [height=<px>] [scroll=<y>] [signedout]");
            return 2;
        }
        var (address, output) = (args[0], Path.GetFullPath(args[1]));
        var options = args.Skip(2).ToArray();
        if (GalleryRoute.Parse(address) is not { } route)
        {
            Console.Error.WriteLine($"no gallery page at {address}");
            return 1;
        }
        double Option(string name, double fallback) => options.FirstOrDefault(o => o.StartsWith(name + "=", StringComparison.Ordinal)) is { } value
            ? double.Parse(value[(name.Length + 1)..], System.Globalization.CultureInfo.InvariantCulture)
            : fallback;

        if (options.Contains("signedout"))
        {
            DemoSession.SignOut();
        }
        Shooter.ApplyTheme(options.Contains("dark"), TKCode.Arc.Themes.Corners.ArcCornerStyle.Default);
        var window = new GalleryWindow { Width = Option("width", 1440), Height = Option("height", 960) };
        window.Show();
        window.Shell.Navigate(route);
        Program.Pump(60);
        if (window.GetVisualDescendants().OfType<DemoPageView>().FirstOrDefault(page => page.IsEffectivelyVisible) is { } demoPage
            && demoPage.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroller)
        {
            Console.WriteLine($"opened at offset {scroller.Offset.Y:0}");
            if (options.Any(o => o.StartsWith("scroll=", StringComparison.Ordinal)))
            {
                scroller.Offset = new Vector(0, Option("scroll", 0));
            }
            Program.Pump(20);
            Console.WriteLine($"page height {scroller.Extent.Height:0}");
        }
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(output, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        Console.WriteLine($"saved {output} at {window.Shell.Route.Path}");
        window.Close();
        return 0;
    }
}
