using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One row of an <see cref="Accordion"/>: <see cref="HeaderedContentControl.Header"/> is the question (the whole row
/// is the trigger) and <see cref="ContentControl.Content"/> the answer. The answer stays mounted while closed so a
/// second click mid-flight reverses from where it is; a closed answer takes no pointer or keyboard focus.
/// </summary>
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(PanelPart, typeof(SpringSizeDecorator))]
[TemplatePart(AnswerPart, typeof(Control))]
[PseudoClasses(ExpandedPseudoClass, LargePseudoClass)]
public class AccordionItem : HeaderedContentControl
{
    public const string TriggerPart = "PART_Trigger";
    public const string PanelPart = "PART_Panel";
    public const string AnswerPart = "PART_Answer";
    public const string ExpandedPseudoClass = ":expanded";
    public const string LargePseudoClass = ":large";

    /// <summary>Whether the answer is open. Two-way.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AccordionItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Raised after the row opens or closes.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<AccordionItem, RoutedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>Raised after the row opens or closes.</summary>
    public event EventHandler<RoutedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private AccordionSize _size;

    internal AccordionSize Size
    {
        get => _size;
        set
        {
            _size = value;
            PseudoClasses.Set(LargePseudoClass, value == AccordionSize.Large);
        }
    }

    private AccordionTrigger? _trigger;
    private Control? _panel;
    private Control? _answer;
    private TranslateTransform? _answerShift;
    private MotionValue<double>? _reveal;
    private MotionValue<double>? _settleY;
    private MotionValue<double>? _settleBlur;

    static AccordionItem()
    {
        FocusableProperty.OverrideDefaultValue<AccordionItem>(false);
        FocusModality.Watch();
    }

    private Accordion? Owner => ItemsControl.ItemsControlFromItemContainer(this) as Accordion;

    /// <summary>Moves keyboard focus to the row trigger.</summary>
    public void FocusTrigger() => _trigger?.Focus(NavigationMethod.Directional);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        base.OnApplyTemplate(e);
        _trigger = e.NameScope.Find<AccordionTrigger>(TriggerPart);
        _panel = e.NameScope.Find<Control>(PanelPart);
        _answer = e.NameScope.Find<Control>(AnswerPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.Row = this;
        }
        if (_answer is not null)
        {
            _answerShift = new TranslateTransform();
            _answer.RenderTransform = _answerShift;
        }
        _reveal = null;
        _settleY = null;
        _settleBlur = null;
        ApplyState(animate: false);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            ApplyState(animate: true);
            _trigger?.RaiseExpandCollapseChanged(!expanded, expanded);
            Owner?.OnItemExpandedChanged(this, expanded);
            RaiseEvent(new RoutedEventArgs(ExpandedChangedEvent));
        }
        else if (change.Property == HeaderProperty && change.NewValue is string header)
        {
            AutomationProperties.SetName(this, header);
        }
    }

    /// <summary>
    /// P6 panel motion: the height lives in the <see cref="SpringSizeDecorator"/> (smooth spring). Opacity opens on
    /// standard + enter and closes on fast + standard; the answer settles 6px down (smooth) out of a 2px blur
    /// (standard + enter). Code-driven because opening and closing use different specs on the same properties.
    /// </summary>
    private void ApplyState(bool animate)
    {
        if (_panel is null || _answer is null || _answerShift is null)
        {
            return;
        }
        var expanded = IsExpanded;
        if (!expanded && _answer.IsKeyboardFocusWithin)
        {
            // A closed answer is out of reach: focus moves back to its question (with the keyboard ring after keys only).
            _trigger?.Focus(FocusModality.For(this));
        }
        ExpandInert.Apply(_answer, !expanded);
        _reveal ??= Create(v => _panel.Opacity = Math.Clamp(v, 0, 1), expanded ? 1 : 0);
        _settleY ??= Create(v => _answerShift.Y = v, expanded ? 0 : -6);
        _settleBlur ??= Create(SetBlur, 0);
        var full = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (!full)
        {
            // Reduced and Off: the same end states in one step.
            _reveal.Jump(expanded ? 1 : 0);
            _settleY.Jump(expanded ? 0 : -6);
            _settleBlur.Jump(0);
            return;
        }
        if (expanded)
        {
            _ = _reveal.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            _ = _settleY.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            if (!_settleBlur.IsAnimating)
            {
                _settleBlur.Jump(2);
            }
            _ = _settleBlur.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            var close = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
            _ = _reveal.AnimateTo(0, close);
            _ = _settleY.AnimateTo(-6, close);
            CloseBlur(close);
        }
    }

    private async void CloseBlur(MotionSpec spec)
    {
        if (_settleBlur is null)
        {
            return;
        }
        if (await _settleBlur.AnimateTo(2, spec))
        {
            // A closed answer is invisible: drop the blur so nothing renders it.
            _settleBlur.Jump(0);
        }
    }

    private void SetBlur(double radius)
    {
        if (_answer is null)
        {
            return;
        }
        if (radius < 0.05)
        {
            _answer.Effect = null;
            return;
        }
        _answer.Effect = ArcBlur.Get(radius);
    }

    private MotionValue<double> Create(Action<double> apply, double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += apply;
        apply(initial);
        return value;
    }
}

/// <summary>The row trigger of an <see cref="AccordionItem"/>: a button that exposes the expand / collapse pattern.</summary>
internal sealed class AccordionTrigger : AvaloniaButton
{
    public AccordionItem? Row { get; set; }

    protected override Type StyleKeyOverride => typeof(AccordionTrigger);

    public AccordionTrigger()
    {
        // Radix places the trigger inside an h3.
        AutomationProperties.SetHeadingLevel(this, 3);
    }

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AccordionTriggerAutomationPeer(this);

    private sealed class AccordionTriggerAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public AccordionTriggerAutomationPeer(AccordionTrigger owner) : base(owner)
        {
        }

        private AccordionItem? Row => ((AccordionTrigger)Owner).Row;

        public ExpandCollapseState ExpandCollapseState => Row?.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Row is { } row)
            {
                row.IsExpanded = true;
            }
        }

        public void Collapse()
        {
            if (Row is { } row)
            {
                row.IsExpanded = false;
            }
        }
    }
}
