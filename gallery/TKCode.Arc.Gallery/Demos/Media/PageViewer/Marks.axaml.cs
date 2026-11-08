using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class PageViewerMarksDemo : UserControl
{
    public PageViewerMarksDemo()
    {
        InitializeComponent();
        Viewer.Layers.Add(new MarkLayer());
    }

    /// <summary>Click a page to drop a mark; the mark stays on the page through zooming and scrolling.</summary>
    private sealed class MarkLayer : PageLayer
    {
        private const double Radius = 10;
        private static readonly IBrush s_fill = new SolidColorBrush(Color.FromArgb(0xB0, 0xE5, 0x48, 0x4D));
        private readonly List<(int Page, Point Point)> _marks = new();
        private static Cursor? s_cursor;

        protected override void Render(DrawingContext context, in PageLayerContext page)
        {
            foreach (var (index, point) in _marks)
            {
                if (index == page.PageIndex)
                {
                    var center = page.PageBounds.TopLeft + (Vector)point * page.Scale;
                    context.DrawEllipse(s_fill, null, center, Radius * page.Scale, Radius * page.Scale);
                }
            }
        }

        protected override void OnPointerPressed(PageLayerPointerArgs e)
        {
            _marks.Add((e.PageIndex, e.PointInPage));
            e.Handled = true;
            Invalidate();
        }

        protected override Cursor? GetCursor(int pageIndex, Point pointInPage) =>
            s_cursor ??= new Cursor(StandardCursorType.Cross);
    }
}
