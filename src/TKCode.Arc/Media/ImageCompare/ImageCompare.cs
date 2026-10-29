using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Event data of <see cref="ImageCompare.PositionChanged"/>.</summary>
public sealed class ImageComparePositionChangedEventArgs : RoutedEventArgs
{
    internal ImageComparePositionChangedEventArgs(RoutedEvent routedEvent, double position)
        : base(routedEvent)
    {
        Position = position;
    }

    /// <summary>The committed divider position, in percent from the left (or top).</summary>
    public double Position { get; }
}

/// <summary>
/// Before and after, split by a divider (Arc image compare). Drag anywhere to move it: the handle follows 1:1,
/// rubber-bands past the edges and stays where it is released. A press on the image springs the divider there; on
/// touch the press waits for a drag along the divider's axis so the page still scrolls, and a tap moves the divider.
/// Arrow keys step 1% (Shift 10%), PageUp / PageDown 10%, Home and End reveal one side fully, and a double-click on
/// the handle springs it back to the middle. Changing <see cref="Orientation"/> swings the divider a quarter turn.
/// <para>
/// <see cref="Before"/> / <see cref="After"/> take any content (an <c>Image</c> with <c>Stretch="UniformToFill"</c>, a
/// drawing, a control). <see cref="BeforeSource"/> / <see cref="AfterSource"/> load a picture with an
/// <c>ArcImage</c> on top of that content: it fades in from a soft blur while downloading, and when it fails the
/// content underneath shows instead.
/// </para>
/// </summary>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(BeforeLayerPart, typeof(Panel))]
[TemplatePart(BeforeChipPart, typeof(Control))]
[TemplatePart(AfterChipPart, typeof(Control))]
[TemplatePart(AfterBottomChipPart, typeof(Control))]
[TemplatePart(DividerPart, typeof(ImageCompareDivider))]
[TemplatePart(HandlePart, typeof(ImageCompareHandle))]
[TemplatePart(BeforeImagePart, typeof(ArcImage))]
[TemplatePart(AfterImagePart, typeof(ArcImage))]
[PseudoClasses(DraggingPseudoClass, BeforeImagePseudoClass, AfterImagePseudoClass, BeforeLoadingPseudoClass, AfterLoadingPseudoClass)]
public class ImageCompare : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string BeforeLayerPart = "PART_BeforeLayer";
    public const string BeforeChipPart = "PART_BeforeChip";
    public const string AfterChipPart = "PART_AfterChip";
    public const string AfterBottomChipPart = "PART_AfterBottomChip";
    public const string DividerPart = "PART_Divider";
    public const string HandlePart = "PART_Handle";
    public const string BeforeImagePart = "PART_BeforeImage";
    public const string AfterImagePart = "PART_AfterImage";

    /// <summary>Set while the divider is dragged (the handle stretches into a capsule).</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while <see cref="BeforeSource"/> is shown (set and not failed).</summary>
    public const string BeforeImagePseudoClass = ":before-image";

    /// <summary>Set while <see cref="AfterSource"/> is shown (set and not failed).</summary>
    public const string AfterImagePseudoClass = ":after-image";

    /// <summary>Set while the before picture is still downloading after mount (it fades in from a blur).</summary>
    public const string BeforeLoadingPseudoClass = ":before-loading";

    /// <summary>Set while the after picture is still downloading after mount.</summary>
    public const string AfterLoadingPseudoClass = ":after-loading";

    private const double DefaultWidth = 480;

    #region Properties

    /// <summary>The original, shown on the left (or top). Sized to cover the frame.</summary>
    public static readonly StyledProperty<object?> BeforeProperty =
        AvaloniaProperty.Register<ImageCompare, object?>(nameof(Before));

    /// <summary>The result, shown on the right (or bottom).</summary>
    public static readonly StyledProperty<object?> AfterProperty =
        AvaloniaProperty.Register<ImageCompare, object?>(nameof(After));

    /// <summary>Picture drawn over <see cref="Before"/> (an <c>ArcImage</c>, cover fit).</summary>
    public static readonly StyledProperty<ImageSource?> BeforeSourceProperty =
        AvaloniaProperty.Register<ImageCompare, ImageSource?>(nameof(BeforeSource));

    /// <summary>Picture drawn over <see cref="After"/>.</summary>
    public static readonly StyledProperty<ImageSource?> AfterSourceProperty =
        AvaloniaProperty.Register<ImageCompare, ImageSource?>(nameof(AfterSource));

    /// <summary>Where the divider sits, in percent from the left (or top). Two-way; defaults to 50.</summary>
    public static readonly StyledProperty<double> PositionProperty =
        AvaloniaProperty.Register<ImageCompare, double>(nameof(Position), 50, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => double.IsNaN(value) ? 50 : Math.Clamp(value, 0, 100));

    /// <summary>Vertical stacks the images top and bottom; changing it swings the divider a quarter turn.</summary>
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ImageCompare, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>Caption over the before side (default "Before").</summary>
    public static readonly StyledProperty<string?> BeforeLabelProperty =
        AvaloniaProperty.Register<ImageCompare, string?>(nameof(BeforeLabel), "Before");

    /// <summary>Caption over the after side (default "After").</summary>
    public static readonly StyledProperty<string?> AfterLabelProperty =
        AvaloniaProperty.Register<ImageCompare, string?>(nameof(AfterLabel), "After");

    /// <summary>Shows the captions; they fade as the divider reaches them (Arc <c>labels={false}</c> hides them).</summary>
    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<ImageCompare, bool>(nameof(ShowLabels), true);

    /// <summary>Accessible name of the divider (default "Before and after").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ImageCompare, string?>(nameof(Label), "Before and after");

    /// <summary>Frame proportions, width / height (default 3 / 2).</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ImageCompare, double>(nameof(AspectRatio), 1.5);

    /// <summary>Raised when the divider position is committed: live once per whole percent while dragging, and on release.</summary>
    public static readonly RoutedEvent<ImageComparePositionChangedEventArgs> PositionChangedEvent =
        RoutedEvent.Register<ImageCompare, ImageComparePositionChangedEventArgs>(nameof(PositionChanged), RoutingStrategies.Bubble);

    public object? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    public object? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    public ImageSource? BeforeSource { get => GetValue(BeforeSourceProperty); set => SetValue(BeforeSourceProperty, value); }

    public ImageSource? AfterSource { get => GetValue(AfterSourceProperty); set => SetValue(AfterSourceProperty, value); }

    public double Position { get => GetValue(PositionProperty); set => SetValue(PositionProperty, value); }

    public Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    public string? BeforeLabel { get => GetValue(BeforeLabelProperty); set => SetValue(BeforeLabelProperty, value); }

    public string? AfterLabel { get => GetValue(AfterLabelProperty); set => SetValue(AfterLabelProperty, value); }

    public bool ShowLabels { get => GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    /// <inheritdoc cref="PositionChangedEvent"/>
    public event EventHandler<ImageComparePositionChangedEventArgs>? PositionChanged
    {
        add => AddHandler(PositionChangedEvent, value);
        remove => RemoveHandler(PositionChangedEvent, value);
    }

    /// <summary>Share of the after image in whole percent (the slider value, "40% after").</summary>
    public int AfterShare => (int)ImageCompareMath.JsRound(100 - Position);

    /// <summary>True while the divider is being dragged.</summary>
    public bool IsDragging => _dragging;

    #endregion

    // Motion: the divider is a committed position plus a catch-up offset. A press springs the offset to zero while the
    // pointer moves the position 1:1, so the divider glides to the finger and never trails it. These are MotionValues
    // rather than ControlTheme transitions because drags write them every move and releases hand over their velocity.
    private readonly MotionValue<double> _pos;
    private readonly MotionValue<double> _lag;
    private readonly MotionValue<double> _theta;
    private readonly MotionValue<Size> _capsule;
    private readonly MotionValue<double> _spread;
    private readonly VelocityTracker _placedVelocity = new();
    private readonly DragTracker _pointerDrag;
    private DragTracker? _touchDrag;
    private readonly RectangleGeometry _clip = new();
    private readonly MatrixTransform _clipTransform = new();
    private readonly TranslateTransform _handleOffset = new();

    private Border? _frame;
    private Panel? _beforeLayer;
    private Control? _beforeChip;
    private Control? _afterChip;
    private Control? _afterBottomChip;
    private ImageCompareDivider? _divider;
    private ImageCompareHandle? _handle;
    private ArcImage? _beforeImage;
    private ArcImage? _afterImage;
    private ImageSource? _failedBefore;
    private ImageSource? _failedAfter;

    private ImageCompareDrag? _drag;
    private bool _dragging;
    private bool _doubleClickPending;
    private double _latest;
    private double _goal;
    private Size _frameSize;
    private Rect _beforeChipRect;
    private Rect _afterChipRect;
    private Point _rootOrigin;

    private sealed class ImageCompareDrag
    {
        public bool Press;
        public bool Started;
        public double Grab;
        public double Raw;
    }

    static ImageCompare()
    {
        AffectsMeasure<ImageCompare>(AspectRatioProperty);
    }

    public ImageCompare()
    {
        _latest = Position;
        _goal = Position;
        _pos = new MotionValue<double>(this, Position, MotionVectors.Double);
        _lag = new MotionValue<double>(this, 0, MotionVectors.Double);
        _theta = new MotionValue<double>(this, 0, MotionVectors.Double);
        _capsule = new MotionValue<Size>(this, new Size(40, 40), MotionVectors.Size);
        _spread = new MotionValue<double>(this, 0, MotionVectors.Double);
        _pos.Changed += OnMotion;
        _lag.Changed += OnMotion;
        _theta.Changed += OnMotion;
        _capsule.Changed += OnShapeMotion;
        _spread.Changed += OnMotion;
        _clip.Transform = _clipTransform;

        // A mouse, a pen or a press on the handle answers at once; a finger on the photo waits for a drag along the axis.
        _pointerDrag = new DragTracker(this, new DragOptions { Threshold = 0, HandledEventsToo = true })
        {
            CanBegin = e => IsOnHandle(e) || e.Pointer.Type != PointerType.Touch
        };
        _pointerDrag.Pressed += (_, e) => BeginPress(e, press: _pressOnImage, startNow: true);
        _pointerDrag.Moved += (_, e) => Follow(e);
        _pointerDrag.Completed += (_, _) => End();
        _pointerDrag.Released += (_, _) => End();
        _pointerDrag.Cancelled += (_, _) => End();
        CreateTouchTracker();
    }

    private bool _pressOnImage;

    private void CreateTouchTracker()
    {
        _touchDrag?.Dispose();
        var axis = Orientation == Orientation.Vertical ? DragAxis.Vertical : DragAxis.Horizontal;
        _touchDrag = new DragTracker(this, new DragOptions { Threshold = 6, Axis = axis, HandledEventsToo = true })
        {
            CanBegin = e => e.Pointer.Type == PointerType.Touch && !IsOnHandle(e)
        };
        _touchDrag.Pressed += (_, e) => BeginPress(e, press: true, startNow: false);
        _touchDrag.Started += (_, e) =>
        {
            if (_drag is { } state)
            {
                Start(state, PercentAt(e.Position));
            }
        };
        _touchDrag.Moved += (_, e) => Follow(e);
        _touchDrag.Completed += (_, _) => End();
        _touchDrag.Cancelled += (_, _) => End();
        _touchDrag.Released += (_, e) =>
        {
            var state = _drag;
            _drag = null;
            // A tap on the photo moves the divider there; a vertical pan that was handed to the page does nothing.
            var travel = e.Position - e.Start;
            if (state is { Started: false } && Math.Abs(travel.X) < 6 && Math.Abs(travel.Y) < 6)
            {
                FocusQuietly();
                Commit(ImageCompareMath.Clamp(PercentAt(e.Position), 0, 100));
            }
        };
    }

    private bool IsOnHandle(PointerPressedEventArgs e)
    {
        var onHandle = _handle is not null && e.Source is Visual source &&
                       (ReferenceEquals(source, _handle) || _handle.IsVisualAncestorOf(source));
        _pressOnImage = !onHandle;
        return onHandle;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var onHandle = _handle is not null && e.Source is Visual source &&
                       (ReferenceEquals(source, _handle) || _handle.IsVisualAncestorOf(source));
        // Pointer capture sends the double-click to the frame, so it checks where the presses began.
        _doubleClickPending = onHandle && e.ClickCount == 2;
    }

    #region Drag

    private void BeginPress(DragGestureEventArgs e, bool press, bool startNow)
    {
        if (_frame is null || _frameSize.Width <= 0)
        {
            return;
        }
        var top = TopLevel.GetTopLevel(this);
        _rootOrigin = top is null ? default : this.TranslatePoint(default, top) ?? default;
        var state = new ImageCompareDrag { Press = press };
        _drag = state;
        if (startNow)
        {
            Start(state, PercentAt(e.Position));
        }
    }

    private double AxisSize => Orientation == Orientation.Vertical ? _frameSize.Height : _frameSize.Width;

    private double PercentAt(Point topLevelPosition)
    {
        var local = topLevelPosition - _rootOrigin;
        var size = Math.Max(1, AxisSize);
        return (Orientation == Orientation.Vertical ? local.Y : local.X) / size * 100;
    }

    private bool Reduced => !ArcMotion.IsFull(this);

    private void Start(ImageCompareDrag state, double at)
    {
        var from = _pos.Value + _lag.Value;
        state.Started = true;
        state.Grab = state.Press ? 0 : at - from;
        _lag.Jump(0);
        _pos.Jump(from);
        _placedVelocity.Reset();
        SetDragging(true);
        FocusQuietly();
        FollowAt(state, at, first: true);
    }

    private void Follow(DragGestureEventArgs e)
    {
        if (_drag is { Started: true } state)
        {
            FollowAt(state, PercentAt(e.Position));
        }
    }

    /// <summary>Puts the line under the pointer, with resistance past the edges, and commits once per whole percent.</summary>
    private void FollowAt(ImageCompareDrag state, double at, bool first = false)
    {
        var raw = at - state.Grab;
        var edge = ImageCompareMath.Clamp(raw, 0, 100);
        var placed = ImageCompareMath.Place(raw, AxisSize, Reduced);
        state.Raw = raw;
        if (first && state.Press && !Reduced)
        {
            var from = _pos.Value;
            _pos.Jump(placed);
            _lag.Jump(from - placed);
            _ = _lag.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy));
        }
        else
        {
            _pos.Jump(placed);
        }
        _placedVelocity.AddSample(placed, TimeSpan.FromTicks(DateTime.UtcNow.Ticks));
        Commit(edge, live: true);
    }

    private void End()
    {
        var state = _drag;
        _drag = null;
        if (state is { Started: true })
        {
            // The divider stays where it was let go. Past an edge, or still catching up to a press, it springs on with
            // the speed it had.
            var from = _pos.Value + _lag.Value;
            var posVelocity = _placedVelocity.GetVelocity(TimeSpan.FromTicks(DateTime.UtcNow.Ticks)).X;
            var velocity = _lag.Velocity + (from is > 100 or < 0 ? posVelocity : 0);
            var target = ImageCompareMath.Clamp(_lag.Value != 0 ? state.Raw : from, 0, 100);
            _lag.Jump(0);
            _pos.Jump(from);
            _goal = Math.Round(target, 2);
            if (from != target)
            {
                if (Reduced)
                {
                    _pos.Jump(target);
                }
                else
                {
                    _ = _pos.AnimateTo(target, MotionSpec.Spring(ArcSpring.Snappy), velocity);
                }
            }
            Commit(target);
            SetDragging(false);
        }
        if (_doubleClickPending)
        {
            _doubleClickPending = false;
            if (_latest != 50)
            {
                Commit(50);
            }
        }
    }

    private void SetDragging(bool dragging)
    {
        if (_dragging == dragging)
        {
            return;
        }
        _dragging = dragging;
        PseudoClasses.Set(DraggingPseudoClass, dragging);
        var vertical = Orientation == Orientation.Vertical;
        var capsule = dragging ? (vertical ? new Size(36, 56) : new Size(56, 36)) : new Size(40, 40);
        _ = _capsule.AnimateTo(capsule, MotionSpec.Spring(ArcSpring.Morph));
        _ = _spread.AnimateTo(dragging ? 4 : 0, MotionSpec.Spring(ArcSpring.Morph));
    }

    /// <summary>Focus that follows a pointer keeps the handle quiet: no ring until a key is pressed.</summary>
    private void FocusQuietly()
    {
        if (_handle is { IsFocused: false } handle)
        {
            handle.Focus(NavigationMethod.Pointer);
        }
    }

    #endregion

    private void Commit(double next, bool live = false)
    {
        if (live && ImageCompareMath.JsRound(next) == ImageCompareMath.JsRound(_latest))
        {
            return;
        }
        var value = Math.Round(next, 2);
        if (value == _latest)
        {
            return;
        }
        _latest = value;
        // Drag commits are ignored by OnPositionChanged (the drag owns the divider); a release sets the goal first.
        // Keys, taps and double-clicks spring the divider there.
        SetCurrentValue(PositionProperty, value);
        RaiseEvent(new ImageComparePositionChangedEventArgs(PositionChangedEvent, value));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _handle is null || !ReferenceEquals(e.Source, _handle))
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (ImageCompareMath.KeyTarget(e.Key, shift, Orientation == Orientation.Vertical, _latest) is not { } next)
        {
            return;
        }
        e.Handled = true;
        _handle.ShowRing();
        var clamped = ImageCompareMath.Clamp(next, 0, 100);
        if (clamped == _latest)
        {
            // A key pressed at a limit gives the divider a small kick outward.
            if (!Reduced && next != clamped)
            {
                var velocity = Math.Sign(next - clamped) * ImageCompareMath.BumpSpeed / Math.Max(1, AxisSize) * 100;
                _ = _pos.AnimateTo(_goal, MotionSpec.Spring(ArcSpring.Morph), velocity);
            }
            return;
        }
        Commit(clamped);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty)
        {
            OnPositionChanged();
        }
        else if (change.Property == OrientationProperty)
        {
            CreateTouchTracker();
            var target = Orientation == Orientation.Vertical ? 90 : 0;
            if (_theta.Value != target)
            {
                if (Reduced)
                {
                    _theta.Jump(target);
                }
                else
                {
                    _ = _theta.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
                }
            }
        }
        else if (change.Property == BeforeSourceProperty)
        {
            _failedBefore = null;
            PseudoClasses.Set(BeforeLoadingPseudoClass, BeforeSource is not null);
            UpdateImageStates(initial: false);
        }
        else if (change.Property == AfterSourceProperty)
        {
            _failedAfter = null;
            PseudoClasses.Set(AfterLoadingPseudoClass, AfterSource is not null);
            UpdateImageStates(initial: false);
        }
        else if (change.Property == LabelProperty && _handle is not null)
        {
            AutomationProperties.SetName(_handle, Label);
        }
    }

    /// <summary>Positions that arrive from outside a drag (bindings, keys, a double-click) spring the divider there.</summary>
    private void OnPositionChanged()
    {
        var current = Position;
        _latest = current;
        if (_drag is { Started: true } || _goal == current)
        {
            return;
        }
        _goal = current;
        var from = _pos.Value + _lag.Value;
        _lag.Jump(0);
        _pos.Jump(from);
        if (Reduced)
        {
            _pos.Jump(current);
        }
        else
        {
            _ = _pos.AnimateTo(current, MotionSpec.Spring(ArcSpring.Snappy));
        }
    }

    #region Template and layout

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_beforeImage is not null)
        {
            _beforeImage.PropertyChanged -= OnImagePropertyChanged;
        }
        if (_afterImage is not null)
        {
            _afterImage.PropertyChanged -= OnImagePropertyChanged;
        }
        if (_handle is not null)
        {
            _handle.Owner = null;
            _handle.RenderTransform = null;
        }
        if (_beforeLayer is not null)
        {
            _beforeLayer.Clip = null;
        }
        _frame = e.NameScope.Find<Border>(FramePart);
        _beforeLayer = e.NameScope.Find<Panel>(BeforeLayerPart);
        _beforeChip = e.NameScope.Find<Control>(BeforeChipPart);
        _afterChip = e.NameScope.Find<Control>(AfterChipPart);
        _afterBottomChip = e.NameScope.Find<Control>(AfterBottomChipPart);
        _divider = e.NameScope.Find<ImageCompareDivider>(DividerPart);
        _handle = e.NameScope.Find<ImageCompareHandle>(HandlePart);
        _beforeImage = e.NameScope.Find<ArcImage>(BeforeImagePart);
        _afterImage = e.NameScope.Find<ArcImage>(AfterImagePart);
        if (_beforeLayer is not null)
        {
            _beforeLayer.Clip = _clip;
        }
        if (_handle is not null)
        {
            _handle.Owner = this;
            _handle.RenderTransform = _handleOffset;
            _handle.RenderTransformOrigin = RelativePoint.TopLeft;
            AutomationProperties.SetName(_handle, Label);
        }
        if (_beforeImage is not null)
        {
            _beforeImage.PropertyChanged += OnImagePropertyChanged;
        }
        if (_afterImage is not null)
        {
            _afterImage.PropertyChanged += OnImagePropertyChanged;
        }
        _theta.Jump(Orientation == Orientation.Vertical ? 90 : 0);
        UpdateImageStates(initial: true);
    }

    private void OnImagePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ArcImage.IsLoadingProperty || e.Property == ArcImage.IsImageLoadedProperty ||
            e.Property == ArcImage.IsFailedProperty)
        {
            UpdateImageStates(initial: false);
        }
    }

    private void UpdateImageStates(bool initial)
    {
        UpdateImageState(_beforeImage, BeforeSource, ref _failedBefore, BeforeImagePseudoClass, BeforeLoadingPseudoClass, initial);
        UpdateImageState(_afterImage, AfterSource, ref _failedAfter, AfterImagePseudoClass, AfterLoadingPseudoClass, initial);
    }

    private void UpdateImageState(ArcImage? image, ImageSource? source, ref ImageSource? failed, string showClass,
                                  string loadingClass, bool initial)
    {
        if (source is not null && image is { IsFailed: true })
        {
            failed = source;
        }
        var show = source is not null && !ReferenceEquals(failed, source);
        PseudoClasses.Set(showClass, show);
        // Only a picture that is still downloading after mount fades in; a decoded one shows at once.
        var loading = show && image is { IsImageLoaded: false } && (image.IsLoading || initial || PseudoClasses.Contains(loadingClass));
        PseudoClasses.Set(loadingClass, loading);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : DefaultWidth;
        var ratio = AspectRatio > 0 && double.IsFinite(AspectRatio) ? AspectRatio : 1.5;
        var size = new Size(width, width / ratio);
        base.MeasureOverride(size);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        _frameSize = _frame?.Bounds.Size ?? finalSize;
        _beforeChipRect = _beforeChip?.Bounds ?? default;
        _afterChipRect = _afterChip?.Bounds ?? default;
        var reach = 2 * (_frameSize.Width + _frameSize.Height) + 16;
        var rect = new Rect(-reach, -reach, reach, 2 * reach);
        if (_clip.Rect != rect)
        {
            _clip.Rect = rect;
        }
        UpdateVisuals();
        return arranged;
    }

    #endregion

    #region Rendering state

    private void OnMotion(double _) => UpdateVisuals();

    private void OnShapeMotion(Size _) => UpdateVisuals();

    /// <summary>The divider's pivot in pixels, blending from the horizontal to the vertical layout as it turns.</summary>
    private static Point Center(double percent, Size m, double turn) =>
        new(ImageCompareMath.Lerp(percent / 100 * m.Width, m.Width / 2, turn),
            ImageCompareMath.Lerp(m.Height / 2, percent / 100 * m.Height, turn));

    private void UpdateVisuals()
    {
        var m = _frameSize;
        if (_frame is null || m.Width <= 0 || m.Height <= 0)
        {
            return;
        }
        var shown = _pos.Value + _lag.Value;
        var p = ImageCompareMath.Clamp(shown, 0, 100);
        var angle = _theta.Value;
        var turn = ImageCompareMath.Clamp(angle / 90, 0, 1);
        var radians = angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // The before side of a line through the pivot at the current angle: a half plane, cut from the frame.
        if (_beforeLayer is not null)
        {
            var clipCenter = Center(p, m, turn);
            _clipTransform.Matrix = Matrix.CreateRotation(radians) * Matrix.CreateTranslation(clipCenter.X, clipCenter.Y);
            _beforeLayer.InvalidateVisual();
        }

        var pivot = Center(shown, m, turn);
        _divider?.Place(pivot, angle);

        if (_handle is not null)
        {
            // The handle keeps the line's pace in the middle and slows near an edge, so it is never cut off.
            var x = shown / 100 * m.Width;
            var y = shown / 100 * m.Height;
            var offset = ImageCompareMath.Lerp(ImageCompareMath.Soften(x, m.Width) - x, ImageCompareMath.Soften(y, m.Height) - y, turn);
            _handleOffset.X = pivot.X + offset * cos - ImageCompareHandle.Extent / 2;
            _handleOffset.Y = pivot.Y + offset * sin - ImageCompareHandle.Extent / 2;
            _handle.SetShape(_capsule.Value, _spread.Value, angle);
        }

        // Captions fade as the divider reaches them, and hand over mid-turn when the after caption changes corner.
        var across = ImageCompareMath.Clamp(1 - turn * 3, 0, 1);
        var down = ImageCompareMath.Clamp(turn * 3 - 2, 0, 1);
        const double inset = ImageCompareMath.ChipInset;
        if (_beforeChip is not null)
        {
            _beforeChip.Opacity = across * ImageCompareMath.Fade(p / 100 * m.Width - (inset + _beforeChipRect.Width + 8)) +
                                  down * ImageCompareMath.Fade(p / 100 * m.Height - (inset + _beforeChipRect.Height + 8));
        }
        if (_afterChip is not null)
        {
            _afterChip.Opacity = across * ImageCompareMath.Fade(m.Width - inset - _afterChipRect.Width - 8 - p / 100 * m.Width);
        }
        if (_afterBottomChip is not null)
        {
            _afterBottomChip.Opacity = down * ImageCompareMath.Fade(m.Height - inset - _afterChipRect.Height - 8 - p / 100 * m.Height);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ImageCompareAutomationPeer(this);

    private sealed class ImageCompareAutomationPeer : ControlAutomationPeer
    {
        public ImageCompareAutomationPeer(ImageCompare owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((ImageCompare)Owner).Label;
    }
}
