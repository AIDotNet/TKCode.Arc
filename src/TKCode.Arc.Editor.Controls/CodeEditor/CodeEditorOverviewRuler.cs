using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Editor.ViewParts;

namespace TKCode.Arc;

/// <summary>
/// The overview ruler of a <see cref="CodeEditorView"/> (upstream's decorationsOverviewRuler canvas): the marks of the
/// decorations with overview ruler options and of the cursors over the document's whole height, at the right edge of
/// the view under the vertical scroll bar. It takes no input: the scroll bar above it pages and drags.
/// </summary>
internal sealed class CodeEditorOverviewRuler : Control
{
    private readonly CodeEditorView _view;

    internal CodeEditorOverviewRuler(CodeEditorView view)
    {
        _view = view;
        IsHitTestVisible = false;
    }

    /// <summary>How many times the ruler has been drawn (for tests).</summary>
    internal int RenderCount { get; private set; }

    /// <summary>The last frame drawn (for tests).</summary>
    internal OverviewRulerFrame? Frame { get; private set; }

    /// <summary>The ruler's area in the view.</summary>
    internal Rect RulerBounds
    {
        get
        {
            var width = Math.Min(_view.OverviewRulerWidth, Bounds.Width);
            return new Rect(Bounds.Width - width, 0, width, Bounds.Height);
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        RenderCount++;
        if (_view.OverviewRulerPart is not { } ruler)
        {
            Frame = null;
            return;
        }

        var frame = ruler.Render();
        Frame = frame;
        if (frame.IsHidden)
        {
            return;
        }

        var area = RulerBounds;
        if (frame.Background is { } background)
        {
            context.FillRectangle(ToBrush(background), area);
        }

        var ratio = frame.Settings.PixelRatio > 0 ? frame.Settings.PixelRatio : 1;
        using (context.PushClip(area))
        {
            foreach (var rect in frame.Rects)
            {
                context.FillRectangle(ToBrush(rect.Color), new Rect(area.X + (rect.X / ratio), area.Y + (rect.Y / ratio), rect.Width / ratio, rect.Height / ratio));
            }
        }
    }

    private static IBrush ToBrush(Editor.RgbaColor color) => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
}
