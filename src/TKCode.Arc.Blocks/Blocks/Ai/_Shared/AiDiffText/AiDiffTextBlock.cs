using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The colors an <see cref="AiDiffTextBlock"/> draws with, computed by <see cref="AiDiffText"/> from its theme brushes:
/// the three word colors, the signs, the tints, the strikethrough and the four chip tones.
/// </summary>
internal sealed class AiDiffPalette
{
    /// <summary>Kept words.</summary>
    public IBrush? Equal { get; init; }

    /// <summary>Inserted words.</summary>
    public IBrush? Insert { get; init; }

    /// <summary>Deleted words.</summary>
    public IBrush? Delete { get; init; }

    /// <summary>The "+" sign.</summary>
    public IBrush? InsertSign { get; init; }

    /// <summary>The "−" sign.</summary>
    public IBrush? DeleteSign { get; init; }

    /// <summary>Tint behind inserted runs.</summary>
    public IBrush? InsertTint { get; init; }

    /// <summary>Tint behind deleted runs (none on the card).</summary>
    public IBrush? DeleteTint { get; init; }

    /// <summary>The strikethrough of deleted words.</summary>
    public IPen? Strike { get; init; }

    /// <summary>Chip fills by tone (index 0 is tone 1).</summary>
    public IBrush?[] ChipFill { get; init; } = new IBrush?[4];

    /// <summary>Chip inner outlines by tone.</summary>
    public IPen?[] ChipRing { get; init; } = new IPen?[4];

    /// <summary>Chip names by tone.</summary>
    public IBrush?[] ChipText { get; init; } = new IBrush?[4];
}

/// <summary>
/// The text of an <see cref="AiDiffText"/>: a <see cref="DecoratedTextBlock"/> that shows an <see cref="AiDiffDocument"/>.
/// Each run of the document gets its own run properties (word colors, the medium-weight sign, the smaller chip name, the
/// transparent chip markers and the paddings squeezed to their widths), its marks become <see cref="TextRangeDecoration"/>s
/// (tints and chip fills under the glyphs, strikethroughs over them), and each chip gets a 1px inner outline drawn after the
/// text. Selection works as in any selectable text block; a copy drops the signs and paddings
/// (<see cref="AiDiffDocument.CopyText"/>).
/// </summary>
/// <remarks>
/// The document is the only source of <c>Text</c>: do not set it, or the runs of the document no longer match and the text
/// is drawn plain. The paddings are no-break spaces whose font size is chosen so they are exactly as wide as the document
/// asks in the current font (measured once per typeface), so the geometry does not depend on the font's space width.
/// </remarks>
internal sealed class AiDiffTextBlock : DecoratedTextBlock
{
    private AiDiffDocument _document = AiDiffDocument.Empty;
    private AiDiffAppearance _appearance;
    private AiDiffPalette? _palette;

    // Advance of a string at 1 DIP in the current typeface (paddings and chip markers are sized from it).
    private readonly Dictionary<string, double> _advances = new(StringComparer.Ordinal);
    private Typeface _advanceTypeface;

    // The run properties of the last layout, reused while nothing they depend on changed (a selection drag lays the text
    // out again on every move).
    private TextRunProperties[]? _runs;
    private RunsKey _runsKey;

    // The chip boxes of the current layout, for the inner outlines; measured again when the layout or document changes.
    private readonly List<(RoundedRect Box, int Tone)> _chipBoxes = new();
    private TextLayout? _chipLayout;

    static AiDiffTextBlock()
    {
        // Selectable with the pointer like web text, but not a stop on the Tab path.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<AiDiffTextBlock>(false);
        TextWrappingProperty.OverrideDefaultValue<AiDiffTextBlock>(TextWrapping.Wrap);
    }

    public AiDiffTextBlock()
    {
        AddHandler(CopyingToClipboardEvent, OnCopying);
    }

    protected override Type StyleKeyOverride => typeof(AiDiffTextBlock);

    /// <summary>The document shown.</summary>
    public AiDiffDocument Document => _document;

    /// <summary>The colors; null draws the words in <c>Foreground</c> without marks.</summary>
    public AiDiffPalette? Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            ApplyPalette();
            InvalidateTextLayout();
        }
    }

    /// <summary>How many times the run properties were built (tests: a new selection reuses them).</summary>
    internal int RunPropertiesBuilt { get; private set; }

    /// <summary>The text a copy of the current selection gives (signs and paddings left out).</summary>
    internal string SelectedCopyText => _document.CopyText(SelectionStart, SelectionEnd);

    /// <summary>Shows <paramref name="document"/>: its text, runs and marks (outsets and radii for <paramref name="appearance"/>).</summary>
    public void Show(AiDiffDocument document, AiDiffAppearance appearance)
    {
        _document = document;
        _appearance = appearance;
        _chipLayout = null;
        Decorations.Clear();
        foreach (var mark in document.Marks)
        {
            Decorations.Add(new TextRangeDecoration(mark.Start, mark.Length)
            {
                Extent = TextRangeExtent.Text,
                Outset = AiDiffDocument.OutsetOf(mark.Kind, appearance),
                CornerRadius = AiDiffDocument.RadiusOf(mark.Kind),
                Tag = mark
            });
        }
        ApplyPalette();
        if (Text == document.Text)
        {
            // Same characters, other runs (a new appearance or chip tones).
            InvalidateTextLayout();
        }
        else
        {
            ClearSelection();
            SetCurrentValue(TextProperty, document.Text);
        }
    }

    protected override TextLayout CreateTextLayout(string? text)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var size = FontSize;
        var key = new RunsKey(_document, _palette, typeface, size, LetterSpacing, FontFeatures, _palette?.Equal ?? Foreground);
        if (_runs is null || _runsKey != key)
        {
            var defaults = new GenericTextRunProperties(typeface, size, null, key.Equal, null, BaselineAlignment.Baseline, null, FontFeatures);
            _runs = [defaults, .. RunProperties(typeface, size, defaults)];
            _runsKey = key;
            RunPropertiesBuilt++;
        }
        var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment, true, false, _runs[0], TextWrapping, LineHeight, 0,
            LetterSpacing);
        var source = text == _document.Text && _document.Spans.Count > 0
            ? new DiffTextSource(text, _document, _runs)
            : new DiffTextSource(text ?? string.Empty, null, _runs);
        var max = GetMaxSizeFromConstraint();
        return new TextLayout(source, paragraph, TextTrimming, max.Width, max.Height, MaxLines);
    }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        base.RenderTextLayout(context, origin);
        if (_palette is not { } palette || _document.Marks.Count == 0)
        {
            return;
        }
        var layout = TextLayout;
        if (!ReferenceEquals(layout, _chipLayout))
        {
            MeasureChips(layout);
        }
        if (_chipBoxes.Count == 0)
        {
            return;
        }
        using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            foreach (var (box, tone) in _chipBoxes)
            {
                if (palette.ChipRing[tone - 1] is { } ring)
                {
                    // A 1px line just inside the box (CSS inset box-shadow).
                    context.DrawRectangle(null, ring, box.Deflate(0.5, 0.5));
                }
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _chipLayout = null;
        _chipBoxes.Clear();
    }

    /// <summary>The chip boxes of <paramref name="layout"/>: each line of a chip's range, grown by the chip outset.</summary>
    private void MeasureChips(TextLayout layout)
    {
        _chipLayout = layout;
        _chipBoxes.Clear();
        var outset = AiDiffDocument.OutsetOf(AiDiffMarkKind.Chip, _appearance);
        foreach (var mark in _document.Marks)
        {
            if (mark.Kind != AiDiffMarkKind.Chip)
            {
                continue;
            }
            foreach (var rect in TextRangeRenderer.GetRects(layout, mark.Start, mark.Length))
            {
                var box = new Rect(rect.X - outset.Left, rect.Y - outset.Top, rect.Width + outset.Left + outset.Right,
                    rect.Height + outset.Top + outset.Bottom);
                if (box.Width > 1 && box.Height > 1)
                {
                    _chipBoxes.Add((new RoundedRect(box, Math.Min(AiDiffDocument.ChipRadius, Math.Min(box.Width, box.Height) / 2)), mark.Tone));
                }
            }
        }
    }

    /// <summary>Puts the palette's brushes and pens on the decorations (no relayout).</summary>
    private void ApplyPalette()
    {
        var palette = _palette;
        foreach (var decoration in Decorations)
        {
            if (decoration.Tag is not AiDiffMark mark)
            {
                continue;
            }
            switch (mark.Kind)
            {
                case AiDiffMarkKind.InsertTint:
                    decoration.Background = palette?.InsertTint;
                    break;
                case AiDiffMarkKind.DeleteTint:
                    decoration.Background = palette?.DeleteTint;
                    break;
                case AiDiffMarkKind.Chip:
                    decoration.Background = palette?.ChipFill[mark.Tone - 1];
                    break;
                case AiDiffMarkKind.Strike:
                    decoration.StrikeThrough = palette?.Strike;
                    break;
            }
        }
        InvalidateDecorations();
    }

    /// <summary>The run properties of every span of the document, in span order.</summary>
    private TextRunProperties[] RunProperties(Typeface typeface, double size, TextRunProperties defaults)
    {
        var palette = _palette;
        var spans = _document.Spans;
        var result = new TextRunProperties[spans.Count];
        var features = FontFeatures;
        var letterSpacing = LetterSpacing;
        TextRunProperties? insert = null;
        TextRunProperties? delete = null;
        TextRunProperties? insertSign = null;
        TextRunProperties? deleteSign = null;
        var chipNames = new TextRunProperties?[4];
        var squeezed = new Dictionary<(string, double), TextRunProperties>();
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            result[i] = span.Kind switch
            {
                AiDiffSpanKind.Insert => insert ??= Props(typeface, size, palette?.Insert),
                AiDiffSpanKind.Delete => delete ??= Props(typeface, size, palette?.Delete),
                AiDiffSpanKind.InsertSign => insertSign ??= Props(Medium(typeface), size, palette?.InsertSign),
                AiDiffSpanKind.DeleteSign => deleteSign ??= Props(Medium(typeface), size, palette?.DeleteSign),
                AiDiffSpanKind.ChipName => chipNames[span.Tone - 1] ??= Props(typeface, size * AiDiffText.ChipFontScale, palette?.ChipText[span.Tone - 1]),
                AiDiffSpanKind.Space or AiDiffSpanKind.ChipBrace => Squeezed(_document.Text.Substring(span.Start, span.Length), span.Width),
                _ => defaults
            };
        }
        return result;

        TextRunProperties Props(Typeface face, double em, IBrush? brush) =>
            new GenericTextRunProperties(face, em, null, brush ?? defaults.ForegroundBrush, null, BaselineAlignment.Baseline, null, features);

        // Transparent glyphs whose font size makes the run `width` wide (letter spacing included).
        TextRunProperties Squeezed(string text, double width)
        {
            if (!squeezed.TryGetValue((text, width), out var props))
            {
                var advance = Advance(typeface, text);
                var em = advance > 0 ? (width - letterSpacing * text.Length) / advance : width;
                props = new GenericTextRunProperties(typeface, Math.Max(0.1, em), null, Brushes.Transparent, null, BaselineAlignment.Baseline,
                    null, features);
                squeezed.Add((text, width), props);
            }
            return props;
        }
    }

    private static Typeface Medium(Typeface typeface) => new(typeface.FontFamily, typeface.Style, FontWeight.Medium, typeface.Stretch);

    /// <summary>The advance of <paramref name="text"/> at a font size of 1, cached per typeface.</summary>
    private double Advance(Typeface typeface, string text)
    {
        if (_advanceTypeface != typeface)
        {
            _advances.Clear();
            _advanceTypeface = typeface;
        }
        if (!_advances.TryGetValue(text, out var advance))
        {
            using var layout = new TextLayout(text, typeface, 100);
            advance = layout.WidthIncludingTrailingWhitespace / 100;
            _advances.Add(text, advance);
        }
        return advance;
    }

    private void OnCopying(object? sender, RoutedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, this))
        {
            return;
        }
        // The selection as text: without the signs and paddings that are only drawn.
        e.Handled = true;
        var text = SelectedCopyText;
        if (text.Length > 0)
        {
            _ = ArcClipboard.CopyTextAsync(this, text);
        }
    }

    /// <summary>What the run properties depend on.</summary>
    private readonly record struct RunsKey(AiDiffDocument? Document, AiDiffPalette? Palette, Typeface Typeface, double Size,
                                           double LetterSpacing, FontFeatureCollection? Features, IBrush? Equal);

    /// <summary>
    /// Feeds the layout one run per document span (from the asked index to the end of its span). The properties are the
    /// defaults then one per span.
    /// </summary>
    private readonly struct DiffTextSource : ITextSource
    {
        private readonly string _text;
        private readonly AiDiffDocument? _document;
        private readonly TextRunProperties[] _properties;

        public DiffTextSource(string text, AiDiffDocument? document, TextRunProperties[] properties)
        {
            _text = text;
            _document = document;
            _properties = properties;
        }

        public TextRun? GetTextRun(int textSourceIndex)
        {
            if (textSourceIndex >= _text.Length)
            {
                return new TextEndOfParagraph();
            }
            if (_document is { } document && document.SpanAt(textSourceIndex) is var index and >= 0)
            {
                var span = document.Spans[index];
                return new TextCharacters(_text.AsMemory(textSourceIndex, span.Start + span.Length - textSourceIndex), _properties[index + 1]);
            }
            return new TextCharacters(_text.AsMemory(textSourceIndex), _properties[0]);
        }
    }
}
