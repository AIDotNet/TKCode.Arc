using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The measured layout rules of <see cref="CardToArticle"/> as pure functions (Arc Card to article, design reference):
/// the grid's columns, the article sheet's box in the window, the article's side padding, the hero's proportional type
/// and the balanced title wrap. Every size inside the picture is a fraction of the picture's width, so the card and the
/// article draw the same picture at two scales and the morph is a pure scale.
/// </summary>
internal static class CardToArticleLayout
{
    /// <summary>A grid at most this wide (the control's own width) has one column.</summary>
    public const double ColumnBreakpoint = 520;

    public const double GridGap = 16;
    public const double GridGapCompact = 12;

    /// <summary>The article sheet: at most 680 wide, inset 16 / 24 from the window, 8 / 12 when the window is at most 520 wide.</summary>
    public const double SheetMaxWidth = 680;
    public const double SheetCompactBreakpoint = 520;
    public const double SheetInsetX = 16;
    public const double SheetInsetY = 24;
    public const double SheetInsetXCompact = 8;
    public const double SheetInsetYCompact = 12;

    /// <summary>The article's side padding: 6% of the window width, between 20 and 48.</summary>
    public const double BodyPaddingMin = 20;
    public const double BodyPaddingMax = 48;
    public const double BodyPaddingFactor = 0.06;

    // The picture, as fractions of its width W.
    public const double TagInset = 0.05;
    public const double TagFont = 0.032;
    public const double TagLineHeight = 1.2;
    public const double TagPaddingX = 0.9;
    public const double TagPaddingY = 0.45;
    public const double CaptionInsetX = 0.055;
    public const double CaptionInsetBottom = 0.05;
    public const double CaptionGap = 0.014;
    public const double TitleFont = 0.074;
    public const double TitleLineHeight = 1.1;
    public const double TitleTracking = -0.03;
    public const double MetaFont = 0.034;
    public const double MetaFontMax = 20;
    public const double MetaLineHeight = 1.4;

    /// <summary>The scrim covers the bottom 60% of the picture.</summary>
    public const double ScrimTop = 0.4;

    public static int Columns(double width, double breakpoint = ColumnBreakpoint) => width > breakpoint ? 2 : 1;

    public static double Gap(int columns) => columns > 1 ? GridGap : GridGapCompact;

    /// <summary>The slot of card <paramref name="index"/> in a grid <paramref name="width"/> wide.</summary>
    public static Rect Slot(int index, double width, double aspectRatio, double breakpoint = ColumnBreakpoint)
    {
        var columns = Columns(width, breakpoint);
        var gap = Gap(columns);
        var column = Math.Max(0, (width - gap * (columns - 1)) / columns);
        var height = column / SafeRatio(aspectRatio);
        var row = index / columns;
        var col = index % columns;
        return new Rect(col * (column + gap), row * (height + gap), column, height);
    }

    /// <summary>The grid's height for <paramref name="count"/> cards.</summary>
    public static double GridHeight(int count, double width, double aspectRatio, double breakpoint = ColumnBreakpoint)
    {
        if (count <= 0)
        {
            return 0;
        }
        return Slot(count - 1, width, aspectRatio, breakpoint).Bottom;
    }

    public static double SafeRatio(double aspectRatio) => double.IsFinite(aspectRatio) && aspectRatio > 0.05 ? aspectRatio : 4.0 / 3.0;

    /// <summary>The article sheet's box in a window (overlay) of <paramref name="viewport"/>: fixed height, centred.</summary>
    public static Rect Sheet(Size viewport)
    {
        var compact = viewport.Width <= SheetCompactBreakpoint;
        var insetX = compact ? SheetInsetXCompact : SheetInsetX;
        var insetY = compact ? SheetInsetYCompact : SheetInsetY;
        var width = Math.Max(0, Math.Min(SheetMaxWidth, viewport.Width - 2 * insetX));
        var height = Math.Max(0, viewport.Height - 2 * insetY);
        return new Rect((viewport.Width - width) / 2, insetY, width, height);
    }

    /// <summary>The article's side padding for a window <paramref name="viewportWidth"/> wide.</summary>
    public static double BodyPaddingX(double viewportWidth) =>
        Math.Clamp(viewportWidth * BodyPaddingFactor, BodyPaddingMin, BodyPaddingMax);

    public static double MetaFontSize(double width) => Math.Min(width * MetaFont, MetaFontMax);

    /// <summary>
    /// <c>text-wrap: balance</c>: the narrowest width (within <paramref name="precision"/>) at which the text still
    /// takes as many lines as at <paramref name="maxWidth"/>, so the lines come out about the same length.
    /// </summary>
    public static double Balance(double maxWidth, Func<double, int> lineCount, double precision = 0.5)
    {
        if (!(maxWidth > 0) || !double.IsFinite(maxWidth))
        {
            return maxWidth;
        }
        var lines = lineCount(maxWidth);
        if (lines <= 1)
        {
            return maxWidth;
        }
        // At width / lines no line can hold more than its share, so the answer lies between that and the full width.
        var low = maxWidth / lines;
        var high = maxWidth;
        while (high - low > precision)
        {
            var mid = (low + high) / 2;
            if (lineCount(mid) <= lines)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }
        return high;
    }
}
