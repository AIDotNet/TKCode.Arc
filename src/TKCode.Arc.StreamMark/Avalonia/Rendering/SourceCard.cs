using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Extensions;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 引用来源卡片：悬停 <c>&lt;Citation id&gt;</c> 时显示在窗口覆盖层上（标题、网站 · 主机、摘要），点击打开来源地址。
/// 自绘，不依赖宿主主题模板。
/// </summary>
public sealed class SourceCard : Control
{
    private const double CardWidth = 320;
    private const double Pad = 12;

    private readonly TextLayout _title;
    private readonly TextLayout? _site;
    private readonly TextLayout? _snippet;
    private readonly MarkdownStyle _style;
    private readonly Action? _open;

    private SourceCard(CitationSource source, MarkdownStyle style, Action? open)
    {
        _style = style;
        _open = open;
        var inner = CardWidth - Pad * 2;
        _title = new TextLayout(source.Title, new Typeface(style.FontFamily, FontStyle.Normal, FontWeight.SemiBold), style.FontSize * 0.95, style.Foreground,
            textWrapping: TextWrapping.Wrap, maxWidth: inner, maxLines: 2, textTrimming: TextTrimming.CharacterEllipsis);
        var host = Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ? uri.Host : null;
        var site = string.Join(" · ", new[] { source.SiteName, host }.Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.OrdinalIgnoreCase));
        if (site.Length > 0)
        {
            _site = new TextLayout(site, new Typeface(style.FontFamily), style.FontSize * 0.8, style.SecondaryForeground,
                maxWidth: inner, textTrimming: TextTrimming.CharacterEllipsis);
        }
        if (!string.IsNullOrWhiteSpace(source.Snippet))
        {
            _snippet = new TextLayout(source.Snippet, new Typeface(style.FontFamily), style.FontSize * 0.85, style.SecondaryForeground,
                textWrapping: TextWrapping.Wrap, maxWidth: inner, maxLines: 3, textTrimming: TextTrimming.CharacterEllipsis);
        }
        Width = CardWidth;
        Height = Pad + _title.Height + (_site is null ? 0 : 4 + _site.Height) + (_snippet is null ? 0 : 6 + _snippet.Height) + Pad;
        Cursor = open is null ? Cursor.Default : new Cursor(StandardCursorType.Hand);
    }

    public bool IsPointerInside { get; private set; }

    /// <summary>在 <paramref name="anchor"/> 中的 <paramref name="anchorRect"/> 下方（放不下时上方）显示卡片；没有覆盖层时返回 null。</summary>
    public static SourceCard? Show(Visual anchor, Rect anchorRect, CitationSource source, MarkdownStyle style, Action? open)
    {
        if (OverlayLayer.GetOverlayLayer(anchor) is not { } layer || anchor.TranslatePoint(anchorRect.TopLeft, layer) is not { } topLeft)
        {
            return null;
        }
        var card = new SourceCard(source, style, open);
        // 覆盖层第一次加入子控件前可能还没有尺寸：用窗口客户区大小
        var area = layer.Bounds.Size is { Width: > 0, Height: > 0 } size ? size : TopLevel.GetTopLevel(anchor)?.ClientSize ?? new Size(double.MaxValue, double.MaxValue);
        var x = Math.Clamp(topLeft.X, 8, Math.Max(8, area.Width - card.Width - 8));
        var y = topLeft.Y + anchorRect.Height + 6;
        if (y + card.Height > area.Height - 8)
        {
            y = Math.Max(8, topLeft.Y - card.Height - 6);
        }
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, y);
        layer.Children.Add(card);
        return card;
    }

    public void Close()
    {
        if (Parent is OverlayLayer layer)
        {
            layer.Children.Remove(this);
        }
    }

    public override void Render(DrawingContext context)
    {
        var rect = new RoundedRect(new Rect(Bounds.Size).Deflate(0.5), 8);
        var shadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 16, Color = Color.FromArgb(0x40, 0, 0, 0) });
        context.DrawRectangle(_style.CodeBackground, new Pen(_style.CodeBorderBrush, 1), rect, shadow);
        var y = Pad;
        _title.Draw(context, new Point(Pad, y));
        y += _title.Height;
        if (_site is not null)
        {
            y += 4;
            _site.Draw(context, new Point(Pad, y));
            y += _site.Height;
        }
        if (_snippet is not null)
        {
            y += 6;
            _snippet.Draw(context, new Point(Pad, y));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        IsPointerInside = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        IsPointerInside = false;
        Close();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && _open is not null)
        {
            _open();
            Close();
            e.Handled = true;
        }
    }
}
