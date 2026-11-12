using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Drawing shared by a tab (the base layer) and the lens (the same tab redrawn inverted): the glyph, the badge and its
/// rolling digits. Everything it draws comes from caches, so a frame of motion allocates nothing.
/// </summary>
internal static class LiquidTabBarPainter
{
    private const double ViewBox = 24;

    /// <summary>Draws a 24 × 24 glyph at <paramref name="origin"/>, stroked with <paramref name="pen"/> and filled with <paramref name="fill"/>.</summary>
    public static void DrawGlyph(DrawingContext context, Geometry glyph, Point origin, IBrush? fill, IPen? pen)
    {
        var scale = LiquidTabBarMetrics.GlyphSize / ViewBox;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(origin.X, origin.Y)))
        {
            context.DrawGeometry(fill, pen, glyph);
        }
    }

    /// <summary>The badge pill of <paramref name="digitCount"/> digits whose top-left corner is <paramref name="origin"/>.</summary>
    public static Rect BadgeRect(Point origin, int digitCount, double digitWidth)
    {
        var width = Math.Max(LiquidTabBarMetrics.BadgeMinWidth, digitCount * digitWidth + 2 * LiquidTabBarMetrics.BadgePadding);
        return new Rect(origin.X, origin.Y, width, LiquidTabBarMetrics.BadgeHeight);
    }

    /// <summary>
    /// Draws a badge: a ring in the color behind it, the pill, and each digit column at its rolling position (a column
    /// between two digits shows both, offset by the fraction, clipped to the digit line).
    /// </summary>
    public static void DrawBadge(DrawingContext context, Rect pill, double presence, IBrush? ring, IBrush? fill,
        LiquidTabBarDigits digits, LiquidTabBarBadgeRoll roll)
    {
        if (presence <= 0.001 || roll.Count == 0)
        {
            return;
        }
        var scale = 0.6 + 0.4 * Math.Clamp(presence, 0, 1);
        var center = pill.Center;
        using (context.PushOpacity(Math.Clamp(presence, 0, 1)))
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(center.X, center.Y)))
        {
            var radius = pill.Height / 2;
            if (ring is not null)
            {
                var outer = pill.Inflate(LiquidTabBarMetrics.BadgeRing);
                context.DrawRectangle(ring, null, new RoundedRect(outer, outer.Height / 2));
            }
            if (fill is not null)
            {
                context.DrawRectangle(fill, null, new RoundedRect(pill, radius));
            }
            var lineHeight = pill.Height;
            var x = pill.X + (pill.Width - roll.Count * digits.DigitWidth) / 2;
            using (context.PushClip(new Rect(pill.X, pill.Y, pill.Width, lineHeight)))
            {
                for (var i = 0; i < roll.Count; i++)
                {
                    var position = roll.PositionOf(i);
                    var lower = (int)Math.Floor(position);
                    var fraction = position - lower;
                    DrawDigit(context, digits, lower, x, pill.Y - fraction * lineHeight, lineHeight);
                    if (fraction > 0.001)
                    {
                        DrawDigit(context, digits, lower + 1, x, pill.Y + (1 - fraction) * lineHeight, lineHeight);
                    }
                    x += digits.DigitWidth;
                }
            }
        }
    }

    private static void DrawDigit(DrawingContext context, LiquidTabBarDigits digits, int digit, double x, double top, double lineHeight)
    {
        if (digit < 0 || digit > 9 || digits.Get(digit) is not { } layout)
        {
            return;
        }
        layout.Draw(context, new Point(x + (digits.DigitWidth - layout.WidthIncludingTrailingWhitespace) / 2,
            top + (lineHeight - layout.Height) / 2));
    }
}

/// <summary>The ten digit glyphs of a badge in one brush and font, built once and reused every frame.</summary>
internal sealed class LiquidTabBarDigits
{
    private readonly TextLayout?[] _layouts = new TextLayout?[10];
    private IBrush? _brush;
    private Typeface _typeface;
    private double _size;

    /// <summary>The widest digit (columns are this wide, like tabular figures).</summary>
    public double DigitWidth { get; private set; } = 7;

    /// <summary>Makes sure the glyphs match <paramref name="brush"/>, <paramref name="typeface"/> and <paramref name="size"/>.</summary>
    public void Update(IBrush? brush, Typeface typeface, double size)
    {
        if (ReferenceEquals(brush, _brush) && typeface == _typeface && size.Equals(_size) && _layouts[0] is not null)
        {
            return;
        }
        Clear();
        _brush = brush;
        _typeface = typeface;
        _size = size;
        if (brush is null || size <= 0)
        {
            return;
        }
        var widest = 0.0;
        for (var d = 0; d < 10; d++)
        {
            var layout = new TextLayout(((char)('0' + d)).ToString(), typeface, size, brush);
            _layouts[d] = layout;
            widest = Math.Max(widest, layout.WidthIncludingTrailingWhitespace);
        }
        DigitWidth = Math.Ceiling(widest);
    }

    /// <summary>The glyph of <paramref name="digit"/>, or null before <see cref="Update"/>.</summary>
    public TextLayout? Get(int digit) => _layouts[digit];

    /// <summary>Releases the glyphs.</summary>
    public void Clear()
    {
        for (var i = 0; i < _layouts.Length; i++)
        {
            _layouts[i]?.Dispose();
            _layouts[i] = null;
        }
    }
}

/// <summary>
/// The digit columns of a badge while its count rolls: each column turns from its old digit to its new one (passing the
/// digits between, like a strip of 0–9 sliding), all on one progress value.
/// </summary>
internal sealed class LiquidTabBarBadgeRoll
{
    private int[] _from = new int[3];
    private int[] _to = new int[3];

    /// <summary>Number of digit columns shown.</summary>
    public int Count { get; private set; }

    /// <summary>How far the roll has gone, 0 (old digits) to 1 (new digits).</summary>
    public double Progress { get; set; } = 1;

    /// <summary>The count being shown (the target of the roll).</summary>
    public int Value { get; private set; }

    /// <summary>Where column <paramref name="i"/> (most significant first) is now, as a digit position.</summary>
    public double PositionOf(int i) => _from[i] + (_to[i] - _from[i]) * Math.Clamp(Progress, 0, 1);

    /// <summary>
    /// Starts rolling to <paramref name="value"/> from where the columns are now (<paramref name="animate"/>) or shows it
    /// at once. Returns whether a roll started.
    /// </summary>
    public bool SetValue(int value, bool animate)
    {
        value = Math.Max(0, value);
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var count = text.Length;
        var oldCount = Count;
        var from = new int[count];
        for (var i = 0; i < count; i++)
        {
            // Columns are matched by place value: the ones column keeps turning when a tens column appears.
            var oldIndex = i - (count - oldCount);
            from[i] = animate && oldIndex >= 0 && oldIndex < oldCount
                ? (int)Math.Round(PositionOf(oldIndex))
                : 0;
        }
        var to = new int[count];
        for (var i = 0; i < count; i++)
        {
            to[i] = text[i] - '0';
        }
        _from = from;
        _to = to;
        Count = count;
        Value = value;
        var moves = false;
        for (var i = 0; i < count; i++)
        {
            moves |= from[i] != to[i];
        }
        Progress = animate && moves ? 0 : 1;
        return animate && moves;
    }
}
