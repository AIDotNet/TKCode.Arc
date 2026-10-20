using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Brushes for the syntax roles, resolved by <see cref="CodeBlock"/> from its token.</summary>
internal sealed class CodeBlockSyntaxPalette
{
    private readonly IBrush?[] _brushes = new IBrush?[10];

    public IBrush? this[CodeBlockSyntaxKind kind]
    {
        get => _brushes[(int)kind];
        set => _brushes[(int)kind] = value;
    }
}

/// <summary>
/// The selectable source text. One <see cref="TextLayout"/> per change, fed by a text source that hands out the
/// highlighted spans as runs (binary search, no per-token controls or inlines). Selection and Ctrl+C come from
/// <see cref="SelectableTextBlock"/>; the selection keeps the syntax colors (no selection foreground).
/// </summary>
internal sealed class CodeBlockText : SelectableTextBlock
{
    private readonly List<CodeBlockSyntaxSpan> _spans = new();
    private readonly TextRunProperties?[] _kindProperties = new TextRunProperties?[10];

    public CodeBlockSyntaxPalette? Palette { get; set; }

    protected override Type StyleKeyOverride => typeof(CodeBlockText);

    /// <summary>Number of highlighted spans (diagnostics and tests).</summary>
    public int SpanCount => _spans.Count;

    internal IReadOnlyList<CodeBlockSyntaxSpan> Spans => _spans;

    /// <summary>Tokenizes <paramref name="text"/> once and shows it.</summary>
    public void SetSource(string text, string language)
    {
        _spans.Clear();
        CodeBlockTokenizer.Tokenize(text, language, _spans);
        if (Text == text)
        {
            InvalidateTextLayout();
        }
        else
        {
            Text = text;
        }
    }

    /// <summary>Call after the palette brushes change.</summary>
    public void RefreshPalette() => InvalidateTextLayout();

    protected override TextLayout CreateTextLayout(string? text)
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var defaults = new GenericTextRunProperties(typeface, FontSize, null, Foreground, null, BaselineAlignment.Baseline, null, FontFeatures);
        var italic = new Typeface(FontFamily, FontStyle.Italic, FontWeight, FontStretch);
        for (var i = 1; i < _kindProperties.Length; i++)
        {
            var kind = (CodeBlockSyntaxKind)i;
            var brush = Palette?[kind] ?? Foreground;
            _kindProperties[i] = new GenericTextRunProperties(kind == CodeBlockSyntaxKind.Comment ? italic : typeface, FontSize,
                null, brush, null, BaselineAlignment.Baseline, null, FontFeatures);
        }
        var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment.Left, true, false, defaults,
            TextWrapping.NoWrap, LineHeight, 0, LetterSpacing);
        var source = new CodeBlockTextSource(text ?? string.Empty, defaults, _spans, _kindProperties);
        return new TextLayout(source, paragraph, null, double.PositiveInfinity, double.PositiveInfinity, 0);
    }

    private readonly struct CodeBlockTextSource : ITextSource
    {
        private readonly string _text;
        private readonly TextRunProperties _defaults;
        private readonly List<CodeBlockSyntaxSpan> _spans;
        private readonly TextRunProperties?[] _kinds;

        public CodeBlockTextSource(string text, TextRunProperties defaults, List<CodeBlockSyntaxSpan> spans, TextRunProperties?[] kinds)
        {
            _text = text;
            _defaults = defaults;
            _spans = spans;
            _kinds = kinds;
        }

        public TextRun? GetTextRun(int textSourceIndex)
        {
            if (textSourceIndex >= _text.Length)
            {
                return new TextEndOfParagraph();
            }
            // First span that ends after the index.
            var lo = 0;
            var hi = _spans.Count - 1;
            var found = _spans.Count;
            while (lo <= hi)
            {
                var mid = (lo + hi) >> 1;
                var span = _spans[mid];
                if (span.Start + span.Length > textSourceIndex)
                {
                    found = mid;
                    hi = mid - 1;
                }
                else
                {
                    lo = mid + 1;
                }
            }
            if (found < _spans.Count)
            {
                var span = _spans[found];
                if (span.Start <= textSourceIndex)
                {
                    var end = span.Start + span.Length;
                    return new TextCharacters(_text.AsMemory(textSourceIndex, end - textSourceIndex), _kinds[(int)span.Kind] ?? _defaults);
                }
                return new TextCharacters(_text.AsMemory(textSourceIndex, span.Start - textSourceIndex), _defaults);
            }
            return new TextCharacters(_text.AsMemory(textSourceIndex), _defaults);
        }
    }
}
