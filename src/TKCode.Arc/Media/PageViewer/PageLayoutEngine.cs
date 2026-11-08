using Avalonia;

namespace TKCode.Arc;

/// <summary>A point of a page that should stay under a point of the viewport while the layout changes.</summary>
internal readonly record struct PageAnchor(Point ViewportPoint, int PageIndex, Point PointInPage);

/// <summary>
/// Where the pages of a <see cref="PageViewer"/> go: page sizes in, content rectangles out (spec section 4). Pure, and
/// O(log n) per query: the cumulative page heights are read once in <see cref="SetPages"/>, and a page's top is
/// <c>cumulative·zoom + index·spacing</c>, so changing the zoom costs nothing. Coordinates are content coordinates, before
/// the scroll offset; a viewport size is passed in because short content is centered inside it.
/// </summary>
internal sealed class PageLayoutEngine
{
    /// <summary>The zoom steps of <see cref="PageViewer.ZoomIn"/> and <see cref="PageViewer.ZoomOut"/>.</summary>
    public static readonly double[] ZoomSteps = [0.1, 0.25, 0.5, 0.75, 1, 1.25, 1.5, 2, 3, 4, 6, 8];

    private const double StepEpsilon = 1e-3;

    private double[] _widths = [];
    private double[] _heights = [];
    private double[] _cumulative = [0];
    private double _maxWidth;
    private double _maxHeight;

    /// <summary>How many pages there are.</summary>
    public int PageCount => _widths.Length;

    /// <summary>Content DIPs per page DIP.</summary>
    public double Zoom { get; set; } = 1;

    /// <summary>Gap between pages.</summary>
    public double Spacing { get; set; }

    /// <summary>Canvas around the pages.</summary>
    public double Padding { get; set; }

    /// <summary>Column of pages or one page.</summary>
    public PageLayoutMode Mode { get; set; }

    /// <summary>The page laid out in <see cref="PageLayoutMode.Single"/>.</summary>
    public int SingleIndex { get; set; }

    /// <summary>Reads the page sizes (O(n), once per document or change).</summary>
    public void SetPages(int count, Func<int, Size> pageSize)
    {
        var widths = new double[count];
        var heights = new double[count];
        var cumulative = new double[count + 1];
        double maxWidth = 0;
        double maxHeight = 0;
        for (var i = 0; i < count; i++)
        {
            var size = pageSize(i);
            widths[i] = size.Width;
            heights[i] = size.Height;
            cumulative[i + 1] = cumulative[i] + size.Height;
            maxWidth = Math.Max(maxWidth, size.Width);
            maxHeight = Math.Max(maxHeight, size.Height);
        }
        _widths = widths;
        _heights = heights;
        _cumulative = cumulative;
        _maxWidth = maxWidth;
        _maxHeight = maxHeight;
    }

    /// <summary>The size of a page in page DIPs.</summary>
    public Size GetPageSize(int index) => new(_widths[index], _heights[index]);

    /// <summary>The size of everything: all pages (or the single one) and the canvas padding.</summary>
    public Size ContentSize
    {
        get
        {
            if (PageCount == 0)
            {
                return default;
            }
            if (Mode == PageLayoutMode.Single)
            {
                var index = ClampIndex(SingleIndex);
                return new Size(_widths[index] * Zoom + 2 * Padding, _heights[index] * Zoom + 2 * Padding);
            }
            return new Size(_maxWidth * Zoom + 2 * Padding,
                _cumulative[PageCount] * Zoom + (PageCount - 1) * Spacing + 2 * Padding);
        }
    }

    /// <summary>Where a page goes: horizontally centered, the content vertically centered when it is shorter than the viewport.</summary>
    public Rect GetPageRect(int index, Size viewport)
    {
        var content = ContentSize;
        var width = _widths[index] * Zoom;
        var height = _heights[index] * Zoom;
        var x = (Math.Max(viewport.Width, content.Width) - width) / 2;
        var top = Origin(viewport.Height);
        return Mode == PageLayoutMode.Single
            ? new Rect(x, top, width, height)
            : new Rect(x, PageTop(index, top), width, height);
    }

    /// <summary>The pages that intersect the vertical band; <c>Last &lt; First</c> when there are none. O(log n).</summary>
    public (int First, int Last) GetVisibleRange(double top, double bottom, double viewportHeight)
    {
        if (PageCount == 0)
        {
            return (0, -1);
        }
        if (Mode == PageLayoutMode.Single)
        {
            var index = ClampIndex(SingleIndex);
            return (index, index);
        }
        var origin = Origin(viewportHeight);
        var first = FirstPageEndingAfter(top, origin, 0);
        var low = first;
        var high = PageCount - 1;
        var last = first - 1;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            if (PageTop(mid, origin) < bottom)
            {
                last = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return (first, last);
    }

    /// <summary>The page at a content height: the page itself, or the closer page in a gap. O(log n).</summary>
    public int PageIndexAt(double y, double viewportHeight)
    {
        if (PageCount == 0)
        {
            return 0;
        }
        if (Mode == PageLayoutMode.Single)
        {
            return ClampIndex(SingleIndex);
        }
        return Math.Min(PageCount - 1, FirstPageEndingAfter(y, Origin(viewportHeight), Spacing / 2));
    }

    /// <summary>The page exactly under a content point, and the point in page DIPs; false over the canvas. O(log n).</summary>
    public bool HitTest(Point content, Size viewport, out int pageIndex, out Point pointInPage)
    {
        pageIndex = -1;
        pointInPage = default;
        if (PageCount == 0 || Zoom <= 0)
        {
            return false;
        }
        var index = PageIndexAt(content.Y, viewport.Height);
        var rect = GetPageRect(index, viewport);
        if (!rect.Contains(content))
        {
            return false;
        }
        pageIndex = index;
        pointInPage = new Point((content.X - rect.X) / Zoom, (content.Y - rect.Y) / Zoom);
        return true;
    }

    /// <summary>The nearest page to a content point and the point in its page DIPs (outside the page when over the canvas).</summary>
    public (int PageIndex, Point PointInPage) Locate(Point content, Size viewport)
    {
        var index = PageIndexAt(content.Y, viewport.Height);
        if (PageCount == 0 || Zoom <= 0)
        {
            return (index, default);
        }
        var rect = GetPageRect(index, viewport);
        return (index, new Point((content.X - rect.X) / Zoom, (content.Y - rect.Y) / Zoom));
    }

    /// <summary>A point of a page in content coordinates.</summary>
    public Point ToContent(int pageIndex, Point pointInPage, Size viewport)
    {
        if (PageCount == 0)
        {
            return default;
        }
        var rect = GetPageRect(ClampIndex(pageIndex), viewport);
        return new Point(rect.X + pointInPage.X * Zoom, rect.Y + pointInPage.Y * Zoom);
    }

    /// <summary>The zoom a fit mode asks for; null when there is nothing to fit (no pages, no room, or <see cref="PageFitMode.None"/>).</summary>
    public double? GetFitZoom(PageFitMode mode, Size viewport)
    {
        if (mode == PageFitMode.None || PageCount == 0)
        {
            return null;
        }
        var index = ClampIndex(SingleIndex);
        var pageWidth = Mode == PageLayoutMode.Single ? _widths[index] : _maxWidth;
        var pageHeight = Mode == PageLayoutMode.Single ? _heights[index] : _maxHeight;
        var availableWidth = viewport.Width - 2 * Padding;
        var availableHeight = viewport.Height - 2 * Padding;
        if (pageWidth <= 0 || pageHeight <= 0 || availableWidth <= 0 || availableHeight <= 0)
        {
            return null;
        }
        return mode switch
        {
            PageFitMode.Width => availableWidth / pageWidth,
            PageFitMode.Height => availableHeight / pageHeight,
            _ => Math.Min(availableWidth / pageWidth, availableHeight / pageHeight)
        };
    }

    /// <summary>The first zoom step above <paramref name="zoom"/>, or the last step.</summary>
    public static double NextZoomStep(double zoom)
    {
        foreach (var step in ZoomSteps)
        {
            if (step > zoom + StepEpsilon)
            {
                return step;
            }
        }
        return ZoomSteps[^1];
    }

    /// <summary>The first zoom step below <paramref name="zoom"/>, or the first step.</summary>
    public static double PreviousZoomStep(double zoom)
    {
        for (var i = ZoomSteps.Length - 1; i >= 0; i--)
        {
            if (ZoomSteps[i] < zoom - StepEpsilon)
            {
                return ZoomSteps[i];
            }
        }
        return ZoomSteps[0];
    }

    private int ClampIndex(int index) => Math.Clamp(index, 0, Math.Max(0, PageCount - 1));

    private double Origin(double viewportHeight) => Padding + Math.Max(0, (viewportHeight - ContentSize.Height) / 2);

    private double PageTop(int index, double origin) => origin + _cumulative[index] * Zoom + index * Spacing;

    private double PageBottom(int index, double origin) => origin + _cumulative[index + 1] * Zoom + index * Spacing;

    /// <summary>The first page whose bottom (plus <paramref name="slack"/>) is past <paramref name="y"/>; <see cref="PageCount"/> when none.</summary>
    private int FirstPageEndingAfter(double y, double origin, double slack)
    {
        var low = 0;
        var high = PageCount;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            if (PageBottom(mid, origin) + slack > y)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }
        return low;
    }
}
