using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="PageLayer"/> draws a page.</summary>
/// <param name="PageIndex">The page, from 0.</param>
/// <param name="PageBounds">The page's rectangle in the presenter's coordinates.</param>
/// <param name="Scale">Presenter DIPs per page DIP (the zoom on screen at this moment).</param>
public readonly record struct PageLayerContext(int PageIndex, Rect PageBounds, double Scale);

/// <summary>A pointer event on a page, as a <see cref="PageLayer"/> sees it.</summary>
public sealed class PageLayerPointerArgs
{
    internal PageLayerPointerArgs(int pageIndex, Point pointInPage, KeyModifiers modifiers, int clickCount, PointerType pointerType = PointerType.Mouse)
    {
        PageIndex = pageIndex;
        PointInPage = pointInPage;
        Modifiers = modifiers;
        ClickCount = clickCount;
        PointerType = pointerType;
    }

    /// <summary>The page under the pointer; while a layer holds a drag, the nearest page.</summary>
    public int PageIndex { get; }

    /// <summary>The pointer in page coordinates: page DIPs from the top-left corner of the page.</summary>
    public Point PointInPage { get; }

    /// <summary>The modifier keys held.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>1 for a single click, 2 for a double click and so on; 0 for moves and releases.</summary>
    public int ClickCount { get; }

    /// <summary>What the pointer is. A finger scrolls the view unless a layer handles its press, so a layer usually leaves a touch drag alone.</summary>
    public PointerType PointerType { get; }

    /// <summary>Set to stop the event: earlier layers do not see it, and a handled press keeps the drag for the layers.</summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Draws over pages and takes pointer input on them (<see cref="PageViewer.Layers"/>): highlights, selections, links,
/// marks. Layers draw in list order, over the page bitmap and clipped to the page; pointer events go from the last layer
/// to the first until one sets <see cref="PageLayerPointerArgs.Handled"/>.
/// </summary>
public abstract class PageLayer
{
    internal event Action? InvalidateRequested;

    /// <summary>Draws on one page; called once for every visible page. Do not change invalidating state in here.</summary>
    protected internal abstract void Render(DrawingContext context, in PageLayerContext page);

    /// <summary>A press of the left button or the primary touch on a page.</summary>
    protected internal virtual void OnPointerPressed(PageLayerPointerArgs e)
    {
    }

    /// <summary>The pointer moved over a page, or anywhere while a layer holds a drag.</summary>
    protected internal virtual void OnPointerMoved(PageLayerPointerArgs e)
    {
    }

    /// <summary>The pressed pointer was released.</summary>
    protected internal virtual void OnPointerReleased(PageLayerPointerArgs e)
    {
    }

    /// <summary>A drag a layer held ended without a release (the capture was lost or the viewer left the tree).</summary>
    protected internal virtual void OnPointerDragCancelled()
    {
    }

    /// <summary>The cursor this layer wants at a point of a page; null leaves it to the layers below.</summary>
    protected internal virtual Cursor? GetCursor(int pageIndex, Point pointInPage) => null;

    /// <summary>Asks the viewer to draw the layers again and to ask them for the cursor again.</summary>
    public void Invalidate() => InvalidateRequested?.Invoke();
}
