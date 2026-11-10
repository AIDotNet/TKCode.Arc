using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The big picture of a <see cref="ProductGallery"/>: three pooled image slots (previous, current, next) side by side,
/// moved together by one offset, so a swipe drags the neighbour in and a change of picture slides across without building
/// anything. With a mouse or pen over it, the current picture zooms toward the pointer.
/// </summary>
/// <remarks>
/// Motion is code-driven: the slide is handed the swipe's release velocity (<see cref="MotionValue{T}"/> springs keep it),
/// and the zoom follows the pointer every move, neither of which a ControlTheme transition can do.
/// </remarks>
internal sealed class ProductGalleryStage : Control
{
    /// <summary>The slide that settles a picture change: critically damped, 0.42s.</summary>
    internal static readonly SpringSpec SlideSpring = SpringSpec.FromVisual(0.42, 0);

    private static readonly MotionSpec s_zoomIn = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard);
    private static readonly MotionSpec s_zoomOut = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
    private static readonly MotionSpec s_follow = MotionSpec.Spring(SpringSpec.FromVisual(0.18, 0));
    private static readonly MotionSpec s_fade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    private readonly ArcImage[] _slots = new ArcImage[3];
    private readonly MatrixTransform[] _transforms = [new(), new(), new()];
    private readonly DragTracker _drag;
    private MotionValue<double>? _offset;
    private MotionValue<double>? _zoom;
    private MotionValue<Point>? _focus;
    private MotionValue<double>? _fade;
    private bool _zoomWanted;
    private int _settleGeneration;

    public ProductGalleryStage()
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = new ArcImage
            {
                RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
                RenderTransform = _transforms[i],
                IsHitTestVisible = false
            };
            _slots[i] = slot;
            VisualChildren.Add(slot);
            LogicalChildren.Add(slot);
        }
        _drag = new DragTracker(this, new DragOptions { Axis = DragAxis.Horizontal });
        _drag.CanBegin = e => e.Pointer.Type == PointerType.Touch && Owner is { Count: > 1 };
        _drag.Started += (_, _) => OnDragStarted();
        _drag.Moved += (_, e) => OnDragMoved(e.Offset.X);
        _drag.Completed += (_, e) => OnDragEnded(e.Velocity.X);
        _drag.Cancelled += (_, _) => OnDragEnded(0);
    }

    internal ProductGallery? Owner { get; set; }

    /// <summary>The image slot showing the current picture.</summary>
    internal ArcImage Current => _slots[1];

    /// <summary>Space between the pictures while they slide, set by the theme.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ProductGalleryStage, double>(nameof(Gap), 16);

    static ProductGalleryStage()
    {
        AffectsArrange<ProductGalleryStage>(GapProperty);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>The slide offset of the current picture, in px (0 at rest).</summary>
    internal double Offset => _offset?.Value ?? 0;

    /// <summary>The current zoom of the picture (1 at rest).</summary>
    internal double Zoom => _zoom?.Value ?? 1;

    /// <summary>True while a touch swipe holds the pictures.</summary>
    internal bool IsSwiping => _drag.IsDragging;

    private double Pitch => Bounds.Width + Math.Max(0, Gap);

    internal void SetStretch(Stretch stretch)
    {
        foreach (var slot in _slots)
        {
            slot.Stretch = stretch;
        }
    }

    /// <summary>Shows the pictures around <paramref name="index"/> at rest, with no motion.</summary>
    internal void ShowAtRest(int index)
    {
        _settleGeneration++;
        OffsetValue.Jump(0);
        Fade.Jump(1);
        SetSources(index, behind: null, direction: 0);
        Apply();
    }

    /// <summary>
    /// Moves to <paramref name="index"/> from <paramref name="previous"/>. The old picture stays in the neighbouring slot on
    /// the side it leaves by, so a jump across several pictures still slides between the two that are seen.
    /// <paramref name="startOffset"/> is where the old picture is on screen (a swipe's position) and
    /// <paramref name="velocity"/> the swipe's speed in px/s.
    /// </summary>
    internal void Show(int index, int previous, double startOffset = 0, double velocity = 0)
    {
        var direction = Math.Sign(index - previous);
        if (direction == 0 || previous < 0)
        {
            ShowAtRest(index);
            return;
        }
        ZoomOut();
        SetSources(index, behind: previous, direction);
        var generation = ++_settleGeneration;
        if (ArcMotion.IsFull(this) && Bounds.Width > 0)
        {
            // The old picture is now the neighbour behind; put it where it was, then slide everything home.
            OffsetValue.Jump(startOffset + direction * Pitch);
            Fade.Jump(1);
            Apply();
            _ = SettleAsync(generation, velocity);
            return;
        }
        OffsetValue.Jump(0);
        Apply();
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced)
        {
            Fade.Jump(0);
            _ = FadeInAsync(generation);
        }
        else
        {
            Fade.Jump(1);
            RestoreNeighbours();
        }
    }

    private async Task SettleAsync(int generation, double velocity)
    {
        var arrived = await MomentumProjector.SettleTo(OffsetValue, 0, velocity, SlideSpring);
        if (arrived && generation == _settleGeneration)
        {
            RestoreNeighbours();
        }
    }

    private async Task FadeInAsync(int generation)
    {
        var arrived = await Fade.AnimateTo(1, s_fade);
        if (arrived && generation == _settleGeneration)
        {
            RestoreNeighbours();
        }
    }

    private void RestoreNeighbours()
    {
        if (Owner is { } owner)
        {
            SetSources(owner.SelectedIndex, behind: null, direction: 0);
        }
    }

    private void SetSources(int index, int? behind, int direction)
    {
        var owner = Owner;
        _slots[1].Source = owner?.SourceAt(index);
        _slots[0].Source = owner?.SourceAt(direction > 0 && behind is { } a ? a : index - 1);
        _slots[2].Source = owner?.SourceAt(direction < 0 && behind is { } b ? b : index + 1);
    }

    private MotionValue<double> OffsetValue => _offset ??= Value(0, _ => Apply());

    private MotionValue<double> Fade => _fade ??= Value(1, v => _slots[1].Opacity = Math.Clamp(v, 0, 1));

    private MotionValue<double> ZoomValue => _zoom ??= Value(1, _ => Apply());

    private MotionValue<Point> FocusPoint
    {
        get
        {
            if (_focus is null)
            {
                _focus = new MotionValue<Point>(this, new Point(0.5, 0.5), MotionVectors.Point);
                _focus.Changed += _ => Apply();
            }
            return _focus;
        }
    }

    private MotionValue<double> Value(double initial, Action<double> changed)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += changed;
        return value;
    }

    // Hover zoom ---------------------------------------------------------------------------------------------------------

    private bool CanZoom(PointerEventArgs e) =>
        e.Pointer.Type != PointerType.Touch && Owner is { IsHoverZoomEnabled: true, ZoomScale: > 1.001 } && _slots[1].IsImageLoaded &&
        !_drag.IsDragging && Math.Abs(Offset) < 0.5;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        TrackZoom(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        TrackZoom(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ZoomOut();
    }

    private void TrackZoom(PointerEventArgs e)
    {
        if (!CanZoom(e) || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            ZoomOut();
            return;
        }
        var p = e.GetPosition(this);
        var target = new Point(Math.Clamp(p.X / Bounds.Width, 0, 1), Math.Clamp(p.Y / Bounds.Height, 0, 1));
        var full = ArcMotion.IsFull(this);
        if (!_zoomWanted)
        {
            _zoomWanted = true;
            // The zoom starts where the pointer came in, then follows it.
            FocusPoint.Jump(target);
            var scale = Owner!.ZoomScale;
            if (full)
            {
                _ = ZoomValue.AnimateTo(scale, s_zoomIn);
            }
            else
            {
                ZoomValue.Jump(scale);
            }
            Owner.OnZoomChanged(true);
            return;
        }
        if (full)
        {
            _ = FocusPoint.AnimateTo(target, s_follow);
        }
        else
        {
            FocusPoint.Jump(target);
        }
    }

    /// <summary>Returns the picture to its resting size.</summary>
    internal void ZoomOut()
    {
        if (!_zoomWanted)
        {
            return;
        }
        _zoomWanted = false;
        if (ArcMotion.IsFull(this))
        {
            _ = ZoomValue.AnimateTo(1, s_zoomOut);
        }
        else
        {
            ZoomValue.Jump(1);
        }
        Owner?.OnZoomChanged(false);
    }

    /// <summary>Applies the zoom value right away (a new zoom scale while zoomed).</summary>
    internal void RefreshZoom()
    {
        if (_zoomWanted && Owner is { } owner)
        {
            ZoomValue.Jump(owner.ZoomScale);
        }
    }

    // Swipe --------------------------------------------------------------------------------------------------------------

    private double _dragStart;

    private void OnDragStarted()
    {
        ZoomOut();
        _settleGeneration++;
        _offset?.Stop();
        _dragStart = Offset;
        Fade.Jump(1);
    }

    private void OnDragMoved(double dx)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var raw = _dragStart + dx;
        // Past the first or the last picture the stage resists, like a page pulled past its end.
        var index = owner.SelectedIndex;
        var min = index >= owner.Count - 1 ? 0 : -Pitch;
        var max = index <= 0 ? 0 : Pitch;
        var value = RubberBand.Clamp(raw, min, max, Math.Max(1, Bounds.Width));
        OffsetValue.Jump(value);
    }

    private void OnDragEnded(double velocity)
    {
        if (Owner is not { } owner)
        {
            return;
        }
        var offset = Offset;
        Span<double> points = [-Pitch, 0, Pitch];
        var choice = MomentumProjector.SelectSnapPoint(points, offset, velocity,
            SnapProjection.Linear(TimeSpan.FromMilliseconds(200), fromIndex: 1, flickVelocity: 360));
        var index = owner.SelectedIndex;
        var target = choice switch
        {
            0 when index < owner.Count - 1 => index + 1,
            2 when index > 0 => index - 1,
            _ => index
        };
        if (target == index)
        {
            var generation = ++_settleGeneration;
            _ = SettleAsync(generation, velocity);
            return;
        }
        owner.SelectFromSwipe(target, offset, velocity);
    }

    // Layout and drawing -------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var ratio = Owner?.AspectRatio is > 0 and var r && double.IsFinite(r) ? r : 1;
        double width;
        double height;
        if (double.IsFinite(availableSize.Width))
        {
            width = availableSize.Width;
            height = width / ratio;
            if (double.IsFinite(availableSize.Height) && height > availableSize.Height)
            {
                height = availableSize.Height;
            }
        }
        else if (double.IsFinite(availableSize.Height))
        {
            height = availableSize.Height;
            width = height * ratio;
        }
        else
        {
            width = 480;
            height = width / ratio;
        }
        var slot = new Size(width, height);
        foreach (var image in _slots)
        {
            image.Measure(slot);
        }
        return slot;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pitch = finalSize.Width + Math.Max(0, Gap);
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i].Arrange(new Rect((i - 1) * pitch, 0, finalSize.Width, finalSize.Height));
        }
        Apply();
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        // Paint the whole box so the stage takes the pointer everywhere, not only over the picture.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    /// <summary>Writes the slide and the zoom into the slots' transforms (no layout).</summary>
    private void Apply()
    {
        var offset = Offset;
        for (var i = 0; i < _slots.Length; i++)
        {
            var matrix = Matrix.CreateTranslation(offset, 0);
            if (i == 1)
            {
                var zoom = Zoom;
                if (Math.Abs(zoom - 1) > 1e-4)
                {
                    // Zoom about the point of the picture that sits under the pointer's place on the stage, so the
                    // pointer at an edge of the stage shows that edge of the picture.
                    var size = _slots[1].Bounds.Size;
                    var focus = _focus?.Value ?? new Point(0.5, 0.5);
                    var ox = focus.X * size.Width;
                    var oy = focus.Y * size.Height;
                    matrix = Matrix.CreateTranslation(-ox, -oy) * Matrix.CreateScale(zoom, zoom) *
                             Matrix.CreateTranslation(ox + offset, oy);
                }
            }
            _transforms[i].Matrix = matrix;
        }
    }
}
