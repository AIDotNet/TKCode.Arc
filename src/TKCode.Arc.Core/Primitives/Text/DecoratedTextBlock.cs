using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A <see cref="SelectableTextBlock"/> that draws <see cref="Decorations"/> on character ranges of its text: rounded
/// backgrounds under the glyphs (repeated on every wrapped line) and strikethroughs / underlines over them, through
/// <see cref="TextRangeRenderer"/>. Decorations never relayout: adding, removing or repainting them (opacity, brushes,
/// pens) only redraws, so a highlight can fade every frame. Selection, copy and the rest of
/// <see cref="SelectableTextBlock"/> are unchanged; the selection highlight draws over the backgrounds.
/// </summary>
/// <remarks>
/// <see cref="Visual.ClipToBounds"/> defaults to false (true on <see cref="TextBlock"/>) so a background's outset can
/// reach past the first and last lines and the line ends.
/// </remarks>
public class DecoratedTextBlock : SelectableTextBlock
{
    static DecoratedTextBlock()
    {
        ClipToBoundsProperty.OverrideDefaultValue<DecoratedTextBlock>(false);
    }

    /// <summary>Creates the block with an empty <see cref="Decorations"/> list.</summary>
    public DecoratedTextBlock()
    {
        // Clear reports the removed items, so their caches let go of the layout too.
        Decorations = new AvaloniaList<TextRangeDecoration> { ResetBehavior = ResetBehavior.Remove };
        Decorations.CollectionChanged += OnDecorationsChanged;
    }

    /// <summary>
    /// The decorations, drawn in order (later ones over earlier ones). Indices are into <c>Text</c>, or into the
    /// concatenated run text of <c>Inlines</c> (an <c>InlineUIContainer</c> counts as one character).
    /// </summary>
    public AvaloniaList<TextRangeDecoration> Decorations { get; }

    /// <summary>
    /// Redraws after a decoration was changed in place (opacity, brushes, pens, outset, radius, range). Never
    /// measures again: a changed range is measured on the next draw against the current layout.
    /// </summary>
    public void InvalidateDecorations() => InvalidateVisual();

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        // The base draws the selection highlight, then the glyphs; backgrounds go under both, lines over them.
        if (Decorations.Count == 0)
        {
            base.RenderTextLayout(context, origin);
            return;
        }
        var layout = TextLayout;
        TextRangeRenderer.DrawBackgrounds(context, layout, Decorations, origin);
        base.RenderTextLayout(context, origin);
        TextRangeRenderer.DrawLines(context, layout, Decorations, origin);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // The base disposes its layout; do not keep it alive through the caches.
        foreach (var decoration in Decorations)
        {
            decoration.ResetCache();
        }
    }

    private void OnDecorationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is { } removed)
        {
            foreach (var item in removed)
            {
                if (item is TextRangeDecoration decoration)
                {
                    decoration.ResetCache();
                }
            }
        }
        InvalidateVisual();
    }
}
