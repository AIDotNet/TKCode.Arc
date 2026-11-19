using Avalonia.Media;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.Mermaid;

/// <summary>
/// mermaid.js 官方主题里决定观感的几个颜色。数值取自 mermaid 12.2 在流程图上实际输出的样式表
/// （<c>.node rect</c>、<c>.cluster rect</c>、<c>.edgePaths .path</c>、<c>.edgeLabel</c>、<c>.label text</c>）：
/// dark 来自 <c>theme-dark</c>，default 来自 <c>theme-default</c>，neutral 来自 <c>theme-neutral</c>。
/// </summary>
internal sealed record MermaidPalette(
    Color Text,
    Color Line,
    Color NodeFill,
    Color NodeBorder,
    Color ClusterFill,
    Color ClusterBorder,
    Color EdgeLabel,
    Color Neutral)
{
    /// <summary>方案对应的配色；<see cref="MermaidTheme.Native"/> 没有固定配色，返回 null。</summary>
    public static MermaidPalette? For(MermaidTheme theme, bool isDark)
    {
        if (theme == MermaidTheme.Native)
        {
            return null;
        }

        if (isDark)
        {
            return DarkPalette;
        }

        return theme == MermaidTheme.MermaidJsNeutral ? NeutralPalette : DefaultPalette;
    }

    // dark：节点 #1f2020 / #ccc，文字 #ccc，连线 lightgrey，子图 hsl(180,1.6%,28.4%) + 25% 白描边，连线标签 #585858 @ 50%
    private static readonly MermaidPalette DarkPalette = new(
        Text: Color.FromRgb(0xCC, 0xCC, 0xCC),
        Line: Color.FromRgb(0xD3, 0xD3, 0xD3),
        NodeFill: Color.FromRgb(0x1F, 0x20, 0x20),
        NodeBorder: Color.FromRgb(0xCC, 0xCC, 0xCC),
        ClusterFill: Color.FromRgb(0x47, 0x49, 0x49),
        ClusterBorder: DiagramRenderContext.Flatten(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF), Color.FromRgb(0x47, 0x49, 0x49)),
        EdgeLabel: Color.FromArgb(0x80, 0x58, 0x58, 0x58),
        Neutral: Color.FromRgb(0x9A, 0x9A, 0x9A));

    // default：节点 #ECECFF / #9370DB，文字与连线 #333，子图 #ffffde / #aaaa33，连线标签 rgba(232,232,232,.8)
    private static readonly MermaidPalette DefaultPalette = new(
        Text: Color.FromRgb(0x33, 0x33, 0x33),
        Line: Color.FromRgb(0x33, 0x33, 0x33),
        NodeFill: Color.FromRgb(0xEC, 0xEC, 0xFF),
        NodeBorder: Color.FromRgb(0x93, 0x70, 0xDB),
        ClusterFill: Color.FromRgb(0xFF, 0xFF, 0xDE),
        ClusterBorder: Color.FromRgb(0xAA, 0xAA, 0x33),
        EdgeLabel: Color.FromArgb(0xCC, 0xE8, 0xE8, 0xE8),
        Neutral: Color.FromRgb(0x77, 0x77, 0x77));

    // neutral：节点 #eee / #999，文字 #000，连线 #666，子图 #fcfcfc / #707070，连线标签白底
    private static readonly MermaidPalette NeutralPalette = new(
        Text: Color.FromRgb(0x00, 0x00, 0x00),
        Line: Color.FromRgb(0x66, 0x66, 0x66),
        NodeFill: Color.FromRgb(0xEE, 0xEE, 0xEE),
        NodeBorder: Color.FromRgb(0x99, 0x99, 0x99),
        ClusterFill: Color.FromRgb(0xFC, 0xFC, 0xFC),
        ClusterBorder: Color.FromRgb(0x70, 0x70, 0x70),
        EdgeLabel: Colors.White,
        Neutral: Color.FromRgb(0x77, 0x77, 0x77));
}
