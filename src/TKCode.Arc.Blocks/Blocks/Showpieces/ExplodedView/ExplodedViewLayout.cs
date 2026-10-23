using Avalonia;

namespace TKCode.Arc;

/// <summary>The three layouts of an <see cref="ExplodedView"/>, chosen by the control's own width.</summary>
internal enum ExplodedViewBreakpoint
{
    /// <summary>Below 560: one column; the stop buttons share the row above the caption; labels show names only.</summary>
    Compact,

    /// <summary>560 to 879: intro, stage and a footer with the caption beside the stop buttons.</summary>
    Medium,

    /// <summary>880 and wider: a 380 px column (intro on top, footer at the bottom) beside a full-height stage.</summary>
    Wide
}

/// <summary>Width-dependent sizes: paddings and the title size.</summary>
internal readonly record struct ExplodedViewMetrics(
    ExplodedViewBreakpoint Breakpoint,
    double Side,
    double IntroTop,
    double FooterBottom,
    double TitleSize)
{
    public bool IsCompact => Breakpoint == ExplodedViewBreakpoint.Compact;

    public bool IsWide => Breakpoint == ExplodedViewBreakpoint.Wide;
}

/// <summary>Where the four regions go.</summary>
internal readonly record struct ExplodedViewRects(Rect Intro, Rect Stage, Rect Caption, Rect Stops, double StopWidth);

/// <summary>
/// The responsive layout of an <see cref="ExplodedView"/> (Arc Exploded view, design reference; breakpoints on the
/// control's width like a container query). Pure functions.
/// </summary>
internal static class ExplodedViewLayout
{
    public const double WideWidth = 880;
    public const double CompactWidth = 560;
    public const double WideColumn = 380;
    public const double IntroMaxWidth = 544;
    public const double PaddingRatio = 0.05;
    public const double PaddingMin = 20;
    public const double PaddingMax = 40;
    public const double WidePaddingMax = 64;
    public const double IntroTopMin = 24;
    public const double FooterBottomRatio = 0.04;
    public const double FooterBottomMin = 20;
    public const double TitleRatio = 0.05;
    public const double TitleMin = 28;
    public const double TitleMax = 52;
    public const double FooterColumnGap = 32;
    public const double WideFooterGap = 20;
    public const double CompactFooterGap = 4;
    public const double StopsOutdent = 8;
    public const double StopWidth = 36;
    public const double StopHeight = 44;

    /// <summary>The breakpoint for a control width.</summary>
    public static ExplodedViewBreakpoint BreakpointFor(double width) =>
        width >= WideWidth ? ExplodedViewBreakpoint.Wide : width >= CompactWidth ? ExplodedViewBreakpoint.Medium : ExplodedViewBreakpoint.Compact;

    /// <summary>Paddings and the title size for a control width.</summary>
    public static ExplodedViewMetrics Metrics(double width)
    {
        var breakpoint = BreakpointFor(width);
        var ratio = PaddingRatio * width;
        var title = Math.Clamp(TitleRatio * width, TitleMin, TitleMax);
        if (breakpoint == ExplodedViewBreakpoint.Wide)
        {
            var pad = Math.Min(ratio, WidePaddingMax);
            return new ExplodedViewMetrics(breakpoint, pad, pad, pad, title);
        }
        return new ExplodedViewMetrics(breakpoint,
            Math.Clamp(ratio, PaddingMin, PaddingMax),
            Math.Clamp(ratio, IntroTopMin, PaddingMax),
            Math.Max(FooterBottomMin, FooterBottomRatio * width),
            title);
    }

    /// <summary>Width of the stop button row in the medium and wide layouts.</summary>
    public static double StopsWidth(int stopCount) => stopCount * StopWidth;

    /// <summary>Width the intro (title and description) is laid out at.</summary>
    public static double IntroWidth(in ExplodedViewMetrics metrics, double width) => metrics.IsWide
        ? WideColumn - metrics.Side
        : Math.Max(0, Math.Min(IntroMaxWidth, width) - 2 * metrics.Side);

    /// <summary>Width the captions are laid out at (they are measured at it to find the tallest).</summary>
    public static double CaptionWidth(in ExplodedViewMetrics metrics, double width, int stopCount) => metrics.Breakpoint switch
    {
        ExplodedViewBreakpoint.Wide => WideColumn - metrics.Side,
        ExplodedViewBreakpoint.Medium => Math.Max(0, width - 2 * metrics.Side - FooterColumnGap - StopsWidth(stopCount)),
        _ => Math.Max(0, width - 2 * metrics.Side)
    };

    /// <summary>Places the intro, the stage, the caption and the stop row in a frame of <paramref name="size"/>.</summary>
    public static ExplodedViewRects Arrange(in ExplodedViewMetrics m, Size size, double introHeight, double captionHeight, int stopCount)
    {
        var w = size.Width;
        var h = size.Height;
        var introW = IntroWidth(m, w);
        var introBottom = introHeight > 0 ? m.IntroTop + introHeight : 0;
        var intro = new Rect(m.Side, m.IntroTop, introW, Math.Max(0, introHeight));
        var captionW = CaptionWidth(m, w, stopCount);
        switch (m.Breakpoint)
        {
            case ExplodedViewBreakpoint.Wide:
            {
                var stopsY = h - m.FooterBottom - StopHeight;
                var stops = new Rect(m.Side - StopsOutdent, stopsY, StopsWidth(stopCount), StopHeight);
                var caption = new Rect(m.Side, stopsY - WideFooterGap - captionHeight, captionW, captionHeight);
                var stage = new Rect(WideColumn, 0, Math.Max(0, w - WideColumn), h);
                return new ExplodedViewRects(intro, stage, caption, stops, StopWidth);
            }
            case ExplodedViewBreakpoint.Medium:
            {
                var bottom = h - m.FooterBottom;
                var stopsW = StopsWidth(stopCount);
                var stops = new Rect(w - m.Side - stopsW, bottom - StopHeight, stopsW, StopHeight);
                var caption = new Rect(m.Side, bottom - captionHeight, captionW, captionHeight);
                var footerTop = bottom - Math.Max(captionHeight, StopHeight);
                var stage = new Rect(0, introBottom, w, Math.Max(0, footerTop - introBottom));
                return new ExplodedViewRects(intro, stage, caption, stops, StopWidth);
            }
            default:
            {
                var captionY = h - m.FooterBottom - captionHeight;
                var caption = new Rect(m.Side, captionY, captionW, captionHeight);
                var stopsW = Math.Max(0, w - 2 * m.Side + 2 * StopsOutdent);
                var stopsY = captionY - CompactFooterGap - StopHeight;
                var stops = new Rect(m.Side - StopsOutdent, stopsY, stopsW, StopHeight);
                var stage = new Rect(0, introBottom, w, Math.Max(0, stopsY - introBottom));
                return new ExplodedViewRects(intro, stage, caption, stops, stopCount > 0 ? stopsW / stopCount : StopWidth);
            }
        }
    }
}
