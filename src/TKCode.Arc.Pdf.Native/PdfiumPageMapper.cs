using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Converts PDF page space (origin usually at the bottom left, y up, shifted by the page box and turned by <c>/Rotate</c>) to page
/// coordinates (origin at the top left, y down, DIPs). The transform comes from <c>FPDF_PageToDevice</c>, not from a hand-written
/// rotation formula. That function rounds to whole device pixels, so it is asked once for three far-apart points on a large device,
/// and the conversion itself is plain arithmetic.
/// </summary>
internal readonly struct PdfiumPageMapper
{
    private const double DevicePerDip = 16;
    private const double ProbeDistance = 1000;

    private readonly double _originX, _originY, _xToX, _xToY, _yToX, _yToY;

    private PdfiumPageMapper(double originX, double originY, double xToX, double xToY, double yToX, double yToY)
    {
        (_originX, _originY, _xToX, _xToY, _yToX, _yToY) = (originX, originY, xToX, xToY, yToX, yToY);
    }

    /// <summary>Reads the transform of a page whose size in DIPs is <paramref name="pageSize"/>.</summary>
    public static unsafe PdfiumPageMapper Create(nint page, Size pageSize)
    {
        var width = Math.Max(1, (int)Math.Round(pageSize.Width * DevicePerDip));
        var height = Math.Max(1, (int)Math.Round(pageSize.Height * DevicePerDip));
        var (originX, originY) = Probe(0, 0);
        var (alongXX, alongXY) = Probe(ProbeDistance, 0);
        var (alongYX, alongYY) = Probe(0, ProbeDistance);
        return new PdfiumPageMapper(
            originX,
            originY,
            (alongXX - originX) / ProbeDistance,
            (alongXY - originY) / ProbeDistance,
            (alongYX - originX) / ProbeDistance,
            (alongYY - originY) / ProbeDistance);

        (double X, double Y) Probe(double x, double y)
        {
            int deviceX, deviceY;
            PdfiumInterop.FPDF_PageToDevice(page, 0, 0, width, height, 0, x, y, &deviceX, &deviceY);
            return (deviceX / DevicePerDip, deviceY / DevicePerDip);
        }
    }

    /// <summary>The page coordinates of a point in page space.</summary>
    public Point Map(double x, double y) =>
        new(_originX + x * _xToX + y * _yToX, _originY + x * _xToY + y * _yToY);

    /// <summary>The page coordinates of a rectangle in page space, whichever way the page is turned.</summary>
    public Rect Map(double left, double top, double right, double bottom)
    {
        var first = Map(left, top);
        var second = Map(right, bottom);
        return new Rect(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Abs(second.X - first.X),
            Math.Abs(second.Y - first.Y));
    }

    /// <summary>
    /// A destination's point: what a destination leaves out (it keeps the current position there) becomes the page's left or top
    /// edge. Which axis of the page an axis of page space runs along depends on the rotation.
    /// </summary>
    public Point MapDestination(double? x, double? y)
    {
        var point = Map(x ?? 0, y ?? 0);
        var (resultX, resultY) = (point.X, point.Y);
        if (x is null)
        {
            if (Math.Abs(_xToX) >= Math.Abs(_xToY))
            {
                resultX = 0;
            }
            else
            {
                resultY = 0;
            }
        }
        if (y is null)
        {
            if (Math.Abs(_yToY) >= Math.Abs(_yToX))
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
}
