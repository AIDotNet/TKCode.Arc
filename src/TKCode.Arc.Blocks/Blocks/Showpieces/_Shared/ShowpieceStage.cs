using System.Diagnostics;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>What the mouse wheel does over a <see cref="ShowpieceStage"/>.</summary>
internal enum ShowpieceWheelMode
{
    /// <summary>Every wheel event zooms (Semantic zoom; the page needs room to scroll around the stage).</summary>
    Zoom,

    /// <summary>Notched mouse wheels and Ctrl / ⌘ zoom; trackpad scrolling passes to the page (Customer galaxy).</summary>
    NotchedZoom,

    /// <summary>Only Ctrl / ⌘ + wheel (and pinch) zooms; plain scrolling passes to the page (Activity terrain).</summary>
    CtrlZoom,

    /// <summary>The wheel is ignored and scrolls the page (Revenue globe).</summary>
    PassThrough
}

/// <summary>Where a zoom request came from.</summary>
internal enum ShowpieceZoomSource
{
    Wheel,

    /// <summary>Ctrl / ⌘ + wheel, a trackpad magnify gesture or a touch pinch.</summary>
    Pinch,

    Keyboard,

    Button
}

/// <summary>
/// Base of the Showpiece canvases: a focusable, self-drawn <see cref="Control"/> that turns pointer, wheel and key
/// input into hooks, drives one frame loop and redraws on theme and motion-level changes.
/// <list type="bullet">
/// <item>Input: drags through a Core <see cref="DragTracker"/> (5px threshold; a press under it is a tap), double
/// clicks, wheel per <see cref="WheelMode"/>, Ctrl / ⌘ + wheel and trackpad magnify as pinch, touch pinch, hover with a
/// hit-test hook (<see cref="HitTestItem"/>) that also picks the cursor, and key routing
/// (<see cref="OnStageKeyDown"/>: + / = / − zoom by <see cref="KeyZoomFactor"/>, 0 / Home reset).</item>
/// <item>Frames: a Core <see cref="LiveLoop"/> runs only while something moves (<see cref="RequestFrames"/>, the
/// <see cref="Camera"/> moving, or <see cref="OnAdvance"/> returning true), only while attached, visible and in view;
/// when it stops the stage draws its resting frame (<see cref="OnLoopStopped"/>). Idle stages request no frames.</item>
/// <item>A <see cref="Camera"/> (2D or orbit) gets the stage size and motion level, is advanced every frame, and by
/// default receives drags (pan / orbit), zoom requests and resets.</item>
/// <item>Theme changes clear <see cref="TextCache"/> and call <see cref="OnThemeChanged"/>; motion level changes call
/// <see cref="OnMotionLevelChanged"/>. The focus ring (2px <c>FocusRing</c>, 2px outside, keyboard focus only) and
/// the clip to <see cref="CornerRadius"/> are drawn by the base around <see cref="RenderStage"/>.</item>
/// </list>
/// Blocks derive an internal stage (a template part of their public control) and override the hooks.
/// </summary>
/// <remarks>
/// Code-driven motion: cameras and per-frame scene state (dimming, hover growth, drift) advance together in one frame
/// loop with explicit time steps; a ControlTheme transition cannot drive self-drawn scene state.
/// </remarks>
[PseudoClasses(DraggingPseudoClass)]
internal abstract class ShowpieceStage : Control
{
    public const string DraggingPseudoClass = ":dragging";

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ShowpieceStage, IBrush?>(nameof(Background));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ShowpieceStage, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<ShowpieceWheelMode> WheelModeProperty =
        AvaloniaProperty.Register<ShowpieceStage, ShowpieceWheelMode>(nameof(WheelMode));

    public static readonly StyledProperty<double> WheelSensitivityProperty =
        AvaloniaProperty.Register<ShowpieceStage, double>(nameof(WheelSensitivity), ShowpieceWheel.WheelSensitivity);

    public static readonly StyledProperty<double> PinchSensitivityProperty =
        AvaloniaProperty.Register<ShowpieceStage, double>(nameof(PinchSensitivity), ShowpieceWheel.PinchSensitivity);

    public static readonly StyledProperty<double> KeyZoomFactorProperty =
        AvaloniaProperty.Register<ShowpieceStage, double>(nameof(KeyZoomFactor), 1.5);

    /// <summary>Fill under the scene (also makes the whole stage hit-testable); default transparent.</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Clip radius of the scene; the focus ring follows it, 2px outside.</summary>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>What the wheel does (default <see cref="ShowpieceWheelMode.Zoom"/>).</summary>
    public ShowpieceWheelMode WheelMode { get => GetValue(WheelModeProperty); set => SetValue(WheelModeProperty, value); }

    /// <summary>Plain wheel zoom sensitivity per web px (default 0.0022: ×1.246 per notch).</summary>
    public double WheelSensitivity { get => GetValue(WheelSensitivityProperty); set => SetValue(WheelSensitivityProperty, value); }

    /// <summary>Ctrl + wheel / pinch sensitivity per web px (default 0.012; Activity terrain 0.01).</summary>
    public double PinchSensitivity { get => GetValue(PinchSensitivityProperty); set => SetValue(PinchSensitivityProperty, value); }

    /// <summary>Zoom step of the + / − keys (Semantic zoom 1.5, Customer galaxy 1.6, Activity terrain 1.25).</summary>
    public double KeyZoomFactor { get => GetValue(KeyZoomFactorProperty); set => SetValue(KeyZoomFactorProperty, value); }

    private static readonly Cursor s_handCursor = new(StandardCursorType.Hand);
    private static readonly Cursor s_dragCursor = new(StandardCursorType.SizeAll);

    private readonly DragTracker _drag;
    private readonly LiveLoop _loop;
    private readonly ShowpieceWheelClassifier _wheel = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IShowpieceCamera? _camera;
    private ShowpieceTextCache? _textCache;
    private ArcMotionLevel _motionLevel = ArcMotionLevel.Full;
    private bool _motionKnown;
    private Pen? _ringPen;
    private Color _ringColor;
    private bool _themeDirty = true;
    private object? _pressItem;
    private Point _pressPosition;
    private double _lastPinchScale = 1;

    /// <param name="dragThreshold">Pointer travel in px before a press becomes a drag (Arc: 5; 4px is still a click).</param>
    protected ShowpieceStage(double dragThreshold = 5)
    {
        _drag = new DragTracker(this, new DragOptions { Threshold = dragThreshold });
        _drag.Pressed += OnDragPressed;
        _drag.Starting += OnDragStarting;
        _drag.Started += OnDragStarted;
        _drag.Moved += OnDragMoved;
        _drag.Completed += OnDragCompleted;
        _drag.Cancelled += OnDragCancelled;
        _drag.Released += OnDragReleased;
        _loop = new LiveLoop(this, OnFrame) { MinimumLevel = ArcMotionLevel.Reduced };
        _loop.RunningChanged += OnLoopRunningChanged;
        ResourcesChanged += OnStageResourcesChanged;
        AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnMagnify);
        GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
    }

    static ShowpieceStage()
    {
        FocusableProperty.OverrideDefaultValue<ShowpieceStage>(true);
        FocusAdornerProperty.OverrideDefaultValue<ShowpieceStage>(null);
        AffectsRender<ShowpieceStage>(BackgroundProperty, CornerRadiusProperty);
    }

    /// <summary>The frame loop (blocks may change <see cref="LiveLoop.MinimumLevel"/>; default Reduced so short fades run).</summary>
    protected LiveLoop Loop => _loop;

    /// <summary>Frames are being delivered.</summary>
    public bool IsLoopRunning => _loop.IsRunning;

    /// <summary>A drag is in progress.</summary>
    public bool IsDragging => _drag.IsDragging;

    /// <summary>The effective motion level (inherited level and theme motion mode).</summary>
    public ArcMotionLevel MotionLevel
    {
        get
        {
            EnsureMotionLevel();
            return _motionLevel;
        }
    }

    /// <summary>The item under the pointer from the last hover (null when none or the pointer left).</summary>
    public object? HoverItem { get; private set; }

    /// <summary>Label of the stage for screen readers (<c>aria-roledescription</c>): "interactive chart", "zoomable map".</summary>
    public string? RoleDescription { get; set; }

    /// <summary>Text, font and brush cache for the scene; cleared on theme changes.</summary>
    protected ShowpieceTextCache TextCache => _textCache ??= new ShowpieceTextCache();

    /// <summary>
    /// The stage's camera: sized with the stage, given its motion level, advanced every frame, redrawn when it
    /// changes, and (by default) driven by drags, zoom requests and resets.
    /// </summary>
    protected IShowpieceCamera? Camera
    {
        get => _camera;
        set
        {
            if (ReferenceEquals(_camera, value))
            {
                return;
            }
            if (_camera is not null)
            {
                _camera.Changed -= OnCameraChanged;
            }
            _camera = value;
            if (value is not null)
            {
                value.MotionLevel = MotionLevel;
                value.SetViewport(Bounds.Size);
                value.Changed += OnCameraChanged;
            }
            InvalidateVisual();
        }
    }

    /// <summary>Asks for frames; they run until nothing moves (see <see cref="OnAdvance"/>).</summary>
    public void RequestFrames() => _loop.Start();

    // Hooks ------------------------------------------------------------------------------------------------------------

    /// <summary>Draws the scene (clipped to the stage and its corner radius).</summary>
    protected abstract void RenderStage(DrawingContext context);

    /// <summary>
    /// Advances scene state (fades, dimming, drift, per-item springs) by <paramref name="dt"/> seconds after the camera;
    /// return true while it still moves: the stage redraws after every frame that returns true, and stops asking for
    /// frames (then draws the resting frame) once this and the camera are both at rest.
    /// </summary>
    protected virtual bool OnAdvance(double dt) => false;

    /// <summary>The frame loop stopped (nothing moves, or a gate closed): update state for the resting frame.</summary>
    protected virtual void OnLoopStopped()
    {
    }

    /// <summary>The theme or accent changed: drop cached brushes and pens (the text cache is already cleared).</summary>
    protected virtual void OnThemeChanged()
    {
    }

    /// <summary>The effective motion level changed (the camera already follows it).</summary>
    protected virtual void OnMotionLevelChanged(ArcMotionLevel level)
    {
    }

    /// <summary>The item at a stage point (null for empty space); hovered items get the hand cursor.</summary>
    protected virtual object? HitTestItem(Point position) => null;

    /// <summary>The pointer moved over the stage (not dragging) or left it (<paramref name="position"/> null).</summary>
    protected virtual void OnHoverChanged(Point? position, object? item)
    {
    }

    /// <summary>A press released under the drag threshold.</summary>
    protected virtual void OnTap(Point position, object? item, KeyModifiers modifiers)
    {
    }

    /// <summary>A double click / double tap.</summary>
    protected virtual void OnDoubleTap(Point position, object? item)
    {
    }

    /// <summary>A drag is about to start at <paramref name="position"/>; return false to refuse it.</summary>
    protected virtual bool OnDragStarting(Point position, object? pressedItem) => true;

    /// <summary>A drag started. Default: <see cref="ShowpieceCamera2D.BeginPan"/> / <see cref="ShowpieceOrbitCamera.BeginDrag"/>.</summary>
    protected virtual void OnDragStarted(Point position)
    {
        switch (_camera)
        {
            case ShowpieceCamera2D camera2D:
                camera2D.BeginPan();
                break;
            case ShowpieceOrbitCamera orbit:
                orbit.BeginDrag();
                break;
        }
    }

    /// <summary>The drag moved by <paramref name="delta"/> px. Default: pan / orbit the camera.</summary>
    protected virtual void OnDragMoved(Vector delta, Point position)
    {
        switch (_camera)
        {
            case ShowpieceCamera2D camera2D:
                camera2D.PanBy(delta);
                break;
            case ShowpieceOrbitCamera orbit:
                orbit.DragBy(delta);
                break;
        }
    }

    /// <summary>The drag ended with a release at <paramref name="velocity"/> px/s. Default: release the camera.</summary>
    protected virtual void OnDragCompleted(Vector velocity)
    {
        switch (_camera)
        {
            case ShowpieceCamera2D camera2D:
                camera2D.EndPan(velocity);
                break;
            case ShowpieceOrbitCamera orbit:
                orbit.EndDrag(velocity);
                break;
        }
    }

    /// <summary>The drag was abandoned (capture lost). Default: release the camera without speed.</summary>
    protected virtual void OnDragCancelled() => OnDragCompleted(default);

    /// <summary>
    /// A zoom request by <paramref name="factor"/> about <paramref name="anchor"/> (stage px; null = the center).
    /// Default: <see cref="ShowpieceCamera2D.ZoomBy"/> (anchored) or <see cref="ShowpieceOrbitCamera.ZoomBy"/>.
    /// </summary>
    protected virtual void OnZoom(double factor, Point? anchor, ShowpieceZoomSource source)
    {
        switch (_camera)
        {
            case ShowpieceCamera2D camera2D:
                camera2D.ZoomBy(factor, anchor);
                break;
            case ShowpieceOrbitCamera orbit:
                orbit.ZoomBy(factor);
                break;
        }
    }

    /// <summary>The wheel scrolled without zooming (the event stays unhandled so the page scrolls).</summary>
    protected virtual void OnWheelScrolled(PointerWheelEventArgs e)
    {
    }

    /// <summary>0 / Home (and Fit buttons): back to the overview. Default: fit / reset the camera.</summary>
    protected virtual void OnResetView()
    {
        switch (_camera)
        {
            case ShowpieceCamera2D camera2D:
                camera2D.Fit();
                break;
            case ShowpieceOrbitCamera orbit:
                orbit.Reset();
                break;
        }
    }

    /// <summary>
    /// Key routing; return true when handled. Default: + / = / NumPad+ zoom in by <see cref="KeyZoomFactor"/>,
    /// − / NumPad− zoom out, 0 / Home reset. Override for arrows, Enter, Escape (call the base for the defaults).
    /// </summary>
    protected virtual bool OnStageKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
            e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }
        switch (e.Key)
        {
            case Key.OemPlus:
            case Key.Add:
                OnZoom(KeyZoomFactor, null, ShowpieceZoomSource.Keyboard);
                return true;
            case Key.OemMinus:
            case Key.Subtract:
                OnZoom(1 / KeyZoomFactor, null, ShowpieceZoomSource.Keyboard);
                return true;
            case Key.D0:
            case Key.NumPad0:
            case Key.Home:
                OnResetView();
                return true;
        }
        return false;
    }

    /// <summary>Summary read by screen readers when <c>AutomationProperties.Name</c> is not set.</summary>
    protected virtual string? GetAccessibleSummary() => null;

    internal string? AccessibleSummary => GetAccessibleSummary();

    /// <summary>Runs the zoom hook as a button would (zoom controls next to the stage).</summary>
    public void ZoomFromButton(double factor) => OnZoom(factor, null, ShowpieceZoomSource.Button);

    /// <summary>Runs the reset hook as a Fit button would.</summary>
    public void ResetView() => OnResetView();

    // Frames -------------------------------------------------------------------------------------------------------------

    private bool OnFrame(double dt)
    {
        // Camera changes redraw through Changed; a camera that only waits (a delayed settle) costs no redraw.
        var cameraMoving = _camera?.Advance(dt) ?? false;
        var sceneMoving = OnAdvance(dt);
        if (sceneMoving)
        {
            InvalidateVisual();
        }
        return cameraMoving || sceneMoving;
    }

    private void OnLoopRunningChanged(bool running)
    {
        if (running)
        {
            return;
        }
        OnLoopStopped();
        InvalidateVisual();
    }

    private void OnCameraChanged()
    {
        InvalidateVisual();
        if (_camera is { IsMoving: true })
        {
            RequestFrames();
        }
    }

    // Lifecycle ----------------------------------------------------------------------------------------------------------

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeVariantChanged;
        _themeDirty = true;
        RefreshMotionLevel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e) => MarkThemeChanged();

    private void OnStageResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        // Theme and accent switches arrive here; so does the theme's motion mode.
        MarkThemeChanged();
        RefreshMotionLevel();
    }

    private void MarkThemeChanged()
    {
        _themeDirty = true;
        _textCache?.Clear();
        _ringPen = null;
        OnThemeChanged();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArcMotion.LevelProperty)
        {
            RefreshMotionLevel();
        }
    }

    private void EnsureMotionLevel()
    {
        if (!_motionKnown)
        {
            _motionLevel = ArcMotion.GetEffectiveLevel(this);
            _motionKnown = true;
        }
    }

    private void RefreshMotionLevel()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_motionKnown && level == _motionLevel)
        {
            return;
        }
        _motionKnown = true;
        _motionLevel = level;
        if (_camera is not null)
        {
            _camera.MotionLevel = level;
        }
        OnMotionLevelChanged(level);
        InvalidateVisual();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        _camera?.SetViewport(e.NewSize);
        InvalidateVisual();
    }

    // Input --------------------------------------------------------------------------------------------------------------

    private Point ToLocal(Point topLevelPoint)
    {
        if (TopLevel.GetTopLevel(this) is { } root && root.TranslatePoint(topLevelPoint, this) is { } local)
        {
            return local;
        }
        return topLevelPoint;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && e.ClickCount >= 2 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var position = e.GetPosition(this);
            OnDoubleTap(position, HitTestItem(position));
        }
        if (!IsFocused)
        {
            Focus(NavigationMethod.Pointer);
        }
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        _pressPosition = ToLocal(e.Start);
        _pressItem = HitTestItem(_pressPosition);
    }

    private void OnDragStarting(object? sender, DragGestureEventArgs e)
    {
        if (!OnDragStarting(_pressPosition, _pressItem))
        {
            e.Cancel = true;
        }
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        PseudoClasses.Set(DraggingPseudoClass, true);
        Cursor = s_dragCursor;
        if (HoverItem is not null)
        {
            HoverItem = null;
            OnHoverChanged(null, null);
        }
        OnDragStarted(_pressPosition);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e) => OnDragMoved(e.Delta, ToLocal(e.Position));

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        EndDragVisuals();
        OnDragCompleted(e.Velocity);
    }

    private void OnDragCancelled(object? sender, DragGestureEventArgs e)
    {
        EndDragVisuals();
        OnDragCancelled();
    }

    private void OnDragReleased(object? sender, DragGestureEventArgs e) => OnTap(_pressPosition, _pressItem, e.KeyModifiers);

    private void EndDragVisuals()
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        Cursor = null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag.IsDragging || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var position = e.GetPosition(this);
        var item = HitTestItem(position);
        HoverItem = item;
        Cursor = item is not null ? s_handCursor : null;
        OnHoverChanged(position, item);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_drag.IsDragging)
        {
            return;
        }
        HoverItem = null;
        Cursor = null;
        OnHoverChanged(null, null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled)
        {
            return;
        }
        var pinch = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var time = e.Timestamp != 0 ? TimeSpan.FromMilliseconds(e.Timestamp) : _clock.Elapsed;
        var device = _wheel.Classify(e.Delta, time);
        var zoom = WheelMode switch
        {
            ShowpieceWheelMode.Zoom => true,
            ShowpieceWheelMode.NotchedZoom => pinch || device == ShowpieceWheelDevice.Notched,
            ShowpieceWheelMode.CtrlZoom => pinch,
            _ => false
        };
        if (!zoom || e.Delta.Y == 0)
        {
            OnWheelScrolled(e);
            return;
        }
        var sensitivity = pinch ? PinchSensitivity : WheelSensitivity;
        OnZoom(ShowpieceWheel.ZoomFactor(e.Delta.Y, sensitivity), e.GetPosition(this),
            pinch ? ShowpieceZoomSource.Pinch : ShowpieceZoomSource.Wheel);
        e.Handled = true;
    }

    private void OnMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (WheelMode == ShowpieceWheelMode.PassThrough)
        {
            return;
        }
        var factor = ShowpieceWheel.MagnifyFactor(e.Delta.X, PinchSensitivity);
        if (Math.Abs(factor - 1) > 1e-9)
        {
            OnZoom(factor, e.GetPosition(this), ShowpieceZoomSource.Pinch);
        }
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (WheelMode == ShowpieceWheelMode.PassThrough || !(e.Scale > 0))
        {
            return;
        }
        // Pinch reports the scale since the gesture began; zoom by the change since the last event.
        var factor = e.Scale / _lastPinchScale;
        _lastPinchScale = e.Scale;
        OnZoom(factor, e.ScaleOrigin, ShowpieceZoomSource.Pinch);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _lastPinchScale = 1;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && OnStageKeyDown(e))
        {
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    // Rendering ----------------------------------------------------------------------------------------------------------

    public sealed override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius;
        var rounded = new RoundedRect(bounds, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        // Fill first: a self-drawn control only hit-tests where it painted.
        context.DrawRectangle(Background ?? Brushes.Transparent, null, rounded);
        using (radius == default ? context.PushClip(bounds) : context.PushClip(rounded))
        {
            RenderStage(context);
        }
        if (IsFocused && Classes.Contains(":focus-visible"))
        {
            DrawFocusRing(context, bounds, radius);
        }
    }

    private void DrawFocusRing(DrawingContext context, Rect bounds, CornerRadius radius)
    {
        if (_themeDirty || _ringPen is null)
        {
            _ringColor = ArcTokens.GetColor(this, ArcDesignTokenKind.FocusRing);
            _ringPen = new Pen(new SolidColorBrush(_ringColor), 2);
            _themeDirty = false;
        }
        // 2px ring, 2px outside the stage: the pen's center line sits 3px out.
        const double outset = 3;
        var rect = bounds.Inflate(outset);
        context.DrawRectangle(null, _ringPen, new RoundedRect(rect, radius.TopLeft + outset, radius.TopRight + outset,
            radius.BottomRight + outset, radius.BottomLeft + outset));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ShowpieceStageAutomationPeer(this);
}

/// <summary>
/// Screen-reader view of a <see cref="ShowpieceStage"/> (Arc's <c>role=application</c> with a role description): a
/// custom control named by <c>AutomationProperties.Name</c> or the stage's summary, described by
/// <c>AutomationProperties.HelpText</c>; overlay children (cards, buttons) stay reachable.
/// </summary>
internal sealed class ShowpieceStageAutomationPeer : ControlAutomationPeer
{
    public ShowpieceStageAutomationPeer(ShowpieceStage owner)
        : base(owner)
    {
    }

    private new ShowpieceStage Owner => (ShowpieceStage)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetLocalizedControlTypeCore() => Owner.RoleDescription ?? "interactive chart";

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Owner.AccessibleSummary;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
