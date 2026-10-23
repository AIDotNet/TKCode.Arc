using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The quiet pill that shows the selected model and its effort and asks its owner to open the model menu: mark, name,
/// effort and a chevron in a 40px bordered pill. It opens nothing itself: a primary-button press raises
/// <see cref="OpenRequested"/> with <see cref="ModelMenuOpenFocus.Surface"/> (on press, not release), and Enter, Space or
/// ArrowDown raise it with <see cref="ModelMenuOpenFocus.Selected"/>; the owner opens or closes its menu and sets
/// <see cref="IsOpen"/> while it is open. The pill never clicks (<c>Click</c> and <c>Command</c> do not fire).
/// <code>
/// &lt;arc:ModelPill Model="{Binding Selected}" Effort="{Binding Effort}" IsOpen="{Binding MenuOpen}"
///                OpenRequested="OnOpenRequested" /&gt;
/// </code>
/// A new model crossfades the mark (out 100ms, in 160ms, no movement); a new name or effort crossfades the text, and the
/// pill's width follows the new text on a critically damped spring, revealing it as the pill grows. When its slot is too
/// narrow only the text is clipped (the mark and the chevron stay). Hover and the open menu tint the pill; the chevron
/// turns over on Arc's CSS spring. Reduced motion crossfades in 120ms and jumps the width; Off changes everything at once.
/// <para>
/// <see cref="Appearance"/> <see cref="ModelPillAppearance.Toolbar"/> is the chip of a composer toolbar: 36px, no border,
/// clear until hovered (text color at 6%) or open (10%), the short name and the effort 6px apart. A narrow composer sets
/// <see cref="ShowEffort"/> false and, narrower still, <see cref="ShowText"/> false (only the mark and the chevron stay,
/// pseudo-class <c>:compact</c>); those switches and the appearance are layout choices, so they change the pill at once
/// instead of crossfading and springing.
/// </para>
/// </summary>
[TemplatePart(MarkPart, typeof(ArcContentSwap))]
[TemplatePart(TextPart, typeof(ArcContentSwap))]
[TemplatePart(TextSizePart, typeof(SpringSizeDecorator))]
[PseudoClasses(OpenPseudoClass, HoverPseudoClass, EmptyPseudoClass, CompactPseudoClass)]
public class ModelPill : AvaloniaButton
{
    public const string MarkPart = "PART_Mark";
    public const string TextPart = "PART_Text";
    public const string TextSizePart = "PART_TextSize";

    public const string OpenPseudoClass = ":open";
    public const string HoverPseudoClass = ":hover";
    public const string EmptyPseudoClass = ":empty";
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>The model the pill shows; null shows <see cref="EmptyText"/>.</summary>
    public static readonly StyledProperty<AiModel?> ModelProperty =
        AvaloniaProperty.Register<ModelPill, AiModel?>(nameof(Model));

    /// <summary>The effort shown after the name when the model offers effort levels; null shows the name only.</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<ModelPill, ModelEffort?>(nameof(Effort));

    /// <summary>The providers that supply the mark of a model without its own (looked up by <see cref="AiModel.ProviderId"/>).</summary>
    public static readonly StyledProperty<IEnumerable<AiModelProvider>?> ProvidersProperty =
        AvaloniaProperty.Register<ModelPill, IEnumerable<AiModelProvider>?>(nameof(Providers));

    /// <summary>Set by the owner while its menu is open: the pill keeps its hover look and the chevron points up.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModelPill, bool>(nameof(IsOpen));

    /// <summary>Whether the effort follows the name (default true); false shows the name only.</summary>
    public static readonly StyledProperty<bool> ShowEffortProperty =
        AvaloniaProperty.Register<ModelPill, bool>(nameof(ShowEffort), true);

    /// <summary>The muted text shown when no model is selected. Default "Choose a model".</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<ModelPill, string>(nameof(EmptyText), "Choose a model");

    /// <summary>The effort names and the accessible names, with English defaults.</summary>
    public static readonly StyledProperty<ModelPillLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelPill, ModelPillLabels>(nameof(Labels), new ModelPillLabels());

    /// <summary>
    /// The standalone pill (<see cref="ModelPillAppearance.Default"/>), the borderless 36px chip of a composer toolbar
    /// (<see cref="ModelPillAppearance.Toolbar"/>, short name) or the 34px button of a chat header
    /// (<see cref="ModelPillAppearance.Header"/>). Changing it restyles the pill at once.
    /// </summary>
    public static readonly StyledProperty<ModelPillAppearance> AppearanceProperty =
        AvaloniaProperty.Register<ModelPill, ModelPillAppearance>(nameof(Appearance));

    /// <summary>
    /// Whether the name (and effort) show (default true). False leaves only the mark and the chevron (pseudo-class
    /// <c>:compact</c>), for a narrow composer; the accessible name still carries the model and the effort.
    /// </summary>
    public static readonly StyledProperty<bool> ShowTextProperty =
        AvaloniaProperty.Register<ModelPill, bool>(nameof(ShowText), true);

    /// <summary>Size of the model mark (from the token: 16).</summary>
    internal static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<ModelPill, double>(nameof(MarkSize), 16);

    /// <summary>Space between the name and the effort (from the token: 7 standalone, 6 in a toolbar).</summary>
    internal static readonly StyledProperty<double> EffortGapProperty =
        AvaloniaProperty.Register<ModelPill, double>(nameof(EffortGap), 7);

    /// <summary>
    /// Raised when the pill asks its owner to open the menu, or to close it when <see cref="IsOpen"/> is true (the owner
    /// toggles): on a primary-button press with <see cref="ModelMenuOpenFocus.Surface"/>, on Enter, Space or ArrowDown
    /// with <see cref="ModelMenuOpenFocus.Selected"/>.
    /// </summary>
    public static readonly RoutedEvent<ModelPillOpenRequestedEventArgs> OpenRequestedEvent =
        RoutedEvent.Register<ModelPill, ModelPillOpenRequestedEventArgs>(nameof(OpenRequested), RoutingStrategies.Bubble);

    /// <summary>The model the pill shows; null shows <see cref="EmptyText"/>.</summary>
    public AiModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>The effort shown after the name when the model offers effort levels; null shows the name only.</summary>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <summary>The providers that supply the mark of a model without its own (looked up by <see cref="AiModel.ProviderId"/>).</summary>
    public IEnumerable<AiModelProvider>? Providers { get => GetValue(ProvidersProperty); set => SetValue(ProvidersProperty, value); }

    /// <summary>Set by the owner while its menu is open: the pill keeps its hover look and the chevron points up.</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>Whether the effort follows the name (default true); false shows the name only.</summary>
    public bool ShowEffort { get => GetValue(ShowEffortProperty); set => SetValue(ShowEffortProperty, value); }

    /// <summary>The muted text shown when no model is selected. Default "Choose a model".</summary>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <summary>The effort names and the accessible names, with English defaults.</summary>
    public ModelPillLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="AppearanceProperty"/>
    public ModelPillAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <inheritdoc cref="ShowTextProperty"/>
    public bool ShowText { get => GetValue(ShowTextProperty); set => SetValue(ShowTextProperty, value); }

    internal double MarkSize { get => GetValue(MarkSizeProperty); set => SetValue(MarkSizeProperty, value); }

    internal double EffortGap { get => GetValue(EffortGapProperty); set => SetValue(EffortGapProperty, value); }

    /// <summary>
    /// Raised when the pill asks its owner to open the menu, or to close it when <see cref="IsOpen"/> is true (the owner
    /// toggles): on a primary-button press with <see cref="ModelMenuOpenFocus.Surface"/>, on Enter, Space or ArrowDown
    /// with <see cref="ModelMenuOpenFocus.Selected"/>.
    /// </summary>
    public event EventHandler<ModelPillOpenRequestedEventArgs>? OpenRequested
    {
        add => AddHandler(OpenRequestedEvent, value);
        remove => RemoveHandler(OpenRequestedEvent, value);
    }

    #endregion

    private ArcContentSwap? _markSwap;
    private ArcContentSwap? _textSwap;
    private SpringSizeDecorator? _textSize;
    private ModelMark? _mark;
    private AiModel? _markModel;
    private ModelPillText? _text;
    private AiModel? _observedModel;
    private INotifyCollectionChanged? _observedProviders;
    private bool _attached;

    protected override Type StyleKeyOverride => typeof(ModelPill);

    /// <summary>The text layer on screen (name and effort, or the empty text).</summary>
    internal ModelPillText? CurrentText => _text;

    /// <summary>The mark on screen, or null when the model has none.</summary>
    internal ModelMark? CurrentMark => _mark;

    private bool IsToolbar => Appearance == ModelPillAppearance.Toolbar;

    #region Content

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _markSwap = e.NameScope.Find<ArcContentSwap>(MarkPart);
        _textSwap = e.NameScope.Find<ArcContentSwap>(TextPart);
        _textSize = e.NameScope.Find<SpringSizeDecorator>(TextSizePart);
        // New parts start with the current content (their first content never animates).
        _mark = null;
        _markModel = null;
        _text = null;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ModelProperty)
        {
            ObserveModel();
            Refresh();
        }
        else if (change.Property == ProvidersProperty)
        {
            ObserveProviders();
            Refresh();
        }
        else if (change.Property == EffortProperty || change.Property == EmptyTextProperty || change.Property == LabelsProperty)
        {
            Refresh();
        }
        else if (change.Property == ShowEffortProperty || change.Property == AppearanceProperty)
        {
            Refresh(layout: true);
        }
        else if (change.Property == ShowTextProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, !change.GetNewValue<bool>());
            Refresh(layout: true);
        }
        else if (change.Property == MarkSizeProperty && _mark is not null)
        {
            _mark.Size = MarkSize;
        }
        else if (change.Property == EffortGapProperty && _text is not null)
        {
            _text.Gap = EffortGap;
        }
        else if (change.Property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(OpenPseudoClass, open);
            if (ControlAutomationPeer.FromElement(this) is ModelPillAutomationPeer peer)
            {
                peer.RaiseExpandCollapseChanged(open);
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            // A pill disabled under the pointer gets no PointerExited.
            PseudoClasses.Set(HoverPseudoClass, false);
        }
    }

    /// <summary>The effort the text shows: only for a model that offers levels, and only when <see cref="ShowEffort"/>.</summary>
    private ModelEffort? ShownEffort(AiModel model) => ShowEffort && model.SupportsEffort ? Effort : null;

    /// <summary>
    /// The effort the accessible name says. A toolbar hides the effort (and the text) only for room, so its name keeps the
    /// effort in use; the standalone pill names what it shows.
    /// </summary>
    private ModelEffort? NamedEffort(AiModel model) => IsToolbar ? (model.SupportsEffort ? Effort : null) : ShownEffort(model);

    /// <summary>
    /// Shows the current model, effort and words. A <paramref name="layout"/> change (appearance, effort or text shown or
    /// hidden) is the composer adapting to its width, not a new choice: the text changes and the width jumps at once.
    /// </summary>
    private void Refresh(bool layout = false)
    {
        var model = Model;
        PseudoClasses.Set(EmptyPseudoClass, model is null);
        UpdateMark(model);
        UpdateText(model, layout);
        AutomationProperties.SetName(this, model is null
            ? Labels.AutomationNameEmpty(EmptyText)
            : NamedEffort(model) is { } effort
                ? Labels.AutomationName(model, effort)
                : Labels.AutomationNameWithoutEffort(model));
    }

    /// <summary>
    /// A different model brings a new mark layer (the swap crossfades it); the same model with a new mark or color (its
    /// provider arrived later, an edited mark) updates the mark in place.
    /// </summary>
    private void UpdateMark(AiModel? model)
    {
        if (_markSwap is null)
        {
            return;
        }
        var mark = ModelMark.Resolve(model, Providers, out var color);
        _markSwap.IsVisible = mark is not null;
        if (ReferenceEquals(_markModel, model))
        {
            if (_mark is not null)
            {
                _mark.Mark = mark;
                _mark.MarkColor = color;
            }
            else if (mark is not null)
            {
                _mark = new ModelMark { Mark = mark, MarkColor = color, Size = MarkSize };
                ReplaceWithoutMotion(_markSwap, _mark);
            }
            return;
        }
        _markModel = model;
        _mark = mark is null ? null : new ModelMark { Mark = mark, MarkColor = color, Size = MarkSize };
        _markSwap.Content = _mark;
    }

    private static void ReplaceWithoutMotion(ArcContentSwap swap, object? content)
    {
        // SetCurrentValue keeps the template's Mode the value a theme can still change.
        var mode = swap.Mode;
        swap.SetCurrentValue(ArcContentSwap.ModeProperty, ContentSwapMode.None);
        swap.Content = content;
        swap.SetCurrentValue(ArcContentSwap.ModeProperty, mode);
    }

    /// <summary>
    /// The text layer changes (and crossfades) only when the words on screen change; a <paramref name="layout"/> change
    /// puts a fresh layer in place without motion (also the same words: a fade still running while the text was hidden
    /// must not show up) and jumps the width.
    /// </summary>
    private void UpdateText(AiModel? model, bool layout)
    {
        if (_textSwap is null)
        {
            return;
        }
        var empty = model is null;
        var name = model is null ? EmptyText : IsToolbar ? model.DisplayName : model.Name;
        var effort = model is not null && ShownEffort(model) is { } level ? Labels.EffortName(level) : null;
        var same = _text is not null && _text.Text == name && _text.Effort == effort && _text.IsPlaceholder == empty;
        if (same && !layout)
        {
            return;
        }
        _text = new ModelPillText { Text = name, Effort = effort, IsPlaceholder = empty, Gap = EffortGap, IsRegular = Appearance == ModelPillAppearance.Header };
        if (layout)
        {
            ReplaceWithoutMotion(_textSwap, _text);
            JumpTextWidth();
        }
        else
        {
            _textSwap.Content = _text;
        }
    }

    /// <summary>The next measure of the text region jumps to its new width instead of springing.</summary>
    private void JumpTextWidth() => (_textSize as ModelPillTextSize)?.JumpOnNextMeasure();

    #endregion

    #region Data observation

    // The model and the provider list are app data that can outlive the pill: they are followed only while the pill is
    // in a window, and caught up with when it comes back.

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveModel();
        ObserveProviders();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveModel();
        ObserveProviders();
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    private void ObserveModel()
    {
        var next = _attached ? Model : null;
        if (ReferenceEquals(next, _observedModel))
        {
            return;
        }
        if (_observedModel is not null)
        {
            _observedModel.PropertyChanged -= OnModelPropertyChanged;
        }
        _observedModel = next;
        if (next is not null)
        {
            next.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void ObserveProviders()
    {
        var next = _attached ? Providers as INotifyCollectionChanged : null;
        if (ReferenceEquals(next, _observedProviders))
        {
            return;
        }
        if (_observedProviders is not null)
        {
            _observedProviders.CollectionChanged -= OnProvidersChanged;
        }
        _observedProviders = next;
        if (next is not null)
        {
            next.CollectionChanged += OnProvidersChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiModel.NameProperty || e.Property == AiModel.ShortNameProperty || e.Property == AiModel.MarkProperty ||
            e.Property == AiModel.MarkColorProperty || e.Property == AiModel.ProviderIdProperty || e.Property == AiModel.EffortLevelsProperty)
        {
            Refresh();
        }
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    #endregion

    #region Input

    /// <summary>Asks the owner to open (or close) the menu; a disabled pill asks nothing.</summary>
    internal void RequestOpen(ModelMenuOpenFocus focus)
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }
        RaiseEvent(new ModelPillOpenRequestedEventArgs(OpenRequestedEvent, this, focus));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        // Arc opens on press, not release, for mouse, pen and touch alike, but not with Ctrl held (a context click on
        // macOS). Button's own press handling is skipped: it would show the pressed state and click on release.
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                e.Handled = true;
                RequestOpen(ModelMenuOpenFocus.Surface);
            }
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Enter, Space and ArrowDown open with the selected model highlighted; ArrowUp does nothing. Shortcuts with
        // Ctrl, Alt or the platform key pass through to the app.
        if (!e.Handled && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) == 0 &&
            e.Key is Key.Enter or Key.Space or Key.Down)
        {
            e.Handled = true;
            RequestOpen(ModelMenuOpenFocus.Selected);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // Space already asked on its way down; Button would click again on the way up.
        if (e.Key == Key.Space)
        {
            e.Handled = true;
            return;
        }
        base.OnKeyUp(e);
    }

    /// <summary>The pill never clicks: <see cref="OpenRequested"/> replaces <c>Click</c> and <c>Command</c>.</summary>
    protected override void OnClick()
    {
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover is (hover: hover) and (pointer: fine): touch never leaves a hover look behind.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ModelPillAutomationPeer(this);

    /// <summary>
    /// A button that expands a menu (<c>aria-haspopup="menu"</c>, <c>aria-expanded</c>): its name carries the model and the
    /// effort, so the mark and the text layers are not exposed. Invoke, Expand and Collapse ask the owner like a key does.
    /// </summary>
    private sealed class ModelPillAutomationPeer(ModelPill owner) : ButtonAutomationPeer(owner), IInvokeProvider, IExpandCollapseProvider
    {
        private ModelPill Pill => (ModelPill)Owner;

        void IInvokeProvider.Invoke()
        {
            EnsureEnabled();
            Pill.RequestOpen(ModelMenuOpenFocus.Selected);
        }

        public ExpandCollapseState ExpandCollapseState => Pill.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand()
        {
            EnsureEnabled();
            if (!Pill.IsOpen)
            {
                Pill.RequestOpen(ModelMenuOpenFocus.Selected);
            }
        }

        public void Collapse()
        {
            EnsureEnabled();
            if (Pill.IsOpen)
            {
                Pill.RequestOpen(ModelMenuOpenFocus.Selected);
            }
        }

        public void RaiseExpandCollapseChanged(bool open) =>
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// The text region of a <see cref="ModelPill"/>: Core's width spring, plus a one-off jump. A layout switch (appearance,
/// effort or text shown or hidden) is not a new choice, so the region lands on the new width at once. The spring jumps
/// whenever its motion level is not Full, so every measure of the next layout pass that measures the region runs with the
/// level held at Off (the new text can reach the region in a second measure of that pass, once its own measure caught
/// up). A hidden region keeps the request until the pass in which it is measured again (when it shows).
/// </summary>
internal sealed class ModelPillTextSize : SpringSizeDecorator
{
    private bool _jump;
    private bool _waitingForPass;

    // Styles written for the Core decorator (`arc|SpringSizeDecorator#PART_TextSize`) keep matching.
    protected override Type StyleKeyOverride => typeof(SpringSizeDecorator);

    public void JumpOnNextMeasure()
    {
        _jump = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_jump)
        {
            return base.MeasureOverride(availableSize);
        }
        if (!_waitingForPass)
        {
            _waitingForPass = true;
            LayoutUpdated += OnLayoutUpdated;
        }
        SetValue(ArcMotion.LevelProperty, ArcMotionLevel.Off);
        try
        {
            return base.MeasureOverride(availableSize);
        }
        finally
        {
            ClearValue(ArcMotion.LevelProperty);
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // The pass that measured the region (and its new content) is over: later changes spring again.
        LayoutUpdated -= OnLayoutUpdated;
        _waitingForPass = false;
        _jump = false;
    }
}

/// <summary>
/// One text layer of a <see cref="ModelPill"/>: the model name and, <see cref="Gap"/> after it on the same baseline (7px,
/// 6 in a toolbar), the muted effort; or the muted empty text. A new layer is made for every change so the pill can
/// crossfade the old one out.
/// </summary>
[PseudoClasses(PlaceholderPseudoClass, RegularPseudoClass)]
internal sealed class ModelPillText : TemplatedControl
{
    public const string PlaceholderPseudoClass = ":placeholder";

    /// <summary>The name in regular weight (a chat header's pill) instead of medium.</summary>
    public const string RegularPseudoClass = ":regular";

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ModelPillText, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string?> EffortProperty =
        AvaloniaProperty.Register<ModelPillText, string?>(nameof(Effort));

    public static readonly StyledProperty<bool> IsPlaceholderProperty =
        AvaloniaProperty.Register<ModelPillText, bool>(nameof(IsPlaceholder));

    public static readonly StyledProperty<bool> IsRegularProperty =
        AvaloniaProperty.Register<ModelPillText, bool>(nameof(IsRegular));

    /// <summary>Space between the name and the effort (the pill passes its token value).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ModelPillText, double>(nameof(Gap), 7);

    static ModelPillText()
    {
        FocusableProperty.OverrideDefaultValue<ModelPillText>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<ModelPillText>(false);
    }

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    public bool IsPlaceholder { get => GetValue(IsPlaceholderProperty); set => SetValue(IsPlaceholderProperty, value); }

    /// <summary>Whether the name is in regular weight.</summary>
    public bool IsRegular { get => GetValue(IsRegularProperty); set => SetValue(IsRegularProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlaceholderProperty)
        {
            PseudoClasses.Set(PlaceholderPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == IsRegularProperty)
        {
            PseudoClasses.Set(RegularPseudoClass, change.GetNewValue<bool>());
        }
    }
}
