using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public sealed partial class AgentStatus
{
    private const double PillHeight = 40;
    private const double RowHeight = 38;
    private const double PanelMinWidth = 384;
    private const double CompactBreakpoint = 400;

    private AgentStatusSurface _surface = null!;
    private Border _clip = null!;
    private Grid _row = null!;
    private AgentStatusHead _head = null!;
    private AgentStatusFace _face = null!;
    private StackPanel _actions = null!;
    private Button _inlineDeny = null!;
    private Button _inlineApprove = null!;
    private Button _inlineRetry = null!;
    private StackPanel _body = null!;
    private TextBlock _agentNameText = null!;
    private TextBlock _counterText = null!;
    private AgentStatusSegments _segments = null!;
    private AgentStatusStepList _stepList = null!;
    private TextBlock _empty = null!;
    private Grid _footer = null!;
    private Button _primary = null!;
    private Button _secondary = null!;
    private Button _openRun = null!;
    private Border? _slot;
    private MotionValue<double> _pillWidth = null!;
    private MotionValue<double> _open = null!;
    private MotionValue<double> _panelHeight = null!;
    private MotionValue<double> _bodyOpacity = null!;
    private MotionValue<double> _actionsOpacity = null!;
    private double _panelWidth = PanelMinWidth;
    private bool _bodyVisible;
    private bool _floating;
    private bool _sized;
    private Panel? _overlay;
    private TopLevel? _hookedTop;
    private int _openToken;
    private NavigationMethod _focusMethod = NavigationMethod.Pointer;
    private KeyModifiers _focusModifiers;
    private bool _opened;
    private ITimer? _openedTimer;
    private bool _actionsShown;

    /// <summary>Creates a status pill.</summary>
    public AgentStatus()
    {
        BuildParts();
        _steps.CollectionChanged += OnStepsChanged;
        UpdatePseudoClasses();
    }

    /// <summary>The surface (the pill, or the open panel), inline or floating in the overlay layer. Tests read it.</summary>
    internal Border Surface => _surface;

    internal AgentStatusHead Head => _head;

    internal AgentStatusFace Face => _face;

    internal StackPanel Body => _body;

    internal AgentStatusSegments Segments => _segments;

    internal AgentStatusStepList StepList => _stepList;

    internal Button PrimaryButton => _primary;

    internal Button SecondaryButton => _secondary;

    internal Button OpenRunButton => _openRun;

    internal Button InlineDenyButton => _inlineDeny;

    internal Button InlineApproveButton => _inlineApprove;

    internal Button InlineRetryButton => _inlineRetry;

    internal TextBlock CounterText => _counterText;

    internal TextBlock EmptyText => _empty;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>True while the surface floats in the overlay layer.</summary>
    internal bool IsFloating => _floating;

    /// <summary>True while the pill is compact (the container is narrower than 400px).</summary>
    internal bool IsCompact => _compact;

    /// <summary>The panel's width at its last opening.</summary>
    internal double PanelWidth => _panelWidth;

    /// <summary>The width the pill rests at.</summary>
    internal double PillTargetWidth => _pillWidth.Target;

    /// <summary>How far open the panel is (0 closed, 1 open).</summary>
    internal double OpenProgress => _open.Value;

    private static TextBlock Text(double size, ArcDesignTokenKind sizeKind, ArcDesignTokenKind brush)
    {
        var text = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, FontFeatures = AgentStatusInked.Tabular };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, brush);
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, sizeKind);
        ArcTokens.Bind(text, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        return text;
    }

    private void BuildParts()
    {
        _pillWidth = new MotionValue<double>(this, 0, MotionVectors.Double);
        _pillWidth.Changed += _ => ApplyGeometry();
        _open = new MotionValue<double>(this, 0, MotionVectors.Double);
        _open.Changed += _ => ApplyGeometry();
        _panelHeight = new MotionValue<double>(this, 0, MotionVectors.Double);
        _panelHeight.Changed += _ => ApplyGeometry();
        _bodyOpacity = new MotionValue<double>(this, 0, MotionVectors.Double);
        _bodyOpacity.Changed += v => _body.Opacity = v;
        _actionsOpacity = new MotionValue<double>(this, 1, MotionVectors.Double);
        _actionsOpacity.Changed += v => _actions.Opacity = v;

        _face = new AgentStatusFace { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        _face.NaturalWidthChanged += () =>
        {
            if (_built)
            {
                UpdateSizes();
            }
        };
        _head = new AgentStatusHead
        {
            Content = _face,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = default,
            Height = RowHeight
        };
        _head.Click += (_, _) => Toggle();
        _head.ExpandRequested += expand => IsExpanded = expand;

        _inlineDeny = MakeButton();
        _inlineApprove = MakeButton();
        _inlineRetry = MakeButton();
        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _inlineDeny, _inlineApprove, _inlineRetry },
            IsVisible = false
        };
        Grid.SetColumn(_actions, 1);
        _row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = RowHeight, Children = { _head, _actions } };

        _agentNameText = Text(12, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        _counterText = Text(12, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted);
        _counterText.TextTrimming = TextTrimming.None;
        _counterText.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_counterText, 1);
        var meta = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(2, 0),
            ColumnSpacing = 12,
            Children = { _agentNameText, _counterText }
        };
        _segments = new AgentStatusSegments();
        var progress = new StackPanel { Spacing = 8, Margin = new Thickness(0, 2, 0, 0), Children = { meta, _segments } };

        _stepList = new AgentStatusStepList { TimeText = StepTime };
        _empty = Text(14, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.TextMuted);
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.TextTrimming = TextTrimming.None;
        _empty.IsVisible = false;
        var steps = new Panel { Children = { _stepList, _empty } };

        _primary = MakeButton();
        _secondary = MakeButton();
        _secondary.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(_secondary, 1);
        _openRun = MakeButton();
        _openRun.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_openRun, 3);
        _footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Height = 36,
            Children = { _primary, _secondary, _openRun }
        };

        _body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(12, 6, 12, 12),
            Children = { progress, steps, _footer },
            IsVisible = false,
            Opacity = 0
        };
        AutomationProperties.SetAccessibilityView(_body, AccessibilityView.Content);

        // The content is clipped to the rounded surface by an inner border, so the surface itself may draw its shadow outside.
        _clip = new Border { ClipToBounds = true, Child = new StackPanel { Children = { _row, _body } } };
        _clip.BindCornerRadius(ArcDesignTokenKind.RadiusStep19);
        _surface = new AgentStatusSurface
        {
            BorderThickness = new Thickness(1),
            ClipToBounds = false,
            Child = _clip
        };
        _surface.BindCornerRadius(ArcDesignTokenKind.RadiusStep20);
        _surface.AddHandler(KeyDownEvent, OnSurfaceKeyDown, RoutingStrategies.Bubble);
        _surface.GotFocus += (_, e) =>
        {
            _focusMethod = e.NavigationMethod;
            _focusModifiers = e.KeyModifiers;
        };
    }

    private Button MakeButton()
    {
        var button = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, IsVisible = false };
        button.Click += (_, _) =>
        {
            if (button.Tag is ButtonState { Action: not AgentStatusActions.None } state)
            {
                Invoke(state.Action);
            }
        };
        return button;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_slot is not null && ReferenceEquals(_slot.Child, _surface))
        {
            _slot.Child = null;
        }
        _slot = e.NameScope.Find<Border>(SlotPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_slot is not null)
        {
            _slot.Height = PillHeight;
            if (!_floating)
            {
                _slot.Child = _surface;
            }
        }
        _built = true;
        ApplyLevel();
        Refresh();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var available = double.IsInfinity(availableSize.Width) ? double.NaN : availableSize.Width;
        if (!available.Equals(_available))
        {
            _available = available;
            var compact = double.IsFinite(available) && available < CompactBreakpoint;
            if (compact != _compact)
            {
                _compact = compact;
                Dispatcher.UIThread.Post(Refresh);
            }
            else
            {
                Dispatcher.UIThread.Post(UpdateSizes);
            }
        }
        return base.MeasureOverride(availableSize);
    }

    // The footprint and the surface: the pill's width follows its content on a spring; opening grows the same surface to the
    // panel's size on another, so the pill and the panel read as one box. ControlTheme transitions cannot size a surface
    // from content measured elsewhere, so the two springs are MotionValues.
    private void UpdateSizes()
    {
        if (!_built)
        {
            return;
        }
        var inlineWidth = 0.0;
        if (_actions.IsVisible)
        {
            _actions.Measure(new Size(double.PositiveInfinity, 30));
            inlineWidth = _actions.DesiredSize.Width;
        }
        var natural = 2 + _face.NaturalWidth + inlineWidth;
        var target = double.IsFinite(_available) ? Math.Min(natural, Math.Max(0, _available)) : natural;
        if (!_sized)
        {
            _sized = true;
            _pillWidth.Jump(target);
            ApplyGeometry();
        }
        else if (Math.Abs(_pillWidth.Target - target) > 0.01)
        {
            _pillWidth.AnimateTo(target, MotionSpec.Spring(ArcSpring.Quick));
        }
        RetargetPanelHeight(animate: true);
    }

    private double ComputePanelHeight()
    {
        _body.Measure(new Size(_panelWidth - 2, double.PositiveInfinity));
        return 2 + RowHeight + _body.DesiredSize.Height;
    }

    private void RetargetPanelHeight(bool animate)
    {
        if (!_bodyVisible || !_body.IsVisible)
        {
            return;
        }
        var height = ComputePanelHeight();
        if (Math.Abs(_panelHeight.Target - height) <= 0.01)
        {
            return;
        }
        if (animate && IsExpanded && _open.Value > 0.98)
        {
            _panelHeight.AnimateTo(height, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _panelHeight.Jump(height);
        }
    }

    private void ApplyGeometry()
    {
        if (!_built)
        {
            return;
        }
        var p = Math.Clamp(_open.Value, 0, 1);
        var pill = _pillWidth.Value;
        _surface.Width = pill + (_panelWidth - pill) * p;
        _surface.Height = PillHeight + (Math.Max(PillHeight, _panelHeight.Value) - PillHeight) * p;
        _surface.CornerRadius = new CornerRadius(20 + 2 * p);
        _clip.CornerRadius = new CornerRadius(19 + 2 * p);
        if (_slot is not null)
        {
            _slot.Width = pill;
        }
        if (_floating)
        {
            PositionFloating();
        }
    }

    private void OnExpandedChanged(bool expanded)
    {
        RaiseEvent(new AgentStatusExpandedChangedEventArgs(ExpandedChangedEvent, expanded));
        if (!_built)
        {
            return;
        }
        _head.IsExpandedState = expanded;
        UpdatePseudoClasses();
        if (_attached)
        {
            if (expanded)
            {
                OpenPanel();
            }
            else
            {
                ClosePanel();
            }
        }
        Refresh();
    }

    private void OpenPanel()
    {
        _openToken++;
        var maxWidth = double.IsFinite(_available) ? _available : double.PositiveInfinity;
        _panelWidth = Math.Min(maxWidth, Math.Max(PanelMinWidth, _pillWidth.Target));
        _body.Width = Math.Max(0, _panelWidth - 2 - 24);
        _body.IsVisible = true;
        _bodyVisible = true;
        _bodyOpacity.Jump(0);
        _body.Opacity = 0;
        UpdateBody(TimeProvider.GetUtcNow());
        _panelHeight.Jump(ComputePanelHeight());
        Float();
        HookOutsidePress(true);
        _ = _open.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        _ = _bodyOpacity.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.Standard, TimeSpan.FromMilliseconds(80)));
        ApplyGeometry();
    }

    private void ClosePanel()
    {
        var token = ++_openToken;
        HookOutsidePress(false);
        _ = HideBodyAsync(token);
        _ = DockAfterCloseAsync(token);
    }

    private async Task HideBodyAsync(int token)
    {
        var done = await _bodyOpacity.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(75), ArcEasings.Standard));
        if (done && token == _openToken && !IsExpanded)
        {
            _bodyVisible = false;
            _body.IsVisible = false;
        }
    }

    private async Task DockAfterCloseAsync(int token)
    {
        var done = await _open.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        if (done && token == _openToken && !IsExpanded)
        {
            Dock();
        }
    }

    private void ReleasePanel()
    {
        HookOutsidePress(false);
        _openToken++;
        Dock();
    }

    private static OverlayLayer? FindOverlay(Visual owner)
    {
        var top = TopLevel.GetTopLevel(owner);
        if (top is null)
        {
            return null;
        }
        var overlay = OverlayLayer.GetOverlayLayer(owner) ?? OverlayLayer.GetOverlayLayer(top);
        if (overlay is not null)
        {
            return overlay;
        }
        var manager = owner.FindAncestorOfType<VisualLayerManager>(includeSelf: true)
                      ?? top.GetVisualDescendants().OfType<VisualLayerManager>().FirstOrDefault();
        if (manager is null)
        {
            return null;
        }
        manager.EnableOverlayLayer = true;
        return OverlayLayer.GetOverlayLayer(owner) ?? OverlayLayer.GetOverlayLayer(top);
    }

    private (Control? Element, NavigationMethod Method, KeyModifiers Modifiers) CaptureFocus()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (focused is not null && (ReferenceEquals(focused, _surface) || _surface.IsVisualAncestorOf(focused)))
        {
            return (focused, _focusMethod, _focusModifiers);
        }
        return default;
    }

    private static void RestoreFocus((Control? Element, NavigationMethod Method, KeyModifiers Modifiers) focus)
    {
        if (focus.Element is { } element)
        {
            element.Focus(focus.Method, focus.Modifiers);
        }
    }

    /// <summary>Moves the surface into the window's overlay layer so a header or a scroller cannot clip the open panel.</summary>
    private void Float()
    {
        if (_floating || _slot is null)
        {
            return;
        }
        var overlay = FindOverlay(this);
        if (overlay is null)
        {
            return;
        }
        var focus = CaptureFocus();
        _slot.Child = null;
        overlay.Children.Add(_surface);
        _overlay = overlay;
        _floating = true;
        _surface.IsFloating = true;
        _slot.LayoutUpdated += OnSlotLayoutUpdated;
        PositionFloating();
        RestoreFocus(focus);
    }

    private void Dock()
    {
        if (!_floating)
        {
            return;
        }
        var focus = CaptureFocus();
        if (_slot is not null)
        {
            _slot.LayoutUpdated -= OnSlotLayoutUpdated;
        }
        _overlay?.Children.Remove(_surface);
        _overlay = null;
        _floating = false;
        _surface.IsFloating = false;
        if (_slot is not null)
        {
            _slot.Child = _surface;
        }
        RestoreFocus(focus);
    }

    private void OnSlotLayoutUpdated(object? sender, EventArgs e) => PositionFloating();

    private void PositionFloating()
    {
        if (_overlay is null || _slot is null)
        {
            return;
        }
        var origin = _slot.TranslatePoint(default, _overlay);
        if (origin is not { } point)
        {
            return;
        }
        var width = _surface.Width;
        var pill = _pillWidth.Value;
        var x = PanelAlignment switch
        {
            AgentStatusPanelAlignment.Start => point.X,
            AgentStatusPanelAlignment.End => point.X + pill - width,
            _ => point.X + pill / 2 - width / 2
        };
        if (!double.IsNaN(x) && !double.IsNaN(point.Y))
        {
            Canvas.SetLeft(_surface, x);
            Canvas.SetTop(_surface, point.Y);
        }
    }

    private void HookOutsidePress(bool hook)
    {
        if (hook)
        {
            if (_hookedTop is null && TopLevel.GetTopLevel(this) is { } top)
            {
                _hookedTop = top;
                top.AddHandler(PointerPressedEvent, OnOutsidePress, RoutingStrategies.Tunnel, handledEventsToo: true);
            }
        }
        else if (_hookedTop is { } hooked)
        {
            hooked.RemoveHandler(PointerPressedEvent, OnOutsidePress);
            _hookedTop = null;
        }
    }

    private void OnOutsidePress(object? sender, PointerPressedEventArgs e)
    {
        if (!IsExpanded)
        {
            return;
        }
        if (e.Source is Visual source && (ReferenceEquals(source, _surface) || _surface.IsVisualAncestorOf(source)))
        {
            return;
        }
        // Focus is not moved: it goes where the press lands.
        Collapse();
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsExpanded)
        {
            return;
        }
        if (e.Key == Key.Escape)
        {
            Collapse();
            FocusHead();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && _floating && _slot is not null)
        {
            // The floating surface lives in the overlay layer: Tab out of its first or last control continues from the
            // pill's place in the page instead of wrapping inside the overlay.
            var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            var focusables = _surface.GetVisualDescendants().OfType<Control>()
                .Where(c => c.Focusable && c.IsTabStop && c.IsEffectivelyEnabled && c.IsEffectivelyVisible).ToList();
            var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
            var focused = focusManager?.GetFocusedElement();
            if (focusManager is not null && focusables.Count > 0 && ReferenceEquals(focused, shift ? focusables[0] : focusables[^1]))
            {
                focusManager.TryMoveFocus(shift ? NavigationDirection.Previous : NavigationDirection.Next,
                    new FindNextElementOptions { FocusedElement = _slot });
                e.Handled = true;
            }
        }
    }

    /// <summary>Puts focus on the pill with the keyboard ring (used after Escape and when a focused control disappears).</summary>
    internal void FocusHead() => _head.Focus(NavigationMethod.Tab);
}
