using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The layer over the layout's content that slides out an auto-hidden pane (spec 5.4, 8): a card along one edge, as wide
/// (left, right) or high (top, bottom) as the pane was docked, at most <c>FlyoutMaxShare</c> of the layout, with a resize grip
/// on its inner edge. It slides in on the smooth spring and out in 180ms (<c>ArcEasings.Exit</c>); with reduced motion it
/// fades in and out in 120ms instead. The layer itself takes no pointer input; only the card and the grip do.
/// </summary>
internal sealed class DockAutoHideFlyout : Control
{
    /// <summary>Extra distance the hidden card moves past the edge so its shadow is out of sight too.</summary>
    private const double ShadowAllowance = 32;

    private readonly DockAutoHideCard _card;
    private readonly Border _grip;
    private readonly TranslateTransform _cardShift = new();
    private readonly TranslateTransform _gripShift = new();
    private MotionValue<double>? _offset;
    private MotionValue<double>? _opacity;
    private DockLayout? _layout;
    private DockPane? _pane;
    private DockSide _side;
    private double _extent;
    private bool _hiding;
    private double _hideTarget;
    private Point? _dragStart;
    private double _dragExtent;

    public DockAutoHideFlyout()
    {
        ClipToBounds = true;
        _card = new DockAutoHideCard { IsVisible = false, RenderTransform = _cardShift };
        _card.PinClicked += OnPinClicked;
        _grip = new Border { Background = Brushes.Transparent, IsVisible = false, RenderTransform = _gripShift };
        _grip.PointerPressed += OnGripPressed;
        _grip.PointerMoved += OnGripMoved;
        _grip.PointerReleased += OnGripReleased;
        _grip.PointerCaptureLost += (_, _) => _dragStart = null;
        VisualChildren.Add(_card);
        LogicalChildren.Add(_card);
        VisualChildren.Add(_grip);
        LogicalChildren.Add(_grip);
    }

    /// <summary>The pane shown (or sliding out), or null.</summary>
    public DockPane? Pane => _pane;

    /// <summary>True while a pane is shown and not on its way out.</summary>
    public bool IsOpen => _pane is not null && !_hiding;

    /// <summary>The edge the card slides from.</summary>
    public DockSide Side => _side;

    /// <summary>The card (tests and focus checks).</summary>
    internal DockAutoHideCard Card => _card;

    /// <summary>The resize grip (tests).</summary>
    internal Border Grip => _grip;

    /// <summary>The card's width or height along its edge's axis, after the 70% limit.</summary>
    internal double EffectiveExtent => ClampExtent(_extent, AxisLength);

    private bool IsVertical => _side is DockSide.Left or DockSide.Right;

    private double AxisLength => IsVertical ? Bounds.Width : Bounds.Height;

    private double MaxShare => this.TryFindResource(DockLayoutTokens.Key(DockLayoutTokenKind.FlyoutMaxShare), out var value) && value is double share ? share : 0.7;

    private double GripThickness => this.TryFindResource(DockAutoHideTokens.Key(DockAutoHideTokenKind.GripThickness), out var value) && value is double t ? t : 6;

    public void Attach(DockLayout? layout)
    {
        _layout = layout;
        _card.Layout = layout;
    }

    /// <summary>Slides out <paramref name="pane"/> from <paramref name="side"/>, <paramref name="extent"/> px wide or high.</summary>
    public void Show(DockPane pane, DockSide side, double extent)
    {
        var swap = !ReferenceEquals(pane, _pane) || side != _side;
        _hiding = false;
        _pane = pane;
        _side = side;
        _extent = extent;
        _grip.Cursor = new Cursor(IsVertical ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth);
        _card.SetPane(pane);
        _card.IsVisible = true;
        _grip.IsVisible = !(_layout?.IsLocked ?? false);
        EnsureMotion();
        if (swap)
        {
            _offset!.Jump(HiddenOffset);
            _opacity!.Jump(ReducedMotion ? 0 : 1);
        }
        InvalidateArrange();
        if (ReducedMotion)
        {
            _offset!.Jump(0);
            _ = _opacity!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            _opacity!.Jump(1);
            _ = _offset!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        }
    }

    /// <summary>Slides the card out (or takes it away at once) and lets go of the pane once it is gone.</summary>
    public void Hide(bool animate)
    {
        if (_pane is null)
        {
            return;
        }
        _hiding = true;
        EnsureMotion();
        if (!animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off || !_card.IsEffectivelyVisible)
        {
            Finish();
            return;
        }
        // Finished from the value's own last step (no continuation that hops through the thread pool).
        _hideTarget = ReducedMotion ? 0 : HiddenOffset;
        if (ReducedMotion)
        {
            _ = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            _ = _offset!.AnimateTo(_hideTarget, MotionSpec.Tween(ArcMotionPresets.Exit, ArcEasings.Exit));
        }
    }

    /// <summary>Called on every step of the hiding value: the card goes once the value reached its target.</summary>
    private void OnHideStep(double value, bool reduced)
    {
        if (_hiding && _pane is not null && reduced == ReducedMotion && Math.Abs(value - _hideTarget) < 1e-6)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _hiding = false;
        _offset?.Stop();
        _opacity?.Stop();
        _card.SetPane(null);
        _card.IsVisible = false;
        _grip.IsVisible = false;
        _pane = null;
        _dragStart = null;
    }

    private bool ReducedMotion => ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Reduced;

    private double HiddenOffset => EffectiveExtent + ShadowAllowance;

    private void EnsureMotion()
    {
        if (_offset is null)
        {
            _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
            _offset.Changed += value =>
            {
                ApplyOffset();
                OnHideStep(value, reduced: false);
            };
        }
        if (_opacity is null)
        {
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += value =>
            {
                _card.Opacity = value;
                _grip.Opacity = value;
                OnHideStep(value, reduced: true);
            };
        }
    }

    private void ApplyOffset()
    {
        var offset = _offset?.Value ?? 0;
        var (x, y) = _side switch
        {
            DockSide.Left => (-offset, 0.0),
            DockSide.Right => (offset, 0.0),
            DockSide.Top => (0.0, -offset),
            _ => (0.0, offset)
        };
        _cardShift.X = x;
        _cardShift.Y = y;
        _gripShift.X = x;
        _gripShift.Y = y;
    }

    private double ClampExtent(double extent, double length)
    {
        if (length <= 0)
        {
            return Math.Max(0, extent);
        }
        var max = length * MaxShare;
        var min = Math.Min(DockNode.DefaultMinSize, max);
        return Math.Clamp(extent > 0 ? extent : DockPane.DefaultPreferredSize, min, max);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
        var (card, grip) = Rects(size);
        _card.Measure(card.Size);
        _grip.Measure(grip.Size);
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (card, grip) = Rects(finalSize);
        _card.Arrange(card);
        _grip.Arrange(grip);
        ApplyOffset();
        return finalSize;
    }

    private (Rect Card, Rect Grip) Rects(Size size)
    {
        var extent = ClampExtent(_extent, IsVertical ? size.Width : size.Height);
        var g = GripThickness;
        return _side switch
        {
            DockSide.Left => (new Rect(0, 0, extent, size.Height), new Rect(extent - g / 2, 0, g, size.Height)),
            DockSide.Right => (new Rect(size.Width - extent, 0, extent, size.Height), new Rect(size.Width - extent - g / 2, 0, g, size.Height)),
            DockSide.Top => (new Rect(0, 0, size.Width, extent), new Rect(0, extent - g / 2, size.Width, g)),
            _ => (new Rect(0, size.Height - extent, size.Width, extent), new Rect(0, size.Height - extent - g / 2, size.Width, g))
        };
    }

    private void OnPinClicked(object? sender, RoutedEventArgs e)
    {
        if (_pane is { } pane && _layout is { } layout)
        {
            layout.Pin(pane);
        }
    }

    #region Resize grip

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pane is null || !e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _dragStart = e.GetPosition(this);
        _dragExtent = EffectiveExtent;
        e.Pointer.Capture(_grip);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragStart is not { } start || _pane is not { } pane)
        {
            return;
        }
        var now = e.GetPosition(this);
        var delta = _side switch
        {
            DockSide.Left => now.X - start.X,
            DockSide.Right => start.X - now.X,
            DockSide.Top => now.Y - start.Y,
            _ => start.Y - now.Y
        };
        _extent = ClampExtent(_dragExtent + delta, AxisLength);
        pane.AutoHideExtent = _extent;
        InvalidateMeasure();
        InvalidateArrange();
        e.Handled = true;
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }
        _dragStart = null;
        e.Pointer.Capture(null);
        e.Handled = true;
        _layout?.OnResized();
    }

    #endregion
}

/// <summary>
/// The card of the auto-hide flyout: a header with the pane's icon, title and pin button, then the pane itself.
/// <c>SurfaceRaised</c>, <c>ShadowFloating</c>, <c>RadiusStep12</c>, like <c>ArcPopup</c>.
/// </summary>
[TemplatePart(ContentPart, typeof(Decorator))]
[TemplatePart(PinPart, typeof(AvaloniaButton))]
internal sealed class DockAutoHideCard : TemplatedControl
{
    public const string ContentPart = "PART_Content";
    public const string PinPart = "PART_Pin";

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DockAutoHideCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DockAutoHideCard, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<bool> CanPinProperty =
        AvaloniaProperty.Register<DockAutoHideCard, bool>(nameof(CanPin), true);

    private Decorator? _content;
    private AvaloniaButton? _pin;
    private DockPane? _pane;
    private IDisposable? _title;
    private IDisposable? _icon;

    static DockAutoHideCard()
    {
        FocusableProperty.OverrideDefaultValue<DockAutoHideCard>(true);
    }

    public DockAutoHideCard()
    {
        AddHandler(ContextRequestedEvent, OnContextRequested);
    }

    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Whether the pin button shows (not while the layout is locked).</summary>
    public bool CanPin { get => GetValue(CanPinProperty); set => SetValue(CanPinProperty, value); }

    public DockLayout? Layout { get; set; }

    /// <summary>The pin button (tests).</summary>
    internal AvaloniaButton? PinButton => _pin;

    /// <summary>Raised when the pin button is clicked.</summary>
    public event EventHandler<RoutedEventArgs>? PinClicked;

    /// <summary>Shows <paramref name="pane"/> in the card (null empties it).</summary>
    public void SetPane(DockPane? pane)
    {
        if (ReferenceEquals(pane, _pane))
        {
            return;
        }
        _title?.Dispose();
        _icon?.Dispose();
        _title = null;
        _icon = null;
        if (_pane is not null && _content is not null && ReferenceEquals(_content.Child, _pane))
        {
            _content.Child = null;
        }
        _pane = pane;
        if (pane is null)
        {
            ClearValue(TitleProperty);
            ClearValue(IconProperty);
            return;
        }
        _title = Bind(TitleProperty, pane.GetObservable(DockPane.TitleProperty));
        _icon = Bind(IconProperty, pane.GetObservable(DockPane.IconProperty));
        AutomationProperties.SetName(this, pane.ToString());
        UpdateContent();
    }

    private void UpdateContent()
    {
        if (_content is null || _pane is null || ReferenceEquals(_content.Child, _pane))
        {
            return;
        }
        DockLayout.Adopt(_pane);
        _content.Child = _pane;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pin is not null)
        {
            _pin.Click -= OnPinClick;
        }
        if (_content is not null)
        {
            _content.Child = null;
        }
        _content = e.NameScope.Find<Decorator>(ContentPart);
        _pin = e.NameScope.Find<AvaloniaButton>(PinPart);
        if (_pin is not null)
        {
            _pin.Click += OnPinClick;
            AutomationProperties.SetName(_pin, DockText.Get(DockLangResourceKind.Pin));
            Tooltip.SetTip(_pin, DockText.Get(DockLangResourceKind.Pin));
        }
        UpdateContent();
    }

    private void OnPinClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        PinClicked?.Invoke(this, e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _pane is null || Layout is null)
        {
            return;
        }
        // On the card itself only: inside the pane these keys belong to its content.
        if (ReferenceEquals(e.Source, this) && (e.Key == Key.Apps || (e.Key == Key.F10 && (e.KeyModifiers & KeyModifiers.Shift) != 0)))
        {
            e.Handled = true;
            Layout.ShowPaneMenu(_pane, this, null);
        }
    }

    private void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // Only the header: the pane's content may have menus of its own.
        if (e.Handled || _pane is null || Layout is null || e.Source is Visual source && _pane.IsVisualAncestorOf(source))
        {
            return;
        }
        e.Handled = true;
        Layout.ShowPaneMenu(_pane, this, e.TryGetPosition(this, out var point) ? point : null);
    }
}
