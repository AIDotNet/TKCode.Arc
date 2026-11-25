// Behavior from vscode (src/vs/editor/browser/viewParts/minimap/minimap.ts: InnerMinimap's DOM part, commit b57d5c9e),
// MIT license: the canvas, the decorations canvas and the slider, the pointer down handler (reveal the line in
// 'proportional' size, start a slider drag in 'fit' / 'fill') and slider dragging with its reset distance on Windows.
//
// Differences:
//  - one visual draws what upstream splits into the canvas, the decorations canvas and the slider's DOM nodes; the
//    lines' image is a WriteableBitmap into which only the rows a frame changed are copied (upstream's putImageData
//    with a dirty rectangle);
//  - the slider's 'mouseover' look (hidden until the pointer is over the minimap or the slider is dragged) is drawn
//    with the theme's slider opacities instead of CSS classes; the shadow is not drawn, since the text ends left of
//    the minimap here;
//  - touch gestures (scrollDueToTouchEvent) are not ported.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.ViewParts;

namespace TKCode.Arc;

/// <summary>
/// The minimap of a <see cref="CodeEditorView"/>: a zoomed out picture of the view lines in their token colors, with
/// the selections, the minimap decorations and the slider that shows (and moves) the viewport. It spans the view and
/// takes the pointer only over the minimap.
/// </summary>
internal sealed class CodeEditorMinimap : Control, ICustomHitTest
{
    private static readonly Cursor s_arrowCursor = new(StandardCursorType.Arrow);

    private readonly CodeEditorView _view;
    private WriteableBitmap? _bitmap;
    private MinimapFrame? _frame;
    private bool _isPointerOver;
    private SliderDrag? _drag;

    internal CodeEditorMinimap(CodeEditorView view)
    {
        _view = view;
        Cursor = s_arrowCursor;
        ClipToBounds = true;
    }

    /// <summary>How many times the minimap has been drawn (for tests).</summary>
    internal int RenderCount { get; private set; }

    /// <summary>How many image rows the last frame copied into the bitmap (for tests).</summary>
    internal int LastCopiedRows { get; private set; }

    /// <summary>The last frame drawn (for tests).</summary>
    internal MinimapFrame? Frame => _frame;

    /// <summary>The lines' image (for tests).</summary>
    internal WriteableBitmap? Bitmap => _bitmap;

    /// <summary>Whether the slider is drawn: always for 'always', over the minimap or while dragging for 'mouseover'.</summary>
    internal bool IsSliderVisible =>
        _frame is { Layout.SliderNeeded: true }
        && (_isPointerOver || _drag is not null || _view.MinimapPart?.Options.ShowSlider == EditorMinimapShowSlider.Always);

    /// <summary>Whether the slider is being dragged.</summary>
    internal bool IsDragging => _drag is not null;

    /// <summary>The minimap's area in the view, or an empty rectangle when it is hidden.</summary>
    internal Rect MinimapBounds =>
        _view.IsMinimapVisible && _view.MinimapPart is { } minimap
            ? new Rect(minimap.Options.MinimapLeft, 0, minimap.Options.MinimapWidth, Bounds.Height)
            : default;

    /// <summary>The slider's area in the view (from the last frame), or an empty rectangle.</summary>
    internal Rect SliderBounds =>
        _frame is { Layout: { SliderNeeded: true } layout } && MinimapBounds is { Width: > 0 } area
            ? new Rect(area.X, layout.SliderTop, area.Width, layout.SliderHeight)
            : default;

    /// <summary>The engine's parts were created or dropped (a new view model): the old frame does not apply.</summary>
    internal void OnPartsChanged()
    {
        _frame = null;
        _drag = null;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public bool HitTest(Point point) => MinimapBounds.Contains(point);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        RenderCount++;
        LastCopiedRows = 0;
        var minimap = _view.MinimapPart;
        if (minimap is null || !_view.IsMinimapVisible)
        {
            _frame = null;
            return;
        }

        if (_frame is null || minimap.ShouldRender() || !ReferenceEquals(_frame.Layout, minimap.LastLayout))
        {
            _frame = minimap.Render();
            UpdateBitmap(_frame);
        }

        var options = minimap.Options;
        var area = new Rect(options.MinimapLeft, 0, options.MinimapWidth, Bounds.Height);
        if (_view.MinimapBackgroundBrush is { } background)
        {
            context.FillRectangle(background, area);
        }

        if (_frame is null)
        {
            return;
        }

        var canvas = new Rect(options.MinimapLeft, 0, options.CanvasOuterWidth, options.CanvasOuterHeight);
        if (_bitmap is { } bitmap)
        {
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
            {
                context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), canvas);
            }
        }

        // the decorations are in canvas pixels
        if (_frame.Decorations.Count > 0 && options.CanvasInnerWidth > 0 && options.CanvasInnerHeight > 0)
        {
            var scaleX = options.CanvasOuterWidth / options.CanvasInnerWidth;
            var scaleY = options.CanvasOuterHeight / options.CanvasInnerHeight;
            using (context.PushClip(area))
            {
                foreach (var rect in _frame.Decorations)
                {
                    var brush = new SolidColorBrush(Color.FromArgb(rect.Color.A, rect.Color.R, rect.Color.G, rect.Color.B));
                    context.FillRectangle(brush, new Rect(canvas.X + (rect.X * scaleX), rect.Y * scaleY, rect.Width * scaleX, rect.Height * scaleY));
                }
            }
        }

        if (IsSliderVisible && _view.MinimapSliderBrush is { } sliderBrush)
        {
            var opacity = _drag is not null
                ? _view.MinimapSliderActiveOpacity
                : SliderBounds.Contains(_lastPointer) ? _view.MinimapSliderHoverOpacity : _view.MinimapSliderOpacity;
            using (context.PushOpacity(opacity))
            {
                context.FillRectangle(sliderBrush, SliderBounds);
            }
        }
    }

    private Point _lastPointer = new(-1, -1);

    // copies the rows of the frame's image that changed into the bitmap (all of them into a new bitmap)
    private void UpdateBitmap(MinimapFrame? frame)
    {
        if (frame?.ImageData is not { } image || image.Width <= 0 || image.Height <= 0)
        {
            _bitmap?.Dispose();
            _bitmap = null;
            return;
        }

        int dirtyY1 = frame.DirtyY1;
        int dirtyY2 = frame.DirtyY2;
        if (_bitmap is null || _bitmap.PixelSize.Width != image.Width || _bitmap.PixelSize.Height != image.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            dirtyY1 = 0;
            dirtyY2 = image.Height;
        }

        dirtyY1 = Math.Clamp(dirtyY1, 0, image.Height);
        dirtyY2 = Math.Clamp(dirtyY2, 0, image.Height);
        if (dirtyY2 <= dirtyY1)
        {
            return;
        }

        var rowLength = image.Width * MinimapCharSheet.RgbaChannelsCnt;
        using var buffer = _bitmap.Lock();
        for (var y = dirtyY1; y < dirtyY2; y++)
        {
            Marshal.Copy(image.Data, y * rowLength, buffer.Address + (y * buffer.RowBytes), rowLength);
        }

        LastCopiedRows = dirtyY2 - dirtyY1;
    }

    // ---- pointer

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        _lastPointer = e.GetPosition(this);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        _lastPointer = new Point(-1, -1);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var minimap = _view.MinimapPart;
        if (minimap is null || _frame is null || minimap.LastLayout is not { } layout)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetPosition(this);
        var properties = e.GetCurrentPoint(this).Properties;
        var isLeftClick = properties.IsLeftButtonPressed;
        var isMouse = e.Pointer.Type == PointerType.Mouse;

        if (isLeftClick && SliderBounds.Contains(point))
        {
            // the slider's own pointer down handler
            StartSliderDragging(e, point.Y, point.X, layout);
            return;
        }

        if (minimap.Options.Size != EditorMinimapSize.Proportional)
        {
            if (isLeftClick)
            {
                // pretend the click occurred in the center of the slider
                var slider = SliderBounds;
                StartSliderDragging(e, slider.Y + (slider.Height / 2), point.X, layout);
                StartDragTo(point);
            }

            return;
        }

        if (isLeftClick || !isMouse)
        {
            if (minimap.GetLineNumberAtY(point.Y) is { } lineNumber)
            {
                minimap.RevealLineNumber(lineNumber);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var wasOverSlider = SliderBounds.Contains(_lastPointer);
        _lastPointer = point;
        if (_drag is not null)
        {
            StartDragTo(point);
            e.Handled = true;
            return;
        }

        if (wasOverSlider != SliderBounds.Contains(point))
        {
            InvalidateVisual();
        }

        // over the minimap: not the text's hover or cursor
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is not null)
        {
            _drag = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_drag is not null)
        {
            _drag = null;
            InvalidateVisual();
        }
    }

    private void StartSliderDragging(PointerPressedEventArgs e, double initialPosY, double initialPosX, MinimapLayout initialSliderState)
    {
        var minimap = _view.MinimapPart!;
        // In `fit`/`fill` mode several editor positions share one slider position (inside a view zone,
        // or near the end with a tall view zone), so put the editor back instead of mapping the slider back
        _drag = new SliderDrag(initialPosY, initialPosX, minimap.GetEditorScrollTop(), initialSliderState);
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    private void StartDragTo(Point point)
    {
        if (_drag is not { } drag || _view.MinimapPart is not { } minimap)
        {
            return;
        }

        var area = MinimapBounds;
        var pointerOrthogonalDelta = Math.Min(
            Math.Abs(point.X - drag.InitialPosX),
            Math.Min(Math.Abs(point.X - area.Left), Math.Abs(point.X - area.Right)));

        if (OperatingSystem.IsWindows() && pointerOrthogonalDelta > Minimap.PointerDragResetDistance)
        {
            // The pointer has wondered away from the scrollbar => reset dragging
            minimap.SetEditorScrollTop(drag.InitialEditorScrollTop);
            return;
        }

        var pointerDelta = point.Y - drag.InitialPosY;
        if (pointerDelta == 0)
        {
            minimap.SetEditorScrollTop(drag.InitialEditorScrollTop);
            return;
        }

        minimap.SetScrollTop(drag.InitialSliderState.GetDesiredScrollTopFromDelta(pointerDelta));
    }

    private sealed record SliderDrag(double InitialPosY, double InitialPosX, double InitialEditorScrollTop, MinimapLayout InitialSliderState);
}
