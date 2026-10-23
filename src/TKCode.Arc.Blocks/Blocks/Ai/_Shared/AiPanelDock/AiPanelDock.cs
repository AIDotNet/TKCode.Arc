using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// Docks the assistant beside a page (spec 3.1, 3.10, 5.1, 5.9, 7): <see cref="Page"/> on the left, <see cref="Panel"/> on
/// the right behind a 1px divider. <see cref="IsOpen"/> (two-way) moves the docked width between <see cref="PanelWidth"/>
/// and 0 on the <see cref="ArcSpring.Settle"/> spring (Arc: critically damped, ω ≈ 15.5); the panel keeps its width and
/// slides with the dock's edge, clipped, while the page column follows (its article re-centers). Narrower than
/// <see cref="SheetBreakpoint"/> the panel is a bottom sheet instead (<see cref="IsSheetMode"/>, <c>:sheet</c>):
/// <see cref="SheetHeightRatio"/> of the height, top corners 20, SurfaceRaised with the floating shadow, sliding up on the
/// same spring over a SurfaceMuted 72% scrim that fades in 200ms (ease-out); the page under an open sheet is inert, and a
/// press on the scrim closes it. Closed, a pill at the page's top right ("✳ <see cref="ReopenText"/>") reopens it.
/// <para>
/// Closing: the panel is inert at once (no pointer, no Tab, and hidden from automation once it is out of sight); Escape in
/// the panel, a scrim press and <see cref="CloseRequestedEvent"/> (the host's close button raises it) close it and move the
/// focus to the reopen pill. Reopening from the pill raises <see cref="ReopenRequested"/> and
/// <see cref="FocusPanelRequested"/> (the owner focuses its box; unhandled, the first focusable element of the panel gets
/// it). Reduced motion moves the dock and the sheet at once (the sheet fades in 160ms) and keeps the fades; Off changes at
/// once. The open/closed state given before the dock is in a window shows without motion.
/// </para>
/// <para>
/// Code-driven (<see cref="MotionValue{T}"/>): the dock width must drive layout every frame on a spring that keeps its
/// velocity when reversed, and the sheet's travel depends on the measured height; a ControlTheme transition can do neither.
/// </para>
/// </summary>
[TemplatePart(LayoutPart, typeof(AiPanelDockLayout))]
[TemplatePart(PagePart, typeof(ContentPresenter))]
[TemplatePart(ReopenPart, typeof(AiPanelReopenButton))]
[TemplatePart(ScrimPart, typeof(Border))]
[TemplatePart(PanelHostPart, typeof(Border))]
[PseudoClasses(SheetPseudoClass, OpenPseudoClass)]
internal sealed class AiPanelDock : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string PagePart = "PART_Page";
    public const string ReopenPart = "PART_Reopen";
    public const string ScrimPart = "PART_Scrim";
    public const string PanelHostPart = "PART_PanelHost";
    public const string SheetPseudoClass = ":sheet";
    public const string OpenPseudoClass = ":open";

    /// <summary>The scrim's opacity over SurfaceMuted when the sheet is open.</summary>
    public const double ScrimAlpha = 0.72;

    /// <summary>How far past its own height a closed sheet sits (its shadow leaves the view too).</summary>
    public const double SheetClosedExtra = 25;

    /// <summary>The scrim's fade.</summary>
    public static readonly TimeSpan ScrimDuration = TimeSpan.FromMilliseconds(200);

    private static readonly Easing s_easeOut = new SplineEasing(0, 0, 0.58, 1);

    #region Properties

    /// <summary>The page (left, or under the sheet).</summary>
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<AiPanelDock, object?>(nameof(Page));

    /// <summary>The assistant panel (right, or in the sheet).</summary>
    public static readonly StyledProperty<object?> PanelProperty =
        AvaloniaProperty.Register<AiPanelDock, object?>(nameof(Panel));

    /// <summary>Whether the assistant is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<AiPanelDock, bool>(nameof(IsOpen), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The docked width of the panel.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<AiPanelDock, double>(nameof(PanelWidth), 360);

    /// <summary>Below this width of the dock the panel is a bottom sheet.</summary>
    public static readonly StyledProperty<double> SheetBreakpointProperty =
        AvaloniaProperty.Register<AiPanelDock, double>(nameof(SheetBreakpoint), 640);

    /// <summary>The sheet's height as a share of the dock's height.</summary>
    public static readonly StyledProperty<double> SheetHeightRatioProperty =
        AvaloniaProperty.Register<AiPanelDock, double>(nameof(SheetHeightRatio), 0.74);

    /// <summary>The assistant's mark on the reopen pill; null draws the default spark.</summary>
    public static readonly StyledProperty<object?> AssistantMarkProperty =
        AvaloniaProperty.Register<AiPanelDock, object?>(nameof(AssistantMark));

    /// <summary>The reopen pill's text (the assistant's name).</summary>
    public static readonly StyledProperty<string?> ReopenTextProperty =
        AvaloniaProperty.Register<AiPanelDock, string?>(nameof(ReopenText));

    /// <summary>The accessible name of the panel (a complementary landmark).</summary>
    public static readonly StyledProperty<string?> PanelNameProperty =
        AvaloniaProperty.Register<AiPanelDock, string?>(nameof(PanelName));

    /// <summary>Whether the panel is a bottom sheet because the dock is narrower than <see cref="SheetBreakpoint"/> (read-only).</summary>
    public static readonly DirectProperty<AiPanelDock, bool> IsSheetModeProperty =
        AvaloniaProperty.RegisterDirect<AiPanelDock, bool>(nameof(IsSheetMode), o => o.IsSheetMode);

    /// <summary>Raised (bubbling) after the reopen pill opened the panel.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ReopenRequestedEvent =
        RoutedEvent.Register<AiPanelDock, RoutedEventArgs>(nameof(ReopenRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Asks the dock to close (bubbling): the host's close button raises it from inside the panel; Escape in the panel and a
    /// press on the scrim raise it too. The dock closes and moves the focus to the reopen pill.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<AiPanelDock, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) after the pill opened the panel: put the focus in the box and mark the event handled.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> FocusPanelRequestedEvent =
        RoutedEvent.Register<AiPanelDock, RoutedEventArgs>(nameof(FocusPanelRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PageProperty"/>
    public object? Page { get => GetValue(PageProperty); set => SetValue(PageProperty, value); }

    /// <inheritdoc cref="PanelProperty"/>
    public object? Panel { get => GetValue(PanelProperty); set => SetValue(PanelProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="PanelWidthProperty"/>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    /// <inheritdoc cref="SheetBreakpointProperty"/>
    public double SheetBreakpoint { get => GetValue(SheetBreakpointProperty); set => SetValue(SheetBreakpointProperty, value); }

    /// <inheritdoc cref="SheetHeightRatioProperty"/>
    public double SheetHeightRatio { get => GetValue(SheetHeightRatioProperty); set => SetValue(SheetHeightRatioProperty, value); }

    /// <inheritdoc cref="AssistantMarkProperty"/>
    public object? AssistantMark { get => GetValue(AssistantMarkProperty); set => SetValue(AssistantMarkProperty, value); }

    /// <inheritdoc cref="ReopenTextProperty"/>
    public string? ReopenText { get => GetValue(ReopenTextProperty); set => SetValue(ReopenTextProperty, value); }

    /// <inheritdoc cref="PanelNameProperty"/>
    public string? PanelName { get => GetValue(PanelNameProperty); set => SetValue(PanelNameProperty, value); }

    /// <inheritdoc cref="IsSheetModeProperty"/>
    public bool IsSheetMode { get => _isSheetMode; private set => SetAndRaise(IsSheetModeProperty, ref _isSheetMode, value); }

    /// <inheritdoc cref="ReopenRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ReopenRequested
    {
        add => AddHandler(ReopenRequestedEvent, value);
        remove => RemoveHandler(ReopenRequestedEvent, value);
    }

    /// <inheritdoc cref="CloseRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    /// <inheritdoc cref="FocusPanelRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? FocusPanelRequested
    {
        add => AddHandler(FocusPanelRequestedEvent, value);
        remove => RemoveHandler(FocusPanelRequestedEvent, value);
    }

    #endregion

    private bool _isSheetMode;
    private AiPanelDockLayout? _layout;
    private ContentPresenter? _page;
    private AiPanelReopenButton? _reopen;
    private Border? _scrim;
    private Border? _host;
    private AiFadePresence? _reopenPresence;
    private readonly TranslateTransform _sheetShift = new();
    private MotionValue<double>? _dock;
    private MotionValue<double>? _sheet;
    private MotionValue<double>? _scrimProgress;
    private MotionValue<double>? _sheetFade;
    private int _generation;
    private bool _keyboard;

    public AiPanelDock()
    {
        AddHandler(CloseRequestedEvent, OnCloseRequested);
        // Which input came last decides whether the focus that moves to the pill shows its ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboard = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The docked width on screen.</summary>
    internal double DockWidth => _dock?.Value ?? (IsOpen && !IsSheetMode ? PanelWidth : 0);

    /// <summary>How far the sheet sits below its open place.</summary>
    internal double SheetOffset => _sheet?.Value ?? 0;

    /// <summary>The scrim's progress (0 hidden, 1 shown at 72%).</summary>
    internal double ScrimProgress => _scrimProgress?.Value ?? 0;

    /// <summary>The reopen pill.</summary>
    internal AiPanelReopenButton? ReopenButton => _reopen;

    /// <summary>The panel host.</summary>
    internal Border? PanelHost => _host;

    /// <summary>The page presenter.</summary>
    internal ContentPresenter? PagePresenter => _page;

    /// <summary>The scrim.</summary>
    internal Border? Scrim => _scrim;

    /// <summary>Opens the panel (no focus change).</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the panel; the focus moves to the reopen pill if it was in the panel.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_reopen is not null)
        {
            _reopen.Click -= OnReopenClick;
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
        _host?.RemoveHandler(KeyDownEvent, OnHostKeyDown);
        _reopenPresence?.Dispose();
        _layout = e.NameScope.Find<AiPanelDockLayout>(LayoutPart);
        _page = e.NameScope.Find<ContentPresenter>(PagePart);
        _reopen = e.NameScope.Find<AiPanelReopenButton>(ReopenPart);
        _scrim = e.NameScope.Find<Border>(ScrimPart);
        _host = e.NameScope.Find<Border>(PanelHostPart);
        if (_reopen is not null)
        {
            _reopen.Click += OnReopenClick;
            _reopenPresence = new AiFadePresence(_reopen, !IsOpen);
        }
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        if (_host is not null)
        {
            _host.RenderTransform = _sheetShift;
            _host.AddHandler(KeyDownEvent, OnHostKeyDown);
            AutomationProperties.SetLandmarkType(_host, AutomationLandmarkType.Complementary);
            AutomationProperties.SetName(_host, PanelName);
        }
        _dock = null;
        _sheet = null;
        _scrimProgress = null;
        _sheetFade = null;
        Apply(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var focusInside = !change.GetNewValue<bool>() && IsFocusInPanel();
            Apply(animate: true);
            if (focusInside)
            {
                FocusReopen();
            }
        }
        else if (change.Property == PanelWidthProperty || change.Property == SheetHeightRatioProperty)
        {
            Apply(animate: false);
        }
        else if (change.Property == SheetBreakpointProperty)
        {
            InvalidateMeasure();
        }
        else if (change.Property == PanelNameProperty && _host is not null)
        {
            AutomationProperties.SetName(_host, PanelName);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            UpdateMode(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateMode(e.NewSize.Width);
        // A closed sheet stays just out of sight when the height changes.
        if (IsSheetMode && !IsOpen && _sheet is { IsAnimating: false })
        {
            _sheet.Jump(ClosedSheetOffset());
        }
    }

    private void UpdateMode(double width)
    {
        var sheet = width < SheetBreakpoint;
        if (sheet == IsSheetMode)
        {
            return;
        }
        IsSheetMode = sheet;
        PseudoClasses.Set(SheetPseudoClass, sheet);
        // Crossing the breakpoint changes the layout at once (nothing to animate between a dock and a sheet).
        Apply(animate: false);
    }

    private double ClosedSheetOffset()
    {
        var height = _layout?.Bounds.Height is > 0 and var h ? h : Bounds.Height;
        return (_layout?.SheetHeight(height) ?? height * SheetHeightRatio) + SheetClosedExtra;
    }

    private ArcMotionLevel LevelFor(bool animate) =>
        animate && TopLevel.GetTopLevel(this) is not null ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

    private void EnsureValues()
    {
        if (_dock is not null)
        {
            return;
        }
        var open = IsOpen;
        _dock = new MotionValue<double>(this, open && !IsSheetMode ? PanelWidth : 0, MotionVectors.Double);
        _dock.Changed += v =>
        {
            if (_layout is not null)
            {
                _layout.DockWidth = v;
            }
        };
        _sheet = new MotionValue<double>(this, open ? 0 : ClosedSheetOffset(), MotionVectors.Double);
        _sheet.Changed += v => _sheetShift.Y = v;
        _scrimProgress = new MotionValue<double>(this, open && IsSheetMode ? 1 : 0, MotionVectors.Double);
        _scrimProgress.Changed += v =>
        {
            if (_scrim is not null)
            {
                _scrim.Opacity = ScrimAlpha * Math.Clamp(v, 0, 1);
            }
        };
        _sheetFade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _sheetFade.Changed += v =>
        {
            if (_host is not null)
            {
                _host.Opacity = Math.Clamp(v, 0, 1);
            }
        };
    }

    /// <summary>Brings every part to the open / closed state of the current mode, with motion when <paramref name="animate"/>.</summary>
    private void Apply(bool animate)
    {
        if (_layout is null || _host is null)
        {
            return;
        }
        EnsureValues();
        var open = IsOpen;
        var sheet = IsSheetMode;
        var level = LevelFor(animate);
        var generation = ++_generation;
        PseudoClasses.Set(OpenPseudoClass, open);
        _layout.IsSheet = sheet;
        _layout.PanelWidth = PanelWidth;
        _layout.SheetHeightRatio = SheetHeightRatio;

        // The panel is inert as soon as it closes; it leaves the visual tree's layout once out of sight.
        var wasHidden = !_host.IsVisible;
        SetInert(_host, !open);
        if (open)
        {
            _host.IsVisible = true;
        }
        if (_page is not null)
        {
            SetInert(_page, sheet && open);
        }
        if (_reopen is not null)
        {
            _reopen.Focusable = !open;
            _reopenPresence?.SetShown(!open);
        }
        if (_scrim is not null)
        {
            _scrim.IsHitTestVisible = sheet && open;
        }

        Task<bool> travel;
        if (sheet)
        {
            _dock!.Jump(0);
            Fade(_scrimProgress!, open ? 1 : 0, level, ScrimDuration, s_easeOut);
            if (level == ArcMotionLevel.Reduced)
            {
                // The sheet does not travel at reduced motion: it fades in and out in its open place (a closed one moves
                // out of sight once it has faded, in HideWhenClosed).
                _sheet!.Jump(0);
                if (open && wasHidden)
                {
                    _sheetFade!.Jump(0);
                }
                travel = _sheetFade!.AnimateTo(open ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _sheetFade!.Jump(1);
                travel = Move(_sheet!, open ? 0 : ClosedSheetOffset(), level);
            }
            if (_scrim is not null)
            {
                _scrim.IsVisible = true;
            }
        }
        else
        {
            _sheet!.Jump(0);
            _sheetFade!.Jump(1);
            _scrimProgress!.Jump(0);
            if (_scrim is not null)
            {
                _scrim.IsVisible = false;
            }
            travel = Move(_dock!, open ? PanelWidth : 0, level);
        }
        if (!open)
        {
            _ = HideWhenClosed(travel, generation);
        }
    }

    private static Task<bool> Move(MotionValue<double> value, double target, ArcMotionLevel level)
    {
        if (level == ArcMotionLevel.Off)
        {
            value.Jump(target);
            return Task.FromResult(true);
        }
        // A spring (Reduced: MotionValue jumps it).
        return value.AnimateTo(target, MotionSpec.Spring(ArcSpring.Settle));
    }

    private static void Fade(MotionValue<double> value, double target, ArcMotionLevel level, TimeSpan duration, Easing easing)
    {
        if (level == ArcMotionLevel.Off)
        {
            value.Jump(target);
            return;
        }
        _ = value.AnimateTo(target, MotionSpec.Tween(duration, easing));
    }

    private async Task HideWhenClosed(Task<bool> travel, int generation)
    {
        var reached = travel.IsCompleted ? travel.Result : await travel;
        if (reached && generation == _generation && !IsOpen && _host is not null)
        {
            // Out of sight: out of layout, rendering and the automation tree until it opens again.
            _host.IsVisible = false;
            if (IsSheetMode)
            {
                _sheet?.Jump(ClosedSheetOffset());
                _sheetFade?.Jump(1);
            }
        }
    }

    /// <summary>
    /// Takes <paramref name="part"/> out of pointer, Tab and automation reach (HTML's <c>inert</c>), or gives it back. Focus
    /// inside is the caller's to move (the dock moves it to its pill when the panel closes).
    /// </summary>
    internal static void SetInert(Control part, bool inert)
    {
        part.IsHitTestVisible = !inert;
        KeyboardNavigation.SetTabNavigation(part, inert ? KeyboardNavigationMode.None : KeyboardNavigationMode.Continue);
        AutomationProperties.SetIsOffscreenBehavior(part, inert ? IsOffscreenBehavior.Offscreen : IsOffscreenBehavior.Default);
    }

    /// <summary>Whether a part of the panel has the keyboard focus.</summary>
    internal bool IsFocusInPanel() =>
        _host is not null && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && _host.IsVisualAncestorOf(focused);

    private void FocusReopen() => _reopen?.Focus(_keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);

    private void OnCloseRequested(object? sender, RoutedEventArgs e)
    {
        if (!IsOpen)
        {
            return;
        }
        // Not marked handled: the owner may listen too (the panel closed because the person asked).
        SetCurrentValue(IsOpenProperty, false);
        FocusReopen();
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && IsOpen)
        {
            e.Handled = true;
            _host!.RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, _host));
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsOpen || !IsSheetMode)
        {
            return;
        }
        e.Handled = true;
        _scrim!.RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, _scrim));
    }

    private void OnReopenClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsOpenProperty, true);
        RaiseEvent(new RoutedEventArgs(ReopenRequestedEvent, this));
        // A panel that was out of sight since it was made has no templates yet, and a control without one cannot take focus.
        UpdateLayout();
        var focus = new RoutedEventArgs(FocusPanelRequestedEvent, this);
        RaiseEvent(focus);
        if (!focus.Handled)
        {
            FocusFirstInPanel();
        }
    }

    private void FocusFirstInPanel()
    {
        if (_host is null)
        {
            return;
        }
        var method = _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        foreach (var visual in _host.GetVisualDescendants())
        {
            if (visual is InputElement { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true, IsTabStop: true } element
                && element.Focus(method))
            {
                return;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
