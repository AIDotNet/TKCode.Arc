using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// One cached line or paragraph of text for a self-drawn part: the layout is rebuilt only when the text, the font, the
/// brush or the room changes, so drawing allocates nothing.
/// </summary>
internal sealed class WeekCalendarText
{
    private string? _text;
    private Typeface _typeface;
    private double _size;
    private IBrush? _brush;
    private double _maxWidth = double.NaN;
    private int _maxLines;
    private double _lineHeight;
    private double _letterSpacing;
    private TextLayout? _layout;

    /// <summary>The layout for the arguments (cached while they stay the same); null for empty text.</summary>
    public TextLayout? Get(string? text, Typeface typeface, double size, IBrush? brush, double maxWidth = double.PositiveInfinity,
                           int maxLines = 1, double lineHeight = double.NaN, double letterSpacing = 0)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        maxWidth = Math.Max(0, maxWidth);
        if (_layout is not null && string.Equals(_text, text, StringComparison.Ordinal) && _typeface == typeface && _size == size &&
            ReferenceEquals(_brush, brush) && SameWidth(_maxWidth, maxWidth) && _maxLines == maxLines &&
            (_lineHeight.Equals(lineHeight)) && _letterSpacing.Equals(letterSpacing))
        {
            return _layout;
        }
        _layout?.Dispose();
        _text = text;
        _typeface = typeface;
        _size = size;
        _brush = brush;
        _maxWidth = maxWidth;
        _maxLines = maxLines;
        _lineHeight = lineHeight;
        _letterSpacing = letterSpacing;
        _layout = new TextLayout(text, typeface, size, brush, TextAlignment.Left,
            maxLines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap, TextTrimming.CharacterEllipsis, null,
            FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, lineHeight, letterSpacing, maxLines);
        return _layout;
    }

    /// <summary>Forgets the layout (theme change).</summary>
    public void Reset()
    {
        _layout?.Dispose();
        _layout = null;
        _text = null;
    }

    private static bool SameWidth(double a, double b) =>
        double.IsPositiveInfinity(a) && double.IsPositiveInfinity(b) || Math.Abs(a - b) < 0.01;

    /// <summary>The natural width of a single line (for flex-like sharing of a row).</summary>
    public static double NaturalWidth(TextLayout? layout) => layout?.WidthIncludingTrailingWhitespace ?? 0;
}

/// <summary>Column edges shared by the header, the all-day row and the grid, so their columns line up to the pixel.</summary>
internal static class WeekCalendarColumns
{
    /// <summary>The x of the left edge of column <paramref name="index"/> of <paramref name="count"/> in <paramref name="width"/>.</summary>
    public static double Edge(int index, int count, double width) =>
        count <= 0 ? 0 : Math.Round(Math.Clamp(index, 0, count) * width / count);

    /// <summary>The rect of a column, full height.</summary>
    public static Rect Column(int index, int count, Size size)
    {
        var left = Edge(index, count, size.Width);
        return new Rect(left, 0, Edge(index + 1, count, size.Width) - left, size.Height);
    }

    /// <summary>The column under <paramref name="x"/> (clamped to the columns).</summary>
    public static int At(double x, int count, double width)
    {
        if (count <= 0 || width <= 0)
        {
            return 0;
        }
        return Math.Clamp((int)Math.Floor(x / width * count), 0, count - 1);
    }
}

/// <summary>Lucide glyphs the calendar needs beyond Core's set.</summary>
internal static class WeekCalendarGlyphs
{
    /// <summary>Lucide <c>map-pin</c> (24×24, stroked).</summary>
    public static Geometry MapPin => ArcIcons.Parse(
        "M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0 M15 10a3 3 0 1 1-6 0a3 3 0 1 1 6 0");
}
