using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The selection drawn over the body: the selected range (a light accent fill with a 1px accent edge), the active cell
/// (a 2px accent ring), the fill handle, the dashed fill preview and the wash that flashes pasted, filled, undone and redone
/// cells. Rectangles live in content coordinates and spring between ranges (snappy, keeping velocity when retargeted), so
/// scrolling moves them at once; pinned edges stay while scrolling edges follow the offset.
/// </summary>
internal sealed class DataGridSelectionLayer : Avalonia.Controls.Control
{
    private readonly DataGrid _grid;
    private readonly DataGridViewport _viewport;
    private MotionValue<Rect>? _range;
    private MotionValue<Rect>? _active;
    private MotionValue<Rect>? _preview;
    private MotionValue<double>? _washOpacity;
    private (bool Left, bool Right) _rangePins;
    private (bool Left, bool Right) _activePins;
    private (bool Left, bool Right) _previewPins;
    private (bool Left, bool Right) _washPins;
    private Rect _washRect;
    private DataGridCellRange? _previewRange;
    private bool _hasTarget;
    private double? _dropX;

    public DataGridSelectionLayer(DataGrid grid, DataGridViewport viewport)
    {
        _grid = grid;
        _viewport = viewport;
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    /// <summary>Shows a vertical line at a viewport x while a column is dragged (null hides it).</summary>
    public void SetColumnDrop(double? x)
    {
        if (_dropX != x)
        {
            _dropX = x;
            InvalidateVisual();
        }
    }

    /// <summary>The selected range as drawn now (content coordinates).</summary>
    internal Rect RangeRect => _range?.Value ?? default;

    /// <summary>Whether the range or the active cell is still springing.</summary>
    internal bool IsMoving => _range is { IsAnimating: true } || _active is { IsAnimating: true };

    private MotionValue<Rect> Create(Rect initial)
    {
        var value = new MotionValue<Rect>(this, initial, MotionVectors.Rect);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    /// <summary>Moves the range and active cell to the grid's selection.</summary>
    public void OnSelectionChanged(bool animate)
    {
        if (!_grid.HasCells)
        {
            _hasTarget = false;
            InvalidateVisual();
            return;
        }
        var (range, rangeLeft, rangeRight) = _viewport.ContentRect(_grid.Range);
        var anchor = _grid.Anchor;
        var (active, activeLeft, activeRight) = _viewport.ContentRect(new DataGridCellRange(anchor.Row, anchor.Column, anchor.Row, anchor.Column));
        var jump = !animate || !_hasTarget || !_grid.IsMotionFull || !IsVisible;
        _rangePins = (rangeLeft, rangeRight);
        _activePins = (activeLeft, activeRight);
        _range ??= Create(range);
        _active ??= Create(active);
        _hasTarget = true;
        if (jump)
        {
            _range.Jump(range);
            _active.Jump(active);
        }
        else
        {
            _ = _range.AnimateTo(range, MotionSpec.Spring(ArcSpring.Snappy));
            _ = _active.AnimateTo(active, MotionSpec.Spring(ArcSpring.Snappy));
        }
        InvalidateVisual();
    }

    /// <summary>Jumps every rectangle to its target (column resize, density change).</summary>
    public void Snap()
    {
        if (!_grid.HasCells)
        {
            return;
        }
        OnSelectionChanged(animate: false);
        if (_previewRange is { } preview)
        {
            var (rect, left, right) = _viewport.ContentRect(preview);
            _previewPins = (left, right);
            _preview?.Jump(rect);
        }
    }

    /// <summary>Shows the dashed fill preview over <paramref name="range"/> (null hides it).</summary>
    public void SetFillPreview(DataGridCellRange? range)
    {
        _previewRange = range;
        if (range is { } target)
        {
            var (rect, left, right) = _viewport.ContentRect(target);
            _previewPins = (left, right);
            if (_preview is null)
            {
                _preview = Create(_range?.Value ?? rect);
            }
            if (_grid.IsMotionFull)
            {
                _ = _preview.AnimateTo(rect, MotionSpec.Spring(ArcSpring.Snappy));
            }
            else
            {
                _preview.Jump(rect);
            }
        }
        InvalidateVisual();
    }

    /// <summary>Flashes a range with the accent wash that fades out (0.9s; a short fade under reduced motion; none when off).</summary>
    public void Flash(DataGridCellRange range)
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !_grid.HasCells)
        {
            return;
        }
        var clamped = new DataGridCellRange(
            Math.Clamp(range.Row0, 0, _grid.DisplayCount - 1), Math.Clamp(range.Column0, 0, _grid.Layout.Count - 1),
            Math.Clamp(range.Row1, 0, _grid.DisplayCount - 1), Math.Clamp(range.Column1, 0, _grid.Layout.Count - 1));
        var (rect, left, right) = _viewport.ContentRect(clamped);
        _washRect = rect;
        _washPins = (left, right);
        _washOpacity ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _washOpacity.Changed -= OnWashChanged;
        _washOpacity.Changed += OnWashChanged;
        _washOpacity.Jump(1);
        var duration = _grid.IsMotionFull ? _grid.WashDuration : ArcMotionPresets.Standard;
        _ = _washOpacity.AnimateTo(0, MotionSpec.Tween(duration, s_easeOut));
    }

    private static readonly Avalonia.Animation.Easings.Easing s_easeOut = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);

    private void OnWashChanged(double value) => InvalidateVisual();

    /// <summary>The fill handle square in body coordinates (null when it is hidden).</summary>
    private Rect? HandleRect()
    {
        if (!_hasTarget || _grid.Edit is not null || _grid.HasDataSource || _range is null)
        {
            return null;
        }
        var (target, _, right) = _viewport.ContentRect(_grid.Range);
        var body = _viewport.BodyRect(target, _rangePins.Left, right);
        var layout = _grid.Layout;
        if (!right && body.Right < layout.PinEdge + 4)
        {
            return null;
        }
        var size = 8.0;
        return new Rect(body.Right - size / 2, body.Bottom - size / 2, size, size);
    }

    /// <summary>Whether a body point is over the fill handle (its hit area reaches 9px beyond the square).</summary>
    public bool IsOverFillHandle(Point point) => HandleRect() is { } handle && handle.Inflate(9).Contains(point);

    public override void Render(DrawingContext context)
    {
        if (_dropX is { } dropX)
        {
            context.DrawLine(_grid.Palette.ActivePen, new Point(dropX, 0), new Point(dropX, Bounds.Height));
        }
        if (!_hasTarget || !_grid.HasCells || _range is null || _active is null)
        {
            return;
        }
        var palette = _grid.Palette;
        var layout = _grid.Layout;
        var size = Bounds.Size;
        var editing = _grid.Edit is not null;
        var radius = 2.0;
        if (_washOpacity is { Value: > 0.001 } wash && Map(_washRect, _washPins, layout, size, out var washRect, out var washClip))
        {
            using (washClip is { } clip ? context.PushClip(clip) : default(DrawingContext.PushedState?))
            using (context.PushOpacity(wash.Value))
            {
                context.DrawRectangle(palette.WashFill, null, washRect);
            }
        }
        if (Map(_range.Value, _rangePins, layout, size, out var rangeRect, out var rangeClip))
        {
            using (rangeClip is { } clip ? context.PushClip(clip) : default(DrawingContext.PushedState?))
            {
                context.DrawRectangle(editing ? null : palette.SelectionFill, palette.SelectionPen, rangeRect.Deflate(0.5), radius, radius);
            }
        }
        if (Map(_active.Value, _activePins, layout, size, out var activeRect, out var activeClip))
        {
            using (activeClip is { } clip ? context.PushClip(clip) : default(DrawingContext.PushedState?))
            {
                context.DrawRectangle(null, palette.ActivePen, activeRect.Deflate(1), radius, radius);
            }
        }
        if (_previewRange is not null && _preview is not null && Map(_preview.Value, _previewPins, layout, size, out var previewRect, out var previewClip))
        {
            using (previewClip is { } clip ? context.PushClip(clip) : default(DrawingContext.PushedState?))
            {
                context.DrawRectangle(palette.PreviewFill, palette.PreviewPen, previewRect.Deflate(0.75), radius, radius);
            }
        }
        if (HandleRect() is { } handle)
        {
            var animated = _viewport.BodyRect(_range.Value, _rangePins.Left, _rangePins.Right);
            var square = new Rect(animated.Right - handle.Width / 2, animated.Bottom - handle.Height / 2, handle.Width, handle.Height);
            context.DrawRectangle(palette.AccentBrush, palette.HandleOutline, square, 2, 2);
        }
    }

    /// <summary>Maps a content rectangle to the body, with the clip that hides it behind the pinned columns when it scrolls.</summary>
    private bool Map(Rect content, (bool Left, bool Right) pins, DataGridLayout layout, Size size, out Rect body, out Rect? clip)
    {
        body = _viewport.BodyRect(content, pins.Left, pins.Right);
        clip = !pins.Left && layout.PinEdge > 0
            ? new Rect(layout.PinEdge, -8, Math.Max(0, size.Width - layout.PinEdge + 8), size.Height + 16)
            : null;
        return body.Bottom >= -4 && body.Top <= size.Height + 4 && body.Width > 0;
    }
}
