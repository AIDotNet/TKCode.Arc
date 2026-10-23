using System.Text.RegularExpressions;
using AtomUI;
using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using SkiaSharp;
using ArcWindow = AtomUI.Desktop.Controls.Window;

namespace TKCode.Arc.DocShots;

/// <summary>The XAML block a screenshot is captured from.</summary>
internal static partial class Scene
{
    /// <summary>The scene width when its root sets none: a form or a card at a comfortable reading width.</summary>
    public const double DefaultWidth = 640;

    /// <summary>The room around the scene, in the page background color.</summary>
    public const double Padding = 24;

    // The same namespaces as DocSnippets, so a block that passes the check loads here as well.
    private const string Wrapper =
        "<StackPanel xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
        "xmlns:arc=\"https://github.com/AIDotNet/TKCode.Arc\" " +
        "xmlns:ill=\"https://github.com/AIDotNet/TKCode.Arc/illustrations\">{0}</StackPanel>";

    /// <summary>The <c>Width</c> on the block's first element, or <see cref="DefaultWidth"/>.</summary>
    public static double WidthOf(string xaml)
        => RootWidth().Match(FirstTag(xaml)) is { Success: true } match &&
           double.TryParse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var width)
            ? width
            : DefaultWidth;

    public static Control Load(string xaml)
    {
        var declaresNamespace = FirstTag(xaml).Contains("xmlns", StringComparison.Ordinal);
        return (Control)AvaloniaRuntimeXamlLoader.Load(declaresNamespace ? xaml : string.Format(Wrapper, xaml));
    }

    private static string FirstTag(string xaml)
    {
        var text = xaml.TrimStart();
        while (text.StartsWith("<!--", StringComparison.Ordinal) && text.IndexOf("-->", StringComparison.Ordinal) is var end and >= 0)
        {
            text = text[(end + 3)..].TrimStart();
        }
        var close = text.IndexOf('>');
        return close < 0 ? text : text[..close];
    }

    [GeneratedRegex(@"\sWidth=""([0-9.]+)""")]
    private static partial Regex RootWidth();
}

/// <summary><c>shoot &lt;en-US.md&gt;...</c>: captures each page's screenshots in the light and the dark theme.</summary>
internal static class Shooter
{
    private const double Scale = 2;

    /// <summary>Frames (about 16 ms each) a scene runs before it is captured.</summary>
    private const int SettleFrames = 150;

    public static int Run(string[] files)
    {
        var pages = new List<DocFile>();
        foreach (var file in files)
        {
            var page = DocFile.Load(file);
            if (!page.IsSource)
            {
                Console.WriteLine($"skip {file}: screenshots are captured from en-US.md (translations show the same pictures)");
                continue;
            }
            pages.Add(page);
        }

        var failures = 0;
        foreach (var dark in new[] { false, true })
        {
            ApplyTheme(dark);
            foreach (var page in pages)
            {
                foreach (var shot in page.Screenshots)
                {
                    if (shot.Scene is null)
                    {
                        Console.WriteLine($"FAIL {page.Path}:{shot.Line} {shot.Name}: no xml block under the image");
                        failures++;
                        continue;
                    }
                    var path = Path.Combine(page.ImagesFolder, $"{shot.Name}{(dark ? ".dark" : string.Empty)}.webp");
                    try
                    {
                        var size = Capture(shot.Scene.Code, path);
                        Console.WriteLine($"ok   {Relative(path)} ({size.Width}x{size.Height})");
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine($"FAIL {page.Path}:{shot.Scene.Line} {shot.Name}: {exception.GetBaseException().Message}");
                        failures++;
                    }
                }
            }
        }

        foreach (var page in pages)
        {
            RemoveStale(page);
        }
        if (s_builtInEncoder > 0)
        {
            Console.WriteLine($"WARN {s_builtInEncoder} capture(s) were written with SkiaSharp's WebP encoder, which makes files about as large as a PNG: " +
                              "install cwebp (brew install webp, apt install webp) and shoot again for files a quarter of the size");
        }
        Console.WriteLine(failures == 0 ? "ALL OK" : $"{failures} problem(s)");
        return failures == 0 ? 0 : 1;
    }

    private static PixelSize Capture(string xaml, string path)
    {
        var width = Scene.WidthOf(xaml);
        var content = Scene.Load(xaml);
        var host = new Border { Padding = new Thickness(Scene.Padding), Width = width + 2 * Scene.Padding, Child = content };
        ArcTokens.Bind(host, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        var window = new ArcWindow
        {
            Content = host,
            Width = width + 2 * Scene.Padding,
            SizeToContent = SizeToContent.Height,
            IsTitleBarVisible = false
        };
        // Captures show the settled state: entrances, counters and charts at their end values. Some of them animate in
        // the composition layer, which only the window's own rendered frame shows (a RenderTargetBitmap of the visual
        // tree catches them at their start), so the window renders at twice the scale and its frame is the capture.
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.SetRenderScaling(Scale);
        window.Show();
        try
        {
            Program.Pump(SettleFrames);
            var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("the window rendered no frame");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            SaveWebp(frame, path);
            return frame.PixelSize;
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Writes <paramref name="frame"/> as lossless WebP: the same pixels as a PNG at about a quarter of the size, which is why the
    /// captures are WebP (a page with many of them downloads about a quarter of the bytes). The compression is cwebp's
    /// (<c>-lossless -z 9</c>; brew install webp, apt install webp): SkiaSharp's own lossless encoder, which is the fallback when
    /// cwebp is not installed, makes files about as large as the PNG, so those are counted and reported at the end.
    /// </summary>
    private static void SaveWebp(WriteableBitmap frame, string path)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"docshots-{Guid.NewGuid():N}.png");
        try
        {
            // Avalonia encodes only PNG and JPEG: the pixels reach cwebp as a PNG, which loses nothing.
            frame.Save(temp, new PngBitmapEncoderOptions { CompressionLevel = System.IO.Compression.CompressionLevel.Fastest });
            if (RunCwebp(temp, path))
            {
                return;
            }
        }
        finally
        {
            File.Delete(temp);
        }
        SaveWebpWithSkia(frame, path);
        s_builtInEncoder++;
    }

    /// <summary>How many captures were written with SkiaSharp's encoder because cwebp was not found.</summary>
    private static int s_builtInEncoder;

    private static bool RunCwebp(string input, string output)
    {
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("cwebp", ["-quiet", "-lossless", "-z", "9", input, "-o", output])
            {
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = System.Diagnostics.Process.Start(start)!;
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"cwebp failed: {error.Trim()}");
            }
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static void SaveWebpWithSkia(WriteableBitmap frame, string path)
    {
        using var locked = frame.Lock();
        var color = locked.Format == PixelFormat.Rgba8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        var alpha = frame.AlphaFormat == AlphaFormat.Unpremul ? SKAlphaType.Unpremul : SKAlphaType.Premul;
        var info = new SKImageInfo(locked.Size.Width, locked.Size.Height, color, alpha);
        using var pixmap = new SKPixmap(info, locked.Address, locked.RowBytes);
        using var output = File.Create(path);
        using var stream = new SKManagedWStream(output);
        if (!pixmap.Encode(stream, new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100)))
        {
            throw new InvalidOperationException("SkiaSharp could not encode the frame as WebP");
        }
    }

    /// <summary>
    /// Deletes captures in the page's <c>images/</c> that no screenshot line names any more, and every PNG: captures used to be
    /// PNG files, and a page shoots its WebP files again.
    /// </summary>
    private static void RemoveStale(DocFile page)
    {
        if (!Directory.Exists(page.ImagesFolder))
        {
            return;
        }
        var wanted = page.Screenshots.SelectMany(s => new[] { $"{s.Name}.webp", $"{s.Name}.dark.webp" }).ToHashSet(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(page.ImagesFolder).Where(f => f.EndsWith(".png", StringComparison.Ordinal) || f.EndsWith(".webp", StringComparison.Ordinal)))
        {
            if (file.EndsWith(".png", StringComparison.Ordinal))
            {
                File.Delete(file);
                Console.WriteLine($"del  {Relative(file)} (a PNG capture; captures are WebP now)");
            }
            else if (!wanted.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
                Console.WriteLine($"del  {Relative(file)} (no longer shown)");
            }
        }
        if (Directory.GetFileSystemEntries(page.ImagesFolder).Length == 0)
        {
            Directory.Delete(page.ImagesFolder);
        }
    }

    public static void ApplyTheme(bool dark)
    {
        var builder = new ThemeConfigBuilder();
        builder = dark ? builder.WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark) : builder.WithAlgorithms(ThemeAlgorithm.Default);
        builder = builder.WithArc(arc => arc.Accent(ArcAccentPreset.Neutral));
        var manager = Application.Current!.GetThemeManager()!;
        var task = manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(), ThemeTransitionReason.UserRequest),
                                           CancellationToken.None);
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        task.GetAwaiter().GetResult();
        Program.Pump(5);
    }

    private static string Relative(string path)
    {
        var docs = path.Replace('\\', '/').LastIndexOf("/Docs/", StringComparison.Ordinal);
        return docs < 0 ? path : path[(docs + 1)..];
    }
}
