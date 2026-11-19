using StreamMark.Avalonia.Plugins;

namespace StreamMark.Mermaid;

/// <summary>Mermaid 图表插件：<c>```mermaid</c> 代码块闭合后渲染为矢量图，可在图表 / 源码间切换。</summary>
public sealed class MermaidPlugin(MermaidDiagramRenderer? renderer = null) : IMarkdownPlugin
{
    public string Name => "mermaid";

    public void Configure(MarkdownPluginBuilder builder) => builder.AddDiagramRenderer(renderer ?? new MermaidDiagramRenderer());
}
