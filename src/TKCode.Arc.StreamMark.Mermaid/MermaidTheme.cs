namespace StreamMark.Mermaid;

/// <summary>Mermaid 图表的配色方案（见 <see cref="MermaidDiagramRenderer.Theme"/>）。</summary>
public enum MermaidTheme
{
    /// <summary>
    /// 复刻 mermaid.js 官方主题，和浏览器里的效果一致：暗色用 <c>dark</c>（深色节点、浅色描边、灰色子图），
    /// 亮色用 <c>default</c>（淡紫节点、淡黄子图）。明暗跟随宿主主题。这是默认值。
    /// </summary>
    MermaidJs,

    /// <summary>复刻 mermaid.js 官方主题：暗色用 <c>dark</c>，亮色用 <c>neutral</c>（灰阶，和黑白灰的界面更协调）。</summary>
    MermaidJsNeutral,

    /// <summary>
    /// Mermaider 原生风格：节点、连线、文字的颜色全部由宿主的前景 / 背景 / 强调色推导，
    /// 子图按色板自动着色，描边偏淡。
    /// </summary>
    Native,
}
