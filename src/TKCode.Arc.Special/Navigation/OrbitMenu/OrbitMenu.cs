using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Quick actions for one object, laid out on an arc around a round trigger. A press, a drag in an action's direction and
/// a release runs that action; a short tap keeps the arc out so an action can be clicked. The target is picked by the
/// pointer's angle around the trigger, so the pointer need not reach the action itself. A lens slides under the target,
/// actions near the pointer are drawn toward it, and a label pill names the target, gives the cancel hint and confirms
/// the action that ran. The trigger shows an ellipsis at rest, a close mark while the arc is out and a check after an
/// action runs.
/// </summary>
/// <remarks>
/// Items are <see cref="OrbitMenuAction"/> elements, written in XAML or bound through <see cref="ItemsControl.ItemsSource"/>.
/// The menu takes only the trigger's room in the layout: the arc is drawn over what is around it and is not clipped, so
/// aim it at empty space with <see cref="Direction"/> and keep <see cref="Radius"/> plus half an action clear. Angles are
/// in degrees measured clockwise from the right, so -90 faces straight up and 90 straight down.
/// </remarks>
[TemplatePart(TriggerPart, typeof(OrbitMenuTrigger))]
[TemplatePart(UnderPart, typeof(Panel))]
[TemplatePart(OverPart, typeof(Panel))]
[PseudoClasses(OpenPseudoClass, DonePseudoClass)]
public class OrbitMenu : ItemsControl
{
    internal const string TriggerPart = "PART_Trigger";
    internal const string UnderPart = "PART_Under";
    internal const string OverPart = "PART_Over";
    internal const string OpenPseudoClass = ":open";
    internal const string DonePseudoClass = ":done";

    /// <summary>Whether the arc is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<OrbitMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How far each action's center sits from the trigger's center, in px (default 104).</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(Radius), 104);

    /// <summary>How many degrees the arc spans (default 150); 360 spreads the actions evenly round a full ring.</summary>
    public static readonly StyledProperty<double> SpreadProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(Spread), 150);

    /// <summary>The angle the arc faces, in degrees clockwise from the right (default -90: up).</summary>
    public static readonly StyledProperty<double> DirectionProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(Direction), -90);

    /// <summary>Accessible name of the trigger and the menu, also the pill's text whenever the arc is out but no action is aimed at.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<OrbitMenu, string>(nameof(Label), "Actions");

    /// <summary>What the pill says while a drag is away from every action (releasing there fires nothing).</summary>
    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<OrbitMenu, string>(nameof(CancelText), "Release to cancel");

    /// <summary>Whether confirmations are announced to assistive technology through a polite live region (default true).</summary>
    public static readonly StyledProperty<bool> AnnouncesProperty =
        AvaloniaProperty.Register<OrbitMenu, bool>(nameof(Announces), true);

    /// <summary>Raised when an action fires, from a release, a click, the keyboard or assistive technology. Bubbles.</summary>
    public static readonly RoutedEvent<OrbitMenuActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<OrbitMenu, OrbitMenuActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private OrbitMenuTrigger? _trigger;
    private Panel? _under;
    private Panel? _over;
    private OrbitMenuField? _field;
    private OrbitMenuTrack? _track;
    private OrbitMenuLens? _lens;
    private OrbitMenuPill? _pill;
    private TextBlock? _announcer;
    private MotionValue<double>? _trackProgress;
    private MotionValue<double>? _lensAngle;
    private MotionValue<double>? _lensPresence;
    private OrbitMenuAction? _lensFollow;
    private PressSession? _session;
    private Point? _pointer;
    private int _target = -1;
    private bool _keyboardTarget;
    private string? _doneText;
    private DispatcherTimer? _doneTimer;
    private TopLevel? _pressRoot;
    private int _focusRequest;
    private bool _full = true;

    static OrbitMenu()
    {
        FocusableProperty.OverrideDefaultValue<OrbitMenu>(false);
        ClipToBoundsProperty.OverrideDefaultValue<OrbitMenu>(false);
        SpecialFocusModality.Watch();
    }

    public OrbitMenu()
    {
        AddHandler(OrbitMenuAction.ClickEvent, OnActionClick);
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(GotFocusEvent, OnAnyGotFocus, RoutingStrategies.Bubble);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <inheritdoc cref="SpreadProperty"/>
    public double Spread { get => GetValue(SpreadProperty); set => SetValue(SpreadProperty, value); }

    /// <inheritdoc cref="DirectionProperty"/>
    public double Direction { get => GetValue(DirectionProperty); set => SetValue(DirectionProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="CancelTextProperty"/>
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <inheritdoc cref="AnnouncesProperty"/>
    public bool Announces { get => GetValue(AnnouncesProperty); set => SetValue(AnnouncesProperty, value); }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<OrbitMenuActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>The trigger (null before the template is applied).</summary>
    internal OrbitMenuTrigger? Trigger => _trigger;

    internal OrbitMenuLens? Lens => _lens;

    internal OrbitMenuPill? Pill => _pill;

    internal OrbitMenuTrack? Track => _track;

    internal OrbitMenuField? Field => _field;

    /// <summary>The index of the targeted action, or -1.</summary>
    internal int TargetIndex => _target;

    /// <summary>The confirmation on show, or null.</summary>
    internal string? DoneText => _doneText;

    /// <summary>The live region's last announcement.</summary>
    internal string? Announcement => _announcer?.Text;

    /// <summary>Milliseconds on a monotonic clock (tests replace it to time a hold).</summary>
    internal Func<long> Clock { get; set; } = () => Environment.TickCount64;

    /// <summary>Opens the arc.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Folds the arc back into the trigger.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens the arc when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    /// <summary>Where the action at <paramref name="index"/> of <paramref name="count"/> rests, relative to the trigger's center.</summary>
    internal static Point PlaceOnArc(int index, int count, double radius, double direction, double spread)
        => OrbitMenuGeometry.PlaceOnArc(index, count, radius, direction, spread);

    #region Containers

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new OrbitMenuAction();

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<OrbitMenuAction>(item, out recycleKey);

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is OrbitMenuAction action && !ReferenceEquals(container, item) && !action.IsSet(OrbitMenuAction.LabelProperty))
        {
            action.Label = item?.ToString();
        }
    }

    /// <inheritdoc />
    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is OrbitMenuAction action)
        {
            var at = IsOpen ? 1 : 0;
            EnsureMotion(action, at);
            action.Travel!.Jump(at);
            action.Fade!.Jump(at);
            SetShown(action, IsOpen);
        }
        Dispatcher.UIThread.Post(Relayout, DispatcherPriority.Render);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is OrbitMenuAction action)
        {
            action.Travel?.Stop();
            action.Fade?.Stop();
            action.Boost?.Stop();
            action.SetTargeted(false);
            if (ReferenceEquals(_lensFollow, action))
            {
                _lensFollow = null;
            }
        }
        _target = -1;
        Dispatcher.UIThread.Post(Relayout, DispatcherPriority.Render);
    }

    private IEnumerable<OrbitMenuAction> Actions()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            if (ContainerFromIndex(i) is OrbitMenuAction action)
            {
                yield return action;
            }
        }
    }

    private OrbitMenuAction? ActionAt(int index) => index >= 0 ? ContainerFromIndex(index) as OrbitMenuAction : null;

    /// <summary>A folded action is out of sight, out of the tab order and out of reach of the pointer.</summary>
    private static void SetShown(OrbitMenuAction action, bool shown)
    {
        action.IsVisible = shown;
        action.IsHitTestVisible = shown;
    }

    private void EnsureMotion(OrbitMenuAction action, double initial)
    {
        if (action.Travel is not null)
        {
            return;
        }
        action.Travel = new MotionValue<double>(action, initial, MotionVectors.Double);
        action.Fade = new MotionValue<double>(action, initial, MotionVectors.Double);
        action.Boost = new MotionValue<double>(action, 0, MotionVectors.Double);
        action.Travel.Changed += _ => OnActionMoved(action);
        action.Fade.Changed += _ => action.ApplyMotion(_full);
        action.Boost.Changed += _ => action.ApplyMotion(_full);
    }

    private void OnActionMoved(OrbitMenuAction action)
    {
        action.ApplyMotion(_full);
        if (ReferenceEquals(action, _lensFollow))
        {
            ApplyLens();
        }
    }

    #endregion

    #region Template and properties

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.Menu = null;
        }
        _trigger = e.NameScope.Find<OrbitMenuTrigger>(TriggerPart);
        _under = e.NameScope.Find<Panel>(UnderPart);
        _over = e.NameScope.Find<Panel>(OverPart);
        _field = null;
        _track = null;
        _lens = null;
        _pill = null;
        _announcer = null;
        _trackProgress = null;
        _lensAngle = null;
        _lensPresence = null;
        if (_trigger is not null)
        {
            _trigger.Menu = this;
            _trigger.Click += OnTriggerClick;
            _trigger.SetOpen(IsOpen);
        }
        UpdateNames();
        if (IsOpen || _doneText is not null)
        {
            // Opened before the template was in (IsOpen set in XAML): the parts start in the open state, without motion.
            EnsureParts();
            if (_track is not null)
            {
                _track.SetProgress(IsOpen ? 1 : 0);
            }
            if (_field is not null)
            {
                _field.IsHitTestVisible = IsOpen;
            }
            UpdatePill();
        }
    }

    /// <summary>The trigger's own template is in: its mark starts in the right state without a morph.</summary>
    internal void OnTriggerTemplateApplied() => _trigger?.Glyph?.SetState(GlyphState, animate: false);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            OnOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == RadiusProperty || change.Property == SpreadProperty || change.Property == DirectionProperty)
        {
            Relayout();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateNames();
            UpdatePill();
        }
        else if (change.Property == CancelTextProperty)
        {
            UpdatePill();
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !change.GetNewValue<bool>())
        {
            EndSession();
            Close();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Relayout();
        if (IsOpen)
        {
            WatchOutsidePresses(true);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        WatchOutsidePresses(false);
        _doneTimer?.Stop();
    }

    private double Token(OrbitMenuTokenKind kind, double fallback)
        => this.TryFindResource(OrbitMenuTokens.Key(kind), out var value) && value is double d ? d : fallback;

    private TimeSpan TokenTime(OrbitMenuTokenKind kind, TimeSpan fallback)
        => this.TryFindResource(OrbitMenuTokens.Key(kind), out var value) && value is TimeSpan t ? t : fallback;

    private Point Center => new(Bounds.Width / 2, Bounds.Height / 2);

    /// <summary>Room the pill keeps from the trigger and from the actions.</summary>
    private const double PillGap = 8;

    private static Vector Delta(Point to, Point from) => new(to.X - from.X, to.Y - from.Y);

    private void UpdateNames()
    {
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, Label);
            AutomationProperties.SetItemStatus(_trigger, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    /// <summary>Creates the lens, the track, the hover field and the pill the first time they are needed.</summary>
    private void EnsureParts()
    {
        if (_under is null || _over is null || _lens is not null)
        {
            return;
        }
        _field = new OrbitMenuField { Menu = this, IsHitTestVisible = false };
        _track = new OrbitMenuTrack();
        _lens = new OrbitMenuLens();
        _under.Children.Add(_field);
        _under.Children.Add(_track);
        _under.Children.Add(_lens);
        _pill = new OrbitMenuPill
        {
            Spacing = Token(OrbitMenuTokenKind.PillSpacing, 6),
            GlyphSize = Token(OrbitMenuTokenKind.PillGlyphSize, 15)
        };
        _over.Children.Add(_pill);
        Relayout();
    }

    /// <summary>Recomputes each action's place on the arc and everything that hangs off the arc.</summary>
    private void Relayout()
    {
        _full = ArcMotion.IsFull(this);
        var count = ItemCount;
        var radius = Radius;
        var direction = Direction;
        var spread = Spread;
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is OrbitMenuAction action)
            {
                var degrees = OrbitMenuGeometry.AngleOf(i, count, direction, spread);
                action.RestAngle = degrees * Math.PI / 180;
                action.OrbitRadius = radius;
                action.Rest = OrbitMenuGeometry.PlaceOnArc(i, count, radius, direction, spread);
            }
        }
        if (_field is not null)
        {
            var extent = 2 * (radius + Token(OrbitMenuTokenKind.FieldPadding, 60));
            _field.Width = extent;
            _field.Height = extent;
        }
        var halfSweep = count < 2 ? 0 : Math.Abs(spread) >= 360 ? 180 * Math.Sign(spread) : spread / 2;
        _track?.SetArc(radius, direction, halfSweep);
        var heading = direction * Math.PI / 180;
        var triggerRadius = Token(OrbitMenuTokenKind.TriggerSize, 56) / 2;
        var actionRadius = (ActionAt(0)?.Size ?? 46) / 2;
        _pill?.SetPlacement(heading, triggerRadius + PillGap, radius - actionRadius - PillGap, radius + actionRadius + PillGap);
        UpdateMagnet();
        ApplyAll();
    }

    private void ApplyAll()
    {
        foreach (var action in Actions())
        {
            action.ApplyMotion(_full);
        }
        ApplyLens();
    }

    #endregion

    #region Opening and folding

    private void OnOpenChanged(bool open)
    {
        PseudoClasses.Set(OpenPseudoClass, open);
        _trigger?.SetOpen(open);
        UpdateNames();
        _full = ArcMotion.IsFull(this);
        if (open)
        {
            ClearDone();
            EnsureParts();
        }
        else
        {
            EndSession();
            _lensFollow = ActionAt(_target);
            _pointer = null;
            SetTarget(-1);
            ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        }
        // Every action needs its motion before the relayout: without one, it would be placed by the new IsOpen, all the
        // way out, and stay there until its staggered spring started (the ring showed open, folded back and fanned out).
        foreach (var action in Actions())
        {
            EnsureMotion(action, open ? 0 : 1);
        }
        Relayout();
        var count = ItemCount;
        var index = 0;
        foreach (var action in Actions())
        {
            var order = open ? index : count - 1 - index;
            index++;
            if (open)
            {
                SetShown(action, true);
            }
            var travel = open
                ? MotionSpec.Spring(OrbitMenuGeometry.OpenSpring, Times(OrbitMenuGeometry.OpenStagger, order))
                : MotionSpec.Spring(OrbitMenuGeometry.CloseSpring, Times(OrbitMenuGeometry.CloseStagger, order));
            var fade = !_full
                ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)
                : open
                    ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, Times(OrbitMenuGeometry.OpenStagger, order))
                    : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard,
                        Times(OrbitMenuGeometry.CloseStagger, order) + OrbitMenuGeometry.CloseFadeDelay);
            _ = action.Travel!.AnimateTo(open ? 1 : 0, travel);
            _ = Settle(action, open, fade);
        }
        if (_track is not null)
        {
            if (_trackProgress is null)
            {
                _trackProgress = new MotionValue<double>(_track, _track.Progress, MotionVectors.Double);
                _trackProgress.Changed += value => _track?.SetProgress(value);
            }
            _ = _trackProgress.AnimateTo(open ? 1 : 0,
                MotionSpec.Spring(open ? OrbitMenuGeometry.MorphSpring : OrbitMenuGeometry.CloseSpring));
        }
        if (_field is not null)
        {
            _field.IsHitTestVisible = open;
        }
        UpdateGlyph();
        UpdatePill();
        WatchOutsidePresses(open);
    }

    private static TimeSpan Times(TimeSpan step, int count) => TimeSpan.FromTicks(step.Ticks * count);

    private async Task Settle(OrbitMenuAction action, bool open, MotionSpec spec)
    {
        var arrived = await action.Fade!.AnimateTo(open ? 1 : 0, spec);
        if (arrived && !open && !IsOpen)
        {
            SetShown(action, false);
        }
    }

    private OrbitMenuGlyphState GlyphState =>
        _doneText is not null ? OrbitMenuGlyphState.Check : IsOpen ? OrbitMenuGlyphState.Close : OrbitMenuGlyphState.Dots;

    private void UpdateGlyph() => _trigger?.Glyph?.SetState(GlyphState, animate: _full);

    #endregion

    #region Targeting, magnet and lens

    private int TargetFrom(Vector offset)
        => OrbitMenuGeometry.TargetAt(offset, ItemCount, Radius, Direction, Spread,
            Token(OrbitMenuTokenKind.DeadZone, 34), Token(OrbitMenuTokenKind.Reach, 76),
            Token(OrbitMenuTokenKind.EndTolerance, 30),
            i => ContainerFromIndex(i) is OrbitMenuAction { IsEffectivelyEnabled: true, IsVisible: true });

    private void SetTarget(int index)
    {
        if (index == _target)
        {
            return;
        }
        var previous = ActionAt(_target);
        _target = index;
        if (index < 0)
        {
            _keyboardTarget = false;
        }
        var next = ActionAt(index);
        if (previous is not null)
        {
            previous.SetTargeted(false);
            _ = previous.Boost?.AnimateTo(0, MotionSpec.Spring(ArcSpring.Quick));
        }
        if (next is not null)
        {
            next.SetTargeted(true);
            _ = next.Boost?.AnimateTo(1, MotionSpec.Spring(ArcSpring.Quick));
        }
        UpdateLens();
        UpdatePill();
    }

    /// <summary>The magnet: actions near the pointer lean toward it and grow (mouse and pen only; Reduced only grows).</summary>
    private void UpdateMagnet()
    {
        var pointer = IsOpen ? _pointer : null;
        foreach (var action in Actions())
        {
            if (pointer is { } at)
            {
                var delta = Delta(at, action.Rest);
                var weight = OrbitMenuGeometry.Weight(delta.Length);
                action.Pull = _full ? delta * (OrbitMenuGeometry.MagnetPull * weight) : default;
                action.PullScale = 1 + OrbitMenuGeometry.MagnetGrowth * weight;
            }
            else
            {
                action.Pull = default;
                action.PullScale = 1;
            }
        }
    }

    private void UpdateLens()
    {
        var target = IsOpen ? ActionAt(_target) : null;
        if (target is not null && _lens is not null)
        {
            _lensFollow = null;
            _lens.SetDanger(target.Tone == OrbitMenuActionTone.Danger);
            EnsureLensMotion();
            var angle = target.RestAngle;
            if (_lensPresence!.Value < 0.05 && _lensPresence.Target < 0.5)
            {
                _lensAngle!.Jump(angle);
            }
            else
            {
                var current = _lensAngle!.Value;
                var turn = OrbitMenuGeometry.Wrap((angle - current) * 180 / Math.PI) * Math.PI / 180;
                _ = _lensAngle.AnimateTo(current + turn, MotionSpec.Spring(ArcSpring.Quick));
            }
            _ = _lensPresence.AnimateTo(1, _full
                ? MotionSpec.Spring(ArcSpring.Quick)
                : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else if (_lensPresence is not null)
        {
            _ = _lensPresence.AnimateTo(0, _full
                ? MotionSpec.Spring(OrbitMenuGeometry.LensHideSpring)
                : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        ApplyLens();
    }

    private void EnsureLensMotion()
    {
        if (_lensAngle is not null || _lens is null)
        {
            return;
        }
        _lensAngle = new MotionValue<double>(_lens, 0, MotionVectors.Double);
        _lensPresence = new MotionValue<double>(_lens, 0, MotionVectors.Double);
        _lensAngle.Changed += _ => ApplyLens();
        _lensPresence.Changed += _ => ApplyLens();
    }

    /// <summary>
    /// Places the lens: on the arc at its gliding angle (pulled along with its target as it arrives), 1.1 times an action
    /// away from the pointer and as large as the action under the pointer, shrinking and fading as it leaves.
    /// </summary>
    private void ApplyLens()
    {
        if (_lens is null || _lensAngle is null || _lensPresence is null)
        {
            return;
        }
        var presence = Math.Max(0, _lensPresence.Value);
        var angle = _lensAngle.Value;
        var radius = Radius;
        if (_lensFollow is { } follow && _full)
        {
            radius *= Math.Clamp(follow.Travel?.Value ?? 1, 0, 1);
        }
        var rest = new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
        var offset = rest;
        if (_full && ActionAt(_target) is { } target)
        {
            var gap = Math.Abs(OrbitMenuGeometry.Wrap((angle - target.RestAngle) * 180 / Math.PI)) * Math.PI / 180 * radius;
            var settle = Math.Exp(-(gap / 50) * (gap / 50));
            offset += target.Pull * settle;
        }
        var grow = _pointer is { } pointer && IsOpen
            ? 1 + OrbitMenuGeometry.MagnetGrowth * OrbitMenuGeometry.Weight(Delta(pointer, rest).Length)
            : 1;
        var scale = OrbitMenuGeometry.LensScale * grow;
        if (_full)
        {
            var shown = scale * presence;
            _lens.Place(offset, shown, shown / OrbitMenuGeometry.LensOpaqueScale);
        }
        else
        {
            _lens.Place(offset, scale, presence);
        }
    }

    private void TrackPointer(Point position, IPointer pointer)
    {
        var offset = Delta(position, Center);
        _pointer = pointer.Type == PointerType.Touch ? null : new Point(offset.X, offset.Y);
        var target = TargetFrom(offset);
        if (target >= 0 || !_keyboardTarget)
        {
            _keyboardTarget = false;
            SetTarget(target);
        }
        UpdateMagnet();
        ApplyAll();
    }

    #endregion

    #region Pill, confirmation and announcements

    private void UpdatePill()
    {
        string? text;
        var done = false;
        if (_doneText is { } confirmation)
        {
            text = confirmation;
            done = true;
        }
        else if (!IsOpen)
        {
            text = null;
        }
        else if (ActionAt(_target) is { } target)
        {
            text = target.Label ?? Label;
        }
        else if (_session is { Dragging: true })
        {
            text = CancelText;
        }
        else
        {
            text = Label;
        }
        if (text is null)
        {
            _pill?.Hide();
            return;
        }
        EnsureParts();
        _pill?.Show(text, done);
    }

    private void ShowDone(string text)
    {
        _doneText = text;
        PseudoClasses.Set(DonePseudoClass, true);
        EnsureParts();
        UpdatePill();
        UpdateGlyph();
        if (Announces)
        {
            Announce(text);
        }
        if (_doneTimer is null)
        {
            _doneTimer = new DispatcherTimer();
            _doneTimer.Tick += (_, _) => ExpireDone();
        }
        _doneTimer.Stop();
        _doneTimer.Interval = TokenTime(OrbitMenuTokenKind.DoneDuration, TimeSpan.FromMilliseconds(1400));
        _doneTimer.Start();
    }

    /// <summary>Ends the confirmation: the pill leaves and the check turns back into the ellipsis.</summary>
    internal void ExpireDone()
    {
        _doneTimer?.Stop();
        if (_doneText is null)
        {
            return;
        }
        _doneText = null;
        PseudoClasses.Set(DonePseudoClass, false);
        UpdatePill();
        UpdateGlyph();
    }

    private void ClearDone()
    {
        _doneTimer?.Stop();
        if (_doneText is null)
        {
            return;
        }
        _doneText = null;
        PseudoClasses.Set(DonePseudoClass, false);
    }

    private void Announce(string text)
    {
        if (_announcer is null)
        {
            if (_over is null)
            {
                return;
            }
            _announcer = new TextBlock
            {
                Width = 1,
                Height = 1,
                Opacity = 0,
                IsHitTestVisible = false,
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            AutomationProperties.SetLiveSetting(_announcer, AutomationLiveSetting.Polite);
            _over.Children.Add(_announcer);
        }
        _announcer.Text = text;
        AutomationProperties.SetName(_announcer, text);
    }

    #endregion

    #region Firing

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not OrbitMenuAction action)
        {
            return;
        }
        var index = IndexFromContainer(action);
        if (index < 0)
        {
            return;
        }
        RaiseEvent(new OrbitMenuActionEventArgs(ActionInvokedEvent, action, ItemFromContainer(action)));
        var keyboard = SpecialFocusModality.LastWasPointer(this) != true;
        if (IsOpen && _target != index)
        {
            SetTarget(index);
        }
        ReturnFocus(keyboard);
        Close();
        ShowDone(action.DoneText ?? action.Label ?? Label);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        // Only the keyboard and assistive technology click the trigger; a pointer press goes to BeginPress.
        e.Handled = true;
        if (IsOpen)
        {
            ReturnFocus(keyboard: true);
            Close();
            return;
        }
        Open();
        FocusActionSoon(last: false);
    }

    #endregion

    #region Press, drag and release

    /// <summary>A press on the trigger: it opens the arc at once and follows the pointer until the release.</summary>
    internal void BeginPress(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!IsEffectivelyEnabled || (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }
        e.Handled = true;
        _trigger?.Focus(NavigationMethod.Pointer);
        _session = new PressSession(e.Pointer, point.Position, Clock(), IsOpen);
        e.Pointer.Capture(this);
        _trigger?.SetHolding(true);
        if (IsOpen)
        {
            ClearDone();
        }
        else
        {
            Open();
        }
        TrackPointer(point.Position, e.Pointer);
        UpdatePill();
        UpdateGlyph();
    }

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_session is { } session)
        {
            if (!ReferenceEquals(e.Pointer, session.Pointer))
            {
                return;
            }
            var position = e.GetPosition(this);
            if (!session.Dragging && Delta(position, session.Start).Length > Token(OrbitMenuTokenKind.DragThreshold, 8))
            {
                session.Dragging = true;
            }
            TrackPointer(position, e.Pointer);
            UpdatePill();
            return;
        }
        if (!IsOpen || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        // Hovering an open arc: the field and the actions bring the pointer here, and it aims the same way a drag does.
        TrackPointer(e.GetPosition(this), e.Pointer);
    }

    private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_session is not { } session || !ReferenceEquals(e.Pointer, session.Pointer))
        {
            return;
        }
        e.Handled = true;
        _session = null;
        _trigger?.SetHolding(false);
        var position = e.GetPosition(this);
        var target = TargetFrom(Delta(position, Center));
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
        }
        if (e.Pointer.Type == PointerType.Touch)
        {
            _pointer = null;
        }
        if (target >= 0 && ActionAt(target) is { } action)
        {
            SetTarget(target);
            action.Invoke();
            return;
        }
        var held = Clock() - session.Started;
        if (!session.Dragging && held < TokenTime(OrbitMenuTokenKind.TapDuration, TimeSpan.FromMilliseconds(350)).TotalMilliseconds)
        {
            // A tap: it keeps a closed arc open (hover and click from here on), and closes an open one.
            if (session.WasOpen)
            {
                Close();
            }
            else
            {
                if (e.Pointer.Type == PointerType.Touch)
                {
                    SetTarget(-1);
                    UpdateMagnet();
                    ApplyAll();
                }
                UpdatePill();
            }
            return;
        }
        // A hold without a drag, or a release away from every action: fold without firing.
        Close();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_session is { } session && ReferenceEquals(e.Pointer, session.Pointer))
        {
            _session = null;
            _trigger?.SetHolding(false);
            Close();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_session is not null || _pointer is null)
        {
            return;
        }
        _pointer = null;
        if (!_keyboardTarget)
        {
            SetTarget(-1);
        }
        UpdateMagnet();
        ApplyAll();
    }

    private void EndSession()
    {
        if (_session is not { } session)
        {
            return;
        }
        _session = null;
        _trigger?.SetHolding(false);
        if (ReferenceEquals(session.Pointer.Captured, this))
        {
            session.Pointer.Capture(null);
        }
    }

    private sealed class PressSession(IPointer pointer, Point start, long started, bool wasOpen)
    {
        public IPointer Pointer { get; } = pointer;

        public Point Start { get; } = start;

        public long Started { get; } = started;

        public bool WasOpen { get; } = wasOpen;

        public bool Dragging { get; set; }
    }

    #endregion

    #region Keyboard and focus

    private void OnAnyGotFocus(object? sender, FocusChangedEventArgs e)
    {
        // The focused action is the target: the lens sits under it and the pill names it.
        if (e.Source is OrbitMenuAction action && IsOpen && IndexFromContainer(action) is var index and >= 0
            && e.NavigationMethod != NavigationMethod.Pointer)
        {
            _keyboardTarget = true;
            SetTarget(index);
        }
    }

    private void FocusActionSoon(bool last)
    {
        var request = ++_focusRequest;
        Dispatcher.UIThread.Post(() =>
        {
            var target = last ? FirstEnabled(ItemCount - 1, -1) : FirstEnabled(0, 1);
            if (request == _focusRequest && IsOpen && target >= 0)
            {
                ContainerFromIndex(target)?.Focus(NavigationMethod.Directional);
            }
        }, DispatcherPriority.Loaded);
    }

    private void ReturnFocus(bool keyboard)
    {
        _focusRequest++;
        if (_trigger is null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not OrbitMenuAction focused
            || IndexFromContainer(focused) < 0)
        {
            return;
        }
        _trigger.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Handled || e.Source is not Visual source)
        {
            base.OnKeyDown(e);
            return;
        }
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            ReturnFocus(keyboard: true);
            Close();
            return;
        }
        var count = ItemCount;
        if (source is OrbitMenuAction action && IndexFromContainer(action) is var current and >= 0)
        {
            int? target = e.Key switch
            {
                Key.Right or Key.Down => Step(current, 1),
                Key.Left or Key.Up => Step(current, -1),
                Key.Home => FirstEnabled(0, 1),
                Key.End => FirstEnabled(count - 1, -1),
                _ => null
            };
            if (target is { } index)
            {
                e.Handled = true;
                if (index >= 0)
                {
                    ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
                }
                return;
            }
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                action.Invoke();
                return;
            }
        }
        else if (ReferenceEquals(source, _trigger) && e.Key is Key.Right or Key.Down or Key.Left or Key.Up)
        {
            // From the trigger the arrows open the arc onto its first action (Up: its last).
            e.Handled = true;
            if (IsOpen)
            {
                var index = e.Key == Key.Up ? FirstEnabled(count - 1, -1) : FirstEnabled(0, 1);
                if (index >= 0)
                {
                    ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
                }
                return;
            }
            Open();
            FocusActionSoon(last: e.Key == Key.Up);
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>The next enabled action from <paramref name="from"/> in <paramref name="direction"/>, round the arc.</summary>
    private int Step(int from, int direction)
    {
        var count = ItemCount;
        for (var i = 1; i <= count; i++)
        {
            var index = ((from + direction * i) % count + count) % count;
            if (IsInvokable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private int FirstEnabled(int from, int direction)
    {
        for (var index = from; index >= 0 && index < ItemCount; index += direction)
        {
            if (IsInvokable(index))
            {
                return index;
            }
        }
        return -1;
    }

    private bool IsInvokable(int index) => ContainerFromIndex(index) is { IsEnabled: true };

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // Tabbing away from the arc folds it.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || _session is not null || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
            {
                return;
            }
            if (!ReferenceEquals(focused, this) && !this.IsVisualAncestorOf(focused))
            {
                Close();
            }
        }, DispatcherPriority.Input);
    }

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
        if (!IsOpen || e.Source is not Visual source)
        {
            return;
        }
        if (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)
            || (source is ILogical logical && this.IsLogicalAncestorOf(logical)))
        {
            return;
        }
        Close();
    }

    #endregion
}
