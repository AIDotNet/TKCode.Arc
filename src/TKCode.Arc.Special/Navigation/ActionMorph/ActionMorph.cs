using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The look of an <see cref="ActionMorph"/>'s button face.</summary>
public enum ActionMorphVariant
{
    /// <summary>A quiet surface with a border, like a secondary button.</summary>
    Secondary,

    /// <summary>The accent fill, like a primary button. The open panel is always a plain surface.</summary>
    Primary
}

/// <summary>
/// One button that grows into a small inline panel for its action, such as a field to name something or a few options,
/// and shrinks back into the button when the action is done. The surface keeps going while it changes size and shape, so
/// the panel reads as the same object as the button. <see cref="ContentControl.Content"/> is the panel's content.
/// </summary>
/// <remarks>
/// The morph stays in the layout: what is next to it moves while it grows. Escape, the close button and a press outside
/// fold it back; a button inside the panel with <c>arc:ActionMorph.Closes="True"</c> folds it back after its click.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(FacePart, typeof(AvaloniaButton))]
[TemplatePart(FaceHostPart, typeof(PresenceHost))]
[TemplatePart(PanelHostPart, typeof(PresenceHost))]
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[PseudoClasses(ExpandedPseudoClass, PrimaryPseudoClass)]
public class ActionMorph : ContentControl
{
    internal const string RootPart = "PART_Root";
    internal const string FacePart = "PART_Face";
    internal const string FaceHostPart = "PART_FaceHost";
    internal const string PanelHostPart = "PART_PanelHost";
    internal const string ClosePart = "PART_Close";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string PrimaryPseudoClass = ":primary";

    /// <summary>The button's label, also the accessible name of the button and of the open panel.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(Label), "Add");

    /// <summary>A 24 × 24 stroked glyph before the label; null shows the label alone.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ActionMorph, Geometry?>(nameof(Glyph), ArcIcons.Plus);

    /// <summary>Whether the panel is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ActionMorph, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The look of the button face.</summary>
    public static readonly StyledProperty<ActionMorphVariant> VariantProperty =
        AvaloniaProperty.Register<ActionMorph, ActionMorphVariant>(nameof(Variant));

    /// <summary>Whether a press outside the morph folds the panel back (default true).</summary>
    public static readonly StyledProperty<bool> CollapsesOnOutsidePressProperty =
        AvaloniaProperty.Register<ActionMorph, bool>(nameof(CollapsesOnOutsidePress), true);

    /// <summary>Whether the open panel shows a close button at its end (default true).</summary>
    public static readonly StyledProperty<bool> ShowsCloseButtonProperty =
        AvaloniaProperty.Register<ActionMorph, bool>(nameof(ShowsCloseButton), true);

    /// <summary>Accessible name and tooltip of the close button.</summary>
    public static readonly StyledProperty<string> CloseLabelProperty =
        AvaloniaProperty.Register<ActionMorph, string>(nameof(CloseLabel), "Close");

    /// <summary>
    /// Set on a button inside the panel: after its click the panel folds back (a Save or Create button, or an option that
    /// is applied as soon as it is picked).
    /// </summary>
    public static readonly AttachedProperty<bool> ClosesProperty =
        AvaloniaProperty.RegisterAttached<ActionMorph, Control, bool>("Closes");

    /// <summary>Raised when the panel opens. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ExpandedEvent =
        RoutedEvent.Register<ActionMorph, RoutedEventArgs>(nameof(Expanded), RoutingStrategies.Bubble);

    /// <summary>Raised when the panel folds back into the button. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CollapsedEvent =
        RoutedEvent.Register<ActionMorph, RoutedEventArgs>(nameof(Collapsed), RoutingStrategies.Bubble);

    private Panel? _root;
    private AvaloniaButton? _face;
    private PresenceHost? _faceHost;
    private PresenceHost? _panelHost;
    private AvaloniaButton? _close;
    private LazyFocusRing? _focusRing;
    private TopLevel? _pressRoot;
    private int _focusRequest;

    static ActionMorph()
    {
        FocusableProperty.OverrideDefaultValue<ActionMorph>(false);
        SpecialFocusModality.Watch();
    }

    public ActionMorph()
    {
        AddHandler(AvaloniaButton.ClickEvent, OnAnyClick);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="VariantProperty"/>
    public ActionMorphVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="CollapsesOnOutsidePressProperty"/>
    public bool CollapsesOnOutsidePress
    {
        get => GetValue(CollapsesOnOutsidePressProperty);
        set => SetValue(CollapsesOnOutsidePressProperty, value);
    }

    /// <inheritdoc cref="ShowsCloseButtonProperty"/>
    public bool ShowsCloseButton { get => GetValue(ShowsCloseButtonProperty); set => SetValue(ShowsCloseButtonProperty, value); }

    /// <inheritdoc cref="CloseLabelProperty"/>
    public string CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    /// <summary>Gets <see cref="ClosesProperty"/>.</summary>
    public static bool GetCloses(Control element) => element.GetValue(ClosesProperty);

    /// <summary>Sets <see cref="ClosesProperty"/>.</summary>
    public static void SetCloses(Control element, bool value) => element.SetValue(ClosesProperty, value);

    /// <inheritdoc cref="ExpandedEvent"/>
    public event EventHandler<RoutedEventArgs>? Expanded
    {
        add => AddHandler(ExpandedEvent, value);
        remove => RemoveHandler(ExpandedEvent, value);
    }

    /// <inheritdoc cref="CollapsedEvent"/>
    public event EventHandler<RoutedEventArgs>? Collapsed
    {
        add => AddHandler(CollapsedEvent, value);
        remove => RemoveHandler(CollapsedEvent, value);
    }

    /// <summary>The button face (null before the template is applied).</summary>
    internal AvaloniaButton? Face => _face;

    /// <summary>Opens the panel; focus moves to its first focusable element.</summary>
    public void Expand() => SetCurrentValue(IsExpandedProperty, true);

    /// <summary>Folds the panel back into the button; focus inside the panel returns to the button.</summary>
    public void Collapse() => SetCurrentValue(IsExpandedProperty, false);

    /// <summary>Opens the panel when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetCurrentValue(IsExpandedProperty, !IsExpanded);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_face is not null)
        {
            _face.Click -= OnFaceClick;
            _face.GotFocus -= OnFaceFocusChanged;
            _face.LostFocus -= OnFaceFocusChanged;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        _root = e.NameScope.Find<Panel>(RootPart);
        _face = e.NameScope.Find<AvaloniaButton>(FacePart);
        _faceHost = e.NameScope.Find<PresenceHost>(FaceHostPart);
        _panelHost = e.NameScope.Find<PresenceHost>(PanelHostPart);
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        if (_face is not null)
        {
            _face.Click += OnFaceClick;
            _face.GotFocus += OnFaceFocusChanged;
            _face.LostFocus += OnFaceFocusChanged;
            _focusRing = new LazyFocusRing(_face, ActionMorphTokens.Key(ActionMorphTokenKind.FocusRingMargin),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep21));
            _focusRing.Attach(_root);
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        UpdateState();
        UpdateNames();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            OnExpandedChanged(expanded);
        }
        else if (change.Property == VariantProperty)
        {
            PseudoClasses.Set(PrimaryPseudoClass, Variant == ActionMorphVariant.Primary);
        }
        else if (change.Property == LabelProperty || change.Property == CloseLabelProperty)
        {
            UpdateNames();
        }
        else if (change.Property == ShowsCloseButtonProperty && _close is not null)
        {
            _close.IsVisible = ShowsCloseButton;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsExpanded)
        {
            WatchOutsidePresses(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchOutsidePresses(false);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && IsExpanded)
        {
            e.Handled = true;
            CollapseFrom(keyboard: true);
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnExpandedChanged(bool expanded)
    {
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        if (!expanded)
        {
            // Give focus back before the panel goes, or it would fall to the window without a ring.
            ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        }
        UpdateState();
        WatchOutsidePresses(expanded);
        if (expanded)
        {
            FocusPanelSoon();
        }
        RaiseEvent(new RoutedEventArgs(expanded ? ExpandedEvent : CollapsedEvent));
    }

    private void UpdateState()
    {
        var expanded = IsExpanded;
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        PseudoClasses.Set(PrimaryPseudoClass, Variant == ActionMorphVariant.Primary);
        if (_faceHost is not null)
        {
            _faceHost.IsPresent = !expanded;
        }
        if (_panelHost is not null)
        {
            _panelHost.IsPresent = expanded;
        }
        if (_face is not null)
        {
            // The face is only the way in: while the panel is open it is neither a tab stop nor a target.
            _face.IsTabStop = !expanded;
            _face.IsHitTestVisible = !expanded;
            AutomationProperties.SetItemStatus(_face, expanded ? "Expanded" : "Collapsed");
        }
        if (_close is not null)
        {
            _close.IsVisible = ShowsCloseButton;
        }
    }

    private void UpdateNames()
    {
        if (_face is not null)
        {
            AutomationProperties.SetName(_face, Label);
        }
        if (_panelHost is not null)
        {
            AutomationProperties.SetName(_panelHost, Label);
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, CloseLabel);
            Tooltip.SetTip(_close, CloseLabel);
        }
    }

    private void FocusPanelSoon()
    {
        var request = ++_focusRequest;
        var method = SpecialFocusModality.For(this);
        // The panel becomes visible in this pass; focus it once it is laid out.
        Dispatcher.UIThread.Post(() =>
        {
            if (request != _focusRequest || !IsExpanded || _panelHost is null)
            {
                return;
            }
            var target = FocusManager.FindFirstFocusableElement(_panelHost) as Control ?? _close;
            target?.Focus(method);
        }, DispatcherPriority.Loaded);
    }

    private void ReturnFocus(bool keyboard)
    {
        _focusRequest++;
        if (_face is null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return;
        }
        if (_panelHost is not null && (ReferenceEquals(focused, _panelHost) || _panelHost.IsVisualAncestorOf(focused)
                                       || (focused is ILogical logical && _panelHost.IsLogicalAncestorOf(logical))))
        {
            // The button comes back first: it cannot take focus while it is hidden.
            if (_faceHost is not null)
            {
                _faceHost.IsPresent = true;
            }
            _face.IsTabStop = true;
            _face.IsHitTestVisible = true;
            _face.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void CollapseFrom(bool keyboard)
    {
        if (!IsExpanded)
        {
            return;
        }
        ReturnFocus(keyboard);
        Collapse();
    }

    private void OnFaceClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Expand();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        CollapseFrom(SpecialFocusModality.LastWasPointer(this) != true);
    }

    private void OnAnyClick(object? sender, RoutedEventArgs e)
    {
        if (!IsExpanded || e.Source is not Control source)
        {
            return;
        }
        for (Control? c = source; c is not null && !ReferenceEquals(c, this); c = c.Parent as Control)
        {
            if (GetCloses(c))
            {
                CollapseFrom(SpecialFocusModality.LastWasPointer(this) != true);
                return;
            }
        }
    }

    private void OnFaceFocusChanged(object? sender, RoutedEventArgs e) => _focusRing?.Update();

    private void WatchOutsidePresses(bool watch)
    {
        if (_pressRoot is not null)
        {
            _pressRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            _pressRoot = null;
        }
        if (watch && TopLevel.GetTopLevel(this) is { } root)
        {
            _pressRoot = root;
            root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsExpanded || !CollapsesOnOutsidePress || e.Source is not Visual source)
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)
            || (source is ILogical logical && this.IsLogicalAncestorOf(logical)))
        {
            return;
        }
        Collapse();
    }
}
