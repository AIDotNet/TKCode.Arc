using System.Xml;
using System.Xml.Linq;
using Avalonia.Media;
using StreamMark.Avalonia.Extensions;
using RenderOptions = Mermaider.Models.RenderOptions;

namespace StreamMark.Mermaid;

/// <summary>Mermaid 图表的大类：决定色板是否中性化、是否重绘子图。</summary>
internal enum DiagramKind
{
    /// <summary>饼图、甘特图、思维导图等：色板是分类色，必须保留。</summary>
    Other,
    Flowchart,
    State,
    Class,
    Sequence,
    Block,
}

/// <summary>
/// 把 <see cref="MermaidPalette"/>（mermaid.js 官方主题的颜色）落到 Mermaider 的渲染选项和它输出的 SVG 上。
/// 节点、连线、文字用 Mermaider 自带的 <c>Surface</c> / <c>Border</c> / <c>Line</c> 等选项钉住；
/// 子图和连线标签的涂法是 Mermaider 的设计（按色板着色的渐变面板、胶囊标签），选项改不了，所以在 SVG 上按语义类重涂。
/// </summary>
internal static class MermaidThemer
{
    private const int NeutralPaletteSize = 12;

    /// <summary>
    /// 从围栏源码判断图表类别：跳过空行、YAML 头（<c>---</c>）、注释和 <c>%%{init}%%</c> 指令，取第一条语句的关键字。
    /// </summary>
    public static DiagramKind Detect(string code)
    {
        var inFrontMatter = false;
        foreach (var raw in code.AsSpan().EnumerateLines())
        {
            var line = raw.Trim();
            if (line.IsEmpty)
            {
                continue;
            }

            if (line.SequenceEqual("---"))
            {
                inFrontMatter = !inFrontMatter;
                continue;
            }

            if (inFrontMatter || line.StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("flowchart", StringComparison.Ordinal) || line.StartsWith("graph", StringComparison.Ordinal))
            {
                return DiagramKind.Flowchart;
            }

            if (line.StartsWith("stateDiagram", StringComparison.Ordinal))
            {
                return DiagramKind.State;
            }

            if (line.StartsWith("classDiagram", StringComparison.Ordinal))
            {
                return DiagramKind.Class;
            }

            if (line.StartsWith("sequenceDiagram", StringComparison.Ordinal))
            {
                return DiagramKind.Sequence;
            }

            return line.StartsWith("block", StringComparison.Ordinal) ? DiagramKind.Block : DiagramKind.Other;
        }

        return DiagramKind.Other;
    }

    /// <summary>用固定配色覆盖选项里由宿主颜色推导的部分。字体、字号、画布底色保持不变。</summary>
    public static RenderOptions Apply(RenderOptions options, MermaidPalette palette, DiagramKind kind)
    {
        var text = CssColorResolver.Format(palette.Text);
        var border = CssColorResolver.Format(palette.NodeBorder);
        options = options with
        {
            Fg = text,
            Muted = text,
            Line = CssColorResolver.Format(palette.Line),
            // 决定节点（菱形）用强调色，保持和普通节点同一色系
            Accent = border,
            Surface = CssColorResolver.Format(palette.NodeFill),
            Border = border,
            Default = border,
            // mermaid.js 的图是平涂、没有阴影
            Gradient = false,
            Elevation = 0,
            Tint = null,
        };

        if (kind != DiagramKind.Other)
        {
            // 结构类图表的色板只用来给容器（子图、命名空间、循环框、block 节点）自动着色：用同一个中性灰，标题等派生色才不会各有各的色相。
            // 图表类（饼图、甘特图等）的色板是分类色，不能动。
            var neutral = CssColorResolver.Format(palette.Neutral);
            options = options with { DataPalette = Enumerable.Repeat(neutral, NeutralPaletteSize).ToArray() };
        }

        return options;
    }

    /// <summary>
    /// 重涂 SVG 里 Mermaider 的涂法改不了的部分：子图（面板色、描边，去掉标题条）、连线标签（矩形底，无描边）
    /// 与推导色的节点形状（胶囊、圆柱、菱形）。
    /// <paramref name="backdrop"/> 是图表背后的不透明底色，半透明的标签底色叠到它上面，免得连线从标签文字后面透出来。
    /// 解析失败时原样返回。
    /// </summary>
    public static string PaintSvg(string svg, MermaidPalette palette, Color backdrop, DiagramKind kind)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(svg), settings);
            document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return svg;
        }

        var paintClusters = kind is DiagramKind.Flowchart or DiagramKind.State or DiagramKind.Class;
        var paintNodes = kind is DiagramKind.Flowchart or DiagramKind.State;
        var clusterFill = CssColorResolver.Format(palette.ClusterFill);
        var clusterBorder = CssColorResolver.Format(palette.ClusterBorder);
        var nodeFill = CssColorResolver.Format(palette.NodeFill);
        var nodeBorder = CssColorResolver.Format(palette.NodeBorder);
        var labelFill = CssColorResolver.Format(DiagramRenderContext.Flatten(palette.EdgeLabel, backdrop));

        var changed = false;
        foreach (var group in document.Descendants().Where(e => e.Name.LocalName == "g").ToList())
        {
            var classes = (string?)group.Attribute("class");
            if (classes is null)
            {
                continue;
            }

            if (paintClusters && HasClass(classes, "subgraph"))
            {
                PaintCluster(group, clusterFill, clusterBorder);
                changed = true;
            }
            else if (HasClass(classes, "edge-label"))
            {
                PaintEdgeLabel(group, labelFill);
                changed = true;
            }
            else if (paintNodes && HasClass(classes, "node"))
            {
                PaintNode(group, nodeFill, nodeBorder);
                changed = true;
            }
        }

        return changed ? document.ToString(SaveOptions.DisableFormatting) : svg;
    }

    private static bool HasClass(string classes, string name)
    {
        foreach (var part in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == name)
            {
                return true;
            }
        }

        return false;
    }

    private static void PaintCluster(XElement group, string fill, string border)
    {
        foreach (var element in group.Elements().ToList())
        {
            switch (element.Name.LocalName)
            {
                case "rect":
                    element.SetAttributeValue("fill", fill);
                    element.SetAttributeValue("stroke", border);
                    element.SetAttributeValue("stroke-width", "1");
                    break;
                case "path": // 标题条（mermaid.js 的子图没有）
                case "line": // 标题条下沿的分隔线
                    element.Remove();
                    break;
            }
        }
    }

    /// <summary>
    /// 胶囊（terminal）、圆柱（data store）和菱形（decision）的填充与描边是 Mermaider 用连线色 / 强调色经 <c>color-mix()</c> 推导的，
    /// 选项钉不住；mermaid.js 里所有形状同色，所以把这些推导出来的颜色换成节点色。
    /// 作者用 <c>style</c> / <c>classDef</c> 指定的颜色是字面量、状态图的起止标记用 <c>var()</c>，都不受影响。
    /// </summary>
    private static void PaintNode(XElement group, string fill, string border)
    {
        foreach (var element in group.Elements())
        {
            var isShape = element.Name.LocalName is "rect" or "polygon" or "circle" or "ellipse" or "path";
            if (isShape && IsDerivedPaint((string?)element.Attribute("fill")))
            {
                element.SetAttributeValue("fill", fill);
            }

            if (IsDerivedPaint((string?)element.Attribute("stroke")))
            {
                element.SetAttributeValue("stroke", border);
            }
        }
    }

    private static bool IsDerivedPaint(string? paint) => paint is not null && paint.StartsWith("color-mix(", StringComparison.Ordinal);

    private static void PaintEdgeLabel(XElement group, string fill)
    {
        foreach (var rect in group.Elements().Where(e => e.Name.LocalName == "rect"))
        {
            rect.SetAttributeValue("fill", fill);
            rect.SetAttributeValue("stroke", "none");
            rect.SetAttributeValue("rx", "4");
            rect.SetAttributeValue("ry", "4");
        }
    }
}
