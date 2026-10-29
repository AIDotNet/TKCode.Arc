using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia;
using TKCode.Arc.Themes.Corners;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Localization;

namespace TKCode.Arc.DocShots;

/// <summary>
/// <c>page &lt;ClassName&gt; &lt;language&gt; &lt;out.png&gt; [dark] [corners=square|half] [scroll=&lt;y&gt;|find=&lt;text&gt;]</c>: the component's
/// page in the gallery window as people see it, to review a page or how its screenshots sit in it.
/// </summary>
internal static class PageShot
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: DocShots page <ClassName> <language> <out.png> [dark] [corners=square|half] [scroll=<y>|find=<text>]");
            return 2;
        }
        var (className, language, output) = (args[0], args[1], Path.GetFullPath(args[2]));
        var options = args.Skip(3).ToArray();
        var entry = GalleryCatalog.AllEntries.FirstOrDefault(e => e.ClassName == className);
        if (entry is null)
        {
            Console.Error.WriteLine($"no gallery entry {className}");
            return 1;
        }

        ArcLanguage.Set(language);
        var corners = options.Contains("corners=square") ? ArcCornerStyle.Square
            : options.Contains("corners=half") ? ArcCornerStyle.Scale(0.5) : ArcCornerStyle.Default;
        Shooter.ApplyTheme(options.Contains("dark"), corners);
        var window = new GalleryWindow { Width = 1480, Height = 1400 };
        window.Show();
        window.Select(entry);
        for (var i = 0; i < 80; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(25);
            foreach (var view in window.GetVisualDescendants().OfType<MarkdownView>())
            {
                view.ApplyLatest();
            }
        }

        // A component page is a GalleryPageView; a Docs page is its Markdown in a ScrollViewer (DocumentPage).
        Visual page = (Visual?)window.GetVisualDescendants().OfType<GalleryPageView>().FirstOrDefault()
                      ?? window.GetVisualDescendants().OfType<Markdown>().First().FindAncestorOfType<ScrollViewer>()!;
        var scroller = page as ScrollViewer ?? page.GetVisualDescendants().OfType<ScrollViewer>().First();
        var offset = 0.0;
        foreach (var option in options)
        {
            if (option.StartsWith("scroll=", StringComparison.Ordinal))
            {
                offset = double.Parse(option["scroll=".Length..], System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (option.StartsWith("find=", StringComparison.Ordinal))
            {
                var text = option["find=".Length..];
                var target = page.GetVisualDescendants().OfType<Control>()
                                 .FirstOrDefault(c => c is GalleryShotView shot && shot.Source.EndsWith(text, StringComparison.Ordinal));
                if (target?.TranslatePoint(new Point(0, 0), (Visual)scroller.Content!) is { } point)
                {
                    offset = Math.Max(0, point.Y - 120);
                }
            }
        }
        scroller.Offset = new Vector(0, offset);
        Program.Pump(20);
        window.CaptureRenderedFrame()!.Save(output, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        Console.WriteLine($"saved {output} (page height {scroller.Extent.Height:0}, offset {offset:0})");
        foreach (var shot in page.GetVisualDescendants().OfType<GalleryShotView>())
        {
            Console.WriteLine($"shot {shot.Source}: {(shot.Bitmap is null ? "MISSING" : $"{shot.Bitmap.PixelSize}")}{(shot.IsDarkShown ? " dark" : string.Empty)} bounds {shot.Bounds.Size}");
        }
        window.Close();
        return 0;
    }
}
