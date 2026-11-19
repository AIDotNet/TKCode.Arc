using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Rendering;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;

namespace StreamMark.Avalonia.Plugins;

/// <summary>
/// 内置组件（总是启用）：<c>&lt;details&gt;/&lt;summary&gt;</c> 与 <c>&lt;think&gt;</c> 折叠块、<c>&lt;Citation /&gt;</c> 引用标签、
/// <c>&lt;cite&gt;</c> 引用文字。<c>kbd / sub / sup / mark / u</c> 在解析时直接映射为样式，<c>img</c> 映射为图片。
/// 宿主可以注册同名组件覆盖这些默认实现。
/// </summary>
public sealed class BuiltInComponentsPlugin : IMarkdownPlugin
{
    public static BuiltInComponentsPlugin Instance { get; } = new();

    public string Name => "builtin-components";

    public void Configure(MarkdownPluginBuilder builder)
    {
        builder.AddBlockComponent("details", _ => new CollapsibleBlockView(), registerTag: false);
        builder.AddBlockComponent("think", _ => new CollapsibleBlockView(), registerTag: false);
        builder.AddInlineComponent("citation", CitationRenderer.Instance, registerTag: false);
        builder.AddInlineComponent("cite", CiteRenderer.Instance, ComponentKind.InlinePair, registerTag: false);
        // diff：统一 / 并排视图（流式中也用，边写边显示）
        builder.AddFenceView("diff", _ => new DiffBlockView(), whileStreaming: true);
        builder.AddFenceView("patch", _ => new DiffBlockView(), whileStreaming: true);
    }
}

/// <summary>未注册视图的块级组件：按普通容器显示其内容。</summary>
public sealed class ComponentContainerView : StackBlockView
{
    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged) =>
        UpdateChildren(context, ((ComponentBlock)block).Children, topKey, ref segment);
}

/// <summary>
/// 可折叠块：<c>&lt;details&gt;</c>（标题取 <c>&lt;summary&gt;</c>）与 <c>&lt;think&gt;</c>（标题取 title 属性，默认 "Thinking"）。
/// 展开状态保存在 <see cref="BlockStateStore"/>（视图回收后保留）；默认折叠，可用 open 属性默认展开。
/// </summary>
public sealed class CollapsibleBlockView : BlockView
{
    private const double HeaderHeight = 28;
    private readonly TextSegmentView _title = new();
    private readonly ContentStack _content = new();
    private bool _open;
    private bool _hoverHeader;
    private bool _hasSummary;

    public CollapsibleBlockView()
    {
        AddChild(_title);
        AddChild(_content);
        Cursor = Cursor.Default;
    }

    public bool IsOpen => _open;

    private string StateName => "collapsible.open:" + ((ComponentBlock?)Block)?.Name;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var component = (ComponentBlock)block;
        // 标题：details 取 <summary> 子块的文字，think 取 title 属性
        var summary = component.Children.FirstOrDefault(c => c is ComponentBlock { Name: "summary" }) as ComponentBlock;
        var titleContent = summary?.Children.FirstOrDefault() is ParagraphBlock p
            ? p.Content
            : new InlineContent(component.Attributes["title"] ?? (component.Name == "think" ? "Thinking" : "Details"), [], [], []);
        // 片段编号与 BlockText 一致：summary 的片段在前（标题即其第一个片段），没有 summary 时标题不参与选区
        var titleKey = summary is not null ? KeyFor(context, topKey, ref segment) : new SegmentKey(context.Entry, topKey, -1);
        _title.Set(context, titleKey, titleContent, context.Style.FontSize, FontWeight.SemiBold, context.Style.SecondaryForeground);
        _hasSummary = summary is not null;
        if (summary is not null)
        {
            segment += Math.Max(0, CountSegments(summary, context.Plugins) - 1);
        }
        var defaultOpen = component.Attributes["open"] is not null;
        _open = context.State.Get(topKey, StateName, defaultOpen);
        var children = component.Children.Where(c => !ReferenceEquals(c, summary)).ToList();
        _content.Update(context, children, topKey, ref segment);
        _content.IsVisible = _open;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static int CountSegments(MarkdownBlock block, PluginRegistry plugins) => BlockText.Segments(block, plugins).Count;

    public void Toggle()
    {
        if (Context is null || Block is null)
        {
            return;
        }
        _open = !_open;
        Context.State.Set(TopKey, StateName, _open);
        _content.IsVisible = _open;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _title.Measure(new Size(Math.Max(0, availableSize.Width - 22), double.PositiveInfinity));
        var height = Math.Max(HeaderHeight, _title.DesiredSize.Height + 6);
        if (_open)
        {
            _content.Measure(new Size(Math.Max(0, availableSize.Width - 14), double.PositiveInfinity));
            height += 4 + _content.DesiredSize.Height;
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : _title.DesiredSize.Width + 22, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var header = Math.Max(HeaderHeight, _title.DesiredSize.Height + 6);
        _title.Arrange(new Rect(22, (header - _title.DesiredSize.Height) / 2, Math.Max(0, finalSize.Width - 22), _title.DesiredSize.Height));
        if (_open)
        {
            _content.Arrange(new Rect(14, header + 4, Math.Max(0, finalSize.Width - 14), _content.DesiredSize.Height));
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var style = Context.Style;
        var header = Math.Max(HeaderHeight, _title.DesiredSize.Height + 6);
        if (_hoverHeader)
        {
            context.DrawRectangle(style.HoverBackground, null, new Rect(0, 0, Math.Min(Bounds.Width, _title.DesiredSize.Width + 30), header), 5, 5);
        }
        else
        {
            context.FillRectangle(Brushes.Transparent, new Rect(0, 0, Bounds.Width, header));
        }
        // 折叠箭头
        var cy = header / 2;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            if (_open)
            {
                g.BeginFigure(new Point(6, cy - 2), false);
                g.LineTo(new Point(10, cy + 2));
                g.LineTo(new Point(14, cy - 2));
            }
            else
            {
                g.BeginFigure(new Point(8, cy - 4), false);
                g.LineTo(new Point(12, cy));
                g.LineTo(new Point(8, cy + 4));
            }
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(style.SecondaryForeground, 1.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
        if (_open)
        {
            context.FillRectangle(style.QuoteBorderBrush, new Rect(9.5, header + 4, 1, Math.Max(0, Bounds.Height - header - 4)));
        }
    }

    private bool InHeader(Point p) => p.Y < Math.Max(HeaderHeight, _title.DesiredSize.Height + 6);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hover = InHeader(e.GetPosition(this));
        if (hover != _hoverHeader)
        {
            _hoverHeader = hover;
            Cursor = hover ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hoverHeader = false;
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // 只在没有拖拽选择时切换
        if (e.InitialPressMouseButton == MouseButton.Left && InHeader(e.GetPosition(this)) && Context is { Selection.IsEmpty: true })
        {
            Toggle();
            e.Handled = true;
        }
    }

    public override TextSegmentView? LastSegment() => _open ? _content.LastSegment() ?? _title : _title;

    public override IEnumerable<ISelectableSegment> Segments()
    {
        var title = _hasSummary ? (IEnumerable<ISelectableSegment>)[_title] : [];
        return _open ? [.. title, .. _content.Segments()] : title;
    }

    /// <summary>折叠块的内容区。</summary>
    private sealed class ContentStack : StackBlockView
    {
        public void Update(RenderContext context, IReadOnlyList<MarkdownBlock> blocks, int topKey, ref int segment) =>
            UpdateChildren(context, blocks, topKey, ref segment);

        protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
        {
        }
    }
}

/// <summary><c>&lt;Citation id="1" /&gt;</c>：小号引用标签，可点击。</summary>
internal sealed class CitationRenderer : IInlineComponentRenderer
{
    public static CitationRenderer Instance { get; } = new();

    public DrawableTextRun CreateRun(InlineObject component, InlineComponentContext context)
    {
        var label = component.Attributes["label"] ?? component.Attributes["id"] ?? "?";
        return new ChipRun(label, context.FontSize * 0.72, context.Style.LinkForeground, context.Style.InlineCodeBackground, context.Properties);
    }

    public string ToPlainText(InlineObject component) => $"[{component.Attributes["label"] ?? component.Attributes["id"]}]";

    public bool IsClickable(ComponentAttributes attributes) => true;
}

/// <summary><c>&lt;cite href="…"&gt;文字&lt;/cite&gt;</c>：链接样式，可点击。</summary>
internal sealed class CiteRenderer : IInlineComponentRenderer
{
    public static CiteRenderer Instance { get; } = new();

    public InlineComponentStyle GetStyle(InlineComponent component, MarkdownStyle style) => new(style.LinkForeground, Underline: component.Attributes["href"] is not null);

    public bool IsClickable(ComponentAttributes attributes) => attributes["href"] is not null || attributes["id"] is not null;
}

/// <summary>圆角小标签。</summary>
internal sealed class ChipRun : DrawableTextRun
{
    private readonly TextLayout _layout;
    private readonly IBrush _background;

    public ChipRun(string text, double fontSize, IBrush foreground, IBrush background, TextRunProperties properties)
    {
        Properties = properties;
        _background = background;
        _layout = new TextLayout(text, new Typeface(properties.Typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold), fontSize, foreground);
    }

    public override TextRunProperties Properties { get; }

    public override Size Size => new(_layout.WidthIncludingTrailingWhitespace + 10, _layout.Height + 2);

    // 标签底部略低于外部文字基线，视觉上与小写字母居中
    public override double Baseline => Size.Height * 0.8;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        var rect = new Rect(origin.X + 1, origin.Y, Size.Width - 2, Size.Height);
        drawingContext.DrawRectangle(_background, null, rect, rect.Height / 2, rect.Height / 2);
        _layout.Draw(drawingContext, new Point(origin.X + 5, origin.Y + 1));
    }
}
