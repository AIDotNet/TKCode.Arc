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
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A round button that fans its actions out around itself on an arc. The actions leave the trigger one after another
/// and spring to their places; the trigger's plus turns into a close mark. Arrow keys walk around the ring, Enter or
/// Space runs the focused action, and Escape folds the ring back into the trigger.
/// </summary>
/// <remarks>
/// Items are <see cref="OrbitMenuAction"/> elements, written in XAML or bound through
/// <see cref="ItemsControl.ItemsSource"/>. The menu takes only the trigger's room in the layout: the actions are drawn
/// over what is around it, so leave them <see cref="Radius"/> plus half an action of room on the sides they open to.
/// Angles are in degrees, clockwise from the right: -90 is straight up, 180 is left.
/// </remarks>
[TemplatePart(TriggerPart, typeof(AvaloniaButton))]
[PseudoClasses(OpenPseudoClass)]
public class OrbitMenu : ItemsControl
{
    internal const string TriggerPart = "PART_Trigger";
    internal const string OpenPseudoClass = ":open";

    /// <summary>Whether the actions are out (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<OrbitMenu, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Distance from the trigger's center to each action's center in px; NaN (default) uses the theme's 88.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(Radius), double.NaN);

    /// <summary>Angle of the first action in degrees, clockwise from the right (default -180: left of the trigger).</summary>
    public static readonly StyledProperty<double> StartAngleProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(StartAngle), -180);

    /// <summary>
    /// How far round the arc goes from the first action to the last, in degrees (default 180: over the top to the right).
    /// Negative goes counterclockwise; 360 or more spreads the actions evenly around a full ring.
    /// </summary>
    public static readonly StyledProperty<double> SweepAngleProperty =
        AvaloniaProperty.Register<OrbitMenu, double>(nameof(SweepAngle), 180);

    /// <summary>The trigger's 24 × 24 stroked glyph; it turns 45 degrees while the menu is open (a plus becomes a close mark).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<OrbitMenu, Geometry?>(nameof(Glyph), ArcIcons.Plus);

    /// <summary>Accessible name and tooltip of the trigger.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<OrbitMenu, string>(nameof(Label), "Actions");

    /// <summary>Whether invoking an action folds the menu back (default true).</summary>
    public static readonly StyledProperty<bool> ClosesOnInvokeProperty =
        AvaloniaProperty.Register<OrbitMenu, bool>(nameof(ClosesOnInvoke), true);

    /// <summary>Raised when one of the actions is invoked. Bubbles.</summary>
    public static readonly RoutedEvent<OrbitMenuActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<OrbitMenu, OrbitMenuActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private OrbitMenuTrigger? _trigger;
    private TopLevel? _pressRoot;
    private int _focusRequest;
    private bool _travels = true;

    static OrbitMenu()
    {
        FocusableProperty.OverrideDefaultValue<OrbitMenu>(false);
        ClipToBoundsProperty.OverrideDefaultValue<OrbitMenu>(false);
        SpecialFocusModality.Watch();
    }

    public OrbitMenu()
    {
        AddHandler(OrbitMenuAction.ClickEvent, OnActionClick);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius { get => GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }

    /// <inheritdoc cref="StartAngleProperty"/>
    public double StartAngle { get => GetValue(StartAngleProperty); set => SetValue(StartAngleProperty, value); }

    /// <inheritdoc cref="SweepAngleProperty"/>
    public double SweepAngle { get => GetValue(SweepAngleProperty); set => SetValue(SweepAngleProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ClosesOnInvokeProperty"/>
    public bool ClosesOnInvoke { get => GetValue(ClosesOnInvokeProperty); set => SetValue(ClosesOnInvokeProperty, value); }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<OrbitMenuActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>The trigger (null before the template is applied).</summary>
    internal AvaloniaButton? Trigger => _trigger;

    /// <summary>Fans the actions out.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Folds the actions back into the trigger.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Opens the menu when it is closed and closes it when it is open.</summary>
    public void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    /// <summary>
    /// Where the action at <paramref name="index"/> of <paramref name="count"/> rests, relative to the trigger's center.
    /// </summary>
    internal static Point PlaceOnArc(int index, int count, double radius, double startAngle, double sweepAngle)
    {
        double angle;
        if (count <= 1)
        {
            angle = startAngle + sweepAngle / 2;
        }
        else if (Math.Abs(sweepAngle) >= 360)
        {
            angle = startAngle + Math.Sign(sweepAngle) * 360.0 * index / count;
        }
        else
        {
            angle = startAngle + sweepAngle * index / (count - 1);
        }
        var radians = angle * Math.PI / 180;
        return new Point(Math.Cos(radians) * radius, Math.Sin(radians) * radius);
    }

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
            action.LaunchScale = Token(OrbitMenuTokenKind.LaunchScale, 0.4);
            SetShown(action, IsOpen);
            action.Travel?.Jump(IsOpen ? 1 : 0);
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
        }
        Dispatcher.UIThread.Post(Relayout, DispatcherPriority.Render);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
        }
        _trigger = e.NameScope.Find<OrbitMenuTrigger>(TriggerPart);
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.SetOpen(IsOpen);
        }
        UpdateNames();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            OnOpenChanged(change.GetNewValue<bool>());
        }
        else if (change.Property == RadiusProperty || change.Property == StartAngleProperty || change.Property == SweepAngleProperty)
        {
            Relayout();
        }
        else if (change.Property == LabelProperty)
        {
            UpdateNames();
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

    private double Token(OrbitMenuTokenKind kind, double fallback)
        => this.TryFindResource(OrbitMenuTokens.Key(kind), out var value) && value is double d ? d : fallback;

    /// <summary>Recomputes each action's place on the arc and moves it there at its current progress.</summary>
    private void Relayout()
    {
        var radius = double.IsNaN(Radius) ? Token(OrbitMenuTokenKind.OrbitRadius, 88) : Radius;
        var count = ItemCount;
        var travels = _travels = ArcMotion.IsFull(this);
        for (var i = 0; i < count; i++)
        {
            if (ContainerFromIndex(i) is OrbitMenuAction action)
            {
                action.Offset = PlaceOnArc(i, count, radius, StartAngle, SweepAngle);
                action.ApplyTravel(action.Travel?.Value ?? (IsOpen ? 1 : 0), travels);
            }
        }
    }

    private void OnOpenChanged(bool open)
    {
        PseudoClasses.Set(OpenPseudoClass, open);
        _trigger?.SetOpen(open);
        if (_trigger is not null)
        {
            AutomationProperties.SetItemStatus(_trigger, open ? "Expanded" : "Collapsed");
        }
        if (!open)
        {
            ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
        }
        var full = ArcMotion.IsFull(this);
        _travels = full;
        Relayout();
        var count = ItemCount;
        var index = 0;
        foreach (var action in Actions())
        {
            var order = open ? index : count - 1 - index;
            index++;
            var delay = TimeSpan.FromTicks(ArcMotionPresets.StaggerItem.Ticks * order);
            if (action.Travel is null)
            {
                var travel = new MotionValue<double>(action, open ? 0 : 1, MotionVectors.Double);
                var owner = action;
                travel.Changed += value => owner.ApplyTravel(value, _travels);
                action.Travel = travel;
            }
            if (open)
            {
                SetShown(action, true);
            }
            var spec = full
                ? open
                    ? MotionSpec.Spring(SpringSpec.FromVisual(0.34, 0.24), delay)
                    : MotionSpec.Spring(ArcSpring.Quick, delay)
                : MotionSpec.Tween(open ? ArcMotionPresets.Fast : ArcMotionPresets.Exit, open ? ArcEasings.Enter : ArcEasings.Exit);
            _ = Settle(action, open, spec);
        }
        WatchOutsidePresses(open);
        if (open && SpecialFocusModality.LastWasPointer(this) != true)
        {
            FocusActionSoon(0);
        }
    }

    private async Task Settle(OrbitMenuAction action, bool open, MotionSpec spec)
    {
        var travel = action.Travel!;
        var arrived = await travel.AnimateTo(open ? 1 : 0, spec);
        if (arrived && !open && !IsOpen)
        {
            SetShown(action, false);
        }
    }

    /// <summary>A folded action is out of sight, out of the tab order and out of reach of the pointer.</summary>
    private static void SetShown(OrbitMenuAction action, bool shown)
    {
        action.IsVisible = shown;
        action.IsHitTestVisible = shown;
    }

    private void UpdateNames()
    {
        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, Label);
            Tooltip.SetTip(_trigger, Label);
            AutomationProperties.SetItemStatus(_trigger, IsOpen ? "Expanded" : "Collapsed");
        }
    }

    private void FocusActionSoon(int index)
    {
        var request = ++_focusRequest;
        Dispatcher.UIThread.Post(() =>
        {
            if (request == _focusRequest && IsOpen && FirstEnabled(index, 1) is var target && target >= 0)
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

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Toggle();
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not OrbitMenuAction action || IndexFromContainer(action) < 0)
        {
            return;
        }
        RaiseEvent(new OrbitMenuActionEventArgs(ActionInvokedEvent, action));
        if (ClosesOnInvoke)
        {
            ReturnFocus(SpecialFocusModality.LastWasPointer(this) != true);
            Close();
        }
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
        else if (ReferenceEquals(source, _trigger) && IsOpen && e.Key is Key.Right or Key.Down or Key.Left or Key.Up)
        {
            // From the trigger, the arrows step onto the ring at its first or its last action.
            e.Handled = true;
            var forward = e.Key is Key.Right or Key.Down;
            var index = forward ? FirstEnabled(0, 1) : FirstEnabled(count - 1, -1);
            if (index >= 0)
            {
                ContainerFromIndex(index)?.Focus(NavigationMethod.Directional);
            }
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>The next enabled action from <paramref name="from"/> in <paramref name="direction"/>, round the ring.</summary>
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
        // Tabbing away from the ring folds it back.
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsOpen || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
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
}
