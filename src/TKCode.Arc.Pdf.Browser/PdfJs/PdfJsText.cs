using System.Text;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The text of a page from pdf.js's text items. An item only knows where it starts and how long it is, not where its characters
/// are, so a character's box is its proportional share of the item by a rough width class. That is close enough for highlights and
/// selection in ordinary text, and can be off by a character width inside a long item in a font with unusual widths (ADR 0041, point 5).
/// </summary>
internal static class PdfJsText
{
    /// <summary>Numbers per item: length of its text, the transform (6), width, height, ascent, descent, flags.</summary>
    public const int Stride = 12;

    public const int FlagEndOfLine = 1;
    public const int FlagRightToLeft = 2;
    public const int FlagVertical = 4;

    /// <summary>Fractions of the font size for a font that does not say: about what ordinary fonts have.</summary>
    private const double DefaultAscent = 0.85;
    private const double DefaultDescent = -0.15;

    /// <param name="text">The items' text, one after the other.</param>
    /// <param name="values"><see cref="Stride"/> numbers per item.</param>
    /// <param name="view">The page's view (<see cref="PdfJsGeometry"/>).</param>
    public static PdfPageText Build(string text, ReadOnlySpan<double> values, ReadOnlySpan<double> view)
    {
        var output = new StringBuilder(text.Length + 16);
        var boxes = new List<Rect>(text.Length + 16);
        var offset = 0;
        var hasPrevious = false;
        var previousEndedLine = false;
        var (previousEndX, previousEndY, previousFont) = (0.0, 0.0, 0.0);
        var (previousAlongX, previousAlongY, previousUpX, previousUpY) = (0.0, 0.0, 0.0, 0.0);

        for (var at = 0; at + Stride <= values.Length; at += Stride)
        {
            var item = values.Slice(at, Stride);
            var length = Math.Min((int)item[0], text.Length - offset);
            var span = text.AsSpan(offset, Math.Max(length, 0));
            offset += span.Length;
            var flags = (int)item[11];
            var vertical = (flags & FlagVertical) != 0;

            // Along the text, and towards the top of its letters, in page space: unit vectors from the transform.
            var (a, b, c, d) = (item[1], item[2], item[3], item[4]);
            var (originX, originY) = (item[5], item[6]);
            var (lengthAB, lengthCD) = (Math.Sqrt(a * a + b * b), Math.Sqrt(c * c + d * d));
            var (alongX, alongY, upX, upY, advance, fontSize) = vertical
                ? (-Safe(c, lengthCD), -Safe(d, lengthCD), Safe(a, lengthAB), Safe(b, lengthAB), item[8], lengthAB)
                : (Safe(a, lengthAB), Safe(b, lengthAB), Safe(c, lengthCD), Safe(d, lengthCD), item[7], lengthCD);

            if (span.Length > 0)
            {
                // pdf.js marks most line ends; columns and tables can also just continue somewhere else.
                if (hasPrevious && !previousEndedLine && output.Length > 0 && !char.IsWhiteSpace(output[^1]) && !char.IsWhiteSpace(span[0]))
                {
                    var (dx, dy) = (originX - previousEndX, originY - previousEndY);
                    if (Math.Abs(dx * previousUpX + dy * previousUpY) > previousFont * 0.5)
                    {
                        Append(output, boxes, '\n');
                    }
                    else if (Math.Abs(dx * previousAlongX + dy * previousAlongY) > previousFont * 0.3)
                    {
                        Append(output, boxes, ' ');
                    }
                }
                var ascent = item[9] > 0 ? Math.Min(item[9], 1.5) : DefaultAscent;
                var descent = item[10] != 0 ? -Math.Min(Math.Abs(item[10]), 1.5) : DefaultDescent;
                AddCharacters(output, boxes, span, view, (flags & FlagRightToLeft) != 0, advance,
                    originX, originY, alongX, alongY, upX * fontSize, upY * fontSize, ascent, descent);
                hasPrevious = true;
                (previousEndX, previousEndY, previousFont) = (originX + alongX * advance, originY + alongY * advance, fontSize);
                (previousAlongX, previousAlongY, previousUpX, previousUpY) = (alongX, alongY, upX, upY);
            }
            previousEndedLine = false;
            if ((flags & FlagEndOfLine) != 0)
            {
                Append(output, boxes, '\n');
                previousEndedLine = true;
            }
        }
        return new PdfPageText(output.ToString(), boxes.ToArray());
    }

    private static double Safe(double value, double length) => length > 0 ? value / length : 0;

    private static void Append(StringBuilder output, List<Rect> boxes, char character)
    {
        output.Append(character);
        boxes.Add(default);
    }

    private static void AddCharacters(StringBuilder output, List<Rect> boxes, ReadOnlySpan<char> text, ReadOnlySpan<double> view, bool rightToLeft, double advance,
        double originX, double originY, double alongX, double alongY, double upX, double upY, double ascent, double descent)
    {
        var total = 0.0;
        for (var i = 0; i < text.Length; i += LengthAt(text, i))
        {
            total += WidthClass(text, i);
        }
        var position = 0.0;
        for (var i = 0; i < text.Length;)
        {
            var units = LengthAt(text, i);
            var valid = units == 2 || !char.IsSurrogate(text[i]);
            var width = WidthClass(text, i);
            Rect box = default;
            if (text[i] is not ('\r' or '\n') && total > 0 && advance > 0 && width > 0)
            {
                var (from, to) = (position / total * advance, (position + width) / total * advance);
                if (rightToLeft)
                {
                    (from, to) = (advance - to, advance - from);
                }
                box = Box(view, originX, originY, alongX, alongY, upX, upY, from, to, ascent, descent);
            }
            position += width;
            // Every text the rest of the code sees must be valid UTF-16 (Normalize, in search, throws on a lone surrogate).
            if (!valid || text[i] == '\0')
            {
                output.Append('�');
            }
            else
            {
                output.Append(text.Slice(i, units));
            }
            boxes.Add(box);
            if (units == 2)
            {
                boxes.Add(box);
            }
            i += units;
        }
    }

    private static Rect Box(ReadOnlySpan<double> view, double originX, double originY, double alongX, double alongY, double upX, double upY,
        double from, double to, double ascent, double descent)
    {
        var (minX, minY, maxX, maxY) = (double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);
        foreach (var along in (ReadOnlySpan<double>)[from, to])
        {
            foreach (var up in (ReadOnlySpan<double>)[ascent, descent])
            {
                var point = PdfJsGeometry.Map(view, originX + alongX * along + upX * up, originY + alongY * along + upY * up);
                (minX, minY, maxX, maxY) = (Math.Min(minX, point.X), Math.Min(minY, point.Y), Math.Max(maxX, point.X), Math.Max(maxY, point.Y));
            }
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static int LengthAt(ReadOnlySpan<char> text, int index) =>
        index + 1 < text.Length && char.IsSurrogatePair(text[index], text[index + 1]) ? 2 : 1;

    /// <summary>A rough width in em: all that can be said about a glyph without its font, and much better than treating all alike, as a line mixing Chinese and Latin shows.</summary>
    private static double WidthClass(ReadOnlySpan<char> text, int index)
    {
        var c = text[index];
        if (LengthAt(text, index) == 2)
        {
            return 1;
        }
        if (char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.EnclosingMark or System.Globalization.UnicodeCategory.Format)
        {
            return 0;
        }
        if (c < 0x80)
        {
            return c switch
            {
                'i' or 'j' or 'l' or 'I' or '.' or ',' or ':' or ';' or '\'' or '!' or '|' or ' ' => 0.28,
                'f' or 't' or 'r' or '(' or ')' or '[' or ']' or '-' or '/' => 0.36,
                'm' or 'w' or 'W' or 'M' or '@' or '%' => 0.85,
                >= 'A' and <= 'Z' => 0.67,
                _ => 0.52
            };
        }
        // CJK, kana, Hangul, full-width forms and their punctuation take a whole em.
        return c is (>= 'ᄀ' and <= 'ᅟ') or (>= '⺀' and <= '꓏') or (>= '가' and <= '힣') or (>= '豈' and <= '﫿') or (>= '︰' and <= '﹯') or (>= '＀' and <= '｠')
            ? 1
            : 0.55;
    }
}
