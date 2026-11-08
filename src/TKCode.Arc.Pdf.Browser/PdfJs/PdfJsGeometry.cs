using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The arithmetic between pdf.js and page coordinates. A view is a page's <c>getViewport({ scale: 1 }).transform</c>: six numbers
/// taking PDF page space (usually y up, shifted by the page box, turned by <c>/Rotate</c>) to a top-left origin, y down, in points.
/// </summary>
internal static class PdfJsGeometry
{
    public const double DipPerPoint = 96.0 / 72.0;

    /// <summary>The page coordinates of a point in page space.</summary>
    public static Point Map(ReadOnlySpan<double> view, double x, double y) => new(
        (view[0] * x + view[2] * y + view[4]) * DipPerPoint,
        (view[1] * x + view[3] * y + view[5]) * DipPerPoint);

    /// <summary>The page coordinates of a rectangle in page space, whichever way the page is turned.</summary>
    public static Rect MapRect(ReadOnlySpan<double> view, double x0, double y0, double x1, double y1)
    {
        var first = Map(view, x0, y0);
        var second = Map(view, x1, y1);
        return new Rect(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));
    }

    /// <summary>
    /// A destination's point: what a destination leaves out (NaN; it keeps the current position there) becomes the page's left or
    /// top edge. Which axis of the page an axis of page space runs along depends on the rotation.
    /// </summary>
    public static Point MapDestination(ReadOnlySpan<double> view, double x, double y)
    {
        var noX = double.IsNaN(x);
        var noY = double.IsNaN(y);
        var point = Map(view, noX ? 0 : x, noY ? 0 : y);
        var (resultX, resultY) = (point.X, point.Y);
        if (noX)
        {
            if (Math.Abs(view[0]) >= Math.Abs(view[1]))
            {
                resultX = 0;
            }
            else
            {
                resultY = 0;
            }
        }
        if (noY)
        {
            if (Math.Abs(view[3]) >= Math.Abs(view[2]))
            {
                resultY = 0;
            }
            else
            {
                resultX = 0;
            }
        }
        return new Point(resultX, resultY);
    }

    /// <summary>
    /// A destination as nine numbers (page, x, y and the target page's view) to what a link or an outline entry holds. Null when it
    /// leads nowhere, or to a page the document does not have. Without a usable view the point is left out, which means the top of the page.
    /// </summary>
    public static PdfDestination? Destination(ReadOnlySpan<double> place, int pageCount)
    {
        var page = place[0];
        if (double.IsNaN(page) || page < 0 || page >= pageCount)
        {
            return null;
        }
        var view = place.Slice(3, 6);
        if ((double.IsNaN(place[1]) && double.IsNaN(place[2])) || double.IsNaN(view[0]))
        {
            return new PdfDestination((int)page);
        }
        return new PdfDestination((int)page, MapDestination(view, place[1], place[2]));
    }

    /// <summary>
    /// Fills in page sizes from two numbers per page in points (rotation included), starting at <paramref name="start"/>. A page that
    /// could not be read must not stop the rest: the neighbour's size keeps the layout going. Tells whether a size changed.
    /// </summary>
    public static bool FillPageSizes(Size[] sizes, int start, ReadOnlySpan<double> points, Size before)
    {
        var changed = false;
        var last = before;
        for (var i = 0; i + 1 < points.Length && start + i / 2 < sizes.Length; i += 2)
        {
            if (points[i] > 0 && points[i + 1] > 0)
            {
                last = new Size(points[i] * DipPerPoint, points[i + 1] * DipPerPoint);
            }
            var index = start + i / 2;
            changed |= sizes[index] != last;
            sizes[index] = last;
        }
        return changed;
    }

    /// <summary>The part of the page a request needs, or null when there is nothing to draw or it is too large to draw.</summary>
    public static PixelRect? RenderArea(PageRenderRequest request, long maxBytes)
    {
        var target = request.TargetSize;
        if (target.Width <= 0 || target.Height <= 0)
        {
            return null;
        }
        var full = new PixelRect(target);
        var area = request.Region is { } region ? region.Intersect(full) : full;
        return area.Width > 0 && area.Height > 0 && (long)area.Width * area.Height * 4 <= maxBytes ? area : null;
    }

    /// <summary>pdf.js's page labels as the document's: one per page, null for a page without one, whatever length the file gave.</summary>
    public static string?[] PageLabels(string[] labels, int pageCount)
    {
        var result = new string?[pageCount];
        for (var i = 0; i < pageCount && i < labels.Length; i++)
        {
            result[i] = string.IsNullOrEmpty(labels[i]) ? null : labels[i];
        }
        return result;
    }
}
