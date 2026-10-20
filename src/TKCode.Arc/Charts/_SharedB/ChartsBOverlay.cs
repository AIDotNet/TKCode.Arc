using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A self-drawn layer that paints on top of a chart's other parts (tooltips), never hit-testable.</summary>
internal sealed class ChartsBOverlay : Avalonia.Controls.Control
{
    private readonly Action<DrawingContext> _paint;

    public ChartsBOverlay(Action<DrawingContext> paint)
    {
        _paint = paint;
        IsHitTestVisible = false;
        ClipToBounds = false;
    }

    public override void Render(DrawingContext context) => _paint(context);
}
