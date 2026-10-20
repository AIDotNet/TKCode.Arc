using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AtomUI.Controls.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Primitives;

/// <summary>
/// S8 modal presence (P11): mounts a panel with a scrim on the window's overlay layer, plays the enter, traps focus
/// and returns it to the opener, dismisses on Escape or a scrim press (both cancellable through
/// <see cref="Dismissing"/>), stacks with other modals (only the topmost answers Escape) and plays the exit before
/// removing the layer. Reopening while the exit plays reverses from wherever the panel is (springs keep velocity),
/// and a leaving layer lets clicks through so the trigger can reopen it.
/// <para>
/// Motion is code-driven (<see cref="MotionValue{T}"/>) rather than ControlTheme transitions because the panel lives in a
/// layer outside any theme, the removal must wait for the exit, a reversed exit must keep spring velocity, and drags and
/// flings hand their velocity to the slide.
/// </para>
/// <para>
/// The panel keeps its logical parent (declare it as a logical child of your control so styles, resources and
/// DataContext flow); when it has none, <see cref="Anchor"/> becomes its logical parent while open.
/// </para>
/// <code>
/// var modal = new ModalPresence(surface, anchor: this) { Entrance = ModalEntrance.Rise, BackdropBlur = 7 };
/// modal.Dismissing += (_, e) =&gt; e.Cancel = HasUnsavedChanges;
/// modal.Open();
/// modal.Close();
/// </code>
/// </summary>
public sealed class ModalPresence : IDisposable
{
    private static int s_zIndex;

    private readonly ModalHost _host;
    private readonly MotionValue<double> _scrim;
    private readonly MotionValue<double> _fade;
    private readonly MotionValue<double> _rise;
    private readonly MotionValue<double> _offset;
    private Panel? _layer;
    private TopLevel? _topLevel;
    private Visual? _anchorInUse;
    private IInputElement? _restoreFocus;
    private bool _restoreFocusVisible;
    private bool _parentedPanel;
    private int _generation;
    private DismissReason _closeReason;
    private double _manualScrim;
    private IBrush? _scrimBrush;
    private ModalEntrance _activeEntrance;

    /// <param name="panel">The surface to show. It is aligned inside the window by its own alignment and margin.</param>
    /// <param name="anchor">Visual that locates the window (and the motion level); defaults to the panel's logical parent.</param>
    public ModalPresence(Control panel, Visual? anchor = null)
    {
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        Anchor = anchor;
        _host = new ModalHost(this);
        _host.PanelHostCore.Panel = panel;
        _scrim = new MotionValue<double>(_host, 0, MotionVectors.Double);
        _fade = new MotionValue<double>(_host, 1, MotionVectors.Double);
        _rise = new MotionValue<double>(_host, 1, MotionVectors.Double);
        _offset = new MotionValue<double>(_host, 0, MotionVectors.Double);
        _scrim.Changed += _ => ApplyScrim();
        _fade.Changed += _ => ApplyPanel();
        _rise.Changed += _ => ApplyPanel();
        _offset.Changed += _ => ApplyPanel();
    }

    #region Configuration

    /// <summary>The surface shown above the scrim.</summary>
    public Control Panel { get; }

    /// <summary>Locates the window and the inherited motion level; defaults to the panel's logical parent.</summary>
    public Visual? Anchor { get; set; }

    public ModalEntrance Entrance { get; set; } = ModalEntrance.Rise;

    /// <summary>Edge for <see cref="ModalEntrance.Slide"/> (and the travel axis of <see cref="PanelOffset"/>).</summary>
    public ModalSide Side { get; set; } = ModalSide.Right;

    /// <summary>Scrim brush; null uses the ArcDesign <c>Scrim</c> token.</summary>
    public IBrush? ScrimBrush
    {
        get => _scrimBrush;
        set
        {
            _scrimBrush = value;
            if (value is null)
            {
                _host.Scrim.ClearValue(Border.BackgroundProperty);
            }
            else
            {
                _host.Scrim.Background = value;
            }
        }
    }

    /// <summary>
    /// Backdrop blur radius in px applied to the window content while the scrim is shown (Arc: dialog 7, drawer 4;
    /// tokens <c>BackdropBlurDialog</c> / <c>BackdropBlurDrawer</c>). Ignored when the <c>ReduceTransparency</c>
    /// token is on, and inside a scoped overlay panel.
    /// </summary>
    public double BackdropBlur { get; set; }

    /// <summary>Escape requests dismissal (default true). Only the topmost modal answers.</summary>
    public bool DismissOnEscape { get; set; } = true;

    /// <summary>A press on the scrim requests dismissal (default true).</summary>
    public bool DismissOnScrimPress { get; set; } = true;

    /// <summary>Keeps keyboard focus inside the panel while this is the topmost modal (default true).</summary>
    public bool TrapFocus { get; set; } = true;

    /// <summary>Returns focus to the element focused before opening (default true).</summary>
    public bool RestoreFocus { get; set; } = true;

    /// <summary>Element focused on open; null focuses the first focusable element of the panel (or the panel host).</summary>
    public IInputElement? InitialFocus { get; set; }

    /// <summary>
    /// Logical root whose descendants count as inside the modal for the focus trap (popups opened from its content, for
    /// example). Defaults to the panel; a declarative owner such as a dialog control sets itself.
    /// </summary>
    public StyledElement? ContainmentRoot { get; set; }

    /// <summary>
    /// For <see cref="ModalEntrance.Custom"/>: plays the panel's enter (<c>true</c>) or exit (<c>false</c>) and returns
    /// a task that completes with true when it arrived (false when interrupted). The exit task gates removal.
    /// </summary>
    public Func<ModalPresence, bool, Task<bool>>? CustomMotion { get; set; }

    #endregion

    #region State

    public ModalState State { get; private set; }

    /// <summary>Opening or open (a leaving modal is not open).</summary>
    public bool IsOpen => State is ModalState.Opening or ModalState.Open;

    /// <summary>The mounted host (scrim + panel host); null before the first open.</summary>
    public ModalHost Host => _host;

    /// <summary>Why the last close happened.</summary>
    public DismissReason CloseReason => _closeReason;

    /// <summary>True when this is the topmost open modal of its window.</summary>
    public bool IsTopmost => _topLevel is { } top && ModalStack.Find(top)?.Topmost == this;

    /// <summary>Size of the overlay layer the modal is mounted on (the viewport for sizing rules).</summary>
    public Size LayerSize => _host.LayerSize;

    /// <summary>Raised when the layer size changes while mounted (window resize).</summary>
    public event EventHandler? LayerSizeChanged;

    /// <summary>Escape, scrim press, close button or gesture asked to close; cancel to stay open.</summary>
    public event EventHandler<ModalDismissingEventArgs>? Dismissing;

    /// <summary>The modal mounted and started entering.</summary>
    public event EventHandler? Opened;

    /// <summary>The exit finished and the layer was removed.</summary>
    public event EventHandler<ModalClosedEventArgs>? Closed;

    /// <summary><see cref="State"/> changed.</summary>
    public event EventHandler? StateChanged;

    /// <summary>The modals mounted on <paramref name="topLevel"/>, bottom to top (leaving ones included).</summary>
    public static IReadOnlyList<ModalPresence> GetModals(TopLevel topLevel)
        => ModalStack.Find(topLevel)?.Items ?? Array.Empty<ModalPresence>();

    #endregion

    #region Scrim and panel motion values

    /// <summary>
    /// Scrim opacity 0–1. Driven by the presence for built-in entrances; for <see cref="ModalEntrance.Custom"/> the owner
    /// sets it (for example as a function of the panel position, like Arc's bottom sheet).
    /// </summary>
    public double ScrimOpacity
    {
        get => _activeEntrance == ModalEntrance.Custom ? _manualScrim : _scrim.Value;
        set
        {
            _manualScrim = Math.Clamp(value, 0, 1);
            if (_activeEntrance == ModalEntrance.Custom || Entrance == ModalEntrance.Custom)
            {
                ApplyScrim();
            }
        }
    }

    /// <summary>Panel size along the <see cref="Side"/> axis (px).</summary>
    public double PanelExtent => Side is ModalSide.Left or ModalSide.Right ? Panel.Bounds.Width : Panel.Bounds.Height;

    /// <summary>Slide offset toward <see cref="Side"/> in px (0 = resting, <see cref="PanelExtent"/> = off-screen).</summary>
    public double PanelOffset => _offset.Value;

    /// <summary>Velocity of the slide offset (px/s).</summary>
    public double PanelVelocity => _offset.Velocity;

    /// <summary>Moves the panel along its slide axis immediately (a drag), stopping any running slide motion.</summary>
    public void SetPanelOffset(double offset) => _offset.Jump(offset);

    /// <summary>Animates the slide offset back to rest (or <paramref name="target"/>), starting with <paramref name="velocity"/> px/s.</summary>
    public Task<bool> SettlePanelAsync(MotionSpec spec, double velocity = 0, double target = 0)
        => _offset.AnimateTo(target, spec, velocity);

    #endregion

    #region Open / close

    /// <summary>
    /// Mounts and enters (or reverses a running exit). Returns false when no window can host it yet (the anchor is not
    /// attached); call again once it is.
    /// </summary>
    public bool Open()
    {
        if (IsOpen)
        {
            return true;
        }
        var anchor = Anchor ?? Panel.Parent as Visual;
        if (anchor is null || TopLevel.GetTopLevel(anchor) is not { } topLevel)
        {
            return false;
        }
        if (State == ModalState.Closing && _layer is not null && ReferenceEquals(_topLevel, topLevel))
        {
            // Caught mid-exit: reverse from the current values.
            _generation++;
            SetState(ModalState.Opening);
            _host.IsHitTestVisible = true;
            _host.ZIndex = ++s_zIndex;
            ModalStack.For(topLevel).Add(this);
            CaptureFocusOrigin(topLevel);
            _ = RunEnter(fresh: false);
            MoveFocusIn();
            Opened?.Invoke(this, EventArgs.Empty);
            return true;
        }
        if (State != ModalState.Closed)
        {
            Unmount(raise: false);
        }
        var layer = ResolveLayer(anchor);
        if (layer is null)
        {
            return false;
        }
        _generation++;
        _layer = layer;
        _topLevel = topLevel;
        _anchorInUse = anchor;
        _activeEntrance = Entrance;
        CaptureFocusOrigin(topLevel);
        if (Panel.Parent is null && anchor is StyledElement logicalAnchor)
        {
            ((ISetLogicalParent)Panel).SetParent(logicalAnchor);
            _parentedPanel = true;
        }
        _host.SetValue(ArcMotion.LevelProperty, ArcMotion.GetLevel(anchor));
        _host.IsHitTestVisible = true;
        _host.ZIndex = ++s_zIndex;
        UpdateLayerSize();
        layer.Children.Add(_host);
        layer.SizeChanged += OnLayerSizeChanged;
        anchor.DetachedFromVisualTree += OnAnchorDetached;
        // Lay out now so the first frame (and the slide distance) uses the real panel size.
        var size = _host.LayerSize;
        _host.Measure(size);
        _host.Arrange(new Rect(size));
        SetState(ModalState.Opening);
        ModalStack.For(topLevel).Add(this);
        _ = RunEnter(fresh: true);
        MoveFocusIn();
        Opened?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Asks to close for <paramref name="reason"/>: raises <see cref="Dismissing"/> and closes unless cancelled.
    /// <paramref name="velocity"/> (px/s toward <see cref="Side"/>) makes a slide exit keep a fling's speed.
    /// </summary>
    public bool Dismiss(DismissReason reason, double velocity = 0)
    {
        if (!IsOpen)
        {
            return false;
        }
        var args = new ModalDismissingEventArgs(reason);
        Dismissing?.Invoke(this, args);
        if (args.Cancel)
        {
            return false;
        }
        Close(reason, velocity);
        return true;
    }

    /// <summary>Plays the exit and removes the layer (no <see cref="Dismissing"/> event).</summary>
    public void Close(DismissReason reason = DismissReason.Programmatic, double velocity = 0)
    {
        if (!IsOpen)
        {
            return;
        }
        _generation++;
        _closeReason = reason;
        SetState(ModalState.Closing);
        // A leaving layer lets clicks through, so the trigger can reopen it mid-exit.
        _host.IsHitTestVisible = false;
        ReturnFocus(reason);
        _ = RunExit(velocity);
    }

    internal void OnScrimPressed()
    {
        if (DismissOnScrimPress && IsTopmost)
        {
            Dismiss(DismissReason.PressOutside);
        }
    }

    private void SetState(ModalState state)
    {
        if (State == state)
        {
            return;
        }
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RunEnter(bool fresh)
    {
        var generation = _generation;
        var level = ArcMotion.GetEffectiveLevel(_host);
        var full = level == ArcMotionLevel.Full;
        var reduced = level == ArcMotionLevel.Reduced;
        var fadeIn = reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter);
        var scrimIn = reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
        Task<bool> panel;
        Task<bool> scrim = Task.FromResult(true);
        switch (_activeEntrance)
        {
            case ModalEntrance.Custom:
                if (fresh)
                {
                    _fade.Jump(1);
                    _rise.Jump(1);
                    _offset.Jump(0);
                }
                panel = CustomMotion?.Invoke(this, true) ?? Task.FromResult(true);
                break;
            case ModalEntrance.Slide:
            {
                if (fresh)
                {
                    _rise.Jump(1);
                    _offset.Jump(full ? PanelExtent : 0);
                    _fade.Jump(full ? 1 : 0);
                    _scrim.Jump(0);
                }
                var slide = full ? _offset.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth)) : Jump(_offset, 0);
                var fade = _fade.AnimateTo(1, fadeIn);
                scrim = _scrim.AnimateTo(1, scrimIn);
                panel = Both(slide, fade);
                break;
            }
            default:
            {
                if (fresh)
                {
                    _offset.Jump(0);
                    _fade.Jump(0);
                    _rise.Jump(full && _activeEntrance == ModalEntrance.Rise ? 0 : 1);
                    _scrim.Jump(0);
                }
                var rise = _activeEntrance == ModalEntrance.Rise ? _rise.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth)) : Jump(_rise, 1);
                var fade = _fade.AnimateTo(1, fadeIn);
                scrim = _scrim.AnimateTo(1, scrimIn);
                panel = Both(rise, fade);
                break;
            }
        }
        ApplyPanel();
        ApplyScrim();
        var arrived = await Both(panel, scrim);
        if (arrived && generation == _generation && State == ModalState.Opening)
        {
            SetState(ModalState.Open);
        }
    }

    private async Task RunExit(double velocity)
    {
        var generation = _generation;
        var level = ArcMotion.GetEffectiveLevel(_host);
        var full = level == ArcMotionLevel.Full;
        var reduced = level == ArcMotionLevel.Reduced;
        var quick = reduced
            ? MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        Task<bool> panel;
        var scrim = _activeEntrance == ModalEntrance.Custom ? Task.FromResult(true) : _scrim.AnimateTo(0, quick);
        switch (_activeEntrance)
        {
            case ModalEntrance.Custom:
                panel = CustomMotion?.Invoke(this, false) ?? Task.FromResult(true);
                break;
            case ModalEntrance.Slide when full:
            {
                // A click or Escape returns the panel with a standard tween; a drag or fling keeps its velocity in a spring of
                // the same length. The shadow fades over the last 35% so it leaves with the panel.
                var extent = Math.Max(PanelExtent, 1);
                var slide = velocity > 0 || _closeReason == DismissReason.Gesture
                    ? _offset.AnimateTo(extent, MotionSpec.Spring(SpringSpec.FromVisual(ArcMotionPresets.Standard.TotalSeconds, 0)), Math.Max(velocity, 0))
                    : _offset.AnimateTo(extent, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
                var lateFade = MotionSpec.Tween(TimeSpan.FromMilliseconds(ArcMotionPresets.Standard.TotalMilliseconds * 0.35),
                    s_linear, TimeSpan.FromMilliseconds(ArcMotionPresets.Standard.TotalMilliseconds * 0.65));
                panel = Both(slide, _fade.AnimateTo(0, lateFade));
                break;
            }
            case ModalEntrance.Rise when full:
                panel = Both(_rise.AnimateTo(0.5, quick), _fade.AnimateTo(0, quick));
                break;
            default:
                panel = _fade.AnimateTo(0, quick);
                break;
        }
        var done = await Both(panel, scrim);
        if (done && generation == _generation && State == ModalState.Closing)
        {
            Unmount(raise: true);
        }
    }

    private static readonly Easing s_linear = new LinearEasing();

    private static Task<bool> Jump(MotionValue<double> value, double target)
    {
        value.Jump(target);
        return Task.FromResult(true);
    }

    private static async Task<bool> Both(Task<bool> a, Task<bool> b)
    {
        var first = await a;
        var second = await b;
        return first && second;
    }

    private void Unmount(bool raise)
    {
        var reason = _closeReason;
        if (_layer is { } layer)
        {
            layer.SizeChanged -= OnLayerSizeChanged;
            layer.Children.Remove(_host);
        }
        if (_anchorInUse is { } anchor)
        {
            anchor.DetachedFromVisualTree -= OnAnchorDetached;
        }
        if (_topLevel is { } topLevel)
        {
            ModalStack.Find(topLevel)?.Remove(this);
        }
        if (_parentedPanel)
        {
            _parentedPanel = false;
            ((ISetLogicalParent)Panel).SetParent(null);
        }
        _layer = null;
        _topLevel = null;
        _anchorInUse = null;
        _restoreFocus = null;
        _generation++;
        SetState(ModalState.Closed);
        if (raise)
        {
            Closed?.Invoke(this, new ModalClosedEventArgs(reason));
        }
    }

    private void OnAnchorDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (State == ModalState.Closed)
        {
            return;
        }
        // The owner left the window (page navigated away): remove at once.
        if (IsOpen)
        {
            _closeReason = DismissReason.Programmatic;
        }
        Unmount(raise: true);
    }

    #endregion

    #region Layer

    private static Panel? ResolveLayer(Visual anchor)
    {
        if (ScopeAwareOverlayLayer.GetLayer(anchor) is { } scoped && scoped.GetVisualParent() is not null)
        {
            return scoped;
        }
        return OverlayLayer.GetOverlayLayer(anchor);
    }

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayerSize();
        LayerSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLayerSize()
    {
        var size = _layer?.Bounds.Size ?? default;
        if ((size.Width <= 0 || size.Height <= 0) && _layer is ScopeAwareOverlayLayer { LayerHost: Visual host })
        {
            size = host.Bounds.Size;
        }
        if ((size.Width <= 0 || size.Height <= 0) && _topLevel is not null)
        {
            size = _topLevel.ClientSize;
        }
        _host.LayerSize = size;
        _host.Width = size.Width;
        _host.Height = size.Height;
        Canvas.SetLeft(_host, 0);
        Canvas.SetTop(_host, 0);
    }

    #endregion

    #region Visual application

    /// <summary>Current backdrop blur radius (blur × scrim progress; zero under ReduceTransparency).</summary>
    internal double CurrentBackdropBlur
    {
        get
        {
            if (BackdropBlur <= 0 || _layer is null || BlurTarget is null)
            {
                return 0;
            }
            if (_anchorInUse is Control control && control.TryFindResource(ArcDesignTokenKind.ReduceTransparency, out var reduce) && reduce is true)
            {
                return 0;
            }
            return BackdropBlur * Math.Clamp(ScrimOpacity, 0, 1);
        }
    }

    /// <summary>Window content under the overlay layer (null inside a scoped overlay panel).</summary>
    internal Visual? BlurTarget => _layer is ScopeAwareOverlayLayer { LayerHost: ScopeAwareOverlayLayerPanel }
        ? null
        : _layer is null ? null : ModalStack.FindBlurTarget(_layer);

    private void ApplyScrim()
    {
        var opacity = _activeEntrance == ModalEntrance.Custom ? _manualScrim : _scrim.Value;
        _host.Scrim.Opacity = Math.Clamp(opacity, 0, 1);
        if (_topLevel is { } topLevel)
        {
            ModalStack.Find(topLevel)?.UpdateBlur();
        }
    }

    private void ApplyPanel()
    {
        var host = _host.PanelHostCore;
        host.Opacity = Math.Clamp(_fade.Value, 0, 1);
        switch (_activeEntrance)
        {
            case ModalEntrance.Rise:
            {
                // Dialog: y 8 → 0 and scale 0.96 → 1 (exit stops at half: y 4, scale 0.98).
                var p = _rise.Value;
                host.SetTransform(new Vector(0, 8 * (1 - p)), 0.96 + 0.04 * p);
                break;
            }
            case ModalEntrance.Slide:
            {
                var d = _offset.Value;
                var offset = Side switch
                {
                    ModalSide.Left => new Vector(-d, 0),
                    ModalSide.Top => new Vector(0, -d),
                    ModalSide.Bottom => new Vector(0, d),
                    _ => new Vector(d, 0)
                };
                host.SetTransform(offset, 1);
                break;
            }
            default:
                host.SetTransform(default, 1);
                break;
        }
    }

    #endregion

    #region Focus

    /// <summary>True when <paramref name="element"/> is inside this modal (panel or popups opened from it).</summary>
    public bool Contains(IInputElement element)
        => ModalStack.IsWithin(_host, element) || ModalStack.IsWithin(Panel, element) ||
           (ContainmentRoot is Visual root && ModalStack.IsWithin(root, element));

    /// <summary>The element initial focus (and the focus trap) sends focus to.</summary>
    public IInputElement? FindFocusTarget()
    {
        if (InitialFocus is { } initial && initial is Visual { IsEffectivelyVisible: true })
        {
            return initial;
        }
        return FocusManager.FindFirstFocusableElement(Panel) ?? (Panel.Focusable ? Panel : _host.PanelHostCore);
    }

    private void CaptureFocusOrigin(TopLevel topLevel)
    {
        var focused = topLevel.FocusManager?.GetFocusedElement();
        if (focused is not null && Contains(focused))
        {
            return;
        }
        _restoreFocus = focused;
        _restoreFocusVisible = focused is StyledElement styled && styled.Classes.Contains(":focus-visible");
    }

    private void MoveFocusIn()
    {
        if (FindFocusTarget() is InputElement target)
        {
            target.Focus(_restoreFocusVisible ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
    }

    private void ReturnFocus(DismissReason reason)
    {
        if (!RestoreFocus || _restoreFocus is not InputElement target || _topLevel is not { } topLevel)
        {
            return;
        }
        var current = topLevel.FocusManager?.GetFocusedElement();
        if (current is not null && !Contains(current))
        {
            return;
        }
        if (TopLevel.GetTopLevel(target) is not null && target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
        {
            var keyboard = reason == DismissReason.Escape || _restoreFocusVisible;
            target.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Unspecified);
        }
    }

    #endregion

    /// <summary>Removes the modal at once (no exit) and releases its resources.</summary>
    public void Dispose()
    {
        if (State != ModalState.Closed)
        {
            Unmount(raise: false);
        }
        _scrim.Dispose();
        _fade.Dispose();
        _rise.Dispose();
        _offset.Dispose();
    }
}
