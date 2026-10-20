using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A row of slides people browse by dragging, flicking, a trackpad swipe, the arrow keys, or the controls below it
/// (Arc carousel). Use it for a short, ordered set of peers such as products or case studies, where the neighbours
/// stay in view as a hint that there is more. The active slide sits in the center, its neighbours shrink and dim with
/// distance, and the page indicator stretches into a pill that travels with the slides frame by frame.
/// <para>
/// Slides come from <c>Items</c> / <c>ItemsSource</c> (with <c>ItemTemplate</c>); <c>SelectedIndex</c> is the active slide
/// (two-way). Set <see cref="Interval"/> to add a play control that rotates through the slides; rotation pauses on
/// hover, keyboard focus, drag, while the carousel is scrolled out of view or the window is minimized, and is
/// unavailable with reduced motion. Only the slides near the current position are realized.
/// </para>
/// </summary>
[TemplatePart(ViewportPart, typeof(Panel))]
[TemplatePart(ControlsPart, typeof(Control))]
[TemplatePart(PagerPart, typeof(Control))]
[TemplatePart(PlayButtonPart, typeof(AvaloniaButton))]
[TemplatePart(PlayIconPart, typeof(ArcContentSwap))]
[TemplatePart(PreviousButtonPart, typeof(AvaloniaButton))]
[TemplatePart(NextButtonPart, typeof(AvaloniaButton))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(DraggingPseudoClass, CompactPseudoClass, ViewportFocusPseudoClass)]
public class Carousel : SelectingItemsControl
{
    public const string ViewportPart = "PART_Viewport";
    public const string ControlsPart = "PART_Controls";
    public const string PagerPart = "PART_Pager";
    public const string PlayButtonPart = "PART_PlayButton";
    public const string PlayIconPart = "PART_PlayIcon";
    public const string PreviousButtonPart = "PART_PreviousButton";
    public const string NextButtonPart = "PART_NextButton";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the track is dragged.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set when the carousel is 340px wide or less: the control row tightens.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the slide viewport has keyboard focus (its ring shows).</summary>
    public const string ViewportFocusPseudoClass = ":viewport-focus";

    private static readonly object s_play = CarouselPlayState.Play;
    private static readonly object s_pause = CarouselPlayState.Pause;

    #region Properties

    /// <summary>Names the carousel for assistive technology, for example "From the Harbour suites".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Carousel, string?>(nameof(Label));

    /// <summary>Share of the carousel width one slide takes (Arc default <c>min(80cqw, 340px)</c>: 0.8).</summary>
    public static readonly StyledProperty<double> SlideWidthRatioProperty =
        AvaloniaProperty.Register<Carousel, double>(nameof(SlideWidthRatio), 0.8);

    /// <summary>Largest slide width in px (340).</summary>
    public static readonly StyledProperty<double> MaxSlideWidthProperty =
        AvaloniaProperty.Register<Carousel, double>(nameof(MaxSlideWidth), 340);

    /// <summary>Gap between slides in px (16).</summary>
    public static readonly StyledProperty<double> SlideGapProperty =
        AvaloniaProperty.Register<Carousel, double>(nameof(SlideGap), 16);

    /// <summary>Names each slide and its indicator; defaults to "2 of 5".</summary>
    public static readonly StyledProperty<Func<int, int, string>?> SlideLabelFormatterProperty =
        AvaloniaProperty.Register<Carousel, Func<int, int, string>?>(nameof(SlideLabelFormatter));

    /// <summary>Time per slide. A positive value adds a play control that rotates through the slides.</summary>
    public static readonly StyledProperty<TimeSpan> IntervalProperty =
        AvaloniaProperty.Register<Carousel, TimeSpan>(nameof(Interval), TimeSpan.Zero);

    /// <summary>
    /// Whether the slides rotate (two-way; the play control toggles it). Off by default, so motion begins with the
    /// viewer; set it to start rotating on load.
    /// </summary>
    public static readonly StyledProperty<bool> IsAutoPlayProperty =
        AvaloniaProperty.Register<Carousel, bool>(nameof(IsAutoPlay), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>True when rotation is available (an interval, two or more slides, full motion).</summary>
    public static readonly DirectProperty<Carousel, bool> CanRotateProperty =
        AvaloniaProperty.RegisterDirect<Carousel, bool>(nameof(CanRotate), o => o.CanRotate);

    /// <summary>True while the rotation timer is actually running (playing and not paused).</summary>
    public static readonly DirectProperty<Carousel, bool> IsRotatingProperty =
        AvaloniaProperty.RegisterDirect<Carousel, bool>(nameof(IsRotating), o => o.IsRotating);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public double SlideWidthRatio { get => GetValue(SlideWidthRatioProperty); set => SetValue(SlideWidthRatioProperty, value); }

    public double MaxSlideWidth { get => GetValue(MaxSlideWidthProperty); set => SetValue(MaxSlideWidthProperty, value); }

    public double SlideGap { get => GetValue(SlideGapProperty); set => SetValue(SlideGapProperty, value); }

    public Func<int, int, string>? SlideLabelFormatter
    {
        get => GetValue(SlideLabelFormatterProperty);
        set => SetValue(SlideLabelFormatterProperty, value);
    }

    public TimeSpan Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

    public bool IsAutoPlay { get => GetValue(IsAutoPlayProperty); set => SetValue(IsAutoPlayProperty, value); }

    private bool _canRotate;

    public bool CanRotate
    {
        get => _canRotate;
        private set => SetAndRaise(CanRotateProperty, ref _canRotate, value);
    }

    private bool _isRotating;

    public bool IsRotating
    {
        get => _isRotating;
        private set => SetAndRaise(IsRotatingProperty, ref _isRotating, value);
    }

    #endregion

    // The track offset is a MotionValue, not a ControlTheme transition: drags write it every move, releases hand their
    // velocity to the settle spring, and a press can catch it mid-flight.
    private readonly MotionValue<double> _x;
    private readonly MotionValue<double> _drain;
    private readonly DispatcherTimer _wheelTimer;
    private readonly List<int> _drained = new();
    private double[] _drains = [];
    private PausableTimer? _timer;
    private int _drainIndex = -1;
    private int _target;
    private double _width;
    private double _slideWidth;
    private double _step;
    private bool _selecting;

    private DragTracker? _drag;
    private bool _dragActive;
    private bool _caught;
    private int _dragFrom;
    private double _dragOrigin;
    private double _dragStartOffset;
    private bool _wheelActive;
    private double _wheelRaw;

    private bool _hovered;
    private bool _focusPaused;
    private bool _inView = true;
    private bool _windowVisible = true;
    private bool _attached;
    private ViewportVisibility? _visibility;
    private Window? _window;

    private CarouselViewport? _viewport;
    private Control? _controls;
    private CarouselPager? _pager;
    private AvaloniaButton? _playButton;
    private ArcContentSwap? _playIcon;
    private MediaButton? _previous;
    private MediaButton? _next;
    private MediaLiveRegion? _announcer;

    static Carousel()
    {
        SelectionModeProperty.OverrideDefaultValue<Carousel>(SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<Carousel>(false);
    }

    public Carousel()
    {
        _x = new MotionValue<double>(this, 0, MotionVectors.Double);
        _x.Changed += OnTrackMotion;
        _drain = new MotionValue<double>(this, 0, MotionVectors.Double);
        _drain.Changed += OnDrainMotion;
        _wheelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _wheelTimer.Tick += OnWheelSettled;
        AddHandler(GotFocusEvent, OnAnyGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        Classes.CollectionChanged += (_, _) => UpdateRotation();
    }

    #region Public API

    /// <summary>Moves to the next slide (no wrap).</summary>
    public void Next() => Select(_target + 1);

    /// <summary>Moves to the previous slide (no wrap).</summary>
    public void Previous() => Select(_target - 1);

    /// <summary>Moves to <paramref name="index"/> (clamped).</summary>
    public void GoTo(int index) => Select(index);

    #endregion

    #region Internal state (track panel, pager, tests)

    internal double TrackOffset => _x.Value;

    internal double Step => _step;

    internal double SlideWidthPx => _slideWidth;

    /// <summary>Continuous slide position the scale, dim and indicator read every frame.</summary>
    internal double Progress => _step > 0 ? -_x.Value / _step : _target;

    internal int TargetIndex => _target;

    internal CarouselTrackPanel? Track => ItemsPanelRoot as CarouselTrackPanel;

    internal CarouselPager? Pager => _pager;

    internal string? LastAnnouncement => _announcer?.LastAnnouncement;

    internal double GetDrain(int index) => index >= 0 && index < _drains.Length ? _drains[index] : 0;

    internal string LabelFor(int index)
    {
        var count = ItemCount;
        return SlideLabelFormatter?.Invoke(index, count) ?? CarouselMath.DefaultSlideLabel(index, count);
    }

    internal void OnTrackMeasured(double width)
    {
        _width = width;
        _slideWidth = CarouselMath.SlideWidth(width, SlideWidthRatio, MaxSlideWidth);
        var step = _slideWidth + SlideGap;
        var changed = step != _step;
        _step = step;
        UpdateControlsInset();
        // Centers move with the width; outside a drag the track snaps to the target slide.
        if (changed && !_dragActive)
        {
            _x.Stop();
            _x.Jump(-_target * step);
        }
    }

    internal void OnItemsReset()
    {
        var count = ItemCount;
        Array.Resize(ref _drains, count);
        Array.Clear(_drains);
        _drained.Clear();
        _drainIndex = -1;
        _target = Math.Clamp(SelectedIndex, 0, Math.Max(0, count - 1));
        if (_step > 0 && !_dragActive)
        {
            _x.Stop();
            _x.Jump(-_target * _step);
        }
        UpdateSelectionState();
    }

    internal void PrepareSlide(Control container, int index)
    {
        if (container is CarouselSlide slide)
        {
            slide.Index = index;
            slide.SetActive(index == SelectedIndex);
            slide.SetLabel(LabelFor(index));
            slide.SetDistance(Progress - index);
        }
    }

    internal void SelectFromTab(int index) => Select(index);

    internal void OnViewportFocusChanged(bool visible) => PseudoClasses.Set(ViewportFocusPseudoClass, visible);

    #endregion

    #region Containers

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new CarouselSlide();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<CarouselSlide>(item, out recycleKey);

    #endregion

    #region Selection and gliding

    /// <summary>Springs the track to a slide from wherever it is, carrying any velocity it already has.</summary>
    private void Glide(int to, double velocity = 0)
    {
        if (_step <= 0)
        {
            return;
        }
        var target = -to * _step;
        if (!ArcMotion.IsFull(this))
        {
            _x.Jump(target);
            return;
        }
        _ = MomentumProjector.SettleTo(_x, target, velocity, ArcSpring.Smooth);
    }

    private void Select(int to, double velocity = 0, bool announce = true)
    {
        var count = ItemCount;
        if (count == 0)
        {
            return;
        }
        var next = Math.Clamp(to, 0, count - 1);
        _target = next;
        Glide(next, velocity);
        if (next == SelectedIndex)
        {
            return;
        }
        _selecting = true;
        try
        {
            SelectedIndex = next;
        }
        finally
        {
            _selecting = false;
        }
        if (announce)
        {
            _announcer?.Announce(LabelFor(next));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty)
        {
            var next = SelectedIndex;
            if (!_selecting && next >= 0 && next != _target)
            {
                _target = next;
                Glide(next);
            }
            UpdateSelectionState();
        }
        else if (change.Property == ItemCountProperty)
        {
            if (_drains.Length != ItemCount)
            {
                Array.Resize(ref _drains, ItemCount);
            }
            UpdateSelectionState();
        }
        else if (change.Property == SlideWidthRatioProperty || change.Property == MaxSlideWidthProperty ||
                 change.Property == SlideGapProperty)
        {
            Track?.InvalidateMeasure();
        }
        else if (change.Property == SlideLabelFormatterProperty)
        {
            UpdateSelectionState();
        }
        else if (change.Property == IntervalProperty)
        {
            UpdateRotation();
        }
        else if (change.Property == IsAutoPlayProperty)
        {
            UpdatePlayButton();
            UpdateRotation();
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == IsKeyboardFocusWithinProperty && !IsKeyboardFocusWithin)
        {
            _focusPaused = false;
            UpdateRotation();
        }
    }

    private void UpdateSelectionState()
    {
        var count = ItemCount;
        var selected = SelectedIndex;
        if (Track is { } track)
        {
            foreach (var pair in track.RealizedSlides)
            {
                if (pair.Value is CarouselSlide slide)
                {
                    slide.SetActive(pair.Key == selected);
                    slide.SetLabel(LabelFor(pair.Key));
                }
            }
        }
        _pager?.Sync(count, selected, LabelFor);
        if (_controls is not null)
        {
            _controls.IsVisible = count > 1;
        }
        if (_previous is not null)
        {
            _previous.IsSoftDisabled = selected <= 0;
        }
        if (_next is not null)
        {
            _next.IsSoftDisabled = selected >= count - 1;
        }
        UpdateRotation();
    }

    private void OnTrackMotion(double _)
    {
        Track?.UpdateTrack();
        var progress = Progress;
        _pager?.SetProgress(progress);
        // A pill that just ran out keeps its state while it shrinks away; once the track is a step past, it resets.
        for (var i = _drained.Count - 1; i >= 0; i--)
        {
            var index = _drained[i];
            if (index != _target && index != _drainIndex && Math.Abs(progress - index) >= 1)
            {
                if (index < _drains.Length)
                {
                    _drains[index] = 0;
                }
                _drained.RemoveAt(i);
                _pager?.InvalidateVisual();
            }
        }
    }

    private void UpdateControlsInset()
    {
        var compact = _width <= CarouselMath.CompactWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_pager is not null)
        {
            _pager.IsCompact = compact;
        }
        if (_controls is not null)
        {
            // Controls line up with the edges of the centered slide.
            var pad = (_width - _slideWidth) / 2;
            if (compact)
            {
                pad = Math.Max(0, pad - 8);
            }
            var margin = new Thickness(pad, 0, pad, 0);
            if (_controls.Margin != margin)
            {
                _controls.Margin = margin;
            }
        }
    }

    #endregion

    #region Rotation

    private void UpdateRotation()
    {
        var count = ItemCount;
        CanRotate = Interval > TimeSpan.Zero && count > 1 && Classes.Contains(ArcMotion.FullPseudoClass);
        if (_playButton is not null)
        {
            _playButton.IsVisible = CanRotate;
        }
        var running = CanRotate && IsAutoPlay && !_hovered && !_focusPaused && !_dragActive && _inView && _windowVisible &&
                      IsInVisualTree();
        var index = SelectedIndex;
        if (running)
        {
            if (IsRotating && _drainIndex == index)
            {
                return;
            }
            if (_drainIndex != index && _drainIndex >= 0)
            {
                _drain.Stop();
            }
            _drainIndex = index;
            var from = GetDrain(index);
            var remaining = TimeSpan.FromTicks((long)(Interval.Ticks * (1 - from)));
            _timer ??= new PausableTimer(remaining, OnRotationElapsed);
            _timer.Restart(remaining);
            _drain.Jump(from);
            _ = _drain.AnimateTo(1, MotionSpec.Tween(remaining, new Avalonia.Animation.Easings.LinearEasing()));
            IsRotating = true;
        }
        else if (IsRotating)
        {
            _timer?.Stop();
            _drain.Stop();
            IsRotating = false;
        }
    }

    private bool IsInVisualTree() => _attached;

    private void OnDrainMotion(double value)
    {
        var index = _drainIndex;
        if (index < 0 || index >= _drains.Length)
        {
            return;
        }
        _drains[index] = value;
        if (value > 0 && !_drained.Contains(index))
        {
            _drained.Add(index);
        }
        _pager?.InvalidateVisual();
    }

    private void OnRotationElapsed()
    {
        var index = SelectedIndex;
        if (index >= 0 && index < _drains.Length)
        {
            _drain.Stop();
            _drains[index] = 1;
            if (!_drained.Contains(index))
            {
                _drained.Add(index);
            }
        }
        IsRotating = false;
        var last = ItemCount - 1;
        Select(index >= last ? 0 : index + 1, announce: false);
        UpdateRotation();
    }

    private void TogglePlay()
    {
        if (!IsAutoPlay)
        {
            SetCurrentValue(IsAutoPlayProperty, true);
            return;
        }
        SetCurrentValue(IsAutoPlayProperty, false);
        // Pausing refills the pill: this slide stays until the viewer moves on.
        var index = SelectedIndex;
        if (index < 0 || index >= _drains.Length)
        {
            return;
        }
        _drainIndex = index;
        if (ArcMotion.IsFull(this))
        {
            _ = _drain.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        else
        {
            _drain.Jump(0);
        }
    }

    private void UpdatePlayButton()
    {
        if (_playIcon is not null)
        {
            _playIcon.Content = IsAutoPlay ? s_pause : s_play;
        }
        if (_playButton is not null)
        {
            AutomationProperties.SetName(_playButton, IsAutoPlay ? "Pause slide rotation" : "Start slide rotation");
        }
    }

    private void OnAnyGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // Keyboard focus anywhere but the play control pauses rotation (Arc: :focus-visible).
        var keyboard = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        _focusPaused = keyboard && !ReferenceEquals(e.Source, _playButton);
        UpdateRotation();
    }

    #endregion

    #region Pointer

    private void AttachViewport(CarouselViewport viewport)
    {
        viewport.Owner = this;
        _drag = new DragTracker(viewport, new DragOptions
        {
            Axis = DragAxis.Horizontal,
            Threshold = CarouselMath.Slop,
            HandledEventsToo = true,
            VelocityWindow = TimeSpan.FromMilliseconds(90),
            VelocityStaleAfter = TimeSpan.FromMilliseconds(50)
        })
        {
            CanBegin = _ => ItemCount >= 2 && _step > 0 && !_dragActive
        };
        _drag.Pressed += OnDragPressed;
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += (_, e) => FinishDrag(e.Velocity.X);
        _drag.Cancelled += (_, _) => FinishDrag(0);
        _drag.Released += OnDragReleased;
        viewport.PointerEntered += OnViewportPointerEntered;
        viewport.PointerExited += OnViewportPointerExited;
        viewport.AddHandler(PointerWheelChangedEvent, OnViewportWheel, RoutingStrategies.Bubble);
    }

    private void DetachViewport(CarouselViewport viewport)
    {
        viewport.Owner = null;
        _drag?.Dispose();
        _drag = null;
        viewport.PointerEntered -= OnViewportPointerEntered;
        viewport.PointerExited -= OnViewportPointerExited;
        viewport.RemoveHandler(PointerWheelChangedEvent, OnViewportWheel);
    }

    private (double Min, double Max) TrackBounds => (-(Math.Max(0, ItemCount - 1)) * _step, 0);

    private double Band(double raw)
    {
        var (min, max) = TrackBounds;
        return CarouselMath.Band(raw, min, max, _width);
    }

    private double Unband(double shown)
    {
        var (min, max) = TrackBounds;
        return CarouselMath.Unband(shown, min, max, _width);
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        // Touching a visibly moving track catches it where it is, the way a hand stops a spinning wheel. A settling
        // tail is left alone so a tap still selects.
        _caught = _x.IsAnimating && Math.Abs(_x.Value + _target * _step) > 12;
        if (_caught)
        {
            _x.Stop();
        }
        _dragFrom = _target;
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        // The slop is consumed rather than applied, so the track starts moving from the finger without a jump.
        _x.Stop();
        _dragOrigin = Unband(_x.Value);
        _dragStartOffset = e.Offset.X;
        _dragActive = true;
        PseudoClasses.Set(DraggingPseudoClass, true);
        UpdateRotation();
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        if (_dragActive)
        {
            _x.Jump(Band(_dragOrigin + e.Offset.X - _dragStartOffset));
        }
    }

    private void FinishDrag(double velocity)
    {
        if (!_dragActive)
        {
            return;
        }
        _dragActive = false;
        PseudoClasses.Set(DraggingPseudoClass, false);
        var shown = _x.Value;
        var to = CarouselMath.SelectTarget(shown, velocity, _step, _dragFrom, ItemCount);
        // Inside the bounds the track moves 1:1 with the pointer, so the release speed carries into the spring; past
        // an edge the banded speed is the true one.
        var (min, max) = TrackBounds;
        var handOff = velocity;
        if (shown < min || shown > max)
        {
            var raw = Unband(shown);
            handOff = (Band(raw + velocity * 0.001) - Band(raw)) / 0.001;
        }
        Select(to, handOff);
        UpdateRotation();
    }

    private void OnDragReleased(object? sender, DragGestureEventArgs e)
    {
        if (_caught)
        {
            _caught = false;
            Select((int)Math.Round(-_x.Value / Math.Max(1, _step)));
            return;
        }
        var travel = e.Position - e.Start;
        if (Math.Abs(travel.X) >= CarouselMath.Slop || Math.Abs(travel.Y) >= CarouselMath.Slop)
        {
            return;
        }
        // A tap on a neighbour brings it to the center.
        var hit = SlideAt(e.Position);
        if (hit >= 0 && hit != _target)
        {
            Select(hit);
        }
    }

    private int SlideAt(Point topLevelPosition)
    {
        if (_viewport is null || _step <= 0 || TopLevel.GetTopLevel(this) is not { } top)
        {
            return -1;
        }
        if (top.TranslatePoint(topLevelPosition, _viewport) is not { } local)
        {
            return -1;
        }
        var pad = (_width - _slideWidth) / 2;
        var along = local.X - _x.Value - pad;
        var index = (int)Math.Round((along - _slideWidth / 2) / _step);
        if (index < 0 || index >= ItemCount)
        {
            return -1;
        }
        var center = index * _step + _slideWidth / 2;
        var scale = 1 - (1 - CarouselMath.NeighbourScale) * CarouselMath.Distance(Progress, index);
        return Math.Abs(along - center) <= _slideWidth / 2 * scale ? index : -1;
    }

    private void OnViewportPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Mouse)
        {
            _hovered = true;
            UpdateRotation();
        }
    }

    private void OnViewportPointerExited(object? sender, PointerEventArgs e)
    {
        _hovered = false;
        UpdateRotation();
    }

    /// <summary>A horizontal trackpad swipe moves the track 1:1 and settles on the nearest slide once it ends.</summary>
    private void OnViewportWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ItemCount < 2 || _dragActive || _step <= 0)
        {
            return;
        }
        // Avalonia reports wheel deltas in lines; scroll viewers move 50px per line.
        var deltaX = e.Delta.X * 50;
        var deltaY = e.Delta.Y * 50;
        if (!_wheelActive && Math.Abs(deltaX) <= Math.Abs(deltaY))
        {
            return;
        }
        e.Handled = true;
        if (!_wheelActive)
        {
            _wheelActive = true;
            _x.Stop();
            _wheelRaw = Unband(_x.Value);
        }
        _wheelRaw += deltaX;
        _x.Jump(Band(_wheelRaw));
        _wheelTimer.Stop();
        _wheelTimer.Start();
    }

    private void OnWheelSettled(object? sender, EventArgs e)
    {
        _wheelTimer.Stop();
        _wheelActive = false;
        Select((int)Math.Round(-_x.Value / Math.Max(1, _step)));
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || ItemCount < 2 || (e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Control | KeyModifiers.Meta)) != 0 ||
            e.Source is TextBox)
        {
            base.OnKeyDown(e);
            return;
        }
        var from = _target;
        int? to = e.Key switch
        {
            Key.Left => from - 1,
            Key.Right => from + 1,
            Key.Home => 0,
            Key.End => ItemCount - 1,
            _ => null
        };
        if (to is not { } requested)
        {
            base.OnKeyDown(e);
            return;
        }
        e.Handled = true;
        var next = Math.Clamp(requested, 0, ItemCount - 1);
        Select(next);
        if (e.Source is CarouselPagerTab && _pager is not null && next < _pager.Tabs.Count)
        {
            _pager.Tabs[next].Focus(NavigationMethod.Directional);
        }
        else if (e.Source is Visual source && _viewport is not null && !ReferenceEquals(source, _viewport) &&
                 _viewport.IsVisualAncestorOf(source))
        {
            _viewport.Focus(NavigationMethod.Directional);
        }
    }

    #endregion

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_viewport is not null)
        {
            DetachViewport(_viewport);
        }
        if (_pager is not null)
        {
            _pager.Owner = null;
        }
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClick;
        }
        if (_previous is not null)
        {
            _previous.Click -= OnPreviousClick;
        }
        if (_next is not null)
        {
            _next.Click -= OnNextClick;
        }
        _viewport = e.NameScope.Find<CarouselViewport>(ViewportPart);
        _controls = e.NameScope.Find<Control>(ControlsPart);
        _pager = e.NameScope.Find<CarouselPager>(PagerPart);
        _playButton = e.NameScope.Find<AvaloniaButton>(PlayButtonPart);
        _playIcon = e.NameScope.Find<ArcContentSwap>(PlayIconPart);
        _previous = e.NameScope.Find<MediaButton>(PreviousButtonPart);
        _next = e.NameScope.Find<MediaButton>(NextButtonPart);
        _announcer = e.NameScope.Find<MediaLiveRegion>(AnnouncerPart);
        if (_viewport is not null)
        {
            AttachViewport(_viewport);
        }
        if (_pager is not null)
        {
            _pager.Owner = this;
        }
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClick;
        }
        if (_playIcon is not null)
        {
            _playIcon.ContentTemplate = CarouselPlayGlyph.Template;
        }
        if (_previous is not null)
        {
            _previous.Click += OnPreviousClick;
            AutomationProperties.SetName(_previous, "Previous slide");
        }
        if (_next is not null)
        {
            _next.Click += OnNextClick;
            AutomationProperties.SetName(_next, "Next slide");
        }
        UpdatePlayButton();
        UpdateControlsInset();
        UpdateSelectionState();
    }

    private void OnPlayClick(object? sender, RoutedEventArgs e) => TogglePlay();

    private void OnPreviousClick(object? sender, RoutedEventArgs e)
    {
        if (_target > 0)
        {
            Select(_target - 1);
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_target < ItemCount - 1)
        {
            Select(_target + 1);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        // ViewportVisibility reports the first reveal (and every change after it); until then the carousel counts as
        // out of view, like an IntersectionObserver that has not fired yet.
        _inView = false;
        _visibility = new ViewportVisibility(this, threshold: 0.001, once: false);
        _visibility.VisibilityChanged += OnVisibilityChanged;
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            _window.PropertyChanged += OnWindowPropertyChanged;
            _windowVisible = _window.WindowState != WindowState.Minimized;
        }
        UpdateRotation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        if (_visibility is not null)
        {
            _visibility.VisibilityChanged -= OnVisibilityChanged;
            _visibility.Dispose();
            _visibility = null;
        }
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
            _window = null;
        }
        _wheelTimer.Stop();
        _wheelActive = false;
        UpdateRotation();
        _timer?.Stop();
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        _inView = visible;
        UpdateRotation();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && _window is not null)
        {
            _windowVisible = _window.WindowState != WindowState.Minimized;
            UpdateRotation();
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new CarouselAutomationPeer(this);

    private sealed class CarouselAutomationPeer : ControlAutomationPeer
    {
        public CarouselAutomationPeer(Carousel owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetLocalizedControlTypeCore() => "carousel";

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((Carousel)Owner).Label;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
