// Ported from vscode (src/vs/editor/browser/viewParts/minimap/minimap.ts: MinimapLayout, commit b57d5c9e), MIT license.
//
// Differences:
//  - create reads the options through IMinimapLayoutOptions (upstream: Pick<MinimapOptions, ...>);
//  - getZoneFreeScrollPosition and getScrollTopFromZoneFreeScrollTop read the vertical offsets through
//    MinimapLineOffsets (upstream: Pick<IViewLayout, ...>), which wraps a view layout or a LinesLayout;
//  - intersectWithViewport returns a nullable tuple.

using TKCode.Arc.Editor.ViewLayout;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.ViewParts;

/// <summary>The minimap options <see cref="MinimapLayout.Create"/> reads.</summary>
internal interface IMinimapLayoutOptions
{
    double PixelRatio { get; }

    double MinimapLineHeight { get; }

    /// <summary>The canvas backing store height (in device px).</summary>
    int CanvasInnerHeight { get; }

    double LineHeight { get; }

    bool MinimapHeightIsEditorHeight { get; }

    /// <summary>The container height (in CSS px).</summary>
    double MinimapHeight { get; }

    double PaddingTop { get; }

    double PaddingBottom { get; }

    bool ScrollBeyondLastLine { get; }
}

/// <summary>The vertical offsets the zone-free scroll position reads (upstream: Pick&lt;IViewLayout, ...&gt;).</summary>
internal readonly struct MinimapLineOffsets
{
    private readonly Func<int, double> _getVerticalOffsetForLineNumber;
    private readonly Func<int, double> _getVerticalOffsetAfterLineNumberWithZones;
    private readonly Func<int, double> _getLineHeightForLineNumber;

    public MinimapLineOffsets(
        Func<int, double> getVerticalOffsetForLineNumber,
        Func<int, double> getVerticalOffsetAfterLineNumberWithZones,
        Func<int, double> getLineHeightForLineNumber)
    {
        _getVerticalOffsetForLineNumber = getVerticalOffsetForLineNumber;
        _getVerticalOffsetAfterLineNumberWithZones = getVerticalOffsetAfterLineNumberWithZones;
        _getLineHeightForLineNumber = getLineHeightForLineNumber;
    }

    public static MinimapLineOffsets From(IViewLayout viewLayout) => new(
        lineNumber => viewLayout.GetVerticalOffsetForLineNumber(lineNumber),
        lineNumber => viewLayout.GetVerticalOffsetAfterLineNumber(lineNumber, true),
        viewLayout.GetLineHeightForLineNumber);

    public static MinimapLineOffsets From(LinesLayout linesLayout) => new(
        lineNumber => linesLayout.GetVerticalOffsetForLineNumber(lineNumber),
        lineNumber => linesLayout.GetVerticalOffsetAfterLineNumber(lineNumber, true),
        linesLayout.GetLineHeightForLineNumber);

    public double GetVerticalOffsetForLineNumber(int lineNumber) => _getVerticalOffsetForLineNumber(lineNumber);

    public double GetVerticalOffsetAfterLineNumber(int lineNumber) => _getVerticalOffsetAfterLineNumberWithZones(lineNumber);

    public double GetLineHeightForLineNumber(int lineNumber) => _getLineHeightForLineNumber(lineNumber);
}

/// <summary>Where the minimap's lines and slider are for a scroll position.</summary>
internal sealed class MinimapLayout
{
    private readonly double _computedSliderRatio;

    public MinimapLayout(
        double scrollTop,
        double scrollHeight,
        bool sliderNeeded,
        double computedSliderRatio,
        double sliderTop,
        double sliderHeight,
        int topPaddingLineCount,
        int startLineNumber,
        int endLineNumber)
    {
        ScrollTop = scrollTop;
        ScrollHeight = scrollHeight;
        SliderNeeded = sliderNeeded;
        _computedSliderRatio = computedSliderRatio;
        SliderTop = sliderTop;
        SliderHeight = sliderHeight;
        TopPaddingLineCount = topPaddingLineCount;
        StartLineNumber = startLineNumber;
        EndLineNumber = endLineNumber;
    }

    /// <summary>The given editor scrollTop (input), measured without view zones in <c>fit</c> / <c>fill</c> mode.</summary>
    public double ScrollTop { get; }

    /// <summary>The given editor scrollHeight (input), measured without view zones in <c>fit</c> / <c>fill</c> mode.</summary>
    public double ScrollHeight { get; }

    public bool SliderNeeded { get; }

    /// <summary>Slider top (in CSS px).</summary>
    public double SliderTop { get; }

    /// <summary>Slider height (in CSS px).</summary>
    public double SliderHeight { get; }

    /// <summary>Empty lines to reserve at the top of the minimap.</summary>
    public int TopPaddingLineCount { get; }

    /// <summary>Minimap render start line number.</summary>
    public int StartLineNumber { get; }

    /// <summary>Minimap render end line number.</summary>
    public int EndLineNumber { get; }

    /// <summary>Compute a desired <c>scrollPosition</c> such that the slider moves by <paramref name="delta"/>.</summary>
    public double GetDesiredScrollTopFromDelta(double delta) =>
        Math.Round(ScrollTop + (delta / _computedSliderRatio), MidpointRounding.AwayFromZero);

    public double GetDesiredScrollTopFromTouchLocation(double pageY) =>
        Math.Round((pageY - (SliderHeight / 2)) / _computedSliderRatio, MidpointRounding.AwayFromZero);

    /// <summary>Intersect a line range with <see cref="StartLineNumber"/> and <see cref="EndLineNumber"/>.</summary>
    public (int StartLineNumber, int EndLineNumber)? IntersectWithViewport(TextRange range)
    {
        var startLineNumber = Math.Max(StartLineNumber, range.StartLineNumber);
        var endLineNumber = Math.Min(EndLineNumber, range.EndLineNumber);
        if (startLineNumber > endLineNumber)
        {
            // entirely outside minimap's viewport
            return null;
        }

        return (startLineNumber, endLineNumber);
    }

    /// <summary>Get the inner minimap y coordinate for a line number.</summary>
    public double GetYForLineNumber(int lineNumber, double minimapLineHeight) =>
        (lineNumber - StartLineNumber + TopPaddingLineCount) * minimapLineHeight;

    /// <summary>
    /// In <c>fit</c> / <c>fill</c> mode the minimap draws every line at the default line height and leaves out view
    /// zones, so the slider has to follow a scroll position that is measured the same way. Without view zones and
    /// custom line heights, this is the identity.
    /// </summary>
    public static (double ScrollTop, double ScrollHeight) GetZoneFreeScrollPosition(
        MinimapLineOffsets viewLayout,
        int lineCount,
        double lineHeight,
        double paddingTop,
        int viewportStartLineNumber,
        double viewportHeight,
        double scrollTop,
        double scrollHeight)
    {
        var zoneFreeLinesBottom = (lineCount * lineHeight) + paddingTop;
        var linesBottom = viewLayout.GetVerticalOffsetAfterLineNumber(lineCount);
        var zoneFreeScrollHeight = scrollHeight - (linesBottom - zoneFreeLinesBottom);
        if (zoneFreeScrollHeight <= viewportHeight)
        {
            // Only the view zones or taller lines make the editor scroll, keep the slider following them
            return (scrollTop, scrollHeight);
        }

        double zoneFreeScrollTop;
        if (scrollTop < paddingTop)
        {
            zoneFreeScrollTop = scrollTop;
        }
        else if (scrollTop >= linesBottom)
        {
            zoneFreeScrollTop = zoneFreeLinesBottom + (scrollTop - linesBottom);
        }
        else
        {
            var lineTop = viewLayout.GetVerticalOffsetForLineNumber(viewportStartLineNumber);
            var lineHeightAtTop = viewLayout.GetLineHeightForLineNumber(viewportStartLineNumber);
            var offsetIntoLine = lineHeightAtTop > 0 ? Math.Min(Math.Max(scrollTop - lineTop, 0), lineHeightAtTop) * lineHeight / lineHeightAtTop : 0;
            zoneFreeScrollTop = ((viewportStartLineNumber - 1) * lineHeight) + paddingTop + offsetIntoLine;
        }

        return (Math.Min(zoneFreeScrollTop, zoneFreeScrollHeight - viewportHeight), zoneFreeScrollHeight);
    }

    /// <summary>The inverse of <see cref="GetZoneFreeScrollPosition"/>: the editor <c>scrollTop</c> for a zone-free <c>scrollTop</c>.</summary>
    public static double GetScrollTopFromZoneFreeScrollTop(
        MinimapLineOffsets viewLayout,
        int lineCount,
        double lineHeight,
        double paddingTop,
        double viewportHeight,
        double zoneFreeScrollTop,
        double scrollHeight)
    {
        var zoneFreeLinesBottom = (lineCount * lineHeight) + paddingTop;
        var linesBottom = viewLayout.GetVerticalOffsetAfterLineNumber(lineCount);
        var zoneFreeScrollHeight = scrollHeight - (linesBottom - zoneFreeLinesBottom);
        if (zoneFreeScrollHeight <= viewportHeight)
        {
            return zoneFreeScrollTop;
        }

        var maxScrollTop = Math.Max(0, scrollHeight - viewportHeight);
        if (zoneFreeScrollTop >= zoneFreeScrollHeight - viewportHeight)
        {
            return maxScrollTop;
        }

        if (zoneFreeScrollTop < paddingTop)
        {
            return Math.Max(0, zoneFreeScrollTop);
        }

        if (zoneFreeScrollTop >= zoneFreeLinesBottom)
        {
            return Math.Min(maxScrollTop, linesBottom + (zoneFreeScrollTop - zoneFreeLinesBottom));
        }

        var lineNumber = (int)Math.Min(Math.Max(Math.Floor((zoneFreeScrollTop - paddingTop) / lineHeight) + 1, 1), lineCount);
        var offsetIntoLine = zoneFreeScrollTop - (((lineNumber - 1) * lineHeight) + paddingTop);
        return Math.Min(maxScrollTop, viewLayout.GetVerticalOffsetForLineNumber(lineNumber) + (offsetIntoLine * viewLayout.GetLineHeightForLineNumber(lineNumber) / lineHeight));
    }

    public static MinimapLayout Create(
        IMinimapLayoutOptions options,
        int viewportStartLineNumber,
        int viewportEndLineNumber,
        double viewportStartLineNumberVerticalOffset,
        double viewportStartLineNumberLineHeight,
        double viewportHeight,
        bool viewportContainsWhitespaceGaps,
        int lineCount,
        int realLineCount,
        double scrollTop,
        double scrollHeight,
        MinimapLayout? previousLayout)
    {
        var pixelRatio = options.PixelRatio;
        var minimapLineHeight = options.MinimapLineHeight;
        var minimapLinesFitting = (int)Math.Floor(options.CanvasInnerHeight / minimapLineHeight);
        var lineHeight = options.LineHeight;

        if (options.MinimapHeightIsEditorHeight)
        {
            var logicalScrollHeight = (realLineCount * options.LineHeight) + options.PaddingTop + options.PaddingBottom;
            if (options.ScrollBeyondLastLine)
            {
                logicalScrollHeight += Math.Max(0, viewportHeight - options.LineHeight - options.PaddingBottom);
            }

            var heightOfSlider = Math.Max(1, Math.Floor(viewportHeight * viewportHeight / logicalScrollHeight));
            var maxSliderTop = Math.Max(0, options.MinimapHeight - heightOfSlider);
            // The slider can move from 0 to `maxMinimapSliderTop`
            // in the same way `scrollTop` can move from 0 to `scrollHeight` - `viewportHeight`.
            var ratio = maxSliderTop / (scrollHeight - viewportHeight);
            var top = scrollTop * ratio;
            var needed = maxSliderTop > 0;
            var maxLinesFitting = (int)Math.Floor(options.CanvasInnerHeight / options.MinimapLineHeight);
            var topPadding = (int)Math.Floor(options.PaddingTop / options.LineHeight);
            return new MinimapLayout(scrollTop, scrollHeight, needed, ratio, top, heightOfSlider, topPadding, 1, Math.Min(lineCount, maxLinesFitting));
        }

        // The visible line count in a viewport can change due to a number of reasons:
        //  a) with the same viewport width, different scroll positions can result in partial lines being visible:
        //    e.g. for a line height of 20, and a viewport height of 600
        //          * scrollTop = 0  => visible lines are [1, 30]
        //          * scrollTop = 10 => visible lines are [1, 31] (with lines 1 and 31 partially visible)
        //          * scrollTop = 20 => visible lines are [2, 31]
        //  b) whitespace gaps might make their way in the viewport (which results in a decrease in the visible line count)
        //  c) we could be in the scroll beyond last line case (which also results in a decrease in the visible line count, down to possibly only one line being visible)

        // We must first establish a desirable slider height.
        double sliderHeight;
        if (viewportContainsWhitespaceGaps && viewportEndLineNumber != lineCount)
        {
            // case b) from above: there are whitespace gaps in the viewport.
            // In this case, the height of the slider directly reflects the visible line count.
            var viewportLineCount = viewportEndLineNumber - viewportStartLineNumber + 1;
            sliderHeight = Math.Floor(viewportLineCount * minimapLineHeight / pixelRatio);
        }
        else
        {
            // The slider has a stable height
            var expectedViewportLineCount = viewportHeight / lineHeight;
            sliderHeight = Math.Floor(expectedViewportLineCount * minimapLineHeight / pixelRatio);
        }

        var extraLinesAtTheTop = Math.Floor(options.PaddingTop / lineHeight);
        var extraLinesAtTheBottom = Math.Floor(options.PaddingBottom / lineHeight);
        if (options.ScrollBeyondLastLine)
        {
            var expectedViewportLineCount = viewportHeight / lineHeight;
            extraLinesAtTheBottom = Math.Max(extraLinesAtTheBottom, expectedViewportLineCount - 1);
        }

        double maxMinimapSliderTop;
        if (extraLinesAtTheBottom > 0)
        {
            var expectedViewportLineCount = viewportHeight / lineHeight;
            // The minimap slider, when dragged all the way down, will contain the last line at its top
            maxMinimapSliderTop = (extraLinesAtTheTop + lineCount + extraLinesAtTheBottom - expectedViewportLineCount - 1) * minimapLineHeight / pixelRatio;
        }
        else
        {
            // The minimap slider, when dragged all the way down, will contain the last line at its bottom
            maxMinimapSliderTop = Math.Max(0, ((extraLinesAtTheTop + lineCount) * minimapLineHeight / pixelRatio) - sliderHeight);
        }

        maxMinimapSliderTop = Math.Min(options.MinimapHeight - sliderHeight, maxMinimapSliderTop);

        // The slider can move from 0 to `maxMinimapSliderTop`
        // in the same way `scrollTop` can move from 0 to `scrollHeight` - `viewportHeight`.
        var computedSliderRatio = maxMinimapSliderTop / (scrollHeight - viewportHeight);
        var sliderTop = scrollTop * computedSliderRatio;

        if (minimapLinesFitting >= extraLinesAtTheTop + lineCount + extraLinesAtTheBottom)
        {
            // All lines fit in the minimap
            var sliderNeeded = maxMinimapSliderTop > 0;
            return new MinimapLayout(scrollTop, scrollHeight, sliderNeeded, computedSliderRatio, sliderTop, sliderHeight, (int)extraLinesAtTheTop, 1, lineCount);
        }
        else
        {
            double consideringStartLineNumber;
            if (viewportStartLineNumber > 1)
            {
                consideringStartLineNumber = viewportStartLineNumber + extraLinesAtTheTop;
            }
            else
            {
                consideringStartLineNumber = Math.Max(1, scrollTop / lineHeight);
            }

            int topPaddingLineCount;
            var startLineNumber = (int)Math.Max(1, Math.Floor(consideringStartLineNumber - (sliderTop * pixelRatio / minimapLineHeight)));
            if (startLineNumber < extraLinesAtTheTop)
            {
                topPaddingLineCount = (int)extraLinesAtTheTop - startLineNumber + 1;
                startLineNumber = 1;
            }
            else
            {
                topPaddingLineCount = 0;
                startLineNumber = Math.Max(1, startLineNumber - (int)extraLinesAtTheTop);
            }

            // Avoid flickering caused by a partial viewport start line
            // by being consistent w.r.t. the previous layout decision
            if (previousLayout is not null && previousLayout.ScrollHeight == scrollHeight)
            {
                if (previousLayout.ScrollTop > scrollTop)
                {
                    // Scrolling up => never increase `startLineNumber`
                    startLineNumber = Math.Min(startLineNumber, previousLayout.StartLineNumber);
                    topPaddingLineCount = Math.Max(topPaddingLineCount, previousLayout.TopPaddingLineCount);
                }

                if (previousLayout.ScrollTop < scrollTop)
                {
                    // Scrolling down => never decrease `startLineNumber`
                    startLineNumber = Math.Max(startLineNumber, previousLayout.StartLineNumber);
                    topPaddingLineCount = Math.Min(topPaddingLineCount, previousLayout.TopPaddingLineCount);
                }
            }

            var endLineNumber = Math.Min(lineCount, startLineNumber - topPaddingLineCount + minimapLinesFitting - 1);
            // When the viewport starts inside a view zone, the top padding or a taller line, the first
            // visible line does not start at `scrollTop`; clamp so the slider never moves backwards.
            // A custom line height can round to 0px, and such a line has no partial progress
            var partialLine = viewportStartLineNumberLineHeight > 0
                ? Math.Min(Math.Max(0, scrollTop - viewportStartLineNumberVerticalOffset), viewportStartLineNumberLineHeight) / viewportStartLineNumberLineHeight
                : 0;

            double sliderTopAligned;
            if (scrollTop >= options.PaddingTop)
            {
                sliderTopAligned = (viewportStartLineNumber - startLineNumber + topPaddingLineCount + partialLine) * minimapLineHeight / pixelRatio;
            }
            else
            {
                sliderTopAligned = scrollTop / options.PaddingTop * (topPaddingLineCount + partialLine) * minimapLineHeight / pixelRatio;
            }

            return new MinimapLayout(scrollTop, scrollHeight, true, computedSliderRatio, sliderTopAligned, sliderHeight, topPaddingLineCount, startLineNumber, endLineNumber);
        }
    }
}
