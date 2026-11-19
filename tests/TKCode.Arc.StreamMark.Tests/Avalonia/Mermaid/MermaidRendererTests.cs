using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StreamMark.Avalonia.Extensions;
using StreamMark.Mermaid;
using Xunit;

namespace StreamMark.Avalonia.Tests.Mermaid;

/// <summary>测试用宿主：按控件尺寸调用 <see cref="DiagramResult.Draw"/>。</summary>
internal sealed class DiagramTestControl(DiagramResult result) : Control
{
    public override void Render(DrawingContext context) => result.Draw(context, new Rect(Bounds.Size));
}

public sealed class MermaidRendererTests(ITestOutputHelper output)
{
    public static readonly Dictionary<string, string> Samples = new()
    {
        ["flowchart"] = """
            flowchart TD
              A[Start] --> B{Is it valid?}
              B -->|Yes| C[Process 中文]
              B -->|No| D[(Database)]
              C --> E((Done))
              D -.-> E
              subgraph Pipeline
                C
                E
              end
            """,
        ["sequence"] = """
            sequenceDiagram
              participant U as User
              participant S as Server
              U->>S: POST /login
              S-->>U: 200 OK
              loop Heartbeat
                U-)S: ping
              end
              Note over U,S: TLS everywhere
            """,
        ["class"] = """
            classDiagram
              class Animal {
                +String name
                +eat() void
              }
              class Dog
              Animal <|-- Dog
              Animal *-- Leg
            """,
        ["state"] = """
            stateDiagram-v2
              [*] --> Idle
              Idle --> Running : start
              Running --> Idle : stop
              Running --> [*]
            """,
        ["pie"] = """
            pie title Pets
              "Dogs" : 386
              "Cats" : 85
              "Rats" : 15
            """,
        ["gantt"] = """
            gantt
              title Release
              dateFormat YYYY-MM-DD
              section Build
              Design :a1, 2024-01-01, 10d
              Implement :after a1, 20d
              section Ship
              Release :2024-02-05, 3d
            """,
    };

    public static TheoryData<string, bool> Cases()
    {
        var data = new TheoryData<string, bool>();
        foreach (var name in Samples.Keys)
        {
            data.Add(name, true);
            data.Add(name, false);
        }

        return data;
    }

    internal static DiagramRenderContext Context(bool dark) => dark
        ? new DiagramRenderContext(true, Color.Parse("#D4D4D4"), Color.Parse("#1E1E1E"), Color.Parse("#4F8CFF"), null, 14)
        : new DiagramRenderContext(false, Color.Parse("#24292F"), Colors.White, Color.Parse("#0969DA"), null, 14);

    [Theory]
    [InlineData("mermaid", true)]
    [InlineData("Mermaid", true)]
    [InlineData(" MERMAID ", true)]
    [InlineData("mmd", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void CanRender_IsCaseInsensitive(string? language, bool expected)
    {
        Assert.Equal(expected, new MermaidDiagramRenderer().CanRender(language));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ResolvedSvg_HasNoCssFunctionsLeft(string name, bool dark)
    {
        var svg = Mermaider.MermaidRenderer.RenderSvg(Samples[name], MermaidDiagramRenderer.CreateOptions(Context(dark)));
        Assert.Contains("color-mix(", svg); // 原始输出确实依赖 color-mix
        var resolved = MermaidDiagramRenderer.ResolveSvg(svg);
        Assert.DoesNotContain("color-mix(", resolved);
        Assert.DoesNotContain("var(", resolved);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Renders_Snapshot(string name, bool dark)
    {
        var context = Context(dark);
        string? error = null;
        var renderer = new MermaidDiagramRenderer { ErrorReported = (m, _) => error = m };
        using var result = await renderer.RenderAsync("mermaid", Samples[name], context, CancellationToken.None);
        Assert.True(result is not null, error);
        Assert.True(result.Size.Width > 20 && result.Size.Height > 20, $"size {result.Size}");

        var stats = await HeadlessApp.Session.Dispatch(() =>
        {
            const double scale = 1.5;
            var control = new DiagramTestControl(result) { Width = result.Size.Width * scale, Height = result.Size.Height * scale };
            var window = new Window
            {
                Content = control,
                Background = new SolidColorBrush(context.Background),
                SizeToContent = SizeToContent.WidthAndHeight,
            };
            window.Show();
            var snapshot = $"mermaid-{name}-{(dark ? "dark" : "light")}";
            HeadlessApp.Save(window, snapshot);
            using var frame = HeadlessApp.Capture(window);
            var s = PixelStats.Measure(frame, context.Background);
            window.Close();
            return Task.FromResult(s);
        }, CancellationToken.None);

        output.WriteLine($"{name} dark={dark}: {stats}");
        // 有实际内容。
        Assert.True(stats.NonBackgroundRatio > 0.02, $"almost nothing drawn: {stats}");
        // color-mix 未展开时节点填充/连线会变成纯黑：不允许出现大面积纯黑。
        Assert.True(stats.BlackRatio < 0.005, $"black fills detected: {stats}");
        // 文字 / 连线与背景的对比：至少有一部分像素接近前景色。
        Assert.True(stats.ForegroundLikeRatio > 0.001, $"no foreground-colored pixels (text/edges): {stats}");
    }

    [Fact]
    public async Task VectorDrawing_StaysCrispWhenScaled()
    {
        using var result = await new MermaidDiagramRenderer().RenderAsync("mermaid", Samples["flowchart"], Context(true), CancellationToken.None);
        Assert.NotNull(result);
        await HeadlessApp.Session.Dispatch(() =>
        {
            var control = new DiagramTestControl(result) { Width = result.Size.Width * 3, Height = result.Size.Height * 3 };
            var window = new Window { Content = control, Background = Brushes.Black, SizeToContent = SizeToContent.WidthAndHeight };
            window.Show();
            HeadlessApp.Save(window, "mermaid-flowchart-dark-3x");
            window.Close();
            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    [Theory]
    [InlineData("this is not mermaid")]
    [InlineData("")]
    [InlineData("   \n  ")]
    public async Task InvalidInput_ReturnsNull_AndReportsError(string code)
    {
        string? error = null;
        var renderer = new MermaidDiagramRenderer { ErrorReported = (m, _) => error = m };
        var result = await renderer.RenderAsync("mermaid", code, Context(true), CancellationToken.None);
        Assert.Null(result);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("graph TD\n  A[Start --> ")]
    [InlineData("sequenceDiagram\n  Alice->>Bob: Hi\n  Bob-->>")]
    [InlineData("classDiagram\n  class A {\n  +foo(")]
    public async Task PartialInput_DoesNotThrow(string code)
    {
        var result = await new MermaidDiagramRenderer().RenderAsync("mermaid", code, Context(false), CancellationToken.None);
        result?.Dispose();
    }

    [Fact]
    public async Task Cancellation_IsHonoured()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new MermaidDiagramRenderer().RenderAsync("mermaid", Samples["flowchart"], Context(true), cts.Token));
    }

    [Fact]
    public void Dispose_IsIdempotent_AndDrawAfterDisposeIsNoOp()
    {
        var result = new MermaidDiagramRenderer().Render(Samples["state"], Context(true));
        Assert.NotNull(result);
        result.Dispose();
        result.Dispose();
    }

    [Fact]
    public void Options_FollowTheme()
    {
        var options = MermaidDiagramRenderer.CreateOptions(new DiagramRenderContext(true, Colors.White, Color.Parse("#101010"), Color.Parse("#FF8800"), "avares://App/Assets#Inter, Noto Sans SC", 15));
        Assert.Equal("#ffffff", options.Fg);
        Assert.Equal("#101010", options.Bg);
        Assert.Equal("#ff8800", options.Accent);
        Assert.Equal("15px", options.FontSize);
        Assert.Equal("Inter", options.Font);
    }

    [Fact]
    public void Performance_TypicalFlowchart()
    {
        var renderer = new MermaidDiagramRenderer();
        var context = Context(true);
        renderer.Render(Samples["flowchart"], context)!.Dispose(); // 预热

        const int n = 50;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < n; i++)
        {
            renderer.Render(Samples["flowchart"], context)!.Dispose();
        }

        var total = sw.Elapsed.TotalMilliseconds / n;

        // 分阶段
        var options = MermaidDiagramRenderer.CreateOptions(context);
        sw.Restart();
        string svg = "";
        for (var i = 0; i < n; i++)
        {
            svg = Mermaider.MermaidRenderer.RenderSvg(Samples["flowchart"], options);
        }

        var layout = sw.Elapsed.TotalMilliseconds / n;
        sw.Restart();
        for (var i = 0; i < n; i++)
        {
            MermaidDiagramRenderer.ResolveSvg(svg);
        }

        var resolve = sw.Elapsed.TotalMilliseconds / n;
        output.WriteLine($"flowchart: total {total:F2} ms/diagram (Mermaider {layout:F2} ms, color resolve {resolve:F3} ms, Svg.Skia ≈ {total - layout - resolve:F2} ms)");
        Assert.True(total < 200, $"too slow: {total} ms");
    }
}

internal readonly record struct PixelStats(double NonBackgroundRatio, double BlackRatio, double ForegroundLikeRatio)
{
    public static PixelStats Measure(Bitmap bitmap, Color background)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        var bgLum = Luminance(background.R, background.G, background.B);
        long nonBg = 0, black = 0, fgLike = 0, total = (long)size.Width * size.Height;
        for (var i = 0; i < buffer.Length; i += 4)
        {
            // 通道顺序（BGRA / RGBA）对下面的判断无影响：只用到差值与亮度近似。
            int c0 = buffer[i], c1 = buffer[i + 1], c2 = buffer[i + 2];
            if (Math.Abs(c0 - background.B) + Math.Abs(c1 - background.G) + Math.Abs(c2 - background.R) > 24 &&
                Math.Abs(c0 - background.R) + Math.Abs(c1 - background.G) + Math.Abs(c2 - background.B) > 24)
            {
                nonBg++;
            }

            if (c0 < 4 && c1 < 4 && c2 < 4)
            {
                black++;
            }

            if (Math.Abs(Luminance(c2, c1, c0) - bgLum) > 0.45)
            {
                fgLike++;
            }
        }

        return new PixelStats((double)nonBg / total, (double)black / total, (double)fgLike / total);
    }

    private static double Luminance(int r, int g, int b) => (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}
