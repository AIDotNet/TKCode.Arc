using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;
using Avalonia.Media;
using StreamMark.Avalonia.Extensions;
using StreamMark.Avalonia.Selection;
using StreamMark.Core.Model;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 块级公式：居中绘制。流式中公式未写完、暂时无法排版时保留上一次成功的结果，
/// 从未成功过则显示源码。过宽时等比缩小（最小 60%）。
/// </summary>
public sealed class MathBlockView : BlockView, ISelectableSegment
{
    private MathLayout? _layout;
    private string _latex = "";
    private string? _error;
    private TextLayout? _source;
    private double _scale = 1;

    public SegmentKey Key { get; private set; }

    public string Text => _latex;

    public Control View => this;

    private double FontSize => (Context?.Style.FontSize ?? 14) * 1.15;

    protected override void OnUpdate(RenderContext context, MarkdownBlock? previous, MarkdownBlock block, int topKey, ref int segment, bool contextChanged)
    {
        var math = (MathBlock)block;
        Key = KeyFor(context, topKey, ref segment);
        if (!contextChanged && math.Latex == _latex)
        {
            return;
        }
        _latex = math.Latex;
        var layout = context.Services.Registry.Math?.Layout(math.Latex.Trim(), display: true, out _error);
        if (layout is not null)
        {
            _layout = layout;
            _error = null;
        }
        else if (!math.IsOpen || contextChanged)
        {
            // 已闭合仍失败：显示源码而不是旧结果
            _layout = math.IsOpen ? _layout : null;
        }
        _source = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_layout is { } layout)
        {
            var width = layout.Width * FontSize;
            _scale = double.IsFinite(availableSize.Width) && width > availableSize.Width ? Math.Max(0.6, availableSize.Width / width) : 1;
            var size = FontSize * _scale;
            return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, (layout.Ascent + layout.Descent) * size + 8);
        }
        if (Context is null)
        {
            return default;
        }
        // 无法排版：以源码显示
        _source = new TextLayout(_latex, new Typeface(Context.Style.MonospaceFontFamily), Context.Style.CodeFontSize, Context.Style.SecondaryForeground,
            textWrapping: TextWrapping.Wrap, maxWidth: double.IsFinite(availableSize.Width) ? availableSize.Width : double.PositiveInfinity);
        return new Size(Math.Min(_source.WidthIncludingTrailingWhitespace, availableSize.Width), _source.Height);
    }

    private Rect FormulaRect
    {
        get
        {
            if (_layout is not { } layout)
            {
                return default;
            }
            var size = FontSize * _scale;
            var width = layout.Width * size;
            var x = Math.Max(0, (Bounds.Width - width) / 2);
            return new Rect(x, 4, width, (layout.Ascent + layout.Descent) * size);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Context is null)
        {
            return;
        }
        var selected = Context.Selection.GetSegmentRange(Key, _latex.Length) is not null;
        if (_layout is { } layout)
        {
            var rect = FormulaRect;
            if (selected)
            {
                context.FillRectangle(Context.Style.SelectionBackground, rect.Inflate(2));
            }
            using (context.PushClip(new Rect(Bounds.Size)))
            {
                layout.Draw(context, rect.TopLeft, FontSize * _scale, Context.Style.Foreground);
            }
            return;
        }
        if (_source is not null)
        {
            if (selected)
            {
                context.FillRectangle(Context.Style.SelectionBackground, new Rect(0, 0, _source.WidthIncludingTrailingWhitespace, _source.Height));
            }
            _source.Draw(context, default);
        }
    }

    public int HitTest(Point point)
    {
        var rect = _layout is null ? new Rect(Bounds.Size) : FormulaRect;
        return point.X < rect.Center.X ? 0 : _latex.Length;
    }

    public Rect GetCharacterBounds(int offset)
    {
        var rect = _layout is null ? new Rect(Bounds.Size) : FormulaRect;
        return offset <= 0 ? new Rect(rect.X, rect.Y, 1, rect.Height) : new Rect(rect.Right, rect.Y, 1, rect.Height);
    }

    public override IEnumerable<ISelectableSegment> Segments() => [this];

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Context is not null)
        {
            Context.Selection.Changed += InvalidateVisual;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Context is not null)
        {
            Context.Selection.Changed -= InvalidateVisual;
        }
    }
}
