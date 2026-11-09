using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Cell text shaped once and drawn many times: the lines a <c>FormattedText</c> with <c>MaxTextWidth</c>,
/// <c>Trimming = CharacterEllipsis</c> and <c>MaxLineCount</c> would produce, kept instead of rebuilt. <c>FormattedText</c>
/// re-formats its lines on every <c>Draw</c> and every metric read (about 3 KB and 0.3 ms per Latin cell, 22 KB with
/// Chinese text), which the hover and selection tweens of a table row pay for every cell on every frame.
/// </summary>
/// <remarks>
/// The line breaking is <c>FormattedText</c>'s own (its enumerator, step for step): lines wrap at words and let a long
/// word overflow (collapsed with the ellipsis), and when the next line does not fit the line count the last line is
/// formatted again without wrapping and collapsed, so it ends with the ellipsis over as much of the remaining text as
/// fits. A <see cref="TextLayout"/> with <c>maxLines</c> instead ends on the wrapped line, which draws differently for
/// capped multi-line cells; single lines and unlimited lines are the same either way.
/// </remarks>
internal sealed class TablesBText
{
    private sealed class Source : ITextSource
    {
        private readonly string _text;
        private readonly TextRunProperties _properties;

        public Source(string text, TextRunProperties properties)
        {
            _text = text;
            _properties = properties;
        }

        public TextRun GetTextRun(int textSourceIndex)
        {
            if (textSourceIndex >= _text.Length)
            {
                return new TextEndOfParagraph();
            }
            return new TextCharacters(_text.AsMemory(textSourceIndex), _properties);
        }
    }

    private readonly List<TextLine> _lines = new();

    /// <summary>Shapes <paramref name="text"/> in <paramref name="typeface"/> at <paramref name="fontSize"/> with <paramref name="brush"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="typeface">The typeface.</param>
    /// <param name="fontSize">The em size.</param>
    /// <param name="brush">The foreground (null when only measuring).</param>
    /// <param name="maxWidth">The width lines break at (at least 1).</param>
    /// <param name="maxLines">Lines kept before the last one is trimmed; 0 for no limit.</param>
    /// <param name="alignment">Horizontal alignment of the lines inside <paramref name="maxWidth"/>.</param>
    /// <param name="features">Font features (tabular figures for numbers).</param>
    public TablesBText(string text, Typeface typeface, double fontSize, IBrush? brush, double maxWidth, int maxLines,
                       TextAlignment alignment = TextAlignment.Left, FontFeatureCollection? features = null)
    {
        if (text.Length == 0)
        {
            return;
        }
        maxWidth = Math.Max(1, maxWidth);
        var limit = maxLines <= 0 ? int.MaxValue : maxLines;
        var runProperties = new GenericTextRunProperties(typeface, fontSize, null, brush, null, BaselineAlignment.Baseline,
            CultureInfo.CurrentCulture, features);
        var wrapping = new GenericTextParagraphProperties(FlowDirection.LeftToRight, alignment, false, false, runProperties,
            TextWrapping.WrapWithOverflow, 0, 0, 0);
        var unwrapped = new GenericTextParagraphProperties(FlowDirection.LeftToRight, alignment, false, false, runProperties,
            TextWrapping.NoWrap, 0, 0, 0);
        var source = new Source(text, runProperties);
        var formatter = TextFormatter.Current;

        TextLine? Format(int position, TextLineBreak? lineBreak, TextParagraphProperties paragraph)
        {
            var line = formatter.FormatLine(source, position, maxWidth, paragraph, lineBreak);
            if (line is not null && line.HasOverflowed && line.Length > 0)
            {
                line = line.Collapse(TextTrimming.CharacterEllipsis.CreateCollapsingProperties(
                    new TextCollapsingCreateInfo(maxWidth, runProperties, FlowDirection.LeftToRight)));
            }
            return line;
        }

        var current = Format(0, null, wrapping);
        var position = 0;
        var count = 0;
        TextLineBreak? previousBreak = null;
        while (current is not null)
        {
            var lineBreak = current.TextLineBreak;
            TextLine? next = null;
            if (position + current.Length < text.Length)
            {
                var fits = false;
                if (count + 1 < limit)
                {
                    next = Format(position + current.Length, lineBreak, wrapping);
                    fits = next is not null;
                }
                if (!fits)
                {
                    next = null;
                    if (!current.HasCollapsed)
                    {
                        // The last line that fits takes the rest of the text, unwrapped, and ends with the ellipsis.
                        var last = Format(position, previousBreak, unwrapped);
                        if (last is not null)
                        {
                            current = last;
                            lineBreak = current.TextLineBreak;
                        }
                    }
                }
            }
            _lines.Add(current);
            Height += current.Height;
            Width = Math.Max(Width, current.Width);
            WidthIncludingTrailingWhitespace = Math.Max(WidthIncludingTrailingWhitespace, current.WidthIncludingTrailingWhitespace);
            if (_lines.Count == 1)
            {
                Baseline = current.Baseline;
            }
            previousBreak = lineBreak;
            if (next is null)
            {
                break;
            }
            position += current.Length;
            count++;
            current = next;
        }
    }

    /// <summary>Sum of the line heights.</summary>
    public double Height { get; }

    /// <summary>Widest line, trailing whitespace excluded.</summary>
    public double Width { get; }

    /// <summary>Widest line, trailing whitespace included.</summary>
    public double WidthIncludingTrailingWhitespace { get; }

    /// <summary>Baseline of the first line.</summary>
    public double Baseline { get; }

    /// <summary>Number of lines.</summary>
    public int LineCount => _lines.Count;

    /// <summary>Draws the lines from <paramref name="origin"/> (the top-left of the text block).</summary>
    public void Draw(DrawingContext context, Point origin)
    {
        var y = origin.Y;
        foreach (var line in _lines)
        {
            line.Draw(context, new Point(origin.X, y));
            y += line.Height;
        }
    }
}
