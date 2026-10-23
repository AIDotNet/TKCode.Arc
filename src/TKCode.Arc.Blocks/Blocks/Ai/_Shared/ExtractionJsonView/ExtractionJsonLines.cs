using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>How the lines of an <see cref="ExtractionJsonLines"/> are drawn: font, line height and a brush per piece kind.</summary>
internal sealed record ExtractionJsonStyle(
    Typeface Typeface,
    double FontSize,
    double LineHeight,
    IBrush Key,
    IBrush String,
    IBrush Number,
    IBrush Muted,
    IBrush Punctuation)
{
    /// <summary>The brush of a piece kind.</summary>
    public IBrush BrushOf(JsonTokenKind kind) => kind switch
    {
        JsonTokenKind.Key => Key,
        JsonTokenKind.String => String,
        JsonTokenKind.Number => Number,
        JsonTokenKind.Muted => Muted,
        _ => Punctuation
    };
}

/// <summary>
/// The lines of an <see cref="ExtractionJsonView"/>, drawn by hand: one cached <see cref="TextLayout"/> per distinct line
/// text (a changed output builds layouts only for the lines whose text changed), only the lines inside the effective
/// viewport are drawn, and the few tinted line backgrounds (the linked line, the one fading out, the flashing one) come
/// from the owner's brushes, which it recolors between frames. Each line is <see cref="InlinePadding"/> in from both sides;
/// the control is at least as wide as the code block so backgrounds reach its edges.
/// </summary>
internal sealed class ExtractionJsonLines : Control
{
    /// <summary>Space before and after a line's text (spec 3.5: lines are padded 0 14).</summary>
    internal const double InlinePadding = 14;

    private readonly Dictionary<string, TextLayout> _cache = new(StringComparer.Ordinal);
    private ExtractionJsonLine[] _lines = [];
    private TextLayout?[] _layouts = [];
    private ExtractionJsonStyle? _style;
    private bool _layoutsStale = true;
    private double _textWidth;
    private Rect _viewport;
    private bool _viewportKnown;

    /// <summary>The view that owns this (its line backgrounds are drawn under the text).</summary>
    internal ExtractionJsonView? Owner { get; set; }

    /// <summary>The lines shown.</summary>
    internal IReadOnlyList<ExtractionJsonLine> Lines => _lines;

    /// <summary>The style in use (null until the owner sets one).</summary>
    internal ExtractionJsonStyle? Style => _style;

    /// <summary>The height of a line.</summary>
    internal double LineHeight => _style?.LineHeight ?? 20.4;

    /// <summary>How many layouts the last rebuild made (the others were reused because their text did not change).</summary>
    internal int LastBuiltCount { get; private set; }

    /// <summary>How many lines the last render drew (the ones in the viewport).</summary>
    internal int LastDrawnCount { get; private set; }

    /// <summary>The layout of line <paramref name="index"/> (built on demand), or null.</summary>
    internal TextLayout? LayoutOf(int index)
    {
        EnsureLayouts();
        return index >= 0 && index < _layouts.Length ? _layouts[index] : null;
    }

    /// <summary>Shows <paramref name="lines"/>; their layouts are made at the next measure.</summary>
    internal void SetLines(ExtractionJsonLine[] lines)
    {
        _lines = lines;
        _layoutsStale = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Sets the font and colors; every layout is made again.</summary>
    internal void SetStyle(ExtractionJsonStyle style)
    {
        if (style == _style)
        {
            return;
        }
        _style = style;
        foreach (var layout in _cache.Values)
        {
            layout.Dispose();
        }
        _cache.Clear();
        _layoutsStale = true;
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Subscribed before the first layout so the first viewport is reported (see the pitfalls on viewport detection).
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EffectiveViewportChanged -= OnEffectiveViewportChanged;
        _viewportKnown = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayouts();
        return new Size(_lines.Length == 0 ? 0 : _textWidth + InlinePadding * 2, _lines.Length * LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        // The whole block takes the pointer (hover and press map to lines), not only the glyphs.
        context.FillRectangle(Brushes.Transparent, new Rect(size));
        if (_lines.Length == 0)
        {
            LastDrawnCount = 0;
            return;
        }
        EnsureLayouts();
        var height = LineHeight;
        var first = 0;
        var last = _lines.Length;
        if (_viewportKnown)
        {
            first = (int)Math.Clamp(Math.Floor(_viewport.Top / height), 0, _lines.Length);
            last = double.IsInfinity(_viewport.Bottom) ? _lines.Length : (int)Math.Clamp(Math.Ceiling(_viewport.Bottom / height), 0, _lines.Length);
        }
        Owner?.DrawLineBackgrounds(context, size.Width, height, first, last);
        var drawn = 0;
        for (var i = first; i < last; i++)
        {
            if (_layouts[i] is { } layout)
            {
                layout.Draw(context, new Point(InlinePadding, i * height));
                drawn++;
            }
        }
        LastDrawnCount = drawn;
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        if (_viewportKnown && viewport == _viewport)
        {
            return;
        }
        var height = LineHeight;
        var changedLines = !_viewportKnown || Math.Floor(viewport.Top / height) != Math.Floor(_viewport.Top / height) ||
                           Math.Ceiling(viewport.Bottom / height) != Math.Ceiling(_viewport.Bottom / height);
        _viewport = viewport;
        _viewportKnown = true;
        if (changedLines)
        {
            InvalidateVisual();
        }
    }

    /// <summary>Makes a layout per line, reusing the layouts of texts that were already shown and dropping the rest.</summary>
    private void EnsureLayouts()
    {
        if (!_layoutsStale || _style is not { } style)
        {
            return;
        }
        _layoutsStale = false;
        var next = new Dictionary<string, TextLayout>(StringComparer.Ordinal);
        var layouts = new TextLayout?[_lines.Length];
        var built = 0;
        var width = 0.0;
        for (var i = 0; i < _lines.Length; i++)
        {
            var line = _lines[i];
            if (!next.TryGetValue(line.Text, out var layout))
            {
                if (_cache.Remove(line.Text, out var kept))
                {
                    layout = kept;
                }
                else
                {
                    layout = Build(line, style);
                    built++;
                }
                next[line.Text] = layout;
            }
            layouts[i] = layout;
            width = Math.Max(width, layout.WidthIncludingTrailingWhitespace);
        }
        foreach (var stale in _cache.Values)
        {
            stale.Dispose();
        }
        _cache.Clear();
        foreach (var entry in next)
        {
            _cache[entry.Key] = entry.Value;
        }
        _layouts = layouts;
        _textWidth = width;
        LastBuiltCount = built;
    }

    private static TextLayout Build(ExtractionJsonLine line, ExtractionJsonStyle style)
    {
        var tokens = line.Tokens;
        var spans = new ValueSpan<TextRunProperties>[tokens.Count];
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            spans[i] = new ValueSpan<TextRunProperties>(token.Start, token.Length,
                new GenericTextRunProperties(style.Typeface, style.FontSize, null, style.BrushOf(token.Kind)));
        }
        return new TextLayout(line.Text, style.Typeface, style.FontSize, style.Punctuation, TextAlignment.Left, TextWrapping.NoWrap,
            TextTrimming.None, null, FlowDirection.LeftToRight, double.PositiveInfinity, double.PositiveInfinity, style.LineHeight, 0, 0,
            null, spans);
    }
}
