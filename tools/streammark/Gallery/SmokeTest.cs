using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Transcript;
using StreamMark.Core.Model;
using StreamMark.Core.Pacing;
using StreamMark.Core.Streaming;
using StreamMark.RaTeX;

namespace StreamMark.Gallery;

/// <summary>
/// NativeAOT 冒烟测试：在发布后的原生二进制里跑一遍关键路径（RaTeX 原生调用 + JSON 解析等），
/// 任何一步失败返回非零退出码。
/// </summary>
internal static class SmokeTest
{
    public static int Run(string[] args)
    {
        var failures = 0;
        void Check(string name, bool ok, string? detail = null)
        {
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}{(detail is null ? "" : " — " + detail)}");
            if (!ok)
            {
                failures++;
            }
        }

        var math = RaTeXEngine.Layout(@"\ce{H2SO4 + 2NaOH -> Na2SO4 + 2H2O}", displayMode: true);
        Check("ratex.layout", math.IsSuccess && math.DisplayList!.Items.Count > 0, math.Error ?? $"{math.DisplayList!.Items.Count} items, width {math.DisplayList.Width:F2}em");

        var broken = RaTeXEngine.Layout(@"\frac{1}{", displayMode: false);
        Check("ratex.error", !broken.IsSuccess && !string.IsNullOrEmpty(broken.Error), broken.Error);

        // 流式模拟：节奏控制器 → 流式文档 → 差异，验证 Core 在 AOT 下的完整路径
        const string markdown = "# Title\n\nSome **bold** text and \\(x^2\\).\n\n```csharp\nvar x = 1;\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n";
        var document = new StreamingDocument();
        var pacer = new StreamPacer();
        var now = TimeSpan.Zero;
        for (var i = 0; i < markdown.Length; i += 5)
        {
            pacer.Push(markdown.AsSpan(i, System.Math.Min(5, markdown.Length - i)), now);
            now += TimeSpan.FromMilliseconds(10);
            document.Append(pacer.Tick(now));
        }
        pacer.Finish(now);
        while (!pacer.IsDrained)
        {
            now += TimeSpan.FromMilliseconds(16);
            document.Append(pacer.Tick(now));
        }
        document.Complete();
        document.WhenIdle().GetAwaiter().GetResult();
        var final = document.Latest;
        var kinds = string.Join(",", final.Blocks.Select(b => b.GetType().Name));
        Check("stream.complete", final.IsComplete && final.SourceLength == markdown.Length, kinds);
        Check("stream.blocks", final.Count == 5 && final[1] is ParagraphBlock { Content.Objects.Length: 1 } && final[2] is CodeBlock { Language: "csharp" } && final[3] is TableBlock, kinds);
        Check("stream.diff", SnapshotDiff.Compute(null, final).Count == final.Count);

        // 编辑器预览：整篇替换后的块对齐、任务标记的源码区间、源码偏移与行列换算
        var edited = new StreamingDocument();
        edited.Load("Intro.\n\n" + markdown);
        edited.WhenIdle().GetAwaiter().GetResult();
        var edit = SnapshotEdit.Compute(final, edited.Latest);
        Check("edit.align", edit.Moves.Count == final.Count && edit.Moves.All(m => m.Fate == BlockFate.Shifted) && edit.Ops.Count(o => o.Kind == DiffKind.Insert) == 1,
            string.Join(",", edit.Moves.Select(m => m.Fate)));
        var task = final[^1] is ListBlock { Items: [{ Task: true } first, ..] } ? first : null;
        Check("task.marker", task is not null && markdown.Substring(task.TaskMarker.Start, task.TaskMarker.Length) == "[x]");
        Check("source.location", SourceText.ToLocation(markdown, markdown.IndexOf("bold", StringComparison.Ordinal)) == new SourceLocation(2, 7));

        // 界面：在无头平台上用 Skia 真实渲染会话视图（公式、高亮、图表、表格），并截图
        try
        {
            RenderTranscript(args, Check);
        }
        catch (Exception ex)
        {
            Check("ui.render", false, ex.ToString());
        }

        Console.WriteLine(failures == 0 ? "SMOKE OK" : $"SMOKE FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    private static void RenderTranscript(string[] args, Action<string, bool, string?> check)
    {
        AppBuilder.Configure<GalleryApp>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var window = new GalleryWindow { Width = 1000, Height = 2400 };
        window.Show();
        var simulation = window.SimulateAsync(delayMs: 0);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var stream = window.Transcript.Entries.OfType<MarkdownEntry>().Last();
        while (DateTime.UtcNow < deadline && !(simulation.IsCompleted && stream.Document.Latest.IsComplete))
        {
            Pump();
        }
        check("ui.stream.complete", stream.Document.Latest.IsComplete, $"{stream.Document.Latest.Count} blocks");

        // 回到顶部看完整的静态演示
        window.Transcript.Offset = new Vector(0, 0);
        DiagramBlockView? diagram = null;
        while (DateTime.UtcNow < deadline)
        {
            Pump();
            diagram = window.GetVisualDescendants().OfType<DiagramBlockView>().FirstOrDefault();
            if (diagram is { IsShowingDiagram: true, Bounds.Height: > 80 })
            {
                break;
            }
        }
        var panel = window.Transcript.Panel;
        check("ui.virtualized", panel.RealizedCount > 0 && panel.RealizedCount < panel.RowCount, $"{panel.RealizedCount}/{panel.RowCount} rows realized");
        check("ui.math", window.GetVisualDescendants().OfType<MathBlockView>().Any(), null);
        check("ui.diagram", diagram is { IsShowingDiagram: true, Bounds.Height: > 80 }, diagram is null ? "no diagram view" : $"showing {diagram.IsShowingDiagram}, height {diagram.Bounds.Height:F0}");
        check("ui.code", window.GetVisualDescendants().OfType<CodeBlockView>().Any(), null);
        check("ui.components", window.GetVisualDescendants().OfType<StreamMark.Avalonia.Plugins.CollapsibleBlockView>().Any(), null);
        check("ui.html", window.GetVisualDescendants().OfType<StreamMark.Avalonia.Rendering.HtmlBoxBlockView>().Count() >= 2, null);

        using var frame = window.CaptureRenderedFrame();
        check("ui.frame", frame is not null, null);
        var output = args.FirstOrDefault(a => a.StartsWith("--out=", StringComparison.Ordinal))?["--out=".Length..];
        if (frame is not null && output is not null)
        {
            using var file = File.Create(output);
            frame.Save(file, new PngBitmapEncoderOptions());
            Console.WriteLine($"saved {output}");
        }
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(16);
    }
}
