using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>Edge from which a <see cref="SideDrawer"/> slides into view.</summary>
public enum SideDrawerSide
{
    /// <summary>Slides in from the leading (left) edge.</summary>
    Left,

    /// <summary>Slides in from the trailing (right) edge.</summary>
    Right,
}

/// <summary>
/// Mobile side drawer: a left- or right-edge slide-out panel placed in <see cref="Page.Overlay"/> for secondary
/// navigation, account switching or filters. Automatically inherits <see cref="Page.InheritedSafeAreaProperty"/>, clamps
/// its width cleanly on narrow phones, supports swipe-to-close with velocity projection, scrim tap, Escape key and
/// Android system back handling. Under reduced motion the panel jumps open and closed.
/// </summary>
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_Panel", typeof(Border))]
[TemplatePart("PART_Safe", typeof(Border))]
[PseudoClasses(":right")]
public class SideDrawer : ContentControl
{
    private const double ClosedGap = 16;
    private const double FlickVelocity = 450;
    private const double Stretch = 32;
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.18);

    /// <summary>Whether the drawer is currently open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SideDrawer, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Which screen edge the drawer slides from: <see cref="SideDrawerSide.Left"/> (default) or <see cref="SideDrawerSide.Right"/>.</summary>
    public static readonly StyledProperty<SideDrawerSide> SideProperty =
        AvaloniaProperty.Register<SideDrawer, SideDrawerSide>(nameof(Side));

    /// <summary>Preferred width of the drawer panel in px (clamped to the viewport): 280.</summary>
    public static readonly StyledProperty<double> DrawerWidthProperty =
        AvaloniaProperty.Register<SideDrawer, double>(nameof(DrawerWidth), 280);

    /// <summary>Optional fixed header slot at the top of the drawer.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<SideDrawer, object?>(nameof(Header));

    /// <summary>Optional fixed footer slot at the bottom of the drawer.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<SideDrawer, object?>(nameof(Footer));

    /// <summary>Whether tapping the scrim, swiping closed, pressing Escape or Android system back closes the drawer: true.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<SideDrawer, bool>(nameof(IsDismissible), true);

    /// <summary>Whether dragging the drawer horizontally toward its edge closes it: true.</summary>
    public static readonly StyledProperty<bool> EnableSwipeToCloseProperty =
        AvaloniaProperty.Register<SideDrawer, bool>(nameof(EnableSwipeToClose), true);

    /// <summary>Raised when the drawer opens.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> OpenedEvent =
        RoutedEvent.Register<SideDrawer, RoutedEventArgs>(nameof(Opened), RoutingStrategies.Bubble);

    /// <summary>Raised when the drawer closes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<SideDrawer, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    private readonly TranslateTransform _translate = new();
    private Border? _scrim;
    private Border? _panel;
    private Border? _safe;
    private MotionValue<double>? _progress; // 0 = open, 1 = closed
    private DragTracker? _drag;
    private IDisposable? _backRegistration;
    private bool _attached;
    private bool _present;
    private bool _dragging;
    private double _dragOrigin;

    static SideDrawer()
    {
        SideProperty.Changed.AddClassHandler<SideDrawer>((d, _) => d.UpdateSide());
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="SideProperty"/>
    public SideDrawerSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <inheritdoc cref="DrawerWidthProperty"/>
    public double DrawerWidth
    {
        get => GetValue(DrawerWidthProperty);
        set => SetValue(DrawerWidthProperty, value);
    }

    /// <inheritdoc cref="HeaderProperty"/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <inheritdoc cref="FooterProperty"/>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="EnableSwipeToCloseProperty"/>
    public bool EnableSwipeToClose
    {
        get => GetValue(EnableSwipeToCloseProperty);
        set => SetValue(EnableSwipeToCloseProperty, value);
    }

    /// <inheritdoc cref="OpenedEvent"/>
    public event EventHandler<RoutedEventArgs>? Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <inheritdoc cref="ClosedEvent"/>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Current horizontal translation of the drawer panel in px (0 when fully open). For tests and diagnostics.</summary>
    public double PanelOffset => _translate.X;

    protected override Type StyleKeyOverride => typeof(SideDrawer);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double MaxWidthRatio =>
        this.TryFindResource(SideDrawerTokens.Key(SideDrawerTokenKind.MaxWidthRatio), out var v) && v is double d && d > 0 && d <= 1 ? d : 0.85;

    private double EffectiveWidth
    {
        get
        {
            var preferred = DrawerWidth > 0 ? DrawerWidth : 280;
            if (Bounds.Width > 0)
            {
                return Math.Min(preferred, Bounds.Width * MaxWidthRatio);
            }
            return preferred;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _panel = e.NameScope.Find<Border>("PART_Panel");
        _safe = e.NameScope.Find<Border>("PART_Safe");
        if (_panel is null)
        {
            return;
        }
        _panel.RenderTransform = _translate;
        _progress = new MotionValue<double>(_panel, IsOpen ? 0 : 1, MotionVectors.Double);
        _progress.Changed += _ => Apply();
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        _drag = new DragTracker(_panel, new DragOptions { Axis = DragAxis.Horizontal, Threshold = 6, RejectCrossAxis = true })
        {
            CanBegin = _ => IsOpen && IsDismissible && EnableSwipeToClose,
        };
        _drag.Started += (_, _) => BeginDrag();
        _drag.Moved += (_, args) => MoveDrag(args.Offset.X);
        _drag.Completed += (_, args) => EndDrag(args.Velocity.X);
        _drag.Cancelled += (_, _) => EndDrag(0);

        _present = IsOpen;
        UpdateSide();
        UpdateWidthAndInsets();
        UpdatePresence();
        UpdateBackRegistration();
        Apply();
    }

    private void Detach()
    {
        _drag?.Dispose();
        _drag = null;
        _progress?.Dispose();
        _progress = null;
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateWidthAndInsets();
        UpdateBackRegistration();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _backRegistration?.Dispose();
        _backRegistration = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            UpdateBackRegistration();
            if (change.GetNewValue<bool>())
            {
                Open();
            }
            else
            {
                Close(null);
            }
        }
        else if (change.Property == BoundsProperty || change.Property == DrawerWidthProperty
                 || change.Property == Page.InheritedSafeAreaProperty)
        {
            UpdateWidthAndInsets();
            Apply();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && IsDismissible)
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && IsDismissible)
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void UpdateSide()
    {
        PseudoClasses.Set(":right", Side == SideDrawerSide.Right);
        UpdateWidthAndInsets();
        Apply();
    }

    private void UpdateWidthAndInsets()
    {
        if (_panel is not null)
        {
            _panel.Width = EffectiveWidth;
        }
        if (_safe is not null)
        {
            var safe = Page.GetInheritedSafeArea(this);
            _safe.Padding = Side == SideDrawerSide.Left
                ? new Thickness(safe.Left, safe.Top, 0, safe.Bottom)
                : new Thickness(0, safe.Top, safe.Right, safe.Bottom);
        }
    }

    private void UpdatePresence()
    {
        IsHitTestVisible = _present;
        if (_panel is not null)
        {
            _panel.IsVisible = _present;
        }
    }

    private void UpdateBackRegistration()
    {
        if (_attached && IsOpen)
        {
            _backRegistration ??= MobileBackNavigation.Register(this, MobileBackPriority.Overlay, HandleBack);
        }
        else
        {
            _backRegistration?.Dispose();
            _backRegistration = null;
        }
    }

    private bool HandleBack()
    {
        if (!IsOpen)
        {
            return false;
        }
        if (IsDismissible)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        return true;
    }

    private void Open()
    {
        _present = true;
        UpdatePresence();
        UpdateWidthAndInsets();
        Haptics.Trigger(HapticKind.Light);
        RaiseEvent(new RoutedEventArgs(OpenedEvent));
        if (_progress is null)
        {
            return;
        }
        if (Reduced)
        {
            _progress.Jump(0);
            Apply();
            return;
        }
        _ = _progress.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
    }

    private void Close(double? velocityProgress)
    {
        RaiseEvent(new RoutedEventArgs(ClosedEvent));
        if (_progress is null)
        {
            _present = false;
            UpdatePresence();
            return;
        }
        if (Reduced)
        {
            _progress.Jump(1);
            _present = false;
            UpdatePresence();
            Apply();
            return;
        }
        _ = _progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth), velocityProgress).ContinueWith(
            t => Dispatcher.UIThread.Post(() =>
            {
                if (t.Result && !IsOpen)
                {
                    _present = false;
                    UpdatePresence();
                }
            }));
    }

    private void BeginDrag()
    {
        if (_progress is null)
        {
            return;
        }
        _dragging = true;
        _dragOrigin = _progress.Value;
        _progress.Stop();
    }

    private void MoveDrag(double deltaX)
    {
        if (!_dragging || _progress is null)
        {
            return;
        }
        var travel = EffectiveWidth + ClosedGap;
        if (travel <= 1)
        {
            return;
        }
        // Closing direction: negative deltaX for Left drawer, positive deltaX for Right drawer.
        var signedDelta = Side == SideDrawerSide.Left ? -deltaX : deltaX;
        var raw = _dragOrigin + signedDelta / travel;
        if (raw < 0)
        {
            var pull = -RubberBand.Apply(-raw * travel, Stretch);
            _progress.Jump(pull / travel);
        }
        else
        {
            _progress.Jump(Math.Clamp(raw, 0, 1));
        }
    }

    private void EndDrag(double velocityX)
    {
        if (!_dragging || _progress is null)
        {
            return;
        }
        _dragging = false;
        var travel = EffectiveWidth + ClosedGap;
        var signedVel = Side == SideDrawerSide.Left ? -velocityX : velocityX;
        var velProgress = travel > 1 ? signedVel / travel : 0;
        var projected = _progress.Value + velProgress * Projection.TotalSeconds;
        var shouldClose = signedVel >= FlickVelocity || projected >= 0.45;
        if (shouldClose && IsDismissible)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else
        {
            if (Reduced)
            {
                _progress.Jump(0);
                Apply();
            }
            else
            {
                _ = _progress.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velProgress);
            }
        }
    }

    private void Apply()
    {
        var p = _progress?.Value ?? (IsOpen ? 0 : 1);
        var travel = EffectiveWidth + ClosedGap;
        var sign = Side == SideDrawerSide.Left ? -1 : 1;
        _translate.X = sign * p * travel;
        if (_scrim is not null)
        {
            _scrim.Opacity = Math.Clamp(1 - p, 0, 1);
        }
    }
}
