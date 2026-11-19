using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StreamMark.Avalonia.Extensions;
using StreamMark.Mermaid;
using Xunit;

namespace StreamMark.Avalonia.Tests.Mermaid;

/// <summary>
/// Mermaid 配色：复刻 mermaid.js 官方主题（dark / default / neutral），以及宿主主题里半透明颜色的处理。
/// 回归点：宿主主题的文字 / 填充 token 常是半透明的，丢掉透明度会得到「白底白字」（亮色下是黑底黑字）。
/// </summary>
public sealed class MermaidThemeTests(ITestOutputHelper output)
{
    private const string Clustered = """
        flowchart LR
          top[Outside] --> A1
          subgraph a[Group A]
            A1[One] --> A2[Two]
          end
          subgraph b[Group B]
            B1{Choice} -->|yes| B2[Three]
          end
          A2 --> B1
        """;

    private const string Shapes = """
        flowchart LR
          A[Rect] --> B(Round)
          B --> C([Stadium])
          C --> D[[Subroutine]]
          D --> E[(Database)]
          E --> F((Circle))
          F --> G{Decision}
          G --> H{{Hexagon}}
          L[Custom] --> A
          style L fill:#f9f,stroke:#333,stroke-width:2px
        """;

    private const string StateMarkers = """
        stateDiagram-v2
          [*] --> Idle
          Idle --> Done
          Done --> [*]
        """;

    /// <summary>常见宿主主题的取值：暗色文字是 85% 的白、代码底是 4% 的白；亮色文字是 88% 的黑、代码底是 2% 的黑。</summary>
    internal static DiagramRenderContext TranslucentHost(bool dark) => dark
        ? new DiagramRenderContext(true, Color.FromArgb(0xD8, 0xF0, 0xF0, 0xF0), Color.FromArgb(0x0A, 0xF0, 0xF0, 0xF0), Color.FromRgb(0x9C, 0x9C, 0x9C), null, 13)
        : new DiagramRenderContext(false, Color.FromArgb(0xE0, 0x1A, 0x1A, 0x1A), Color.FromArgb(0x05, 0x1A, 0x1A, 0x1A), Color.FromRgb(0x4A, 0x4A, 0x4A), null, 13);

    private static string ResolvedSvg(string code, DiagramRenderContext context, MermaidTheme theme)
    {
        var renderer = new MermaidDiagramRenderer { Theme = theme };
        return MermaidDiagramRenderer.ResolveSvg(renderer.BuildSvg(code, context, []));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SemiTransparentHostColors_AreFlattenedOntoTheBackdrop_NotStripped(bool dark)
    {
        var options = MermaidDiagramRenderer.CreateOptions(TranslucentHost(dark));

        Assert.True(CssColorResolver.TryParseColor(options.Bg!, out var bg));
        Assert.True(CssColorResolver.TryParseColor(options.Fg!, out var fg));
        var contrast = Contrast(ToColor(bg), ToColor(fg));
        output.WriteLine($"dark={dark} bg={options.Bg} fg={options.Fg} contrast={contrast:F1}");

        // 丢掉透明度时前景与背景是同一个颜色（对比度 1）
        Assert.NotEqual(options.Bg, options.Fg);
        Assert.True(contrast > 7, $"foreground is not readable on the background: {contrast:F2}");
        // 底色保持明暗方向：暗色仍是深色，亮色仍是浅色
        Assert.Equal(dark, RelativeLuminance(ToColor(bg)) < 0.2);
    }

    [Theory]
    [InlineData(MermaidTheme.MermaidJs, true, "#1f2020", "#cccccc", "#d3d3d3", "#cccccc")]
    [InlineData(MermaidTheme.MermaidJs, false, "#ececff", "#9370db", "#333333", "#333333")]
    [InlineData(MermaidTheme.MermaidJsNeutral, true, "#1f2020", "#cccccc", "#d3d3d3", "#cccccc")]
    [InlineData(MermaidTheme.MermaidJsNeutral, false, "#eeeeee", "#999999", "#666666", "#000000")]
    public void MermaidJsThemes_UseTheOfficialPalette(MermaidTheme theme, bool dark, string surface, string border, string line, string text)
    {
        var options = MermaidDiagramRenderer.CreateOptions(TranslucentHost(dark), theme);

        Assert.Equal(surface, options.Surface);
        Assert.Equal(border, options.Border);
        Assert.Equal(line, options.Line);
        Assert.Equal(text, options.Fg);
        // mermaid.js 的图是平涂、没有阴影
        Assert.False(options.Gradient);
        Assert.Equal(0, options.Elevation);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeTheme_KeepsTheHostDerivedOptions(bool dark)
    {
        var context = TranslucentHost(dark);

        Assert.Equal(MermaidDiagramRenderer.CreateOptions(context), MermaidDiagramRenderer.CreateOptions(context, MermaidTheme.Native));
        Assert.Equal(MermaidTheme.MermaidJs, new MermaidDiagramRenderer().Theme);
    }

    public static TheoryData<string, MermaidTheme, bool> ReadableCases()
    {
        var data = new TheoryData<string, MermaidTheme, bool>();
        foreach (var name in new[] { "flowchart", "sequence", "class", "state" })
        {
            foreach (var theme in new[] { MermaidTheme.MermaidJs, MermaidTheme.MermaidJsNeutral, MermaidTheme.Native })
            {
                data.Add(name, theme, true);
                data.Add(name, theme, false);
            }
        }

        data.Add("clustered", MermaidTheme.MermaidJs, true);
        data.Add("clustered", MermaidTheme.MermaidJs, false);
        return data;
    }

    [Theory]
    [MemberData(nameof(ReadableCases))]
    public void NodeText_IsReadableOnTheNodeFill(string name, MermaidTheme theme, bool dark)
    {
        var code = name == "clustered" ? Clustered : MermaidRendererTests.Samples[name];
        var svg = ResolvedSvg(code, TranslucentHost(dark), theme);
        var backdrop = dark ? Color.FromRgb(0x1D, 0x1D, 0x1D) : Color.FromRgb(0xFA, 0xFA, 0xFA);

        var pairs = NodePairs(svg, backdrop).ToList();
        Assert.NotEmpty(pairs);
        foreach (var (fill, text) in pairs)
        {
            var contrast = Contrast(fill, text);
            Assert.True(contrast >= 4.5, $"{name}/{theme}/dark={dark}: text {text} on fill {fill} has contrast {contrast:F2}");
        }
    }

    [Fact]
    public void MermaidJsDark_PaintsSubgraphsAsFlatGrayPanels_WithoutTheTitleStrip()
    {
        var svg = ResolvedSvg(Clustered, TranslucentHost(true), MermaidTheme.MermaidJs);
        var groups = Subgraphs(XDocument.Parse(svg));

        Assert.Equal(2, groups.Count);
        foreach (var group in groups)
        {
            var rect = Assert.Single(group.Elements(), e => e.Name.LocalName == "rect");
            Assert.Equal("#474949", (string?)rect.Attribute("fill"));
            Assert.DoesNotContain(group.Elements(), e => e.Name.LocalName is "path" or "line");
        }

        // Mermaider 原本按色板给子图着色（橙 / 青 / 紫…），复刻 mermaid.js 时所有子图同色
        Assert.DoesNotContain("#f28e2b", svg);
        Assert.DoesNotContain("#76b7b2", svg);
    }

    [Fact]
    public void MermaidJsLight_PaintsSubgraphsPaleYellow()
    {
        var svg = ResolvedSvg(Clustered, TranslucentHost(false), MermaidTheme.MermaidJs);

        foreach (var group in Subgraphs(XDocument.Parse(svg)))
        {
            var rect = Assert.Single(group.Elements(), e => e.Name.LocalName == "rect");
            Assert.Equal("#ffffde", (string?)rect.Attribute("fill"));
            Assert.Equal("#aaaa33", (string?)rect.Attribute("stroke"));
        }
    }

    [Fact]
    public void NativeTheme_LeavesSubgraphsToMermaider()
    {
        var svg = ResolvedSvg(Clustered, TranslucentHost(true), MermaidTheme.Native);

        foreach (var group in Subgraphs(XDocument.Parse(svg)))
        {
            Assert.Contains(group.Elements(), e => e.Name.LocalName == "path"); // 标题条
        }
    }

    [Theory]
    [InlineData(true, "#1f2020", "#cccccc")]
    [InlineData(false, "#ececff", "#9370db")]
    public void NodeShapes_ShareTheNodeColors_ButKeepAuthorStyles(bool dark, string fill, string border)
    {
        var document = XDocument.Parse(ResolvedSvg(Shapes, TranslucentHost(dark), MermaidTheme.MermaidJs));
        var nodes = document.Descendants().Where(e => e.Name.LocalName == "g" && HasClass(e, "node")).ToList();

        Assert.Equal(9, nodes.Count);
        foreach (var node in nodes)
        {
            var id = (string?)node.Attribute("data-id");
            foreach (var shape in node.Elements().Where(e => e.Name.LocalName is "rect" or "polygon" or "circle" or "ellipse" or "path"))
            {
                var shapeFill = (string?)shape.Attribute("fill");
                var shapeStroke = (string?)shape.Attribute("stroke");
                if (id == "L")
                {
                    // 作者用 style 指定的颜色保持原样
                    Assert.Equal("#f9f", shapeFill);
                    Assert.Equal("#333", shapeStroke);
                    continue;
                }

                // 胶囊、圆柱、菱形原本是 Mermaider 推导的颜色，现在和普通节点一致
                Assert.Equal(fill, shapeFill);
                Assert.True(shapeStroke is "none" || shapeStroke == border, $"{id}: stroke {shapeStroke}");
            }
        }
    }

    [Theory]
    [InlineData(true, "#cccccc")]
    [InlineData(false, "#9370db")]
    public void StateMarkers_KeepTheAccentColor(bool dark, string accent)
    {
        var document = XDocument.Parse(ResolvedSvg(StateMarkers, TranslucentHost(dark), MermaidTheme.MermaidJs));
        var start = document.Descendants().Single(e => e.Name.LocalName == "g" && (string?)e.Attribute("data-shape") == "statestart");

        Assert.Equal(accent, (string?)start.Elements().Single(e => e.Name.LocalName == "circle").Attribute("fill"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EdgeLabels_AreRectanglesWithoutOutline(bool dark)
    {
        var svg = ResolvedSvg(Clustered, TranslucentHost(dark), MermaidTheme.MermaidJs);
        var labels = XDocument.Parse(svg).Descendants().Where(e => e.Name.LocalName == "g" && HasClass(e, "edge-label")).ToList();

        Assert.NotEmpty(labels);
        foreach (var rect in labels.SelectMany(g => g.Elements()).Where(e => e.Name.LocalName == "rect"))
        {
            Assert.Equal("none", (string?)rect.Attribute("stroke"));
            Assert.Equal("4", (string?)rect.Attribute("rx"));
            // 不透明：连线不会从标签文字后面透出来
            Assert.True(CssColorResolver.TryParseColor(((string?)rect.Attribute("fill"))!, out var fill));
            Assert.True(fill.A > 0.999);
        }
    }

    [Fact]
    public void ChartDiagrams_KeepTheirCategoricalColors()
    {
        var svg = ResolvedSvg(MermaidRendererTests.Samples["pie"], TranslucentHost(true), MermaidTheme.MermaidJs);

        var slices = XDocument.Parse(svg).Descendants().Where(e => HasClass(e, "pie-slice")).ToList();
        var fills = slices.SelectMany(s => s.DescendantsAndSelf()).Select(e => (string?)e.Attribute("fill")).Where(f => f is { Length: > 0 } && f != "none").Distinct().ToList();
        output.WriteLine(string.Join(", ", fills));
        Assert.True(fills.Count >= 3, $"pie slices collapsed into {fills.Count} color(s)");
    }

    [Theory]
    [InlineData("flowchart TD\n  A-->B", "Flowchart")]
    [InlineData("graph LR\n  A-->B", "Flowchart")]
    [InlineData("\n\n  flowchart-elk TD\n", "Flowchart")]
    [InlineData("%% 注释\n%%{init: {'theme': 'dark'}}%%\nsequenceDiagram\n  A->>B: hi", "Sequence")]
    [InlineData("---\ntitle: Demo\n---\nstateDiagram-v2\n  [*] --> A", "State")]
    [InlineData("classDiagram\n  class A", "Class")]
    [InlineData("block-beta\n  columns 2", "Block")]
    [InlineData("pie title Pets\n  \"Dogs\" : 1", "Other")]
    [InlineData("gantt\n  title X", "Other")]
    [InlineData("", "Other")]
    public void Detect_ReadsTheFirstStatement(string code, string expected)
    {
        // DiagramKind 是内部类型，测试方法签名里不能出现
        Assert.Equal(Enum.Parse<DiagramKind>(expected), MermaidThemer.Detect(code));
    }

    [Theory]
    [InlineData("flowchart")]
    [InlineData("clustered")]
    [InlineData("sequence")]
    [InlineData("class")]
    [InlineData("state")]
    [InlineData("pie")]
    public async Task Renders_ThemedSnapshots(string name)
    {
        var code = name == "clustered" ? Clustered : MermaidRendererTests.Samples[name];
        foreach (var theme in new[] { MermaidTheme.MermaidJs, MermaidTheme.MermaidJsNeutral, MermaidTheme.Native })
        {
            foreach (var dark in new[] { true, false })
            {
                var context = TranslucentHost(dark);
                var backdrop = dark ? Color.FromRgb(0x1D, 0x1D, 0x1D) : Color.FromRgb(0xFA, 0xFA, 0xFA);
                string? error = null;
                var renderer = new MermaidDiagramRenderer { Theme = theme, ErrorReported = (m, _) => error = m };
                using var result = await renderer.RenderAsync("mermaid", code, context, CancellationToken.None);
                Assert.True(result is not null, error);

                var stats = await HeadlessApp.Session.Dispatch(() =>
                {
                    const double scale = 1.5;
                    var control = new DiagramTestControl(result) { Width = result.Size.Width * scale, Height = result.Size.Height * scale };
                    var window = new Window { Content = control, Background = new SolidColorBrush(backdrop), SizeToContent = SizeToContent.WidthAndHeight };
                    window.Show();
                    HeadlessApp.Save(window, $"mermaid-theme-{name}-{theme}-{(dark ? "dark" : "light")}");
                    using var frame = HeadlessApp.Capture(window);
                    var s = PixelStats.Measure(frame, backdrop);
                    window.Close();
                    return Task.FromResult(s);
                }, CancellationToken.None);

                output.WriteLine($"{name}/{theme}/dark={dark}: {stats}");
                Assert.True(stats.NonBackgroundRatio > 0.02, $"almost nothing drawn: {stats}");
                // color-mix 没展开时节点填充会变成纯黑；neutral 亮色的文字本身就是纯黑（mermaid.js 如此），放宽到只拦大面积黑块
                var maxBlack = theme == MermaidTheme.MermaidJsNeutral && !dark ? 0.03 : 0.005;
                Assert.True(stats.BlackRatio < maxBlack, $"black fills detected: {stats}");
                Assert.True(stats.ForegroundLikeRatio > 0.001, $"no readable text / edges: {stats}");
            }
        }
    }

    // ---- 辅助：从最终 SVG 里取节点填充与文字颜色，按 WCAG 计算对比度 ----

    private static bool HasClass(XElement element, string name) =>
        ((string?)element.Attribute("class"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name) == true;

    private static List<XElement> Subgraphs(XDocument document) =>
        document.Descendants().Where(e => e.Name.LocalName == "g" && HasClass(e, "subgraph")).ToList();

    private static IEnumerable<(Color Fill, Color Text)> NodePairs(string svg, Color backdrop)
    {
        var document = XDocument.Parse(svg);
        foreach (var node in document.Descendants().Where(e => e.Name.LocalName == "g" && (HasClass(e, "node") || HasClass(e, "actor") || HasClass(e, "class-node"))))
        {
            var shape = node.Elements().FirstOrDefault(e => e.Name.LocalName is "rect" or "polygon" or "circle" or "ellipse" or "path");
            var text = node.Descendants().FirstOrDefault(e => e.Name.LocalName == "text");
            if (shape is null || text is null)
            {
                continue;
            }

            foreach (var fill in Fills(document, (string?)shape.Attribute("fill"), backdrop))
            {
                if (TryColor((string?)text.Attribute("fill"), fill, out var foreground))
                {
                    yield return (fill, foreground);
                }
            }
        }
    }

    /// <summary>纯色填充就是它自己；渐变填充（Mermaider 原生风格）取每个色标，逐个检查对比度。</summary>
    private static IEnumerable<Color> Fills(XDocument document, string? fill, Color backdrop)
    {
        if (fill is null)
        {
            yield break;
        }

        if (!fill.StartsWith("url(#", StringComparison.Ordinal))
        {
            if (TryColor(fill, backdrop, out var color))
            {
                yield return color;
            }

            yield break;
        }

        var id = fill["url(#".Length..fill.IndexOf(')')];
        var gradient = document.Descendants().FirstOrDefault(e => (string?)e.Attribute("id") == id);
        foreach (var stop in gradient?.Elements().Where(e => e.Name.LocalName == "stop") ?? [])
        {
            if (TryColor((string?)stop.Attribute("stop-color"), backdrop, out var color))
            {
                yield return color;
            }
        }
    }

    private static bool TryColor(string? value, Color backdrop, out Color color)
    {
        color = default;
        if (value is null || !CssColorResolver.TryParseColor(value, out var parsed))
        {
            return false;
        }

        color = DiagramRenderContext.Flatten(ToColor(parsed), backdrop);
        return true;
    }

    private static Color ToColor(RgbaColor c) =>
        Color.FromArgb((byte)Math.Round(c.A * 255), (byte)Math.Round(c.R * 255), (byte)Math.Round(c.G * 255), (byte)Math.Round(c.B * 255));

    internal static double RelativeLuminance(Color color)
    {
        static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    internal static double Contrast(Color a, Color b)
    {
        var (hi, lo) = (RelativeLuminance(a), RelativeLuminance(b));
        if (hi < lo)
        {
            (hi, lo) = (lo, hi);
        }

        return (hi + 0.05) / (lo + 0.05);
    }
}
