using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One stroked quadratic curve (a smile, a happy eye) whose points move with a pose. The path is kept until a point
/// changes, so a curve that holds still costs nothing and one that is not drawn is never built. A moved curve is a new
/// <see cref="StreamGeometry"/>: a path object edited in place raises a property change for every point and is rebuilt
/// for the renderer all the same.
/// </summary>
internal sealed class AiCurve
{
    private Point _start;
    private Point _control;
    private Point _end;
    private StreamGeometry? _geometry;

    /// <summary>The curve from <paramref name="start"/> to <paramref name="end"/> bent toward <paramref name="control"/>.</summary>
    public Geometry Get(Point start, Point control, Point end)
    {
        if (_geometry is not null && start == _start && control == _control && end == _end)
        {
            return _geometry;
        }
        _start = start;
        _control = control;
        _end = end;
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.SetFillRule(FillRule.EvenOdd);
            context.BeginFigure(start, false);
            context.QuadraticBezierTo(control, end);
            context.EndFigure(false);
        }
        _geometry = geometry;
        return geometry;
    }
}
