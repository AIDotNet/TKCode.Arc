using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A text of a <see cref="DocumentPage"/> (a paragraph, a line, a cell): a plain <see cref="TextBlock"/> that also draws the
/// backgrounds and check underlines of the spans in it, through Core's <see cref="TextRangeRenderer"/> on its own layout
/// (backgrounds under the glyphs, underlines over them). Painting a span only redraws; the text is never laid out again.
/// </summary>
/// <remarks>
/// Not a <see cref="DecoratedTextBlock"/>: the page needs no selection (and no I-beam cursor), so the base is the plain text
/// block. It does not clip, so a background's padding can reach past the first and last lines and the line ends.
/// </remarks>
internal sealed class DocumentPageText : TextBlock
{
    private readonly List<TextRangeDecoration> _decorations = [];

    static DocumentPageText()
    {
        ClipToBoundsProperty.OverrideDefaultValue<DocumentPageText>(false);
    }

    protected override Type StyleKeyOverride => typeof(DocumentPageText);

    /// <summary>The span decorations drawn on the text, backgrounds and underlines (tests).</summary>
    internal IReadOnlyList<TextRangeDecoration> Decorations => _decorations;

    /// <summary>
    /// Where the base draws the layout: the top-left of the padding, moved by the vertical alignment when the box is
    /// shorter than the text (a line height below the font's own, centered as CSS centers the content area).
    /// </summary>
    internal Point TextOrigin
    {
        get
        {
            var padding = Padding;
            if (UseLayoutRounding)
            {
                padding = LayoutHelper.RoundLayoutThickness(padding, LayoutHelper.GetLayoutScale(this));
            }
            var top = padding.Top;
            var textHeight = TextLayout.Height;
            if (Bounds.Height < textHeight)
            {
                top += VerticalAlignment switch
                {
                    VerticalAlignment.Center => (Bounds.Height - textHeight) / 2,
                    VerticalAlignment.Bottom => Bounds.Height - textHeight,
                    _ => 0
                };
            }
            return new Point(padding.Left, top);
        }
    }

    /// <summary>
    /// Adds a span's background and underline (the page owns and updates them). Order does not matter: the renderer draws
    /// every background before the glyphs and every underline after them.
    /// </summary>
    internal void AddSpan(TextRangeDecoration background, TextRangeDecoration underline)
    {
        _decorations.Add(background);
        _decorations.Add(underline);
        InvalidateVisual();
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        if (_decorations.Count == 0)
        {
            base.RenderTextLayout(context, origin);
            return;
        }
        var layout = TextLayout;
        TextRangeRenderer.DrawBackgrounds(context, layout, _decorations, origin);
        base.RenderTextLayout(context, origin);
        TextRangeRenderer.DrawLines(context, layout, _decorations, origin);
    }
}
