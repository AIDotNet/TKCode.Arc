using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

/// <summary>
/// One card of a mobile <see cref="RadioCards"/>: an optional <see cref="Icon"/>, the label (the content), an optional
/// <see cref="Description"/> and a radio indicator. The group creates a card for each item; for a
/// <see cref="RadioCardOption"/> the card takes its label, icon, description, enabled and visible state from the option.
/// A finger on it shades it (and, with full motion, presses it in slightly); lifting the finger on it chooses it. It has no
/// hover state.
/// </summary>
[TemplatePart(IconPart, typeof(ContentPresenter))]
[PseudoClasses(SelectedPseudoClass, PressedPseudoClass, IconPseudoClass, NoIndicatorPseudoClass)]
public class RadioCard : ContentControl
{
    /// <summary>The presenter of <see cref="Icon"/>, filled in code (a geometry becomes a stroke glyph).</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Set while the card is the selected one.</summary>
    public const string SelectedPseudoClass = ":selected";

    /// <summary>Set while a finger (or the mouse button) is down on the card.</summary>
    public const string PressedPseudoClass = ":pressed";

    /// <summary>Set while the card shows an <see cref="Icon"/>: the label moves under the icon row.</summary>
    public const string IconPseudoClass = ":icon";

    /// <summary>Set while the group hides the radio indicator (<see cref="RadioCards.ShowIndicator"/> is false).</summary>
    public const string NoIndicatorPseudoClass = ":no-indicator";

    /// <summary>Whether the card is the group's selected card. The group sets it.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<RadioCard>();

    /// <summary>One or two short lines under the label; also the card's accessible help text.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RadioCard, string?>(nameof(Description));

    /// <summary>
    /// Shown above the label: a <see cref="Geometry"/> is drawn as a 22px stroke glyph in the card's foreground; any other
    /// content (a color swatch, a shape) is shown as it is.
    /// </summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RadioCard, object?>(nameof(Icon));

    private ContentPresenter? _iconPresenter;
    private ArcGlyph? _glyph;
    private IPointer? _pressPointer;
    private LazyFocusRing? _focusRing;
    private RadioCardOption? _option;

    static RadioCard()
    {
        FocusableProperty.OverrideDefaultValue<RadioCard>(true);
    }

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RadioCard);

    private SelectingItemsControl? Owner => ItemsControl.ItemsControlFromItemContainer(this) as SelectingItemsControl;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_iconPresenter is not null)
        {
            _iconPresenter.Content = null;
        }
        _iconPresenter = e.NameScope.Find<ContentPresenter>(IconPart);
        UpdateIcon();
        _focusRing ??= new LazyFocusRing(this, RadioCardTokens.Key(RadioCardTokenKind.FocusRingMargin), RadioCardTokens.Key(RadioCardTokenKind.FocusRingRadius));
        _focusRing.Attach(VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
        {
            PseudoClasses.Set(SelectedPseudoClass, IsSelected);
        }
        else if (change.Property == IconProperty)
        {
            UpdateIcon();
        }
        else if (change.Property == DescriptionProperty)
        {
            AutomationProperties.SetHelpText(this, Description);
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            EndPress();
        }
    }

    /// <summary>Shows <see cref="Icon"/>: a geometry in one reused stroke glyph, anything else as it is.</summary>
    private void UpdateIcon()
    {
        PseudoClasses.Set(IconPseudoClass, Icon is not null);
        if (_iconPresenter is null)
        {
            return;
        }
        object? shown = Icon;
        if (shown is Geometry geometry)
        {
            if (_glyph is null)
            {
                _glyph = new ArcGlyph();
                _glyph.Bind(ArcGlyph.SizeProperty, _glyph.GetResourceObservable(RadioCardTokens.Key(RadioCardTokenKind.IconExtent)));
            }
            _glyph.Data = geometry;
            shown = _glyph;
        }
        _iconPresenter.Content = shown;
        _iconPresenter.IsVisible = shown is not null;
    }

    /// <summary>Takes the label, icon, description and states of <paramref name="option"/> and follows its changes.</summary>
    internal void Follow(RadioCardOption? option)
    {
        if (ReferenceEquals(option, _option))
        {
            return;
        }
        if (_option is not null)
        {
            _option.PropertyChanged -= OnOptionChanged;
        }
        _option = option;
        if (option is not null)
        {
            option.PropertyChanged += OnOptionChanged;
            ApplyOption();
        }
    }

    /// <summary>Whether the card shows a <see cref="RadioCardOption"/> (its properties are then the option's).</summary>
    internal bool FollowsOption => _option is not null;

    private void OnOptionChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => ApplyOption();

    private void ApplyOption()
    {
        if (_option is not { } option)
        {
            return;
        }
        Content = option.Label ?? option.Value?.ToString();
        Description = option.Description;
        Icon = option.Icon;
        IsEnabled = option.IsEnabled;
        IsVisible = option.IsVisible;
    }

    /// <summary>Whether the radio indicator shows; the group sets it from <see cref="RadioCards.ShowIndicator"/>.</summary>
    internal bool IsIndicatorShown
    {
        set => PseudoClasses.Set(NoIndicatorPseudoClass, !value);
    }

    /// <summary>Chooses this card through its group (a tap, Space, Enter).</summary>
    internal void Select()
    {
        if (!IsEffectivelyEnabled || Owner is not { } owner)
        {
            return;
        }
        var index = owner.IndexFromContainer(this);
        if (index >= 0)
        {
            owner.SelectedIndex = index;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEffectivelyEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressPointer = e.Pointer;
        e.Pointer.Capture(this);
        PseudoClasses.Set(PressedPseudoClass, true);
        // A tap never shows the focus ring (focus-visible stays off).
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressPointer is null || e.Pointer != _pressPointer)
        {
            return;
        }
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        EndPress();
        e.Pointer.Capture(null);
        if (inside)
        {
            Select();
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // A scroll that takes the finger over cancels the press, so scrolling a page never chooses a card.
        EndPress();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && (e.Key == Key.Space || e.Key == Key.Enter) && IsEffectivelyEnabled)
        {
            Select();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    private void EndPress()
    {
        _pressPointer = null;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RadioCardAutomationPeer(this);

    /// <summary>A card is a radio button for assistive technology: selected or not, and selectable.</summary>
    private sealed class RadioCardAutomationPeer : ContentControlAutomationPeer, ISelectionItemProvider
    {
        public RadioCardAutomationPeer(RadioCard owner) : base(owner)
        {
        }

        private RadioCard Card => (RadioCard)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        public bool IsSelected => Card.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            ItemsControl.ItemsControlFromItemContainer(Card) is { } owner ? CreatePeerForElement(owner).GetProvider<ISelectionProvider>() : null;

        public void AddToSelection() => Card.Select();

        public void RemoveFromSelection()
        {
            // A radio group cannot be emptied by its cards.
        }

        public void Select() => Card.Select();
    }
}
