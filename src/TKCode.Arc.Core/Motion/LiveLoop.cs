using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Motion;

/// <summary>
/// P21 continuous loop: a per-frame callback on the shared <see cref="MotionScheduler"/> frame batch (skeleton pulse,
/// shimmer sweep, live dot, a timeline's frame reports). The owner says whether it wants the loop with
/// <see cref="Start"/> and <see cref="Stop"/>; the loop itself runs only while the owner is attached, effectively
/// visible, inside the viewport (when <see cref="TracksViewport"/>) and at a motion level of at least
/// <see cref="MinimumLevel"/>. It pauses on its own as soon as one of these stops holding and resumes when they all
/// hold again, so the owner keeps its phase. A running loop costs one delegate call per frame and allocates nothing;
/// a paused loop requests no frames.
/// </summary>
public sealed class LiveLoop : IMotionDriver, IDisposable
{
    private readonly Visual _owner;
    private readonly Func<double, bool> _step;
    private ArcMotionLevel _minimumLevel = ArcMotionLevel.Full;
    private bool _tracksViewport;
    private double _viewportMargin;
    private bool _active;
    private bool _running;
    private bool _inViewport = true;
    private bool _waitingForLayout;
    private bool _viewportHooked;
    private bool _attached;
    private bool _disposed;

    /// <param name="owner">The visual whose attachment, visibility, viewport and motion level gate the loop.</param>
    /// <param name="step">
    /// Called once per frame with the elapsed seconds while the loop runs. Return false when the loop has finished
    /// (it then stops as if <see cref="Stop"/> was called).
    /// </param>
    public LiveLoop(Visual owner, Func<double, bool> step)
    {
        _owner = owner;
        _step = step;
        // Walking up to the TopLevel on every gate check adds up (every Button owns a spinner), so attachment is tracked.
        _attached = owner.IsAttachedToVisualTree();
        owner.AttachedToVisualTree += OnAttached;
        owner.DetachedFromVisualTree += OnDetached;
        owner.PropertyChanged += OnOwnerPropertyChanged;
        if (owner is StyledElement styled)
        {
            styled.ResourcesChanged += OnResourcesChanged;
        }
        TracksViewport = true;
    }

    /// <summary>Raised when frames start (<c>true</c>) or stop (<c>false</c>): draw the resting frame here.</summary>
    public event Action<bool>? RunningChanged;

    /// <summary>
    /// The lowest motion level at which the loop runs: <see cref="ArcMotionLevel.Full"/> (default) for decoration,
    /// <see cref="ArcMotionLevel.Reduced"/> to keep running under reduced motion, <see cref="ArcMotionLevel.Off"/>
    /// for loops that report function (a timeline) rather than decoration.
    /// </summary>
    public ArcMotionLevel MinimumLevel
    {
        get => _minimumLevel;
        set
        {
            _minimumLevel = value;
            Refresh();
        }
    }

    /// <summary>Pauses the loop while the owner is scrolled out of view (default true; needs a <see cref="Layoutable"/> owner).</summary>
    public bool TracksViewport
    {
        get => _tracksViewport;
        set
        {
            if (_tracksViewport == value)
            {
                return;
            }
            _tracksViewport = value;
            _inViewport = true;
            Refresh();
        }
    }

    /// <summary>How far outside the viewport the owner still counts as in view (px; a sweep that starts off-edge).</summary>
    public double ViewportMargin
    {
        get => _viewportMargin;
        set => _viewportMargin = Math.Max(0, value);
    }

    /// <summary>The owner wants the loop: started and neither stopped nor finished (it may be paused by a gate).</summary>
    public bool IsActive => _active;

    /// <summary>Frames are being delivered.</summary>
    public bool IsRunning => _running;

    /// <summary>All gates hold: attached, effectively visible, in view and at <see cref="MinimumLevel"/> or above.</summary>
    public bool CanRun
    {
        get
        {
            if (_disposed || !_attached || !_owner.IsVisible || !_owner.IsEffectivelyVisible)
            {
                return false;
            }
            if (_tracksViewport && !_inViewport)
            {
                return false;
            }
            return ArcMotion.GetEffectiveLevel(_owner) >= _minimumLevel;
        }
    }

    Visual? IMotionDriver.Owner => _owner;

    /// <summary>Asks for the loop; frames start as soon as every gate holds. Idempotent.</summary>
    public void Start()
    {
        // Already delivering frames: the gates were just checked, and re-reading them (resource lookups) per call adds up
        // for callers that start the loop on every pointer move.
        if (_active && _running)
        {
            return;
        }
        _active = true;
        Refresh();
    }

    /// <summary>Withdraws the request; frames stop at once.</summary>
    public void Stop()
    {
        _active = false;
        Refresh();
    }

    /// <summary>
    /// Re-evaluates the gates. The loop already does this when the owner is attached or detached, shown or hidden,
    /// scrolled in or out of view, or its motion level changes; call it after changing anything else the gates read.
    /// </summary>
    public void Refresh()
    {
        HookViewport(_tracksViewport && _active && !_disposed && _owner.IsVisible);
        var run = _active && CanRun;
        // Hidden by an ancestor: nothing on the owner says when that ends, but showing it always runs a layout pass.
        // The owner's own IsVisible is watched through PropertyChanged, so it never needs the window-wide layout event.
        WaitForLayout(_active && !run && !_disposed && _attached && _owner.IsVisible && !_owner.IsEffectivelyVisible);
        if (run == _running)
        {
            return;
        }
        if (run && !MotionScheduler.Register(this, _owner))
        {
            return;
        }
        SetRunning(run);
    }

    // Every viewport listener costs an ancestor walk on each layout pass of the window, so a loop that is not asked for
    // (or whose owner is hidden) listens to nothing. Avalonia reports the viewport again on the first pass after hooking.
    private void HookViewport(bool hook)
    {
        if (hook == _viewportHooked || _owner is not Layoutable layoutable)
        {
            return;
        }
        _viewportHooked = hook;
        if (hook)
        {
            layoutable.EffectiveViewportChanged += OnViewportChanged;
        }
        else
        {
            layoutable.EffectiveViewportChanged -= OnViewportChanged;
            _inViewport = true;
        }
    }

    private void WaitForLayout(bool wait)
    {
        if (wait == _waitingForLayout || _owner is not Layoutable layoutable)
        {
            return;
        }
        _waitingForLayout = wait;
        if (wait)
        {
            layoutable.LayoutUpdated += OnLayoutUpdated;
        }
        else
        {
            layoutable.LayoutUpdated -= OnLayoutUpdated;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Refresh();

    private void SetRunning(bool running)
    {
        if (_running == running)
        {
            return;
        }
        _running = running;
        RunningChanged?.Invoke(running);
    }

    bool IMotionDriver.Advance(double dt)
    {
        if (!_running)
        {
            return false;
        }
        if (!_step(dt))
        {
            _active = false;
            SetRunning(false);
            return false;
        }
        // The step may have stopped the loop itself.
        return _running;
    }

    // The scheduler found the owner detached or hidden: pause until it is attached or shown again.
    // It is being removed from the batch here, so it must not register again from inside this call.
    void IMotionDriver.Snap()
    {
        SetRunning(false);
        WaitForLayout(_active && !_disposed && _attached && _owner.IsVisible && !_owner.IsEffectivelyVisible);
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e) => AttachmentChanged(true);

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e) => AttachmentChanged(false);

    private void AttachmentChanged(bool attached)
    {
        _attached = attached;
        // A detached owner learns its viewport again after the next layout; until then it counts as in view.
        _inViewport = true;
        Refresh();
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => Refresh();

    private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty || e.Property == ArcMotion.LevelProperty || e.Property == ArcMotion.TokenLevelProperty)
        {
            Refresh();
        }
    }

    private void OnViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var size = _owner.Bounds.Size;
        var inView = size.Width <= 0 || size.Height <= 0 || e.EffectiveViewport.Intersects(new Rect(size).Inflate(_viewportMargin));
        if (inView == _inViewport)
        {
            return;
        }
        _inViewport = inView;
        Refresh();
    }

    /// <summary>Stops the loop and lets go of the owner's events.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _active = false;
        SetRunning(false);
        WaitForLayout(false);
        HookViewport(false);
        _disposed = true;
        _owner.AttachedToVisualTree -= OnAttached;
        _owner.DetachedFromVisualTree -= OnDetached;
        _owner.PropertyChanged -= OnOwnerPropertyChanged;
        if (_owner is StyledElement styled)
        {
            styled.ResourcesChanged -= OnResourcesChanged;
        }
    }
}
