using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.Avalonia.Rendering;

/// <summary>渐显与光标共用的单调时钟。</summary>
public static class StreamClock
{
    private static readonly long Origin = Stopwatch.GetTimestamp();

    public static TimeSpan Now => Stopwatch.GetElapsedTime(Origin);
}

/// <summary>行内公式：按基线对齐嵌入文本行。</summary>
internal sealed class MathRun(MathLayout layout, double fontSize, IBrush foreground, TextRunProperties properties) : DrawableTextRun
{
    // 公式两侧留一点间距，避免紧贴相邻文字
    private readonly double _padding = fontSize * 0.08;

    public override TextRunProperties Properties { get; } = properties;

    public override Size Size => new(layout.Width * fontSize + _padding * 2, (layout.Ascent + layout.Descent) * fontSize);

    public override double Baseline => layout.Ascent * fontSize;

    public override void Draw(DrawingContext drawingContext, Point origin) =>
        layout.Draw(drawingContext, new Point(origin.X + _padding, origin.Y), fontSize, foreground);
}

/// <summary>以独立排版的文字绘制的对象（公式无法排版时显示源码、图片的替代文字等）。</summary>
internal sealed class TextObjectRun : DrawableTextRun
{
    private readonly TextLayout _layout;
    private readonly IBrush? _background;
    private readonly double _padding;

    public TextObjectRun(string text, TextRunProperties properties, IBrush? background, double padding = 0)
    {
        Properties = properties;
        _background = background;
        _padding = padding;
        _layout = new TextLayout(text, properties.Typeface, properties.FontRenderingEmSize, properties.ForegroundBrush);
    }

    public override TextRunProperties Properties { get; }

    public override Size Size => new(_layout.WidthIncludingTrailingWhitespace + _padding * 2, _layout.Height);

    public override double Baseline => _layout.TextLines.Count > 0 ? _layout.TextLines[0].Baseline : _layout.Height;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        if (_background is not null)
        {
            drawingContext.DrawRectangle(_background, null, new Rect(origin, Size), 3, 3);
        }
        _layout.Draw(drawingContext, new Point(origin.X + _padding, origin.Y));
    }
}

/// <summary>
/// 行内图片。已加载时按自然尺寸（或 width/height 属性）显示并缩放到可用宽度；
/// 加载中且已知像素尺寸时按宽高比绘制骨架占位（加载完成时不跳动）；其余状态显示小标签：
/// 加载中 / 失败（可重试）/ 需要确认（显示域名，可点击加载）/ 被策略禁止。
/// </summary>
internal sealed class ImageRun : DrawableTextRun
{
    private readonly IImage? _image;
    private readonly Size _size;
    private readonly TextObjectRun? _label;
    private readonly IBrush _skeleton;
    private readonly IBrush _border;

    public ImageRun(ImageSlot? slot, string alt, Size? declared, double maxWidth, TextRunProperties properties, MarkdownStyle style)
    {
        Properties = properties;
        _skeleton = style.InlineCodeBackground;
        _border = style.CodeBorderBrush;
        _image = slot?.Image;
        // 显示尺寸：像浏览器一样按自然像素尺寸（1 像素 = 1 DIP），width/height 属性优先；
        // 只写了宽度时按自然宽高比推算高度。解码出的位图可能更小（按显示宽度解码）或更大（高分屏），只用于绘制
        Size? natural = slot?.PixelSize is { Width: > 0, Height: > 0 } px ? new Size(px.Width, px.Height) : _image?.Size;
        var source = declared is { } d
            ? new Size(d.Width, double.IsNaN(d.Height) ? (natural is { } n ? d.Width * n.Height / n.Width : d.Width * 0.6) : d.Height)
            : natural;
        if (source is { } known && (_image is not null || slot is { IsLoading: true }))
        {
            var scale = double.IsFinite(maxWidth) && known.Width > maxWidth && maxWidth > 0 ? maxWidth / known.Width : 1;
            _size = new Size(Math.Max(1, known.Width * scale), Math.Max(1, known.Height * scale));
            return;
        }
        var text = slot?.Status switch
        {
            Images.ImageLoadStatus.Failed => "⚠ " + (string.IsNullOrWhiteSpace(alt) ? "image" : alt) + "  ↻",
            Images.ImageLoadStatus.NeedsConsent => "🖼 " + slot.Host + " · Load image",
            Images.ImageLoadStatus.Blocked => "🖼 " + (string.IsNullOrWhiteSpace(alt) ? "image" : alt),
            _ => "🖼 " + (string.IsNullOrWhiteSpace(alt) ? "…" : alt),
        };
        _label = new TextObjectRun(text, properties, style.InlineCodeBackground, 6);
        _size = _label.Size;
    }

    public override TextRunProperties Properties { get; }

    public override Size Size => _size;

    public override double Baseline => _label?.Baseline ?? _size.Height;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        var rect = new Rect(origin, _size);
        if (_image is not null)
        {
            using (drawingContext.PushClip(new RoundedRect(rect, 4)))
            {
                drawingContext.DrawImage(_image, new Rect(_image.Size), rect);
            }
            return;
        }
        if (_label is null)
        {
            // 骨架占位
            drawingContext.DrawRectangle(_skeleton, null, rect, 6, 6);
            return;
        }
        _label.Draw(drawingContext, origin);
        drawingContext.DrawRectangle(null, new Pen(_border, 1), rect.Deflate(0.5), 3, 3);
    }
}

/// <summary>任务列表复选框（只读）。</summary>
internal sealed class CheckboxRun(bool isChecked, double fontSize, IBrush foreground, IBrush accent, TextRunProperties properties) : DrawableTextRun
{
    private readonly double _box = Math.Round(fontSize * 0.95);

    public override TextRunProperties Properties { get; } = properties;

    public override Size Size => new(_box + fontSize * 0.45, _box);

    public override double Baseline => _box * 0.85;

    public override void Draw(DrawingContext drawingContext, Point origin)
    {
        var rect = new Rect(origin.X, origin.Y, _box, _box).Deflate(1);
        if (isChecked)
        {
            drawingContext.DrawRectangle(accent, null, rect, 3, 3);
            var pen = new Pen(Brushes.White, Math.Max(1.5, _box / 9), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(rect.X + rect.Width * 0.24, rect.Y + rect.Height * 0.52), false);
                g.LineTo(new Point(rect.X + rect.Width * 0.43, rect.Y + rect.Height * 0.70));
                g.LineTo(new Point(rect.X + rect.Width * 0.77, rect.Y + rect.Height * 0.32));
                g.EndFigure(false);
            }
            drawingContext.DrawGeometry(null, pen, geometry);
        }
        else
        {
            drawingContext.DrawRectangle(null, new Pen(foreground, 1.2), rect, 3, 3);
        }
    }
}
