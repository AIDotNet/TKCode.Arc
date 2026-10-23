using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The tag beside the active thought (hovered, or focused): the full thought on an inverted bubble and its score and fate
/// underneath at 68%. One tag at a time; it rises 3px out of the node's side as it fades in (160ms) and leaves at once.
/// It is decorative for assistive technology (the node's name carries the same words) and takes no input.
/// </summary>
internal sealed class ReasoningTreeTag : Control
{
    /// <summary>Widest bubble, padding (7 top, 10 sides, 8 bottom), radius, line heights and the gap between the lines.</summary>
    public const double MaxBubbleWidth = 264;
    public const double PadX = 10;
    public const double PadTop = 7;
    public const double PadBottom = 8;
    public const double Radius = 10;
    public const double BodyLine = 17.4;
    public const double MetaLine = 16.8;
    public const double LineGap = 2;
    public const double MetaOpacity = 0.68;

    /// <summary>Space between the node and the bubble.</summary>
    public const double Offset = 8;

    /// <summary>The rise of the entrance (px).</summary>
    public const double Rise = 3;

    private readonly ReasoningTreeStage _stage;
    private string _body = string.Empty;
    private string _meta = string.Empty;

    public ReasoningTreeTag(ReasoningTreeStage stage)
    {
        _stage = stage;
        IsHitTestVisible = false;
        Focusable = false;
        IsVisible = false;
        ClipToBounds = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        RenderTransform = Translate;
    }

    /// <summary>The offset applied while the anchor glides and while the bubble rises in.</summary>
    public TranslateTransform Translate { get; } = new();

    /// <summary>The node the tag belongs to (-1 for none).</summary>
    public int Anchor { get; private set; } = -1;

    /// <summary>True when the bubble sits under its node (no room above).</summary>
    public bool IsBelow { get; set; }

    /// <summary>The wall time the tag appeared (for its entrance).</summary>
    public double ShownAt { get; set; } = double.NegativeInfinity;

    /// <summary>The full text (first line).</summary>
    public string Body => _body;

    /// <summary>The score and fate (second line).</summary>
    public string Meta => _meta;

    /// <summary>Shows the tag for <paramref name="anchor"/> with the given lines (or hides it for -1).</summary>
    public void Show(int anchor, string body, string meta)
    {
        Anchor = anchor;
        IsVisible = anchor >= 0;
        if (body != _body || meta != _meta)
        {
            _body = body;
            _meta = meta;
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    /// <summary>Drops the cached layouts (theme change).</summary>
    public void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (body, meta) = Layouts();
        var width = Math.Max(body.WidthIncludingTrailingWhitespace, meta.WidthIncludingTrailingWhitespace);
        return new Size(Math.Ceiling(width + 2 * PadX), Math.Ceiling(PadTop + body.Height + LineGap + meta.Height + PadBottom));
    }

    public override void Render(DrawingContext context)
    {
        if (Anchor < 0)
        {
            return;
        }
        var (body, meta) = Layouts();
        var palette = _stage.Palette;
        var t = _stage.TagProgress;
        using (context.PushOpacity(t))
        {
            context.DrawRectangle(palette.Brush(palette.Foreground), null, new RoundedRect(new Rect(Bounds.Size), Radius));
            body.Draw(context, new Point(PadX, PadTop));
            using (context.PushOpacity(MetaOpacity))
            {
                meta.Draw(context, new Point(PadX, PadTop + body.Height + LineGap));
            }
        }
    }

    // Fetched from the shared cache on every use (the cache may start over and dispose what it held).
    private (TextLayout Body, TextLayout Meta) Layouts()
    {
        var palette = _stage.Palette;
        var brush = palette.Brush(palette.Background);
        var width = MaxBubbleWidth - 2 * PadX;
        return (palette.Texts.Get(_body, palette.Regular, 12, brush, width, 8, BodyLine),
            palette.Texts.Get(_meta, palette.Regular, 12, brush, width, 2, MetaLine));
    }
}
