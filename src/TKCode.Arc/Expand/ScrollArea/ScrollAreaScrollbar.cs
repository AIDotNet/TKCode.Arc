using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One overlay scrollbar of <see cref="ScrollArea"/>: an 11px hit area around a 5px pill that thickens to 7px and
/// darkens from 30% to 46% of the foreground under the pointer or while dragged (fast). It fades in fast and out on
/// the considered duration. Drawn in <see cref="Render"/>; the thumb position and length are written straight from
/// the scroll offset. Code-driven because showing and hiding use different durations and the hot state animates
/// two dimensions of a drawn shape, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class ScrollAreaScrollbar : Control
{
    private const double MinThumb = 28;
    private static readonly Easing Linear = new LinearEasing();

    public ScrollArea? Owner { get; set; }

    public Orientation Orientation { get; set; }

    public bool IsDragging => _drag is not null;

    /// <summary>Thumb start along the track (tests).</summary>
    internal double ThumbOffset => _thumbOffset;

    /// <summary>Thumb length along the track (tests).</summary>
    internal double ThumbLength => _thumbLength;

    /// <summary>Current visibility (0 hidden, 1 shown).</summary>
    internal double Shown => _shown?.Value ?? _shownTarget;

    private double _thumbOffset;
    private double _thumbLength;
    private bool _overflowing;
    private double _max;
    private MotionValue<double>? _shown;
    private double _shownTarget;
    private MotionValue<double>? _hot;
    private bool _pointerOver;
    private DragState? _drag;
    private SolidColorBrush? _thumbBrush;

    private sealed record DragState(double Start, double Scroll, double Ratio);

    public ScrollAreaScrollbar()
    {
        IsHitTestVisible = false;
        Cursor = Cursor.Default;
    }

    /// <summary>Writes the thumb for the current scroll position (no layout).</summary>
    public void Sync(bool overflowing, double scroll, double max, double client, double total)
    {
        _overflowing = overflowing;
        _max = max;
        IsVisible = overflowing;
        if (!overflowing)
        {
            return;
        }
        var length = Orientation == Orientation.Vertical ? Bounds.Height : Bounds.Width;
        if (length <= 0)
        {
            length = Orientation == Orientation.Vertical ? client - 6 : client - 6;
        }
        var size = Math.Max(MinThumb, total > 0 ? length * client / total : length);
        size = Math.Min(size, length);
        _thumbLength = size;
        _thumbOffset = max > 0 ? (length - size) * Math.Clamp(scroll / max, 0, 1) : 0;
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (Owner is { } owner)
        {
            var vertical = Orientation == Orientation.Vertical;
            Sync(_overflowing,
                vertical ? owner.Offset.Y : Math.Abs(owner.Offset.X),
                vertical ? owner.MaxOffset.Y : owner.MaxOffset.X,
                vertical ? owner.Viewport.Height : owner.Viewport.Width,
                vertical ? owner.Extent.Height : owner.Extent.Width);
        }
    }

    /// <summary>Shows or hides the bar: in on the fast duration, out on the considered one (Arc).</summary>
    public void SetShown(bool shown)
    {
        var target = shown ? 1.0 : 0.0;
        IsHitTestVisible = shown;
        if (Math.Abs(_shownTarget - target) < 0.001 && _shown is not null)
        {
            return;
        }
        _shownTarget = target;
        if (_shown is null)
        {
            _shown = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shown.Changed += _ => InvalidateVisual();
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level != ArcMotionLevel.Full)
        {
            _shown.Jump(target);
            return;
        }
        _ = _shown.AnimateTo(target, shown
            ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.Standard));
    }

    private void SetHot(bool hot)
    {
        _hot ??= CreateHot();
        var target = hot ? 1.0 : 0.0;
        if (Math.Abs(_hot.Target - target) < 0.001)
        {
            return;
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            _hot.Jump(target);
            return;
        }
        _ = _hot.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private MotionValue<double> CreateHot()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private Rect ThumbRect()
    {
        var vertical = Orientation == Orientation.Vertical;
        return vertical
            ? new Rect(0, _thumbOffset, Bounds.Width, _thumbLength)
            : new Rect(_thumbOffset, 0, _thumbLength, Bounds.Height);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _pointerOver = true;
            SetHot(true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerOver = false;
        SetHot(_drag is not null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Owner is not { } owner || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var vertical = Orientation == Orientation.Vertical;
        var point = e.GetPosition(this);
        var along = vertical ? point.Y : point.X;
        e.Handled = true;
        if (ThumbRect().Contains(point))
        {
            var length = vertical ? Bounds.Height : Bounds.Width;
            var scroll = vertical ? owner.Offset.Y : owner.Offset.X;
            _drag = new DragState(along, scroll, _max / Math.Max(1, length - _thumbLength));
            e.Pointer.Capture(this);
            SetHot(true);
            owner.OnTrackDragChanged(true);
            return;
        }
        // Pressing the track pages toward the pointer.
        owner.PageToward(Orientation, along < _thumbOffset ? -1 : 1);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag is not { } drag || Owner is not { } owner)
        {
            return;
        }
        var point = e.GetPosition(this);
        var along = Orientation == Orientation.Vertical ? point.Y : point.X;
        owner.DragTo(Orientation, drag.Scroll + (along - drag.Start) * drag.Ratio);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndDrag(null);
    }

    private void EndDrag(IPointer? pointer)
    {
        if (_drag is null)
        {
            return;
        }
        _drag = null;
        pointer?.Capture(null);
        SetHot(_pointerOver);
        Owner?.OnTrackDragChanged(false);
    }

    public override void Render(DrawingContext context)
    {
        var shown = _shown?.Value ?? 0;
        if (!_overflowing || shown <= 0.001 || Owner?.ThumbBrush is not ISolidColorBrush source)
        {
            return;
        }
        var hot = Math.Clamp(_hot?.Value ?? 0, 0, 1);
        // Arc: 5px pill 3px from the edge; 7px and 2px from the edge while hot. 30% → 46% of the foreground.
        var thickness = 5 + 2 * hot;
        var inset = 3 - hot;
        var alpha = 0.30 + 0.16 * hot;
        var color = source.Color;
        _thumbBrush ??= new SolidColorBrush();
        _thumbBrush.Color = Color.FromArgb((byte)Math.Round(255 * alpha * color.A / 255.0), color.R, color.G, color.B);
        var vertical = Orientation == Orientation.Vertical;
        var rect = vertical
            ? new Rect(Bounds.Width - inset - thickness, _thumbOffset, thickness, _thumbLength)
            : new Rect(_thumbOffset, Bounds.Height - inset - thickness, _thumbLength, thickness);
        var radius = thickness / 2;
        using (context.PushOpacity(shown))
        {
            context.DrawRectangle(_thumbBrush, null, new RoundedRect(rect, radius));
        }
    }
}
