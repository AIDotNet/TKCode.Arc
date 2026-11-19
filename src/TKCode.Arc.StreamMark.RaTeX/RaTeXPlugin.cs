using StreamMark.Avalonia.Plugins;

namespace StreamMark.RaTeX;

/// <summary>公式插件：用 RaTeX 排版 <c>$…$</c>、<c>$$…$$</c>、<c>\(…\)</c>、<c>\[…\]</c>。</summary>
public sealed class RaTeXPlugin(RaTeXMathRenderer? renderer = null) : IMarkdownPlugin
{
    public string Name => "ratex";

    public void Configure(MarkdownPluginBuilder builder) => builder.UseMath(renderer ?? RaTeXMathRenderer.Shared);
}
