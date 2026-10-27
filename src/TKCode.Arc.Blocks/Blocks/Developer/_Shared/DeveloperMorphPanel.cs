using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Why a <see cref="DeveloperMorphPanel"/> closed.</summary>
internal enum DeveloperMorphCloseReason
{
    /// <summary>The × toggle.</summary>
    Toggle,

    /// <summary>Escape inside the panel.</summary>
    Escape,

    /// <summary>The block closed it (Cancel, Done, a successful submit, <see cref="DeveloperMorphPanel.IsOpen"/> set).</summary>
    Programmatic
}

/// <summary>Arguments of <see cref="DeveloperMorphPanel.Closed"/>.</summary>
internal sealed class DeveloperMorphClosedEventArgs(DeveloperMorphCloseReason reason) : EventArgs
{
    /// <summary>What closed the panel.</summary>
    public DeveloperMorphCloseReason Reason { get; } = reason;
}

/// <summary>
/// The 28px round toggle at the left of a <see cref="DeveloperMorphPanel"/>'s face (plus, rotating into a close cross). Its
/// hover fill (Foreground 8%) is mixed at run time and set by the panel.
/// </summary>
internal sealed class DeveloperMorphToggle : AvaloniaButton
{
    /// <summary>Fill while the pointer is over it.</summary>
    public static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<DeveloperMorphToggle, IBrush?>(nameof(HoverBrush));

    /// <inheritdoc cref="HoverBrushProperty"/>
    public IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    protected override Type StyleKeyOverride => typeof(DeveloperMorphToggle);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == HoverBrushProperty)
        {
            Background = IsPointerOver && HoverBrush is { } hover ? hover : Brushes.Transparent;
        }
    }
}

/// <summary>
/// A pill button that unfolds in place into a full-width panel (Arc API keys' "Create key" and Webhooks' "Add endpoint";
/// design reference). It is the whole toolbar cell: <see cref="Leading"/> (the "3 endpoints" count) sits at the left and
/// fades out while open; the pill sits at the right. Opening grows the surface leftward to the full width and down to the
/// face plus the <see cref="Content"/> on one Smooth spring, turns the plus 45° into a close cross (Quick), fades the body
/// in after 140ms and focuses its first field. Closing fades the body out (100ms), folds the surface back and returns the
/// focus to the toggle. Escape inside closes; the whole closed pill is clickable (Arc only reacts on the 28px toggle).
/// Changing <see cref="Content"/> while open crossfades (form out 120ms, reveal in 320ms after 60ms rising 8px) and the
/// height springs to the new content. <see cref="Title"/> changes blur-swap. Reduced motion: sizes jump, fades last
/// 120ms; Off: everything jumps.
/// </summary>
[TemplatePart(LeadingPart, typeof(ContentPresenter))]
[TemplatePart(ShellPart, typeof(DeveloperMorphShell))]
[TemplatePart(TogglePart, typeof(DeveloperMorphToggle))]
[TemplatePart(BodyHostPart, typeof(Control))]
[TemplatePart(BodyPart, typeof(ArcContentSwap))]
[PseudoClasses(OpenPseudoClass)]
internal sealed class DeveloperMorphPanel : TemplatedControl
{
    internal const string LeadingPart = "PART_Leading";
    internal const string ShellPart = "PART_Shell";
    internal const string TogglePart = "PART_Toggle";
    internal const string BodyHostPart = "PART_BodyHost";
    internal const string BodyPart = "PART_Body";
    private const string OpenPseudoClass = ":open";

    /// <summary>Open (panel) or closed (pill). Two-way.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The face title ("Create key", then "Key created"); changes blur-swap.</summary>
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, object?>(nameof(Title));

    /// <summary>Weight of the face title (API keys 500, Webhooks 400).</summary>
    public static readonly StyledProperty<FontWeight> TitleFontWeightProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, FontWeight>(nameof(TitleFontWeight), FontWeight.Medium);

    /// <summary>The body shown while open (a form, then a one-time secret).</summary>
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<DeveloperMorphPanel>();

    /// <summary>Template of <see cref="Content"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<DeveloperMorphPanel>();

    /// <summary>Toolbar content at the left (a count) that fades out and stops taking input while open.</summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, object?>(nameof(Leading));

    /// <summary>Accessible name of the toggle while closed; null uses the title text.</summary>
    public static readonly StyledProperty<string?> OpenNameProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, string?>(nameof(OpenName));

    /// <summary>Accessible name of the toggle while open (default "Close").</summary>
    public static readonly StyledProperty<string> CloseNameProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, string>(nameof(CloseName), "Close");

    /// <summary>The element focused when the panel opens; null focuses the first focusable element of the body.</summary>
    public static readonly StyledProperty<IInputElement?> InitialFocusProperty =
        AvaloniaProperty.Register<DeveloperMorphPanel, IInputElement?>(nameof(InitialFocus));

    private ContentPresenter? _leading;
    private DeveloperMorphShell? _shell;
    private DeveloperMorphToggle? _toggle;
    private Control? _bodyHost;
    private ArcContentSwap? _body;
    private MotionValue<double>? _bodyFade;
    private MotionValue<double>? _leadingFade;
    private DeveloperPalette? _palette;
    private DeveloperMorphCloseReason _closeReason = DeveloperMorphCloseReason.Programmatic;
    private bool _keyboard;
    private bool _shellPressed;
    private bool _applied;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="TitleFontWeightProperty"/>
    public FontWeight TitleFontWeight { get => GetValue(TitleFontWeightProperty); set => SetValue(TitleFontWeightProperty, value); }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <inheritdoc cref="ContentTemplateProperty"/>
    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading { get => GetValue(LeadingProperty); set => SetValue(LeadingProperty, value); }

    /// <inheritdoc cref="OpenNameProperty"/>
    public string? OpenName { get => GetValue(OpenNameProperty); set => SetValue(OpenNameProperty, value); }

    /// <inheritdoc cref="CloseNameProperty"/>
    public string CloseName { get => GetValue(CloseNameProperty); set => SetValue(CloseNameProperty, value); }

    /// <inheritdoc cref="InitialFocusProperty"/>
    public IInputElement? InitialFocus { get => GetValue(InitialFocusProperty); set => SetValue(InitialFocusProperty, value); }

    /// <summary>Spring of the opening morph (default Smooth, Arc k ≈ 151–172).</summary>
    public SpringSpec OpenSpring
    {
        get => _openSpring;
        set
        {
            _openSpring = value;
            if (_shell is not null)
            {
                _shell.OpenSpring = value;
            }
        }
    }

    /// <summary>Spring of the closing morph (default Smooth).</summary>
    public SpringSpec CloseSpring
    {
        get => _closeSpring;
        set
        {
            _closeSpring = value;
            if (_shell is not null)
            {
                _shell.CloseSpring = value;
            }
        }
    }

    private SpringSpec _openSpring = DeveloperSprings.Smooth;
    private SpringSpec _closeSpring = DeveloperSprings.Smooth;

    /// <summary>The toggle (focus it, or read its accessible name).</summary>
    public DeveloperMorphToggle? Toggle => _toggle;

    /// <summary>The surface (its current and target sizes).</summary>
    public DeveloperMorphShell? Shell => _shell;

    /// <summary>
    /// The width the open panel has (or will have when the morph ends): blocks pick their stacked form layout from it
    /// rather than from the moving width (Arc ≤ 560).
    /// </summary>
    public double PanelWidth => _shell?.TargetSize.Width ?? Bounds.Width;

    /// <summary>True while the body is shown (open, or still fading out).</summary>
    public bool IsBodyShown => _bodyHost?.IsVisible == true;

    /// <summary>Raised after the panel opened.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the panel closed, with the reason.</summary>
    public event EventHandler<DeveloperMorphClosedEventArgs>? Closed;

    /// <summary>Opens the panel.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the panel for <paramref name="reason"/>; the focus returns to the toggle when it was inside.</summary>
    public void Close(DeveloperMorphCloseReason reason = DeveloperMorphCloseReason.Programmatic)
    {
        if (!IsOpen)
        {
            return;
        }
        _closeReason = reason;
        SetCurrentValue(IsOpenProperty, false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        if (_shell is not null)
        {
            _shell.PointerPressed -= OnShellPressed;
            _shell.PointerReleased -= OnShellReleased;
            _shell.PointerEntered -= OnShellPointer;
            _shell.PointerExited -= OnShellPointer;
        }
        _leading = e.NameScope.Find<ContentPresenter>(LeadingPart);
        _shell = e.NameScope.Find<DeveloperMorphShell>(ShellPart);
        _toggle = e.NameScope.Find<DeveloperMorphToggle>(TogglePart);
        _bodyHost = e.NameScope.Find<Control>(BodyHostPart);
        _body = e.NameScope.Find<ArcContentSwap>(BodyPart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
        }
        if (_shell is not null)
        {
            _shell.OpenSpring = OpenSpring;
            _shell.CloseSpring = CloseSpring;
            _shell.PointerPressed += OnShellPressed;
            _shell.PointerReleased += OnShellReleased;
            _shell.PointerEntered += OnShellPointer;
            _shell.PointerExited += OnShellPointer;
        }
        _bodyFade = null;
        _leadingFade = null;
        _applied = false;
        ApplyOpen(animate: false);
        UpdateBrushes();
        UpdateToggleName();
    }

    public DeveloperMorphPanel()
    {
        // Remember whether the last interaction was the keyboard: focus moved for it shows a ring, for a pointer it does not.
        AddHandler(KeyDownEvent, (_, _) => _keyboard = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboard = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        ResourcesChanged += OnResourcesChanged;
        _palette = null;
        UpdateBrushes();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeChanged(sender, EventArgs.Empty);

    private void OnThemeChanged(object? sender, EventArgs e) => RefreshTheme();

    private void RefreshTheme()
    {
        _palette = null;
        UpdateBrushes();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            var reason = _closeReason;
            _closeReason = DeveloperMorphCloseReason.Programmatic;
            if (!open)
            {
                // Before the body hides: a hidden focused field would drop the focus to nowhere.
                ReturnFocus(reason);
            }
            ApplyOpen(animate: true);
            UpdateBrushes();
            UpdateToggleName();
            if (open)
            {
                Opened?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Closed?.Invoke(this, new DeveloperMorphClosedEventArgs(reason));
            }
        }
        else if (change.Property == TitleProperty || change.Property == OpenNameProperty || change.Property == CloseNameProperty)
        {
            UpdateToggleName();
        }
        else if (change.Property == ContentProperty && IsOpen && FocusIsInsideOrLost())
        {
            // The focused field leaves with the old content (form → reveal): move on to the new content's first stop.
            FocusFirstLater();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsOpen)
        {
            Close(DeveloperMorphCloseReason.Escape);
            e.Handled = true;
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (IsOpen)
        {
            Close(DeveloperMorphCloseReason.Toggle);
        }
        else
        {
            Open();
        }
    }

    private void OnShellPressed(object? sender, PointerPressedEventArgs e)
    {
        // The toggle handles its own presses; the rest of a closed pill opens it too.
        if (IsOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _shellPressed = true;
        UpdateBrushes();
        e.Handled = true;
    }

    private void OnShellReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_shellPressed)
        {
            return;
        }
        _shellPressed = false;
        UpdateBrushes();
        if (!IsOpen && _shell is { } shell && new Rect(shell.Bounds.Size).Contains(e.GetPosition(shell)))
        {
            Open();
            e.Handled = true;
        }
    }

    private void OnShellPointer(object? sender, PointerEventArgs e)
    {
        if (e.RoutedEvent == PointerExitedEvent)
        {
            _shellPressed = false;
        }
        UpdateBrushes();
    }

    private void UpdateBrushes()
    {
        if (_shell is null || VisualRoot is null)
        {
            return;
        }
        _palette ??= DeveloperPalette.For(this);
        var palette = _palette.Value;
        _shell.Background = IsOpen
            ? palette.PillRest
            : _shellPressed ? palette.PillPressed : _shell.IsPointerOver ? palette.PillHover : palette.PillRest;
        if (_toggle is not null)
        {
            _toggle.HoverBrush = palette.ToggleHover;
        }
    }

    private void UpdateToggleName()
    {
        if (_toggle is null)
        {
            return;
        }
        var name = IsOpen ? CloseName : OpenName ?? (Title as string) ?? Title?.ToString() ?? string.Empty;
        AutomationProperties.SetName(_toggle, name);
    }

    private void ApplyOpen(bool animate)
    {
        var open = IsOpen;
        PseudoClasses.Set(OpenPseudoClass, open);
        if (_shell is null)
        {
            return;
        }
        _shell.IsOpen = open;
        animate &= _applied && IsLoaded;
        _applied = true;
        var level = animate ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;

        if (_bodyHost is { } body)
        {
            _bodyFade ??= CreateFade(body, open ? 1 : 0, OnBodyFaded);
            if (open)
            {
                body.IsVisible = true;
                if (level == ArcMotionLevel.Off)
                {
                    _bodyFade.Jump(1);
                }
                else
                {
                    _ = _bodyFade.AnimateTo(1, level == ArcMotionLevel.Full
                        ? MotionSpec.Tween(DeveloperSprings.BodyIn, ArcEasings.Enter, DeveloperSprings.BodyInDelay)
                        : DeveloperSprings.ReducedFade());
                }
                FocusFirstLater();
            }
            else if (level == ArcMotionLevel.Off)
            {
                _bodyFade.Jump(0);
            }
            else
            {
                _ = _bodyFade.AnimateTo(0, level == ArcMotionLevel.Full
                    ? MotionSpec.Tween(DeveloperSprings.BodyOut, ArcEasings.Standard)
                    : DeveloperSprings.ReducedFade());
            }
        }

        if (_leading is { } leading)
        {
            _leadingFade ??= CreateFade(leading, open ? 0 : 1, OnLeadingFaded);
            leading.IsHitTestVisible = !open;
            if (open)
            {
                if (level == ArcMotionLevel.Off)
                {
                    _leadingFade.Jump(0);
                }
                else
                {
                    _ = _leadingFade.AnimateTo(0, DeveloperSprings.ReducedFade());
                }
            }
            else
            {
                leading.IsVisible = true;
                if (level == ArcMotionLevel.Off)
                {
                    _leadingFade.Jump(1);
                }
                else
                {
                    _ = _leadingFade.AnimateTo(1, level == ArcMotionLevel.Full
                        ? MotionSpec.Tween(DeveloperSprings.Standard, ArcEasings.Standard, DeveloperSprings.LeadingInDelay)
                        : DeveloperSprings.ReducedFade());
                }
            }
        }
    }

    private MotionValue<double> CreateFade(Control target, double initial, Action<double> settled)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        target.Opacity = initial;
        value.Changed += v =>
        {
            target.Opacity = Math.Clamp(v, 0, 1);
            settled(v);
        };
        target.IsVisible = initial > 0;
        return value;
    }

    private void OnBodyFaded(double value)
    {
        // The body unmounts once it has faded out; the surface then folds to the face.
        if (!IsOpen && value <= 0 && _bodyFade?.Target <= 0 && _bodyHost is { IsVisible: true } body)
        {
            body.IsVisible = false;
            _shell?.InvalidateMeasure();
        }
    }

    private void OnLeadingFaded(double value)
    {
        // Hidden like Arc's visibility: hidden + inert once faded, so it takes no focus and no clicks.
        if (IsOpen && value <= 0 && _leadingFade?.Target <= 0 && _leading is { IsVisible: true } leading)
        {
            leading.IsVisible = false;
        }
    }

    private void FocusFirstLater()
    {
        if (VisualRoot is null)
        {
            return;
        }
        // The body content is created by its presenter during the next layout pass.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || VisualRoot is null)
            {
                return;
            }
            var method = _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
            var target = InitialFocus ?? FirstFocusable();
            target?.Focus(method);
        }, DispatcherPriority.Loaded);
    }

    private bool FocusIsInsideOrLost()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused);
    }

    private IInputElement? FirstFocusable()
    {
        // Search the current content (not a previous one still fading out of the body).
        Visual? root = Content as Visual ?? _body;
        if (root is null)
        {
            return null;
        }
        foreach (var visual in root.GetVisualDescendants().Prepend(root))
        {
            if (visual is InputElement { Focusable: true, IsEffectivelyEnabled: true, IsEffectivelyVisible: true } element
                && KeyboardNavigation.GetIsTabStop(element))
            {
                return element;
            }
        }
        return null;
    }

    private void ReturnFocus(DeveloperMorphCloseReason reason)
    {
        if (_toggle is null || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        // Only take the focus back when it was in the panel (or nowhere): a click elsewhere keeps its focus.
        if (!FocusIsInsideOrLost())
        {
            return;
        }
        var method = reason == DeveloperMorphCloseReason.Escape || _keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer;
        _toggle.Focus(method);
    }
}
