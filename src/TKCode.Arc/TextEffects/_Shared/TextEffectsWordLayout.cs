using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Text style used to shape the units of a text effect.</summary>
internal readonly record struct TextEffectsStyle(Typeface Typeface, double FontSize, IBrush Brush, double LetterSpacing, double LineHeight);

/// <summary>
/// Splits a short text into words (or explicit lines) and lays them out for per-unit drawing: every word is shaped
/// once (<see cref="TextLayout"/>), wrapped greedily, optionally balanced (CSS <c>text-wrap: balance</c>), and
/// positioned. Rebuilt only when the text, style or width changes; drawing a frame allocates nothing.
/// </summary>
internal sealed class TextEffectsWordLayout
{
    internal sealed class Word
    {
        public required string Text { get; init; }
        public required TextLayout Layout { get; init; }
        public double Width { get; init; }
        public int Index { get; init; }
        public int ForcedLine { get; init; }
        public int Line { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public TextLayout[]? Letters { get; set; }
        public double[]? LetterX { get; set; }
    }

    internal sealed class Line
    {
        public required string Text { get; init; }
        public required TextLayout Layout { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
    }

    public List<Word> Words { get; } = new();
    public List<Line> Lines { get; } = new();
    public double LineHeight { get; private set; }
    public Size Size { get; private set; }
    public int LineCount { get; private set; }

    private readonly record struct BuildKey(char Kind, string Text, TextEffectsStyle Style, double Width, bool Balance, bool Letters);

    private BuildKey? _key;

    private bool Unchanged(BuildKey key)
    {
        if (_key is { } last && last == key)
        {
            return true;
        }
        _key = key;
        return false;
    }

    /// <summary>Splits on <c>\n</c> (forced lines) and spaces (words), dropping empty words.</summary>
    public static List<string[]> Split(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();

    private static TextLayout Shape(string text, in TextEffectsStyle style, double maxWidth = double.PositiveInfinity,
                                    TextWrapping wrapping = TextWrapping.NoWrap) =>
        new(text, style.Typeface, style.FontSize, style.Brush, textWrapping: wrapping, maxWidth: maxWidth,
            lineHeight: style.LineHeight, letterSpacing: style.LetterSpacing);

    /// <summary>Lays out words, wrapping at <paramref name="maxWidth"/>; returns false when nothing changed.</summary>
    public bool BuildWords(string text, in TextEffectsStyle style, double maxWidth, bool balance, bool letters)
    {
        if (Unchanged(new BuildKey('w', text, style, Math.Round(maxWidth, 1), balance, letters)))
        {
            return false;
        }
        Words.Clear();
        Lines.Clear();
        LineHeight = style.LineHeight;
        var space = Shape(" ", style).WidthIncludingTrailingWhitespace;
        var forced = Split(text);
        var index = 0;
        var perLine = new List<List<Word>>();
        for (var f = 0; f < forced.Count; f++)
        {
            var list = new List<Word>();
            foreach (var part in forced[f])
            {
                var layout = Shape(part, style);
                var word = new Word { Text = part, Layout = layout, Width = layout.WidthIncludingTrailingWhitespace, Index = index++, ForcedLine = f };
                if (letters)
                {
                    ShapeLetters(word, style);
                }
                list.Add(word);
            }
            perLine.Add(list);
        }
        var y = 0.0;
        var line = 0;
        var widest = 0.0;
        foreach (var list in perLine)
        {
            if (list.Count == 0)
            {
                y += style.LineHeight;
                line++;
                continue;
            }
            var width = maxWidth;
            if (balance && !double.IsInfinity(maxWidth))
            {
                width = Balance(list, space, maxWidth);
            }
            var x = 0.0;
            var first = true;
            foreach (var word in list)
            {
                if (!first && x + space + word.Width > width + 0.5)
                {
                    widest = Math.Max(widest, x);
                    x = 0;
                    y += style.LineHeight;
                    line++;
                    first = true;
                }
                if (!first)
                {
                    x += space;
                }
                word.X = x;
                word.Y = y;
                word.Line = line;
                x += word.Width;
                first = false;
                Words.Add(word);
            }
            widest = Math.Max(widest, x);
            y += style.LineHeight;
            line++;
        }
        LineCount = line;
        Size = new Size(Math.Ceiling(widest), y);
        return true;
    }

    private static void ShapeLetters(Word word, in TextEffectsStyle style)
    {
        var count = word.Text.Length;
        word.Letters = new TextLayout[count];
        word.LetterX = new double[count];
        for (var i = 0; i < count; i++)
        {
            word.Letters[i] = Shape(word.Text[i].ToString(), style);
            word.LetterX[i] = i == 0 ? 0 : Shape(word.Text[..i], style).WidthIncludingTrailingWhitespace;
        }
    }

    /// <summary>Lines at <paramref name="width"/> with greedy wrapping.</summary>
    internal static int CountLines(IReadOnlyList<double> widths, double space, double width)
    {
        var lines = 1;
        var x = 0.0;
        var first = true;
        foreach (var w in widths)
        {
            if (!first && x + space + w > width + 0.5)
            {
                lines++;
                x = 0;
                first = true;
            }
            x += (first ? 0 : space) + w;
            first = false;
        }
        return lines;
    }

    /// <summary>The narrowest width that keeps the greedy line count, so lines come out evenly filled.</summary>
    internal static double Balance(IReadOnlyList<Word> words, double space, double maxWidth)
    {
        var widths = words.Select(w => w.Width).ToList();
        return BalanceWidths(widths, space, maxWidth);
    }

    internal static double BalanceWidths(IReadOnlyList<double> widths, double space, double maxWidth)
    {
        var target = CountLines(widths, space, maxWidth);
        if (target <= 1)
        {
            return maxWidth;
        }
        var lo = widths.Max();
        var hi = maxWidth;
        for (var i = 0; i < 14 && hi - lo > 0.5; i++)
        {
            var mid = (lo + hi) / 2;
            if (CountLines(widths, space, mid) <= target)
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }
        return hi;
    }

    /// <summary>Lays out explicit lines without reflow (InViewTitle line variant).</summary>
    public bool BuildLines(IReadOnlyList<string> lines, in TextEffectsStyle style)
    {
        if (Unchanged(new BuildKey('l', string.Join("\u0001", lines), style, 0, false, false)))
        {
            return false;
        }
        Words.Clear();
        Lines.Clear();
        LineHeight = style.LineHeight;
        var y = 0.0;
        var widest = 0.0;
        foreach (var text in lines)
        {
            var layout = Shape(text, style);
            Lines.Add(new Line { Text = text, Layout = layout, Y = y, Width = layout.WidthIncludingTrailingWhitespace });
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
            y += style.LineHeight;
        }
        LineCount = lines.Count;
        Size = new Size(Math.Ceiling(widest), y);
        return true;
    }

    /// <summary>Lays out the whole text as one wrapped block (InViewTitle wipe variant).</summary>
    public bool BuildBlock(string text, in TextEffectsStyle style, double maxWidth)
    {
        if (Unchanged(new BuildKey('b', text, style, Math.Round(maxWidth, 1), false, false)))
        {
            return false;
        }
        Words.Clear();
        Lines.Clear();
        LineHeight = style.LineHeight;
        var layout = Shape(text, style, maxWidth, TextWrapping.Wrap);
        Lines.Add(new Line { Text = text, Layout = layout, Y = 0, Width = layout.Width });
        LineCount = layout.TextLines.Count;
        Size = new Size(Math.Ceiling(layout.Width), layout.Height);
        return true;
    }

    public void Invalidate() => _key = null;
}
