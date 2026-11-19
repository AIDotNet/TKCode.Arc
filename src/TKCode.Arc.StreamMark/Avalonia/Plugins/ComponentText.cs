using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Plugins;

/// <summary>
/// 块级组件的复制 / 选区 / 搜索文本：按阅读顺序把片段交给 <paramref name="text"/>。
/// 片段的个数与顺序必须与组件视图在 <c>OnUpdate</c> 里消耗片段号（<c>ref segment</c>）的顺序完全一致。
/// 在界面线程调用；只读模型，不要有副作用。
/// </summary>
public delegate void ComponentTextCollector(ComponentBlock component, ComponentTextContext text);

/// <summary>组件文本的收集器。</summary>
public sealed class ComponentTextContext
{
    private readonly List<BlockText.Segment> _segments;
    private readonly PluginRegistry _plugins;

    internal ComponentTextContext(List<BlockText.Segment> segments, PluginRegistry plugins)
    {
        _segments = segments;
        _plugins = plugins;
    }

    /// <summary>一个纯文本片段（对应视图里一个只显示纯文本的片段，例如从属性取的标题）。</summary>
    public void AddText(string text) => _segments.Add(new BlockText.Segment(null, text, "\n"));

    /// <summary>一个行内内容片段（对应视图里一个 <see cref="Rendering.TextSegmentView"/>）。</summary>
    public void AddInline(InlineContent content) => _segments.Add(new BlockText.Segment(content, null, "\n"));

    /// <summary>按默认规则收集子块（与 <see cref="Rendering.StackBlockView.UpdateChildren"/> 分配片段号的顺序一致）。</summary>
    public void AddBlocks(IEnumerable<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        {
            BlockText.CollectInto(block, _segments, _plugins);
        }
    }
}
