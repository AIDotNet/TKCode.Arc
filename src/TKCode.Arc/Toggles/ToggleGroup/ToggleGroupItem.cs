// Ported from Arc Toggle group (MIT, Copyright (c) 2026 Elia Kuratli): the option's states and its activation rules.
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One option of a <see cref="ToggleGroup"/>: an optional icon and the option's content. The group creates one for each
/// option. For a <see cref="ToggleGroupOption"/> the item takes its content, icon, disabled state, accessible name and
/// tooltip from the option; for any other item the content is the group's <c>ItemTemplate</c>, and a style on
/// <c>arc|ToggleGroupItem</c> (or the group's <c>ItemContainerTheme</c>) can bind <see cref="Icon"/>, <c>IsEnabled</c>,
/// <c>AutomationProperties.Name</c> and <c>arc:Tooltip.Tip</c> to the item. A click, Space and Enter ask the group to
/// toggle it; the shared item base would only select, which cannot press several options or turn one off. The item box
/// never scales, so neighbours stay put; only its content gives a small press.
/// </summary>
[PseudoClasses(SmallPseudoClass, MultiplePseudoClass, HoveringPseudoClass, JoinStartPseudoClass, JoinEndPseudoClass)]
public sealed class ToggleGroupItem : TogglesItem
{
    /// <summary>Set while the group's <see cref="ToggleGroup.Size"/> is Small.</summary>
    public const string SmallPseudoClass = ":small";

    /// <summary>Set while the group's <see cref="ToggleGroup.Mode"/> is Multiple.</summary>
    public const string MultiplePseudoClass = ":multiple";

    /// <summary>A pointer that can hover is over the option; touch never sets it (Arc's hover media query).</summary>
    public const string HoveringPseudoClass = ":hovering";

    /// <summary>Multiple mode: the option and the one before it are both pressed, so their fills join.</summary>
    public const string JoinStartPseudoClass = ":join-start";

    /// <summary>Multiple mode: the option and the one after it are both pressed, so their fills join.</summary>
    public const string JoinEndPseudoClass = ":join-end";

    /// <summary>A 24×24 stroke icon (for example an <see cref="ArcIcons"/> member) drawn before the content.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ToggleGroupItem, Geometry?>(nameof(Icon));

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public const string ContentPart = "PART_Content";

    // Arc's press, used until the item's tokens are found (an item outside a themed tree).
    private static readonly TimeSpan s_pressDown = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan s_pressUp = TimeSpan.FromMilliseconds(320);
    private const double PressedScale = 0.92;

    private IPointer? _press;
    private Control? _content;
    private ScaleTransform? _contentScale;
    private MotionValue<double>? _pressScale;

    private ToggleGroup? Group => Owner as ToggleGroup;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _content = e.NameScope.Find<Control>(ContentPart);
        if (_content is not null && _contentScale is not null)
        {
            _content.RenderTransform = _contentScale;
        }
    }

    /// <summary>
    /// The content's press. Code, not the theme: it goes down in 90ms and comes back in 320ms, and a ControlTheme can only
    /// change the timing by swapping <c>Transitions</c> per state, which Avalonia may apply after the value it should time.
    /// </summary>
    private void AnimatePress(bool down)
    {
        if (_content is not { } content)
        {
            return;
        }
        if (_pressScale is null)
        {
            if (!down || !ArcMotion.IsFull(this))
            {
                return;
            }
            _contentScale = new ScaleTransform(1, 1);
            content.RenderTransform = _contentScale;
            _pressScale = new MotionValue<double>(this, 1, MotionVectors.Double);
            _pressScale.Changed += scale =>
            {
                _contentScale.ScaleX = scale;
                _contentScale.ScaleY = scale;
            };
        }
        if (!ArcMotion.IsFull(this))
        {
            _pressScale.Jump(1);
        }
        else if (down)
        {
            var scale = Token(ToggleGroupItemTokenKind.PressScale, PressedScale);
            _ = _pressScale.AnimateTo(scale, MotionSpec.Tween(Token(ToggleGroupItemTokenKind.PressDownDuration, s_pressDown), ArcEasings.Standard));
        }
        else
        {
            _ = _pressScale.AnimateTo(1, MotionSpec.Tween(Token(ToggleGroupItemTokenKind.PressUpDuration, s_pressUp), ArcEasings.CssSpring));
        }
    }

    /// <summary>The value of one of the item's tokens, read when a press starts or ends.</summary>
    private T Token<T>(ToggleGroupItemTokenKind kind, T fallback)
        => this.TryFindResource(ToggleGroupItemTokens.Key(kind), ActualThemeVariant, out var value) && value is T typed ? typed : fallback;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled)
        {
            _press = null;
            AnimatePress(down: false);
        }
    }

    private protected override (object Margin, object Radius)? FocusRingKeys =>
        (ToggleGroupTokens.Key(ToggleGroupTokenKind.FocusRingMargin), ToggleGroupTokens.Key(ToggleGroupTokenKind.FocusRingRadius));

    internal void SetLook(bool small, bool multiple)
    {
        PseudoClasses.Set(SmallPseudoClass, small);
        PseudoClasses.Set(MultiplePseudoClass, multiple);
    }

    internal void SetJoins(bool start, bool end)
    {
        PseudoClasses.Set(JoinStartPseudoClass, start);
        PseudoClasses.Set(JoinEndPseudoClass, end);
    }

    private protected override void OnIsSelectedChanged() => Group?.OnItemPressedChanged();

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoveringPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoveringPseudoClass, false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (PseudoClasses.Contains(PressedPseudoClass))
        {
            _press = e.Pointer;
            AnimatePress(down: true);
        }
    }

    /// <summary>Replaces the base's answer to a click, which would only select. Losing the capture ends the press there.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_press is null || e.Pointer != _press)
        {
            return;
        }
        _press = null;
        var inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
        e.Pointer.Capture(null);
        if (inside)
        {
            Group?.Toggle(this);
        }
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _press = null;
        AnimatePress(down: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && (e.Key == Key.Space || e.Key == Key.Enter) && IsEffectivelyEnabled)
        {
            Group?.Toggle(this);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => Group?.Mode == ToggleGroupMode.Multiple ? new PressedPeer(this) : new RadioPeer(this);

    /// <summary>Single mode: a radio that stays on, like Arc's role="radio".</summary>
    private sealed class RadioPeer : ContentControlAutomationPeer, ISelectionItemProvider
    {
        public RadioPeer(ToggleGroupItem owner) : base(owner)
        {
        }

        private ToggleGroupItem Item => (ToggleGroupItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.RadioButton;

        public bool IsSelected => Item.IsSelected;

        public ISelectionProvider? SelectionContainer =>
            Item.Group is { } group ? CreatePeerForElement(group).GetProvider<ISelectionProvider>() : null;

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            // A radio group cannot be emptied by its options.
        }

        public void Select() => Item.Group?.Toggle(Item);
    }

    /// <summary>Multiple mode: a button with a pressed state, like Arc's aria-pressed.</summary>
    private sealed class PressedPeer : ContentControlAutomationPeer, IToggleProvider
    {
        public PressedPeer(ToggleGroupItem owner) : base(owner)
        {
        }

        private ToggleGroupItem Item => (ToggleGroupItem)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public ToggleState ToggleState => Item.IsSelected ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Item.Group?.Toggle(Item);
    }
}
