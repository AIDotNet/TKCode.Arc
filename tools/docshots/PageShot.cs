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
/// <c>page &lt;ClassName&gt; &lt;language&gt; &lt;out.png&gt; [dark] [mobile] [open] [fullscreen] [corners=square|half] [scroll=&lt;y&gt;|find=&lt;text&gt;]</c>: the
/// component's page in the gallery window as people see it, to review a page or how its screenshots sit in it; <c>mobile</c>
/// switches the page's Preview to its Mobile size first,
/// <c>fullscreen</c> opens its Fullscreen view.
/// </summary>
internal static class PageShot
{
    public static int Run(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: DocShots page <ClassName> <language> <out.png> [dark] [mobile] [open] [fullscreen] [corners=square|half] [scroll=<y>|find=<text>]");
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
        if (options.Contains("mobile"))
        {
            if (page.GetVisualDescendants().OfType<GalleryDemoView>().FirstOrDefault(v => v.SizeSwitch is not null) is not { } preview)
            {
                Console.Error.WriteLine($"{className}'s page has no Preview with a Mobile size");
                return 1;
            }
            preview.ShowMobile(true);
            Program.Pump(40);
        }
        if (options.FirstOrDefault(o => o.StartsWith("frames=", StringComparison.Ordinal)) is { } frames)
        {
            // frames=fit|mobile: switch the Preview's size and save the stage every few frames (out-0.png, out-1.png, ...),
            // to review the switch's motion.
            var preview = page.GetVisualDescendants().OfType<GalleryDemoView>().First(v => v.SizeSwitch is not null);
            var toMobile = frames == "frames=mobile";
            preview.ShowMobile(!toMobile);
            Program.Pump(60);
            var stage = preview.GetVisualDescendants().OfType<Border>().First(b => b.Child is Grid);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            preview.ShowMobile(toMobile);
            var shotAt = new[] { 1, 4, 8, 14, 22, 40 };
            for (var i = 0; i <= 40; i++)
            {
                Program.Pump(1);
                var phone = preview.Phone;
                var phoneShown = phone is not null && phone.GetVisualParent() is not null;
                Console.WriteLine($"{clock.ElapsedMilliseconds,5}ms outline={(phoneShown ? $"{phone!.CurrentSize.Width:0}x{phone.CurrentSize.Height:0} op={phone.GetVisualDescendants().OfType<Grid>().First().Opacity:0.00}" : "gone")} view={preview.View!.Opacity:0.00} stage={stage.Bounds.Height:0} ghost={preview.GetVisualDescendants().OfType<Image>().Count()}");
                if (!shotAt.Contains(i))
                {
                    continue;
                }
                using var frame = window.CaptureRenderedFrame()!;
                var rect = new Rect(stage.Bounds.Size).TransformToAABB(stage.TransformToVisual(window)!.Value);
                var file = Path.ChangeExtension(output, null) + $"-{Array.IndexOf(shotAt, i)}.png";
                var scale = frame.PixelSize.Width / frame.Size.Width;
                var pixels = new PixelRect((int)(rect.X * scale), (int)(rect.Y * scale), (int)(rect.Width * scale), (int)(rect.Height * scale)).Intersect(new PixelRect(frame.PixelSize));
                using var crop = new Avalonia.Media.Imaging.RenderTargetBitmap(pixels.Size, frame.Dpi);
                using (var context = crop.CreateDrawingContext())
                {
                    context.DrawImage(frame, new Rect(pixels.X / scale, pixels.Y / scale, pixels.Width / scale, pixels.Height / scale),
                                      new Rect(0, 0, pixels.Width / scale, pixels.Height / scale));
                }
                crop.Save(file);
            }
            Console.WriteLine($"saved frames {Path.ChangeExtension(output, null)}-0..11.png");
            window.Close();
            return 0;
        }
        if (options.Contains("open"))
        {
            // open: opens the first select, combobox, date or color picker in the Preview (after mobile, inside the phone).
            var preview = page.GetVisualDescendants().OfType<GalleryDemoView>().First(v => v.View is not null);
            var target = preview.View!.GetVisualDescendants().FirstOrDefault(v => v is Select or Combobox or DatePicker or ColorPicker);
            switch (target)
            {
                case Select select:
                    select.Open();
                    break;
                case Combobox combobox:
                    combobox.IsOpen = true;
                    break;
                case DatePicker date:
                    date.SetCurrentValue(DatePicker.IsDropDownOpenProperty, true);
                    break;
                case ColorPicker color:
                    color.SetCurrentValue(ColorPicker.IsDropDownOpenProperty, true);
                    break;
                default:
                    Console.Error.WriteLine("the Preview has no select, combobox, date or color picker");
                    return 1;
            }
            Program.Pump(60);
        }
        if (options.Contains("fullscreen"))
        {
            if (page.GetVisualDescendants().OfType<GalleryDemoView>().FirstOrDefault(v => v.FullscreenButton is not null) is not { } preview)
            {
                Console.Error.WriteLine($"{className}'s page has no Preview with a Fullscreen button");
                return 1;
            }
            preview.OpenFullscreen();
            Program.Pump(60);
        }
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
