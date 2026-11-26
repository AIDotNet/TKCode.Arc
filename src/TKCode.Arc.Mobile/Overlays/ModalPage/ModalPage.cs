using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>How a <see cref="ModalPage"/> covers the screen underneath.</summary>
public enum ModalPagePresentation
{
    /// <summary>A near-full-screen card with rounded top corners and a grabber that can be pulled down to dismiss (iOS page-sheet style).</summary>
    Sheet,

    /// <summary>Covers the entire screen edge to edge (including the top safe area), with no grabber.</summary>
    FullScreen,
}

/// <summary>
/// A full-screen or card-style modal page that slides up over a <see cref="Page"/> in <see cref="Page.Overlay"/>:
/// use it for self-contained multi-field flows such as composing a post, editing a profile or signing in.
/// <para>
/// Provides <see cref="Header"/>, <c>Content</c> and <see cref="Footer"/> slots, optional vertical scrolling
/// (<see cref="IsScrollable"/>), automatic safe-area insets from <see cref="Page.InheritedSafeAreaProperty"/>,
/// automatic soft-keyboard avoidance (<see cref="AvoidKeyboard"/>), pull-down dismissal in
/// <see cref="ModalPagePresentation.Sheet"/> presentation, Escape key and Android system back handling.
/// Under reduced motion the surface jumps open and closed without sliding.
/// </para>
/// </summary>
[TemplatePart("PART_Scrim", typeof(Border))]
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Safe", typeof(Border))]
[TemplatePart("PART_GrabberArea", typeof(Control))]
[TemplatePart("PART_Header", typeof(Control))]
[TemplatePart("PART_Scroller", typeof(ScrollViewer))]
[PseudoClasses(":scrollable", ":fullscreen")]
public class ModalPage : ContentControl
{
    /// <summary>Extra distance past the viewport height when closed so borders stay hidden.</summary>
    private const double ClosedGap = 24;

    /// <summary>A release coasts this long at its speed before deciding whether to dismiss or spring back.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>A downward flick faster than this (px/s) dismisses the sheet presentation.</summary>
    private const double FlickVelocity = 500;

    /// <summary>Rubber-band limit when pulling up past rest.</summary>
    private const double Stretch = 48;

    /// <summary>Whether the modal page is open.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalPage, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Visual style: near-full-screen rounded <see cref="ModalPagePresentation.Sheet"/> (default) or <see cref="ModalPagePresentation.FullScreen"/>.</summary>
    public static readonly StyledProperty<ModalPagePresentation> PresentationProperty =
        AvaloniaProperty.Register<ModalPage, ModalPagePresentation>(nameof(Presentation));

    /// <summary>The top slot, usually a <see cref="Navbar"/> with Cancel and Save actions.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<ModalPage, object?>(nameof(Header));

    /// <summary>The bottom slot, for example a <see cref="Toolbar"/> or primary action button.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ModalPage, object?>(nameof(Footer));

    /// <summary>Whether the body scrolls vertically inside the modal page.</summary>
    public static readonly StyledProperty<bool> IsScrollableProperty =
        AvaloniaProperty.Register<ModalPage, bool>(nameof(IsScrollable));

    /// <summary>Whether pulling down (in Sheet presentation), tapping the top scrim, pressing Escape or Android system back closes the modal page: true.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<ModalPage, bool>(nameof(IsDismissible), true);

    /// <summary>Whether the modal page lifts its content above the soft keyboard reported by <see cref="Page.KeyboardInsetProperty"/>: true.</summary>
    public static readonly StyledProperty<bool> AvoidKeyboardProperty =
        AvaloniaProperty.Register<ModalPage, bool>(nameof(AvoidKeyboard), true);

    private readonly TranslateTransform _translate = new();
    private Border? _scrim;
    private Border? _surface;
    private Border? _safe;
    private Control? _grabber;
    private Control? _header;
    private ScrollViewer? _scroller;
    private MotionValue<double>? _y;
    private DragTracker? _grabberDrag;
    private DragTracker? _headerDrag;
    private IDisposable? _backRegistration;
    private bool _attached;
    private bool _mounted;
    private bool _present;
    private bool _dragging;
    private double _dragOrigin;

    static ModalPage()
    {
        IsScrollableProperty.Changed.AddClassHandler<ModalPage>((p, _) => p.PseudoClasses.Set(":scrollable", p.IsScrollable));
        PresentationProperty.Changed.AddClassHandler<ModalPage>((p, _) => p.UpdatePresentation());
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="PresentationProperty"/>
    public ModalPagePresentation Presentation
    {
        get => GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
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

    /// <inheritdoc cref="IsScrollableProperty"/>
    public bool IsScrollable
    {
        get => GetValue(IsScrollableProperty);
        set => SetValue(IsScrollableProperty, value);
    }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <inheritdoc cref="AvoidKeyboardProperty"/>
    public bool AvoidKeyboard
    {
        get => GetValue(AvoidKeyboardProperty);
        set => SetValue(AvoidKeyboardProperty, value);
    }

    /// <summary>The surface's vertical offset below its open position, in px (0 when fully open). For tests and diagnostics.</summary>
    public double SurfaceOffset => _y?.Value ?? 0;

    protected override Type StyleKeyOverride => typeof(ModalPage);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double SheetTopGap =>
        this.TryFindResource(ModalPageTokens.Key(ModalPageTokenKind.SheetTopGap), out var v) && v is double d ? d : 16;

    private double ClosedOffset => (Bounds.Height > 0 ? Bounds.Height : 900) + ClosedGap;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _scrim = e.NameScope.Find<Border>("PART_Scrim");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _safe = e.NameScope.Find<Border>("PART_Safe");
        _grabber = e.NameScope.Find<Control>("PART_GrabberArea");
        _header = e.NameScope.Find<Control>("PART_Header");
        _scroller = e.NameScope.Find<ScrollViewer>("PART_Scroller");
        if (_surface is null)
        {
            return;
        }
        _surface.RenderTransform = _translate;
        _y = new MotionValue<double>(_surface, IsOpen ? 0 : ClosedOffset, MotionVectors.Double);
        _y.Changed += _ => Apply();
        if (_scrim is not null)
        {
            _scrim.PointerPressed += OnScrimPressed;
        }
        AttachDrag(_grabber, ref _grabberDrag);
        AttachDrag(_header, ref _headerDrag);
        _mounted = false;
        _present = IsOpen;
        UpdatePresentation();
        UpdateInsets();
        UpdatePresence();
        UpdateBackRegistration();
        Apply();
    }

    private void AttachDrag(Control? target, ref DragTracker? tracker)
    {
        if (target is null)
        {
            return;
        }
        tracker = new DragTracker(target, new DragOptions { Axis = DragAxis.Vertical, Threshold = 4, RejectCrossAxis = false })
        {
            CanBegin = _ => IsOpen && IsDismissible && Presentation == ModalPagePresentation.Sheet,
        };
        tracker.Started += (_, _) => BeginDrag();
        tracker.Moved += (_, args) => MoveDrag(args.Offset.Y);
        tracker.Completed += (_, args) => EndDrag(args.Velocity.Y);
        tracker.Cancelled += (_, _) => EndDrag(0);
    }

    private void Detach()
    {
        _grabberDrag?.Dispose();
        _grabberDrag = null;
        _headerDrag?.Dispose();
        _headerDrag = null;
        _y?.Dispose();
        _y = null;
        if (_scrim is not null)
        {
            _scrim.PointerPressed -= OnScrimPressed;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdateInsets();
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
        else if (change.Property == Page.InheritedSafeAreaProperty || change.Property == Page.KeyboardInsetProperty
                 || change.Property == AvoidKeyboardProperty)
        {
            UpdateInsets();
            if (change.Property == Page.KeyboardInsetProperty && AvoidKeyboard && Page.GetKeyboardInset(this) > 0)
            {
                ScrollFocusedIntoView();
            }
        }
        else if (change.Property == BoundsProperty && _y is not null && !_dragging && !_y.IsAnimating)
        {
            _y.Jump(IsOpen ? 0 : ClosedOffset);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen && IsDismissible)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        ScrollFocusedIntoView();
    }

    private void UpdateBackRegistration()
    {
        _backRegistration?.Dispose();
        _backRegistration = null;
        if (_attached && IsOpen)
        {
            _backRegistration = MobileBackNavigation.Register(this, MobileBackPriority.Overlay, HandleBackRequested);
        }
    }

    private bool HandleBackRequested()
    {
        if (!IsOpen)
        {
            return false;
        }
        if (IsDismissible)
        {
            IsOpen = false;
        }
        return true;
    }

    private void UpdatePresentation()
    {
        PseudoClasses.Set(":fullscreen", Presentation == ModalPagePresentation.FullScreen);
        if (_grabber is not null)
        {
            _grabber.IsVisible = Presentation == ModalPagePresentation.Sheet && IsDismissible;
        }
        UpdateInsets();
    }

    /// <summary>Applies safe-area margins/padding and bottom keyboard inset.</summary>
    private void UpdateInsets()
    {
        var safe = Page.GetInheritedSafeArea(this);
        var keyboard = AvoidKeyboard ? Math.Max(0, Page.GetKeyboardInset(this)) : 0;
        var bottom = Math.Max(safe.Bottom, keyboard);

        if (_surface is not null)
        {
            _surface.Margin = Presentation == ModalPagePresentation.Sheet
                ? new Thickness(0, safe.Top + SheetTopGap, 0, 0)
                : default;
        }
        if (_safe is not null)
        {
            var top = Presentation == ModalPagePresentation.FullScreen ? safe.Top : 0;
            _safe.Padding = new Thickness(safe.Left, top, safe.Right, bottom);
            // Content inside the modal is already inset by _safe, so nested overlays do not double-pad.
            _safe.SetValue(Page.InheritedSafeAreaProperty, default(Thickness));
        }
    }

    private void ScrollFocusedIntoView()
    {
        if (!IsOpen || !AvoidKeyboard || Page.GetKeyboardInset(this) <= 0)
        {
            return;
        }
        Dispatcher.UIThread.Post(
            () =>
            {
                if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control focused
                    && this.IsVisualAncestorOf(focused))
                {
                    focused.BringIntoView();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void UpdatePresence()
    {
        var visible = IsOpen || _present;
        IsHitTestVisible = visible;
        if (_surface is not null)
        {
            _surface.IsVisible = visible;
        }
        if (_scrim is not null)
        {
            _scrim.IsVisible = visible;
        }
    }

    private void Apply()
    {
        if (_y is null)
        {
            return;
        }
        var y = _y.Value;
        _translate.Y = y;
        if (_scrim is not null)
        {
            var closed = ClosedOffset;
            var progress = closed > 0 ? 1 - y / closed : (IsOpen ? 1 : 0);
            _scrim.Opacity = Math.Clamp(progress, 0, 1);
        }
    }

    private void Open()
    {
        if (_y is null || _surface is null)
        {
            return;
        }
        _present = true;
        UpdatePresence();
        UpdatePresentation();
        if (!_mounted)
        {
            _mounted = true;
            _y.Jump(ClosedOffset);
        }
        if (Reduced)
        {
            _y.Jump(0);
            return;
        }
        _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
        _surface.Focus();
    }

    private void Close(double? velocity)
    {
        if (_y is null)
        {
            return;
        }
        if (Reduced)
        {
            _y.Jump(ClosedOffset);
            Finish();
            return;
        }
        var spec = MotionSpec.Spring(SpringSpec.FromVisual(0.28, 0));
        _ = _y.AnimateTo(ClosedOffset, spec, velocity).ContinueWith(
            t => Dispatcher.Post(() =>
            {
                if (t.Result)
                {
                    Finish();
                }
            }));
    }

    private void Finish()
    {
        if (!IsOpen)
        {
            _present = false;
            UpdatePresence();
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsOpen && IsDismissible && Presentation == ModalPagePresentation.Sheet)
        {
            e.Handled = true;
            IsOpen = false;
        }
    }

    private void BeginDrag()
    {
        if (_y is null)
        {
            return;
        }
        _y.Stop();
        _dragOrigin = _y.Value < 0 ? -RubberBand.Invert(-_y.Value, Stretch) : _y.Value;
        _dragging = true;
    }

    private void MoveDrag(double offset)
    {
        if (!_dragging || _y is null || Reduced)
        {
            return;
        }
        var raw = _dragOrigin + offset;
        _y.Jump(raw < 0 ? -RubberBand.Apply(-raw, Stretch) : raw);
    }

    private void EndDrag(double velocity)
    {
        if (!_dragging || _y is null)
        {
            return;
        }
        _dragging = false;
        var current = _y.Value;
        if (current < 0)
        {
            velocity = 0;
        }
        Span<double> points = [0, ClosedOffset];
        var chosen = MomentumProjector.SelectSnapPoint(points, current, velocity, SnapProjection.Linear(Projection, 0, FlickVelocity));
        if (chosen == 1)
        {
            IsOpen = false;
            return;
        }
        _ = _y.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth), velocity);
    }
}
