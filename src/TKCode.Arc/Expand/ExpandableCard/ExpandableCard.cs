using TKCode.Arc.Motion;
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
/// Arc expandable card: a summary card whose header opens a details panel below it. Opening is one morph: width and
/// height grow on the same smooth spring (real size, never a scale, so text and corners never stretch) and the
/// chevron turns with it; the details fade in once the box has made room, and on close they fade first and the box
/// follows 60ms later. A second click mid-flight reverses from the current size and velocity. The summary line rolls
/// only the words that changed. Escape closes the card from anywhere inside it and returns focus to the header.
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[TemplatePart(PanelPart, typeof(Control))]
[TemplatePart(DetailsPart, typeof(Control))]
[TemplatePart(ChevronPart, typeof(Control))]
[PseudoClasses(ExpandedPseudoClass)]
public class ExpandableCard : HeaderedContentControl
{
    public const string CardPart = "PART_Card";
    public const string TriggerPart = "PART_Trigger";
    public const string PanelPart = "PART_Panel";
    public const string DetailsPart = "PART_Details";
    public const string ChevronPart = "PART_Chevron";
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>A close that reverses an open still in flight skips the hold (Arc: 450ms).</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(450);

    /// <summary>On close the details fade first, then the box starts to shrink (Arc: 60ms).</summary>
    private static readonly TimeSpan CloseHold = TimeSpan.FromMilliseconds(60);

    /// <summary>Summary line under the title; changed words roll in.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ExpandableCard, string?>(nameof(Description));

    /// <summary>Whether the details are open. Two-way.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandableCard, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Width cap while collapsed, in px; NaN fills the container (Arc width).</summary>
    public static readonly StyledProperty<double> CollapsedWidthProperty =
        AvaloniaProperty.Register<ExpandableCard, double>(nameof(CollapsedWidth), double.NaN);

    /// <summary>Width cap while open, so the card can grow sideways; NaN uses <see cref="CollapsedWidth"/> (Arc expandedWidth).</summary>
    public static readonly StyledProperty<double> ExpandedWidthProperty =
        AvaloniaProperty.Register<ExpandableCard, double>(nameof(ExpandedWidth), double.NaN);

    public static readonly RoutedEvent<RoutedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ExpandableCard, RoutedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="CollapsedWidthProperty"/>
    public double CollapsedWidth { get => GetValue(CollapsedWidthProperty); set => SetValue(CollapsedWidthProperty, value); }

    /// <inheritdoc cref="ExpandedWidthProperty"/>
    public double ExpandedWidth { get => GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }

    /// <summary>Raised after the card opens or closes.</summary>
    public event EventHandler<RoutedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private Control? _card;
    private ExpandableCardTrigger? _trigger;
    private ExpandableCardPanel? _panel;
    private Control? _details;
    private RotateTransform? _chevronRotate;
    private MotionValue<double>? _width;
    private MotionValue<double>? _rotation;
    private MotionValue<double>? _fade;
    private double _room = double.NaN;
    private double _widthTarget = double.NaN;
    private bool _morphPending;
    private DateTime _lastToggle = DateTime.MinValue;
    private bool _hold = true;

    static ExpandableCard()
    {
        FocusableProperty.OverrideDefaultValue<ExpandableCard>(false);
        AffectsMeasure<ExpandableCard>(CollapsedWidthProperty, ExpandedWidthProperty);
        FocusModality.Watch();
    }

    /// <summary>Current (animated) card width (tests).</summary>
    internal double CardWidth => _width?.Value ?? double.NaN;

    /// <summary>Current details opacity (tests).</summary>
    internal double DetailsOpacity => _fade?.Value ?? (IsExpanded ? 1 : 0);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        base.OnApplyTemplate(e);
        _card = e.NameScope.Find<Control>(CardPart);
        _trigger = e.NameScope.Find<ExpandableCardTrigger>(TriggerPart);
        _panel = e.NameScope.Find<ExpandableCardPanel>(PanelPart);
        _details = e.NameScope.Find<Control>(DetailsPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.Card = this;
        }
        if (e.NameScope.Find<Control>(ChevronPart) is { } chevron)
        {
            _chevronRotate = new RotateTransform(IsExpanded ? 180 : 0);
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _chevronRotate;
        }
        _rotation = null;
        _fade = null;
        _panel?.SetExpanded(IsExpanded, MotionSpec.Instant);
        ApplyDetails(animate: false);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && IsExpanded)
        {
            e.Handled = true;
            IsExpanded = false;
            _trigger?.Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            var now = DateTime.UtcNow;
            _hold = now - _lastToggle > SettleTime;
            _lastToggle = now;
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            Morph(expanded);
            _trigger?.RaiseExpandCollapseChanged(!expanded, expanded);
            RaiseEvent(new RoutedEventArgs(ExpandedChangedEvent));
        }
        else if (change.Property == HeaderProperty && change.NewValue is string title)
        {
            AutomationProperties.SetName(this, title);
        }
    }

    private bool IsFullMotion => IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    /// <summary>
    /// The morph spring (smooth, no overshoot) shared by the width, the height and the chevron; on a close that
    /// does not reverse an open in flight, the box waits for the details to fade first.
    /// </summary>
    private MotionSpec BoxSpec(bool expanded) => !IsFullMotion
        ? MotionSpec.Instant
        : expanded || !_hold ? MotionSpec.Spring(ArcSpring.Smooth) : MotionSpec.Spring(ArcSpring.Smooth, CloseHold);

    private void Morph(bool expanded)
    {
        if (!expanded && _details is { IsKeyboardFocusWithin: true })
        {
            // Closed details are inert: focus never stays inside them (the keyboard ring follows keys only).
            _trigger?.Focus(FocusModality.For(this));
        }
        var spec = BoxSpec(expanded);
        _panel?.SetExpanded(expanded, spec);
        _morphPending = true;
        InvalidateMeasure();
        if (_chevronRotate is not null)
        {
            _rotation ??= CreateRotation();
            _ = _rotation.AnimateTo(expanded ? 180 : 0, spec);
        }
        ApplyDetails(animate: true);
    }

    private MotionValue<double> CreateRotation()
    {
        var value = new MotionValue<double>(this, _chevronRotate!.Angle, MotionVectors.Double);
        value.Changed += angle => _chevronRotate!.Angle = angle;
        return value;
    }

    /// <summary>Details fade in once the box has made room (standard, 120ms delay) and fade out first on close (100ms).</summary>
    private void ApplyDetails(bool animate)
    {
        if (_details is null)
        {
            return;
        }
        var expanded = IsExpanded;
        ExpandInert.Apply(_details, !expanded);
        if (_fade is null)
        {
            _fade = new MotionValue<double>(this, expanded ? 1 : 0, MotionVectors.Double);
            _fade.Changed += v => _details.Opacity = Math.Clamp(v, 0, 1);
            _details.Opacity = expanded ? 1 : 0;
        }
        if (!animate || !IsFullMotion)
        {
            _fade.Jump(expanded ? 1 : 0);
            return;
        }
        _ = _fade.AnimateTo(expanded ? 1 : 0, expanded
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard, TimeSpan.FromMilliseconds(120))
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(100), ArcEasings.Standard));
    }

    private static double Cap(double cap, double room) => double.IsNaN(cap) || cap <= 0 ? room : Math.Min(room, cap);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_card is null)
        {
            return base.MeasureOverride(availableSize);
        }
        var room = availableSize.Width;
        if (double.IsInfinity(room))
        {
            // No definite room (for example inside a horizontal stack): the caps decide, else the content does.
            var fallback = Math.Max(Cap(ExpandedWidth, double.MaxValue), Cap(CollapsedWidth, double.MaxValue));
            if (fallback >= double.MaxValue)
            {
                _card.Measure(availableSize);
                fallback = _card.DesiredSize.Width;
            }
            room = fallback;
        }
        var openCap = double.IsNaN(ExpandedWidth) ? CollapsedWidth : ExpandedWidth;
        var target = Cap(IsExpanded ? openCap : CollapsedWidth, room);
        var inner = Cap(openCap, room);
        _width ??= CreateWidth(target);
        var passive = !double.IsNaN(_room) && Math.Abs(_room - room) > 0.5;
        _room = room;
        if (Math.Abs(target - _widthTarget) > 0.01 || double.IsNaN(_widthTarget))
        {
            var first = double.IsNaN(_widthTarget);
            _widthTarget = target;
            if (first || passive || !_morphPending || !IsFullMotion)
            {
                // The first measurement and container resizes land without motion.
                _width.Jump(target);
            }
            else
            {
                _ = _width.AnimateTo(target, BoxSpec(IsExpanded));
            }
        }
        _morphPending = false;
        var width = Math.Max(0, _width.Value);
        // The details are laid out at their final width the whole time, so nothing reflows while the box changes size.
        if (_panel is not null)
        {
            _panel.InnerWidth = Math.Max(0, inner - 2);
        }
        _card.Measure(new Size(width, availableSize.Height));
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(availableSize.Width, room), _card.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_card is null)
        {
            return base.ArrangeOverride(finalSize);
        }
        var width = Math.Min(finalSize.Width, Math.Max(0, _width?.Value ?? finalSize.Width));
        _card.Arrange(new Rect((finalSize.Width - width) / 2, 0, width, _card.DesiredSize.Height));
        return finalSize;
    }

    private MotionValue<double> CreateWidth(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandableCardAutomationPeer(this);

    private sealed class ExpandableCardAutomationPeer : ControlAutomationPeer
    {
        public ExpandableCardAutomationPeer(ExpandableCard owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>
/// The details panel of <see cref="ExpandableCard"/>: its height springs between 0 and the details height on the
/// card's morph spec, and the details are always measured at the open width.
/// </summary>
internal sealed class ExpandableCardPanel : Decorator
{
    private MotionValue<double>? _height;
    private bool _expanded;
    private MotionSpec _pending = MotionSpec.Instant;
    private bool _dirty;
    private double _innerWidth = double.NaN;

    public double InnerWidth
    {
        get => _innerWidth;
        set
        {
            if (Math.Abs(_innerWidth - value) > 0.01 || double.IsNaN(_innerWidth))
            {
                _innerWidth = value;
                InvalidateMeasure();
            }
        }
    }

    public double AnimatedHeight => _height?.Value ?? 0;

    public ExpandableCardPanel()
    {
        ClipToBounds = true;
    }

    public void SetExpanded(bool expanded, MotionSpec spec)
    {
        _expanded = expanded;
        _pending = spec;
        _dirty = true;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        var width = double.IsNaN(_innerWidth) ? availableSize.Width : _innerWidth;
        child.Measure(new Size(width, double.PositiveInfinity));
        var full = child.DesiredSize.Height;
        var target = _expanded ? full : 0;
        if (_height is null)
        {
            _height = new MotionValue<double>(this, target, MotionVectors.Double);
            _height.Changed += _ => InvalidateMeasure();
            _dirty = false;
        }
        if (_dirty)
        {
            _dirty = false;
            _ = _height.AnimateTo(target, _pending);
        }
        else if (Math.Abs(_height.Target - target) > 0.5)
        {
            // The details changed size: follow (with the spring while moving, at once at rest).
            if (_height.IsAnimating)
            {
                _ = _height.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _height.Jump(target);
            }
        }
        var height = Math.Max(0, _height.Value);
        return new Size(Math.Min(availableSize.Width, child.DesiredSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            var width = double.IsNaN(_innerWidth) ? finalSize.Width : _innerWidth;
            child.Arrange(new Rect(0, 0, Math.Max(width, finalSize.Width), child.DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>The header of <see cref="ExpandableCard"/>: the whole header is the trigger.</summary>
internal sealed class ExpandableCardTrigger : AvaloniaButton
{
    public ExpandableCard? Card { get; set; }

    protected override Type StyleKeyOverride => typeof(ExpandableCardTrigger);

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerAutomationPeer(this);

    private sealed class TriggerAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public TriggerAutomationPeer(ExpandableCardTrigger owner) : base(owner)
        {
        }

        private ExpandableCard? Card => ((ExpandableCardTrigger)Owner).Card;

        public ExpandCollapseState ExpandCollapseState => Card?.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Card is { } card)
            {
                card.IsExpanded = true;
            }
        }

        public void Collapse()
        {
            if (Card is { } card)
            {
                card.IsExpanded = false;
            }
        }

        protected override string? GetNameCore() => Card?.Header as string ?? base.GetNameCore();

        protected override string? GetHelpTextCore() => Card?.Description ?? base.GetHelpTextCore();
    }
}
