using Avalonia;
using Avalonia.Media;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 由 HTML 转换来的盒子（div、p、center、section 等）：外边距、边框、内边距、背景、圆角与宽度由自身处理，
/// 文字样式（颜色、字号、粗细、对齐）通过渲染上下文传给其中所有块。设置了宽度且外层居中 / 右对齐时，盒子本身随之对齐。
/// </summary>
public sealed class HtmlBoxBlockView : StackBlockView
{
    private HtmlBoxBlock? _box;
    private Thickness _content;
    private Rect _boxRect;
    // 子块使用的上下文（按外层上下文与本盒子的文字样式缓存，避免每次更新都让子视图重新排版）
    private RenderContext? _childContext;
    private RenderContext? _childParent;
    private HtmlBoxBlock? _childFor;

    protected override Thickness ContentPadding => _content;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var box = (HtmlBoxBlock)block;
        if (_childContext is null || !ReferenceEquals(_childParent, context) || _childFor is null || _childFor.Text != box.Text || _childFor.Box.Background != box.Box.Background)
        {
            _childContext = box.Text.IsDefault && box.Box.Background is null ? context : context.WithHtml(box.Text, box.Box.Background);
            _childParent = context;
            _childFor = box;
        }
        _box = box;
        UpdateChildren(_childContext, box.Children, topKey, ref segment);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var style = _box?.Box ?? HtmlBoxStyle.Default;
        var margin = style.Margin;
        var border = style.BorderColor is not null ? style.BorderWidth : 0;
        var padding = style.Padding;
        var available = double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity;
        var outer = available - margin.Left - margin.Right;
        var width = outer;
        if (style.Width is { } px)
        {
            width = Math.Min(width, px);
        }
        else if (style.WidthPercent is { } percent && double.IsFinite(outer))
        {
            width = outer * percent / 100;
        }
        if (style.MaxWidth is { } max)
        {
            width = Math.Min(width, max);
        }
        width = Math.Max(0, width);
        // 窄于可用宽度的盒子按外层对齐方式摆放
        var offset = 0.0;
        if (double.IsFinite(outer) && width < outer)
        {
            offset = Context?.Inherited?.Align switch
            {
                TextAlign.Center => (outer - width) / 2,
                TextAlign.Right => outer - width,
                _ => 0,
            };
        }
        var left = margin.Left + offset;
        var right = double.IsFinite(available) ? Math.Max(0, available - left - width) : margin.Right;
        _content = new Thickness(left + border + padding.Left, margin.Top + border + padding.Top, right + border + padding.Right, margin.Bottom + border + padding.Bottom);
        var size = base.MeasureOverride(availableSize);
        var height = Math.Max(0, size.Height);
        _boxRect = new Rect(left, margin.Top, double.IsFinite(width) ? width : size.Width - left - right, Math.Max(0, height - margin.Top - margin.Bottom));
        return new Size(double.IsFinite(available) ? available : size.Width, height);
    }

    public override void Render(DrawingContext context)
    {
        if (_box is null || Context is null || _boxRect.Width <= 0 || _boxRect.Height <= 0)
        {
            return;
        }
        var style = _box.Box;
        var background = HtmlPaint.Background(Context, style.Background);
        var pen = style.BorderColor is { } borderColor && style.BorderWidth > 0 && HtmlPaint.Background(Context, borderColor) is { } borderBrush
            ? new Pen(borderBrush, style.BorderWidth)
            : null;
        if (background is null && pen is null)
        {
            return;
        }
        var rect = pen is null ? _boxRect : _boxRect.Deflate(style.BorderWidth / 2);
        context.DrawRectangle(background, pen, rect, style.BorderRadius, style.BorderRadius);
    }
}
