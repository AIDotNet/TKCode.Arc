using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A mobile page: an optional <see cref="Header"/> (usually a <see cref="Navbar"/>), the content, and an optional
/// <see cref="Footer"/> (usually a <see cref="Tabbar"/>), laid out inside the device's safe area. The safe area is
/// the space the notch, rounded corners and gesture bar take; on desktop and in tests it is zero unless set by hand.
/// </summary>
public class Page : ContentControl
{
    /// <summary>The slot above the content, usually a <see cref="Navbar"/>.</summary>
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<Page, object?>(nameof(Header));

    /// <summary>The slot below the content, usually a <see cref="Tabbar"/>.</summary>
    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<Page, object?>(nameof(Footer));

    /// <summary>The slot over the whole page, header and footer included, for a <see cref="Sheet"/>.</summary>
    public static readonly StyledProperty<object?> OverlayProperty =
        AvaloniaProperty.Register<Page, object?>(nameof(Overlay));

    /// <summary>
    /// The insets the page keeps free on each edge. With <see cref="UsePlatformSafeArea"/> the platform sets it
    /// (zero where there is no insets manager); otherwise set it yourself.
    /// </summary>
    public static readonly StyledProperty<Thickness> SafeAreaProperty =
        AvaloniaProperty.Register<Page, Thickness>(nameof(SafeArea));

    /// <summary>Whether <see cref="SafeArea"/> follows the platform's safe area padding.</summary>
    public static readonly StyledProperty<bool> UsePlatformSafeAreaProperty =
        AvaloniaProperty.Register<Page, bool>(nameof(UsePlatformSafeArea), true);

    /// <summary>
    /// Whether the content scrolls vertically inside the page. Off, the content gets the page's height as it is.
    /// On, a <see cref="Navbar"/> in <see cref="Header"/> that has a large title collapses it as the content scrolls.
    /// </summary>
    public static readonly StyledProperty<bool> IsScrollableProperty =
        AvaloniaProperty.Register<Page, bool>(nameof(IsScrollable));

    /// <summary>
    /// Whether the page lays out above the soft keyboard: the content shrinks by the keyboard's height, so the focused
    /// input stays visible, and the footer (a <see cref="Tabbar"/>) goes behind the keyboard the way iOS does. Off, the
    /// keyboard covers the page and only <see cref="KeyboardInset"/> reports it.
    /// </summary>
    public static readonly StyledProperty<bool> AvoidKeyboardProperty =
        AvaloniaProperty.Register<Page, bool>(nameof(AvoidKeyboard), true);

    /// <summary>
    /// The height the soft keyboard covers at the bottom edge, read from the page or from any control inside it with
    /// <see cref="GetKeyboardInset"/> (it is inherited), so an overlay such as a <see cref="Sheet"/> can lift itself.
    /// It is the target height, not the animated one: it is 0 when the keyboard is hidden.
    /// </summary>
    public static readonly AttachedProperty<double> KeyboardInsetProperty =
        AvaloniaProperty.RegisterAttached<Page, AvaloniaObject, double>("KeyboardInset", 0, true);

    /// <summary>
    /// Where the pages below a control get the keyboard's height when the platform has no input pane: set it on a host
    /// (inherited). Null uses <c>TopLevel.InputPane</c>.
    /// </summary>
    public static readonly AttachedProperty<KeyboardInsetSource?> KeyboardInsetSourceProperty =
        AvaloniaProperty.RegisterAttached<Page, AvaloniaObject, KeyboardInsetSource?>("KeyboardInsetSource", null, true);

    /// <summary>Gets the height the soft keyboard covers for <paramref name="element"/>, as the nearest <see cref="Page"/> above it reports.</summary>
    public static double GetKeyboardInset(AvaloniaObject element) => element.GetValue(KeyboardInsetProperty);

    /// <summary>Gets the <see cref="KeyboardInsetSource"/> that <paramref name="element"/> and its descendants use.</summary>
    public static KeyboardInsetSource? GetKeyboardInsetSource(AvaloniaObject element) => element.GetValue(KeyboardInsetSourceProperty);

    /// <summary>Sets the <see cref="KeyboardInsetSource"/> that <paramref name="element"/> and its descendants use.</summary>
    public static void SetKeyboardInsetSource(AvaloniaObject element, KeyboardInsetSource? value) => element.SetValue(KeyboardInsetSourceProperty, value);

    static Page()
    {
        IsScrollableProperty.Changed.AddClassHandler<Page>((page, _) => page.PseudoClasses.Set(":scrollable", page.IsScrollable));
        HeaderProperty.Changed.AddClassHandler<Page>((page, _) => page.UpdateLargeTitle());
    }

    private ScrollViewer? _scroller;
    private Border? _safe;
    private Control? _footer;
    private MotionValue<double>? _shown;
    private KeyboardInsetSource? _source;
    private IInputPane? _pane;
    private double _target;
    private double _lastTarget;
    private TimeSpan? _duration;
    private TopLevel? _topLevel;
    private Avalonia.Controls.Platform.IInsetsManager? _insets;

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

    /// <inheritdoc cref="OverlayProperty"/>
    public object? Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    /// <inheritdoc cref="AvoidKeyboardProperty"/>
    public bool AvoidKeyboard
    {
        get => GetValue(AvoidKeyboardProperty);
        set => SetValue(AvoidKeyboardProperty, value);
    }

    /// <inheritdoc cref="KeyboardInsetProperty"/>
    public double KeyboardInset
    {
        get => GetValue(KeyboardInsetProperty);
        private set => SetValue(KeyboardInsetProperty, value);
    }

    /// <inheritdoc cref="IsScrollableProperty"/>
    public bool IsScrollable
    {
        get => GetValue(IsScrollableProperty);
        set => SetValue(IsScrollableProperty, value);
    }

    /// <inheritdoc cref="SafeAreaProperty"/>
    public Thickness SafeArea
    {
        get => GetValue(SafeAreaProperty);
        set => SetValue(SafeAreaProperty, value);
    }

    /// <inheritdoc cref="UsePlatformSafeAreaProperty"/>
    public bool UsePlatformSafeArea
    {
        get => GetValue(UsePlatformSafeAreaProperty);
        set => SetValue(UsePlatformSafeAreaProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(Page);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        _scroller = e.NameScope.Find<ScrollViewer>("PART_Scroller");
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        _safe = e.NameScope.Find<Border>("PART_Safe");
        if (_footer is not null)
        {
            _footer.SizeChanged -= OnFooterSizeChanged;
        }
        _footer = e.NameScope.Find<Control>("PART_Footer");
        if (_footer is not null)
        {
            _footer.SizeChanged += OnFooterSizeChanged;
        }
        ApplyInsets(_shown?.Value ?? 0);
        UpdateLargeTitle();
    }

    private void OnFooterSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyInsets(_shown?.Value ?? 0);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Track(TopLevel.GetTopLevel(this));
        TrackKeyboard();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Track(null);
        UntrackKeyboard();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsePlatformSafeAreaProperty && _topLevel is not null)
        {
            Track(_topLevel);
        }
        else if (change.Property == SafeAreaProperty)
        {
            ApplyInsets(_shown?.Value ?? 0);
        }
        else if (change.Property == KeyboardInsetSourceProperty && _topLevel is not null)
        {
            TrackKeyboard();
        }
        else if (change.Property == AvoidKeyboardProperty && _topLevel is not null)
        {
            MoveTo(_target, _duration);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        ScrollFocusedIntoView();
    }

    /// <summary>Follows the keyboard inset source of the tree, or else the platform's input pane.</summary>
    private void TrackKeyboard()
    {
        UntrackKeyboard();
        _source = GetKeyboardInsetSource(this);
        if (_source is not null)
        {
            _source.Changed += OnSourceChanged;
            MoveTo(_source.Inset, _source.Duration);
            return;
        }
        _pane = _topLevel?.InputPane;
        if (_pane is not null)
        {
            _pane.StateChanged += OnPaneStateChanged;
            MoveTo(_pane.State == InputPaneState.Open ? _pane.OccludedRect.Height : 0, null);
            return;
        }
        MoveTo(0, null);
    }

    private void UntrackKeyboard()
    {
        if (_source is not null)
        {
            _source.Changed -= OnSourceChanged;
            _source = null;
        }
        if (_pane is not null)
        {
            _pane.StateChanged -= OnPaneStateChanged;
            _pane = null;
        }
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        if (_source is not null)
        {
            MoveTo(_source.Inset, _source.Duration);
        }
    }

    private void OnPaneStateChanged(object? sender, InputPaneStateEventArgs e)
    {
        var inset = e.NewState == InputPaneState.Open ? e.EndRect.Height : 0;
        MoveTo(inset, e.AnimationDuration > TimeSpan.Zero ? e.AnimationDuration : null);
    }

    /// <summary>
    /// Takes the keyboard's new height and brings the layout to it. A MotionValue, not a ControlTheme transition: the
    /// bottom inset is a layout input (the content really shrinks), which a transition on a render property cannot
    /// express, and the platform's own duration differs per event so it is chosen at run time.
    /// </summary>
    private void MoveTo(double inset, TimeSpan? duration)
    {
        _target = Math.Max(0, inset);
        _duration = duration;
        KeyboardInset = _target;
        if (_target > 0)
        {
            _lastTarget = _target;
        }
        var shown = AvoidKeyboard ? _target : 0;
        if (_shown is null)
        {
            _shown = new MotionValue<double>(this, 0, MotionVectors.Double);
            _shown.Changed += ApplyInsets;
        }
        if (Math.Abs(_shown.Target - shown) < 0.01)
        {
            // Already there (the first look at a page with no keyboard): no motion on the first frame.
            return;
        }
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            // Reduced motion and no motion: the layout jumps to the keyboard.
            _shown.Jump(shown);
            ScrollFocusedIntoView();
            return;
        }
        // The platform's own duration keeps the page in step with the keyboard; without them a smooth
        // spring opens it and a snappy one closes it (leaving is faster than arriving).
        var spec = duration is { } d
            ? MotionSpec.Tween(d, ArcEasings.Standard)
            : MotionSpec.Spring(shown > 0 ? ArcSpring.Smooth : ArcSpring.Snappy);
        _ = AnimateShown(shown, spec);
    }

    private async Task AnimateShown(double shown, MotionSpec spec)
    {
        ScrollFocusedIntoView();
        if (_shown is not null && await _shown.AnimateTo(shown, spec))
        {
            ScrollFocusedIntoView();
        }
    }

    /// <summary>Lays out the page above <paramref name="shown"/> pixels of keyboard.</summary>
    private void ApplyInsets(double shown)
    {
        if (_safe is not null)
        {
            var area = SafeArea;
            _safe.Padding = new Thickness(area.Left, area.Top, area.Right, Math.Max(area.Bottom, shown));
        }
        if (_footer is not null)
        {
            // The footer goes behind the keyboard: its row shrinks to nothing as the keyboard arrives and what
            // overflows is under the keyboard. Bounds is the footer's own height, margin aside.
            var progress = _lastTarget > 0 ? Math.Clamp(shown / _lastTarget, 0, 1) : 0;
            _footer.Margin = new Thickness(0, 0, 0, -_footer.Bounds.Height * progress);
        }
    }

    /// <summary>Keeps the focused control visible: a scrolling page scrolls to it, a fixed one has already shrunk.</summary>
    private void ScrollFocusedIntoView()
    {
        if (_target <= 0 || !AvoidKeyboard)
        {
            return;
        }
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_topLevel?.FocusManager?.GetFocusedElement() is Control focused && this.IsVisualAncestorOf(focused))
                {
                    focused.BringIntoView();
                }
            },
            DispatcherPriority.Loaded);
    }

    /// <summary>Follows the top level's safe area while <see cref="UsePlatformSafeArea"/> is on.</summary>
    private void Track(TopLevel? topLevel)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
            _insets = null;
        }
        _topLevel = topLevel;
        if (topLevel is null || !UsePlatformSafeArea)
        {
            return;
        }
        _insets = topLevel.InsetsManager;
        if (_insets is not null)
        {
            _insets.SafeAreaChanged += OnSafeAreaChanged;
        }
        // No insets manager (desktop, headless): the platform reports no safe area.
        SafeArea = _insets?.SafeAreaPadding ?? default;
    }

    private void OnSafeAreaChanged(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) => SafeArea = e.SafeAreaPadding;

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateLargeTitle();

    /// <summary>Collapses the header navbar's large title by how far the content has scrolled.</summary>
    private void UpdateLargeTitle()
    {
        if (Header is Navbar { LargeTitle: true } navbar && _scroller is not null && IsScrollable)
        {
            navbar.CollapseProgress = _scroller.Offset.Y / Navbar.LargeTitleExtent;
        }
    }
}
