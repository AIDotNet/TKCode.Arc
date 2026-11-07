using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc.DocShots;

/// <summary>
/// <c>mobile [out=&lt;dir&gt;] [dark] [&lt;ClassName&gt;...]</c>: the mobile audit (design/engineering/gallery-mobile-preview.md,
/// section 6.3). Shows each component page's Preview at its Mobile size (a 390 × 844 viewport), screenshots the phone and
/// checks what can be checked mechanically: content past the viewport's edges, text cut off without an ellipsis, inner
/// horizontal scrolling, widths the demo's code fixes above the room it gets, and demos taller than the screen. Writes
/// <c>report.md</c> and the screenshots to the output folder (default <c>.work/mobile-audit</c>).
/// </summary>
internal static partial class MobileAudit
{
    /// <summary>Width the demo gets on the phone: the viewport less the screen's padding.</summary>
    private const double Room = 390 - 2 * 16;

    // Components the design already classes as desktop-only (level E): the audit still runs them, the level is fixed.
    private static readonly HashSet<string> s_desktopOnly = new(StringComparer.Ordinal)
    {
        "DataGrid", "TreeTable", "Menubar", "HoverCard", "DockLayout", "GanttTimeline", "PropertyGrid"
    };

    public static int Run(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault(a => a.StartsWith("out=", StringComparison.Ordinal))?["out=".Length..]
                                      ?? Path.Combine(".work", "mobile-audit"));
        var dark = args.Contains("dark");
        var only = args.Where(a => !a.StartsWith("out=", StringComparison.Ordinal) && a != "dark").ToHashSet(StringComparer.Ordinal);
        Directory.CreateDirectory(output);

        ArcLanguage.Set("en-US");
        Shooter.ApplyTheme(dark);
        var window = new Window { Width = 1000, Height = 1000 };
        ArcMotion.SetLevel(window, ArcMotionLevel.Off);
        window.Show();

        var results = new List<Result>();
        var entries = GalleryCatalog.AllEntries
                                    .Where(e => GalleryDocs.HasDemos(e.ClassName))
                                    .Where(e => only.Count == 0 || only.Contains(e.ClassName))
                                    .OrderBy(e => GalleryDocs.FolderOf(e.ClassName), StringComparer.Ordinal)
                                    .ToList();
        foreach (var entry in entries)
        {
            var folder = GalleryDocs.FolderOf(entry.ClassName) ?? entry.ClassName;
            if (folder.StartsWith(MobileCatalog.Folder + "/", StringComparison.Ordinal))
            {
                continue;
            }
            var result = new Result(entry.ClassName, folder, GalleryDocs.PackageOf(entry.ClassName) ?? "?");
            results.Add(result);
            try
            {
                Audit(window, entry.ClassName, result, Path.Combine(output, folder + (dark ? ".dark" : string.Empty) + ".png"));
            }
            catch (Exception e)
            {
                result.Failure = $"{e.GetType().Name}: {e.Message}";
            }
            Console.WriteLine($"{folder}: {result.Level}{(result.Failure is null ? string.Empty : " (" + result.Failure + ")")}");
        }
        window.Close();

        var report = Path.Combine(output, "report.md");
        File.WriteAllText(report, Report(results, dark));
        Console.WriteLine($"audited {results.Count} components; report {report}");
        return 0;
    }

    private static void Audit(Window window, string className, Result result, string shot)
    {
        var heroId = GalleryDocs.DemoIds(className, "en-US").FirstOrDefault() ?? throw new InvalidOperationException("no demo");
        result.Demo = heroId;
        var view = new GalleryDemoView(heroId, hero: true);
        window.Content = new Border { Width = 880, HorizontalAlignment = HorizontalAlignment.Center, Child = view };
        Program.Pump(4);
        if (view.SizeSwitch is null || view.View is not { } demo)
        {
            throw new InvalidOperationException("the Preview has no Mobile size");
        }
        view.ShowMobile(true);
        Program.Pump(8);
        var phone = view.Phone!;
        var viewport = phone.Viewport;

        CheckOverflow(demo, viewport, result);
        CheckCutOff(demo, viewport, result);
        CheckText(demo, result);
        CheckScrolling(demo, result);
        CheckFixedWidths(view.Demo!, result);
        result.Height = demo.Bounds.Height;

        Directory.CreateDirectory(Path.GetDirectoryName(shot)!);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame");
        var rect = new Rect(phone.Bounds.Size).TransformToAABB(phone.TransformToVisual(window)!.Value);
        Crop(frame, rect).Save(shot);
        result.Shot = shot;
        window.Content = null;
    }

    /// <summary>Elements reaching past the viewport's left or right edge, unless an ancestor inside it clips them.</summary>
    private static void CheckOverflow(Control demo, Visual viewport, Result result)
    {
        var flagged = new List<Visual>();
        foreach (var visual in demo.GetVisualDescendants().Prepend(demo))
        {
            if (!visual.IsEffectivelyVisible || visual.Bounds.Width <= 0 || flagged.Any(f => f.IsVisualAncestorOf(visual)))
            {
                continue;
            }
            if (visual.TransformToVisual(viewport) is not { } transform)
            {
                continue;
            }
            var rect = new Rect(visual.Bounds.Size).TransformToAABB(transform);
            if (rect.Right <= viewport.Bounds.Width + 0.5 && rect.Left >= -0.5)
            {
                continue;
            }
            if (ClippedInside(visual, demo, viewport) || Hidden(visual, demo))
            {
                continue;
            }
            flagged.Add(visual);
            result.Overflow.Add($"{Name(visual)} {rect.Left:0}–{rect.Right:0}");
        }
    }

    // Only the demo's own clipping counts: the phone's screen and scroller clip everything, which is what is being looked for.
    private static bool ClippedInside(Visual visual, Visual demo, Visual viewport)
    {
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ReferenceEquals(ancestor, demo.GetVisualParent()))
            {
                return false;
            }
            if (ancestor.ClipToBounds && ancestor.TransformToVisual(viewport) is { } transform)
            {
                var rect = new Rect(ancestor.Bounds.Size).TransformToAABB(transform);
                if (rect.Left >= -0.5 && rect.Right <= viewport.Bounds.Width + 0.5)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Elements reaching more than a few pixels past the nearest ancestor that clips them, when that ancestor does not
    /// scroll: the end is cut off and cannot be reached (a table header past its rounded frame).
    /// </summary>
    private static void CheckCutOff(Control demo, Visual viewport, Result result)
    {
        var flagged = new List<Visual>();
        foreach (var visual in demo.GetVisualDescendants())
        {
            if (visual is not Control { IsEffectivelyVisible: true } control || control.Bounds.Width < 8 ||
                flagged.Any(f => f.IsVisualAncestorOf(visual)) || Hidden(visual, demo))
            {
                continue;
            }
            var clip = visual.GetVisualAncestors().TakeWhile(a => !ReferenceEquals(a, demo.GetVisualParent())).FirstOrDefault(a => a.ClipToBounds);
            // Content past a scrolling viewport is reachable by scrolling; that is the scrolling check's business.
            if (clip is null or ScrollContentPresenter or ScrollViewer)
            {
                continue;
            }
            if (visual.TransformToVisual(clip) is not { } transform)
            {
                continue;
            }
            var rect = new Rect(visual.Bounds.Size).TransformToAABB(transform);
            var past = Math.Max(rect.Right - clip.Bounds.Width, -rect.Left);
            if (past <= 4)
            {
                continue;
            }
            flagged.Add(visual);
            result.CutOff.Add($"{Name(visual)} 超出 {Name(clip)} {past:0}");
        }
    }

    /// <summary>
    /// Whether <paramref name="visual"/> sits in a clipping container of no size (a list kept only for screen readers,
    /// a collapsed panel): nothing of it is on screen, so nothing of it can overflow or be cut.
    /// </summary>
    private static bool Hidden(Visual visual, Visual demo)
    {
        // Fully transparent (labels that wait for their moment, faded-out states) is not on screen either.
        if (visual.Opacity <= 0)
        {
            return true;
        }
        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (ReferenceEquals(ancestor, demo.GetVisualParent()))
            {
                return false;
            }
            if (ancestor.Opacity <= 0 || (ancestor.ClipToBounds && (ancestor.Bounds.Width < 1 || ancestor.Bounds.Height < 1)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Single-line text wider than its box with no ellipsis: the end of the text is simply cut off.</summary>
    private static void CheckText(Control demo, Result result)
    {
        foreach (var text in demo.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!text.IsEffectivelyVisible || string.IsNullOrEmpty(text.Text) || text.TextWrapping != TextWrapping.NoWrap ||
                text.TextTrimming != TextTrimming.None || text.Bounds.Width <= 1 || Hidden(text, demo))
            {
                continue;
            }
            var needed = text.TextLayout.WidthIncludingTrailingWhitespace + text.Padding.Left + text.Padding.Right;
            if (needed > text.Bounds.Width + 1)
            {
                result.Clipped.Add($"\"{Short(text.Text)}\" {needed:0}>{text.Bounds.Width:0}");
            }
        }
    }

    /// <summary>Inner horizontal scrolling (wide tables, tab strips): acceptable for desktop-only components, noted for the rest.</summary>
    private static void CheckScrolling(Control demo, Result result)
    {
        foreach (var scroll in demo.GetVisualDescendants().OfType<ScrollViewer>())
        {
            if (scroll.IsEffectivelyVisible && scroll.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled &&
                scroll.Extent.Width > scroll.Viewport.Width + 1)
            {
                result.Scrolling.Add($"{Name(scroll)} {scroll.Extent.Width:0}/{scroll.Viewport.Width:0}");
            }
        }
    }

    /// <summary>Widths the demo's own code sets above the room the phone gives it.</summary>
    private static void CheckFixedWidths(GalleryDemo demo, Result result)
    {
        foreach (var file in demo.Files)
        {
            foreach (Match match in WidthPattern().Matches(file.Source))
            {
                if (double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var width) && width > Room)
                {
                    result.FixedWidths.Add($"{match.Groups[1].Value}Width={width:0}");
                }
            }
        }
    }

    [GeneratedRegex("""\b(Min)?Width\s*=\s*"?(\d+(?:\.\d+)?)""")]
    private static partial Regex WidthPattern();

    private static string Name(Visual visual)
        => visual is Control { Name: { Length: > 0 } name } ? $"{visual.GetType().Name}#{name}" : visual.GetType().Name;

    private static string Short(string text) => text.Length <= 24 ? text : text[..23] + "…";

    private static Bitmap Crop(Bitmap frame, Rect rect)
    {
        var scale = frame.PixelSize.Width / Math.Max(1, frame.Size.Width);
        var pixels = new PixelRect((int)(rect.X * scale), (int)(rect.Y * scale), (int)(rect.Width * scale), (int)(rect.Height * scale))
            .Intersect(new PixelRect(frame.PixelSize));
        var target = new RenderTargetBitmap(pixels.Size, frame.Dpi);
        using (var context = target.CreateDrawingContext())
        {
            context.DrawImage(frame, new Rect(pixels.X / scale, pixels.Y / scale, pixels.Width / scale, pixels.Height / scale),
                              new Rect(0, 0, pixels.Width / scale, pixels.Height / scale));
        }
        return target;
    }

    private static string Report(List<Result> results, bool dark)
    {
        var text = new StringBuilder();
        text.AppendLine("# 移动端审计报告");
        text.AppendLine();
        text.AppendLine($"- 生成：{DateTime.Now:yyyy-MM-dd HH:mm}，`DocShots.dll mobile`{(dark ? "（暗色）" : string.Empty)}");
        text.AppendLine($"- 视口 390 × 844，示例可用宽度 {Room:0}；每页 Preview 示例；规则见 design/engineering/gallery-mobile-preview.md 第 6.2、6.3 节。");
        text.AppendLine("- 级别是机械判断的建议：A 无问题；B 有溢出、被裁切、文字截断或写死的宽度；E 桌面专属（固定名单）；C、D 需要人工判断（换结构、浮层形态），不在这里自动给出。");
        text.AppendLine();
        foreach (var group in results.GroupBy(r => r.Level).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            text.AppendLine($"- {group.Key}：{group.Count()} 个");
        }
        text.AppendLine();
        text.AppendLine("| 组件 | 目录 | 包 | 建议 | 横向溢出 | 被容器裁切 | 文字截断 | 内部横向滚动 | 写死的宽度 | 高度 | 截图 |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results.OrderBy(r => r.Level, StringComparer.Ordinal).ThenBy(r => r.Folder, StringComparer.Ordinal))
        {
            var shot = r.Shot is null ? "–" : $"[png]({r.Folder}{(dark ? ".dark" : string.Empty)}.png)";
            var height = r.Height > 844 - 32 ? $"{r.Height:0}（需滚动）" : $"{r.Height:0}";
            text.AppendLine($"| {r.ClassName} | {r.Folder} | {r.Package} | {r.Level} | {Cell(r.Overflow)} | {Cell(r.CutOff)} | {Cell(r.Clipped)} | {Cell(r.Scrolling)} | {Cell(r.FixedWidths)} | {(r.Failure is null ? height : "失败：" + r.Failure.Replace("|", "/"))} | {shot} |");
        }
        return text.ToString();
    }

    private static string Cell(List<string> items)
        => items.Count == 0 ? "–" : string.Join("<br>", items.Distinct().Take(4).Select(i => i.Replace("|", "/"))) + (items.Distinct().Count() > 4 ? $"<br>…共 {items.Distinct().Count()} 处" : string.Empty);

    private sealed class Result(string className, string folder, string package)
    {
        public string ClassName { get; } = className;
        public string Folder { get; } = folder;
        public string Package { get; } = package;
        public string? Demo { get; set; }
        public string? Shot { get; set; }
        public string? Failure { get; set; }
        public double Height { get; set; }
        public List<string> Overflow { get; } = [];
        public List<string> Clipped { get; } = [];
        public List<string> CutOff { get; } = [];
        public List<string> Scrolling { get; } = [];
        public List<string> FixedWidths { get; } = [];

        public string Level
            => Failure is not null ? "?"
                : s_desktopOnly.Contains(ClassName) ? "E"
                : Overflow.Count > 0 || CutOff.Count > 0 || Clipped.Count > 0 || FixedWidths.Count > 0 ? "B"
                : "A";
    }
}
