using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Plugins;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 一个文档的渲染上下文：样式、扩展服务、选区、流式状态。同一文档的所有块视图共享一个实例；
/// 样式或服务变化时宿主替换为新实例并通知视图刷新。
/// </summary>
public sealed class RenderContext
{
    public RenderContext(MarkdownStyle style, MarkdownServices services, SelectionModel selection, int entry, StreamingState? streaming, BlockStateStore? state = null, string? imageBaseUri = null)
    {
        ImageBaseUri = imageBaseUri;
        State = state ?? new BlockStateStore();
        Style = style;
        Services = services;
        Selection = selection;
        Entry = entry;
        Streaming = streaming;
    }

    public MarkdownStyle Style { get; }

    public MarkdownServices Services { get; }

    /// <summary>插件注册表（等同于 <c>Services.Registry</c>）。</summary>
    public PluginRegistry Plugins => Services.Registry;

    /// <summary>相对图片地址的基准。</summary>
    public string? ImageBaseUri { get; }

    /// <summary>文档的块级界面状态（跨视图回收与重建保留）。</summary>
    public BlockStateStore State { get; }

    public SelectionModel Selection { get; }

    /// <summary>文档在宿主中的序号（选区坐标的第一维）。</summary>
    public int Entry { get; }

    /// <summary>流式状态（渐显、光标），静态文档为 null。</summary>
    public StreamingState? Streaming { get; }

    /// <summary>外层 HTML 盒子继承下来的文字样式（颜色、字号、粗细、对齐），没有时为 null。</summary>
    public HtmlTextStyle? Inherited { get; private init; }

    /// <summary>外层 HTML 盒子的背景色（用于判断作者颜色是否可读），没有时为 null。</summary>
    public CssColor? HtmlBackground { get; private init; }

    /// <summary>
    /// 任务复选框的点击 / 键盘请求，由宿主视图在启用任务交互时设置；为 null 时复选框只读。
    /// 参数是被点的列表项，返回是否已把请求交给宿主。
    /// </summary>
    internal Func<ListItemBlock, bool>? TaskToggle { get; init; }

    public RenderContext With(MarkdownStyle? style = null, MarkdownServices? services = null, int? entry = null) =>
        new(style ?? Style, services ?? Services, Selection, entry ?? Entry, Streaming, State, ImageBaseUri)
        {
            Inherited = Inherited,
            HtmlBackground = HtmlBackground,
            TaskToggle = TaskToggle,
        };

    /// <summary>进入一个 HTML 盒子：叠加其文字样式与背景。</summary>
    public RenderContext WithHtml(HtmlTextStyle text, CssColor? background) =>
        new(Style, Services, Selection, Entry, Streaming, State, ImageBaseUri)
        {
            Inherited = text.Over(Inherited ?? HtmlTextStyle.Default),
            HtmlBackground = background is { A: > 0 } ? background : HtmlBackground,
            TaskToggle = TaskToggle,
        };
}
