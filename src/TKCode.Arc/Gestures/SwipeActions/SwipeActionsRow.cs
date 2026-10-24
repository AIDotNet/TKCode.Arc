using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One row of <see cref="SwipeActions"/> (Arc SwipeActionsRow): its content follows a horizontal swipe to reveal
/// <see cref="LeadingActions"/> under the left edge and <see cref="TrailingActions"/> under the right. Past the last
/// stop the row still follows with rubber-band resistance; a full swipe commits the outermost action (the action stretches
/// across the row, the row slides away and collapses), and a release settles open or closed on a spring that carries the
/// release velocity. Vertical drags pass through to the page.
/// <para>
/// Every row also has a More actions button with the same actions in a menu, so the gesture is never required: Enter or
/// Space opens it on the first row, arrows move, Escape closes the menu or puts an open row away. Pressing outside an open
/// row closes it, and only one row of a list is open at a time. Use it on its own or inside a <see cref="SwipeActions"/>
/// list (whose actions it uses when it declares none of its own).
/// </para>
/// </summary>
[TemplatePart(ActionsPart, typeof(Control))]
[TemplatePart(ContentHostPart, typeof(Panel))]
[TemplatePart(MoreButtonPart, typeof(Button))]
[TemplatePart(SeparatorPart, typeof(Border))]
[PseudoClasses(OpenPseudoClass, DraggingPseudoClass, LeavingPseudoClass, HasActionsPseudoClass)]
public class SwipeActionsRow : ContentControl
{
    public const string ActionsPart = "PART_Actions";
    public const string ContentHostPart = "PART_ContentHost";
    public const string MoreButtonPart = "PART_MoreButton";
    public const string SeparatorPart = "PART_Separator";
    public const string OpenPseudoClass = ":open";
    public const string DraggingPseudoClass = ":dragging";
    public const string LeavingPseudoClass = ":leaving";
    public const string HasActionsPseudoClass = ":has-actions";

    /// <summary>Default accessible name of the More actions button; {0} is the row's label.</summary>
    public const string DefaultMoreActionsFormat = "More actions for {0}";

    // Physics from the Arc source.
    /// <summary>Movement before a press is read as a swipe or handed to the page as a scroll.</summary>
    private const double Slop = 8;
    /// <summary>Seconds of travel projected from the release velocity.</summary>
    private const double Projection = 0.1;
    /// <summary>Release speed in px/s that counts as a flick.</summary>
    private const double Flick = 900;
    /// <summary>A full swipe needs the open width plus this, or <see cref="FullSwipeRatio"/> of the row.</summary>
    private const double FullSwipeMargin = 48;
    private const double FullSwipeRatio = 0.56;
    /// <summary>Once armed, a full swipe stays armed until the row comes back this far.</summary>
    private const double ArmHysteresis = 20;
    /// <summary>A row heading home may launch at most this many times its distance per second, so it never flashes past zero.</summary>
    private const double HomeLaunchCap = 12;
    private static readonly TimeSpan RestoreDelay = TimeSpan.FromMilliseconds(1400);
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan FadeDelay = TimeSpan.FromMilliseconds(340);
    private static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(150);

    #region 公共属性定义

    /// <summary>Names the row in its More actions button, for example the message subject (Arc: required).</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SwipeActionsRow, string>(nameof(Label), string.Empty);

    /// <summary>Lets a long swipe commit the outermost action without a tap; null follows the list (default on).</summary>
    public static readonly StyledProperty<bool?> IsFullSwipeEnabledProperty =
        AvaloniaProperty.Register<SwipeActionsRow, bool?>(nameof(IsFullSwipeEnabled));

    /// <summary>Left inset of the hairline separator under the row (Arc <c>--swipe-actions-inset</c>, default 16).</summary>
    public static readonly StyledProperty<double> SeparatorInsetProperty =
        AvaloniaProperty.Register<SwipeActionsRow, double>(nameof(SeparatorInset), 16);

    public static readonly DirectProperty<SwipeActionsRow, SwipeActionSide> OpenSideProperty =
        AvaloniaProperty.RegisterDirect<SwipeActionsRow, SwipeActionSide>(nameof(OpenSide), o => o.OpenSide);

    /// <summary>Width of one revealed action at rest (theme: 76px).</summary>
    internal static readonly StyledProperty<double> ActionExtentProperty =
        AvaloniaProperty.Register<SwipeActionsRow, double>(nameof(ActionExtent), 76);

    /// <summary>Menu minimum width (theme: 11rem).</summary>
    internal static readonly StyledProperty<double> MenuMinWidthProperty =
        AvaloniaProperty.Register<SwipeActionsRow, double>(nameof(MenuMinWidth), 176);

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public bool? IsFullSwipeEnabled { get => GetValue(IsFullSwipeEnabledProperty); set => SetValue(IsFullSwipeEnabledProperty, value); }

    public double SeparatorInset { get => GetValue(SeparatorInsetProperty); set => SetValue(SeparatorInsetProperty, value); }

    private SwipeActionSide _openSide;

    /// <summary>The side resting open (or heading there), or <see cref="SwipeActionSide.None"/>.</summary>
    public SwipeActionSide OpenSide
    {
        get => _openSide;
        private set => SetAndRaise(OpenSideProperty, ref _openSide, value);
    }

    internal double ActionExtent { get => GetValue(ActionExtentProperty); set => SetValue(ActionExtentProperty, value); }

    internal double MenuMinWidth { get => GetValue(MenuMinWidthProperty); set => SetValue(MenuMinWidthProperty, value); }

    /// <summary>Actions under the left edge, revealed by swiping right; the first one is outermost and commits on a full swipe.</summary>
    public AvaloniaList<SwipeAction> LeadingActions { get; } = new();

    /// <summary>Actions under the right edge, revealed by swiping left; the last one is outermost and commits on a full swipe.</summary>
    public AvaloniaList<SwipeAction> TrailingActions { get; } = new();

    /// <summary>Raised on the row (and bubbled to its list) after an action ran.</summary>
    public event EventHandler<SwipeActionInvokedEventArgs>? ActionInvoked
    {
        add => AddHandler(SwipeActions.ActionInvokedEvent, value);
        remove => RemoveHandler(SwipeActions.ActionInvokedEvent, value);
    }

    #endregion

    private readonly MotionValue<double> _x;
    private readonly MotionValue<double> _cover;
    private readonly MotionValue<double> _presence;
    private readonly MotionValue<double> _fade;
    private readonly TranslateTransform _translate = new();
    // The actions may be shared (the list's defaults, or app objects): their changes are watched only while the row is
    // shown, so a removed row is not kept alive by actions that outlive it.
    private readonly List<SwipeAction> _watched = new();
    private bool _watching;
    private SwipeActionsRowLayers? _layers;
    private Panel? _host;
    private SwipeActionsMoreButton? _more;
    private Border? _separator;
    private DragTracker? _drag;
    private MenuSurface? _menu;
    private bool _menuDirty = true;
    private bool _menuKeyboard;
    private double _dragOrigin;
    private double _dragBase;
    private bool _armed;
    private bool _leaving;
    private bool _pressWhileOpen;
    private bool _enterPending;
    private double _naturalHeight;
    private int _leaveToken;
    private IDisposable? _restoreTimer;
    private TopLevel? _dismissRoot;

    static SwipeActionsRow()
    {
        FocusableProperty.OverrideDefaultValue<SwipeActionsRow>(false);
    }

    public SwipeActionsRow()
    {
        _x = new MotionValue<double>(this, 0, MotionVectors.Double);
        _x.Changed += OnOffsetChanged;
        _cover = new MotionValue<double>(this, 0, MotionVectors.Double);
        _cover.Changed += _ => _layers?.InvalidateVisual();
        _presence = new MotionValue<double>(this, 1, MotionVectors.Double);
        _presence.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += value => Opacity = value;
        LeadingActions.CollectionChanged += (_, _) => OnActionsChanged();
        TrailingActions.CollectionChanged += (_, _) => OnActionsChanged();
    }

    /// <summary>The list this row belongs to (null when used on its own).</summary>
    internal SwipeActions? List { get; set; }

    /// <summary>The row was generated for a data item (its label comes from the list).</summary>
    internal bool IsGenerated { get; set; }

    /// <summary>Current horizontal offset of the content (positive: leading actions revealed).</summary>
    internal double Offset => _x.Value;

    /// <summary>Progress of the covering action stretching across the row (0..1).</summary>
    internal double CoverProgress => _cover.Value;

    /// <summary>The action stretching over the row on a full swipe or a commit.</summary>
    internal (SwipeActionSide Side, int Index)? Covering { get; private set; }

    internal bool IsLeaving => _leaving;

    internal Control? MoreButton => _more;

    internal MenuSurface? Menu => _menu;

    private bool UsesOwnActions => List is null || LeadingActions.Count > 0 || TrailingActions.Count > 0;

    /// <summary>The actions on <paramref name="side"/>: the row's own, or its list's when the row declares none.</summary>
    internal IReadOnlyList<SwipeAction> ActionsOf(SwipeActionSide side)
    {
        if (side == SwipeActionSide.None)
        {
            return Array.Empty<SwipeAction>();
        }
        var own = UsesOwnActions;
        return side == SwipeActionSide.Leading
            ? own ? LeadingActions : List!.LeadingActions
            : own ? TrailingActions : List!.TrailingActions;
    }

    private bool HasActions => ActionsOf(SwipeActionSide.Leading).Count + ActionsOf(SwipeActionSide.Trailing).Count > 0;

    private bool FullSwipe => IsFullSwipeEnabled ?? List?.IsFullSwipeEnabled ?? true;

    private double RowWidth => Bounds.Width > 0 ? Bounds.Width : 320;

    private string MoreName
    {
        get
        {
            var label = Label;
            if (string.IsNullOrEmpty(label))
            {
                return "More actions";
            }
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, List?.MoreActionsFormat ?? DefaultMoreActionsFormat, label);
        }
    }

    #region 公共方法

    /// <summary>Opens the row on <paramref name="side"/> (springs to its resting open width); None closes it.</summary>
    public void Open(SwipeActionSide side)
    {
        if (_leaving)
        {
            return;
        }
        var count = ActionsOf(side).Count;
        if (count == 0)
        {
            Settle(0);
            return;
        }
        Settle((side == SwipeActionSide.Leading ? 1 : -1) * count * ActionExtent);
    }

    /// <summary>Puts the row away.</summary>
    public void Close()
    {
        if (!_leaving && (Math.Abs(_x.Value) > 0.01 || OpenSide != SwipeActionSide.None))
        {
            Settle(0);
        }
    }

    /// <summary>Runs <paramref name="action"/> as if it was chosen from the menu (it must be one of this row's actions).</summary>
    public void Run(SwipeAction action)
    {
        foreach (var side in new[] { SwipeActionSide.Leading, SwipeActionSide.Trailing })
        {
            var actions = ActionsOf(side);
            for (var i = 0; i < actions.Count; i++)
            {
                if (ReferenceEquals(actions[i], action))
                {
                    Commit(side, i, 0);
                    return;
                }
            }
        }
    }

    #endregion

    #region 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _drag?.Dispose();
        _drag = null;
        if (_host is not null)
        {
            _host.RemoveHandler(PointerPressedEvent, OnHostPointerPressed);
            _host.RemoveHandler(PointerReleasedEvent, OnHostPointerReleased);
            _host.RenderTransform = null;
        }
        if (_more is not null)
        {
            _more.Click -= OnMoreClick;
            _more.RemoveHandler(KeyDownEvent, OnMoreKeyDown);
            _more.Row = null;
        }
        if (_layers is not null)
        {
            _layers.Row = null;
        }
        _layers = e.NameScope.Find<SwipeActionsRowLayers>(ActionsPart);
        _host = e.NameScope.Find<Panel>(ContentHostPart);
        _more = e.NameScope.Find<SwipeActionsMoreButton>(MoreButtonPart);
        _separator = e.NameScope.Find<Border>(SeparatorPart);
        if (_layers is not null)
        {
            _layers.Row = this;
        }
        if (_host is not null)
        {
            _translate.X = _x.Value;
            _host.RenderTransform = _translate;
            _host.AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _host.AddHandler(PointerReleasedEvent, OnHostPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            // Presses on the More button still start a swipe; the button only opens its menu on a click.
            _drag = new DragTracker(_host, new DragOptions { Axis = DragAxis.Horizontal, Threshold = Slop, HandledEventsToo = true })
            {
                CanBegin = press => !_leaving && press.Pointer.IsPrimary
            };
            _drag.Pressed += OnDragPressed;
            _drag.Started += OnDragStarted;
            _drag.Moved += OnDragMoved;
            _drag.Completed += OnDragCompleted;
            _drag.Cancelled += OnDragCancelled;
        }
        if (_more is not null)
        {
            _more.Row = this;
            _more.Click += OnMoreClick;
            _more.AddHandler(KeyDownEvent, OnMoreKeyDown, RoutingStrategies.Tunnel);
        }
        UpdateNames();
        UpdateSeparator();
        UpdateHasActions();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            UpdateNames();
        }
        else if (change.Property == SeparatorInsetProperty)
        {
            UpdateSeparator();
        }
        else if (change.Property == ActionExtentProperty)
        {
            _layers?.InvalidateVisual();
        }
    }

    private void UpdateNames()
    {
        AutomationProperties.SetName(this, Label);
        if (_more is not null)
        {
            AutomationProperties.SetName(_more, MoreName);
        }
        if (_menu is not null)
        {
            AutomationProperties.SetName(_menu, MoreName);
        }
    }

    private void UpdateSeparator()
    {
        if (_separator is not null)
        {
            _separator.Margin = new Thickness(SeparatorInset, 0, 0, 0);
        }
    }

    private void UpdateHasActions() => PseudoClasses.Set(HasActionsPseudoClass, HasActions);

    /// <summary>The actions (or the list's defaults) changed: redraw, rebuild the menu on its next open, close an emptied side.</summary>
    internal void OnActionsChanged()
    {
        WatchActions();
        _menuDirty = true;
        _layers?.Reset();
        UpdateHasActions();
        if (OpenSide != SwipeActionSide.None && ActionsOf(OpenSide).Count == 0)
        {
            Settle(0);
        }
    }

    /// <summary>Watches the actions the row shows while it is shown (and none otherwise).</summary>
    private void WatchActions()
    {
        foreach (var action in _watched)
        {
            action.PropertyChanged -= OnActionPropertyChanged;
        }
        _watched.Clear();
        if (!_watching)
        {
            return;
        }
        foreach (var side in new[] { SwipeActionSide.Leading, SwipeActionSide.Trailing })
        {
            foreach (var action in ActionsOf(side))
            {
                action.PropertyChanged += OnActionPropertyChanged;
                _watched.Add(action);
            }
        }
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        _menuDirty = true;
        _layers?.Reset();
    }

    #endregion

    #region 布局与生命周期

    /// <summary>Rows that enter after their list has loaded grow in (smooth height spring, fade on the enter curve).</summary>
    internal void PlayEntrance()
    {
        _enterPending = true;
        if (TopLevel.GetTopLevel(this) is not null)
        {
            StartEntrance();
        }
    }

    private void StartEntrance()
    {
        _enterPending = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        _fade.Jump(0);
        if (level == ArcMotionLevel.Full)
        {
            _presence.Jump(0);
            _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        }
        else
        {
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ReducedFade, ArcEasings.Standard));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchActions();
        // An action may have changed while the row was away.
        _menuDirty = true;
        _layers?.Reset();
        if (_enterPending)
        {
            StartEntrance();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchActions();
        // A removed row is gone; if it is ever shown again it starts at rest.
        _restoreTimer?.Dispose();
        _restoreTimer = null;
        DetachDismiss();
        _menu?.Close(returnFocus: false);
        _leaveToken++;
        if (_leaving)
        {
            _leaving = false;
            PseudoClasses.Set(LeavingPseudoClass, false);
        }
        _armed = false;
        Covering = null;
        _x.Jump(0);
        _cover.Jump(0);
        _presence.Jump(1);
        _fade.Jump(1);
        SetOpenSide(SwipeActionSide.None);
    }

    /// <summary>Height follows the presence spring while the row enters or collapses (the content keeps its height and is clipped).</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        _naturalHeight = size.Height;
        var presence = _presence.Value;
        return presence >= 0.9999 ? size : new Size(size.Width, size.Height * Math.Max(0, presence));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_presence.Value >= 0.9999)
        {
            return base.ArrangeOverride(finalSize);
        }
        base.ArrangeOverride(new Size(finalSize.Width, Math.Max(finalSize.Height, _naturalHeight)));
        return finalSize;
    }

    private void OnOffsetChanged(double value)
    {
        _translate.X = value;
        _layers?.InvalidateVisual();
    }

    #endregion

    #region 手势

    private int Direction(SwipeActionSide side) => side == SwipeActionSide.Leading ? 1 : -1;

    private static SwipeActionSide SideOf(double value) =>
        value > 0 ? SwipeActionSide.Leading : value < 0 ? SwipeActionSide.Trailing : SwipeActionSide.None;

    private double OpenWidth(SwipeActionSide side) => ActionsOf(side).Count * ActionExtent;

    private int Outermost(SwipeActionSide side) => side == SwipeActionSide.Leading ? 0 : ActionsOf(side).Count - 1;

    private bool CanCommit(SwipeActionSide side) => FullSwipe && ActionsOf(side).Count > 0;

    private double Threshold(SwipeActionSide side) => Math.Max(OpenWidth(side) + FullSwipeMargin, RowWidth * FullSwipeRatio);

    /// <summary>Past its last stop the row still follows, but every pixel costs more (Arc's 0.55 rubber band).</summary>
    private double Constrain(double raw)
    {
        double LimitOf(SwipeActionSide side) =>
            ActionsOf(side).Count == 0 ? 0 : CanCommit(side) ? RowWidth : OpenWidth(side);

        return RubberBand.Clamp(raw, -LimitOf(SwipeActionSide.Trailing), LimitOf(SwipeActionSide.Leading), RowWidth);
    }

    private void StretchTo(double value) => _ = _cover.AnimateTo(value, MotionSpec.Spring(ArcSpring.Morph));

    /// <summary>Past the full-swipe threshold the outermost action stretches across the row (20px hysteresis on the way back).</summary>
    private void Arm(double value)
    {
        var side = SideOf(value);
        if (side == SwipeActionSide.None)
        {
            if (_armed)
            {
                _armed = false;
                StretchTo(0);
            }
            return;
        }
        var limit = Threshold(side);
        var next = CanCommit(side) && Math.Abs(value) > (_armed ? limit - ArmHysteresis : limit);
        if (next == _armed)
        {
            return;
        }
        _armed = next;
        if (next)
        {
            Covering = (side, Outermost(side));
        }
        StretchTo(next ? 1 : 0);
    }

    /// <summary>Springs the row to a stop carrying the release velocity (smooth home, snappy open). Reduced motion jumps.</summary>
    private void Settle(double target, double velocity = 0)
    {
        if (_armed || (target != 0 && _cover.Value != 0))
        {
            StretchTo(0);
        }
        _armed = false;
        var from = _x.Value;
        // Heading home, the spring may not carry the row past zero, or the opposite actions would flash.
        var toward = target == 0 && Math.Sign(velocity) == -Math.Sign(from);
        var launch = toward ? Math.Sign(velocity) * Math.Min(Math.Abs(velocity), Math.Abs(from) * HomeLaunchCap) : velocity;
        var settle = MomentumProjector.SettleTo(_x, target, launch, target == 0 ? ArcSpring.Smooth : ArcSpring.Snappy);
        if (target == 0)
        {
            _ = HomeWhenSettled(settle);
        }
        SetOpenSide(SideOf(target));
    }

    private async Task HomeWhenSettled(Task<bool> settle)
    {
        if (await settle)
        {
            _cover.Jump(0);
            Covering = null;
            _layers?.InvalidateVisual();
        }
    }

    private void Release(double velocity)
    {
        var value = _x.Value;
        var side = SideOf(value);
        if (side == SwipeActionSide.None || ActionsOf(side).Count == 0)
        {
            Settle(0, velocity);
            return;
        }
        var direction = Direction(side);
        var distance = Math.Abs(value);
        var outward = velocity * direction;
        var open = OpenWidth(side);
        var projected = distance + outward * Projection;
        var flung = distance > open && outward > Flick && projected > Threshold(side);
        // A flick back toward home cancels an armed full swipe.
        if (CanCommit(side) && ((_armed && outward > -Flick) || flung))
        {
            Commit(side, Outermost(side), velocity);
            return;
        }
        ReadOnlySpan<double> stops = [0, open];
        var stop = MomentumProjector.SelectSnapPoint(stops, distance, outward, SnapProjection.Linear(TimeSpan.FromSeconds(Projection)));
        Settle(stop == 1 ? direction * open : 0, velocity);
    }

    private void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressWhileOpen = !_leaving && Math.Abs(_x.Value) > 0.5;
    }

    private void OnHostPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // A tap on an open row closes it instead of acting on what is underneath.
        if (_pressWhileOpen && _drag?.IsDragging != true)
        {
            _pressWhileOpen = false;
            e.Handled = true;
            Settle(0);
        }
        _pressWhileOpen = false;
    }

    private void OnDragPressed(object? sender, DragGestureEventArgs e)
    {
        // Catch the row mid-flight: the drag continues from wherever it is now.
        _x.Stop();
        _dragOrigin = _x.Value;
    }

    private void OnDragStarted(object? sender, DragGestureEventArgs e)
    {
        // Measured from the point the swipe locked, so crossing the slop never jumps the row.
        _dragBase = e.Offset.X;
        PseudoClasses.Set(DraggingPseudoClass, true);
        List?.OnRowOpened(this);
    }

    private void OnDragMoved(object? sender, DragGestureEventArgs e)
    {
        var value = Constrain(_dragOrigin + e.Offset.X - _dragBase);
        _x.Jump(value);
        Arm(value);
    }

    private void OnDragCompleted(object? sender, DragGestureEventArgs e)
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        Release(e.Velocity.X);
    }

    private void OnDragCancelled(object? sender, DragGestureEventArgs e)
    {
        PseudoClasses.Set(DraggingPseudoClass, false);
        Release(0);
    }

    #endregion

    #region 打开状态与收起

    private void SetOpenSide(SwipeActionSide side)
    {
        if (OpenSide == side)
        {
            return;
        }
        OpenSide = side;
        PseudoClasses.Set(OpenPseudoClass, side != SwipeActionSide.None);
        if (side == SwipeActionSide.None)
        {
            List?.OnRowClosed(this);
            DetachDismiss();
        }
        else
        {
            List?.OnRowOpened(this);
            AttachDismiss();
        }
    }

    /// <summary>Another row opened: put this one away unless it is leaving or being dragged.</summary>
    internal void CloseForOthers()
    {
        if (!_leaving && _drag?.IsDragging != true && Math.Abs(_x.Value) > 0.01)
        {
            Settle(0);
        }
    }

    private void AttachDismiss()
    {
        if (_dismissRoot is not null || TopLevel.GetTopLevel(this) is not { } root)
        {
            return;
        }
        _dismissRoot = root;
        root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(KeyDownEvent, OnRootKeyDown, RoutingStrategies.Bubble);
    }

    private void DetachDismiss()
    {
        if (_dismissRoot is not { } root)
        {
            return;
        }
        root.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
        root.RemoveHandler(KeyDownEvent, OnRootKeyDown);
        _dismissRoot = null;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Touching anywhere else puts the open row away.
        if (e.Source is Visual visual && (ReferenceEquals(visual, this) || this.IsVisualAncestorOf(visual)))
        {
            return;
        }
        Close();
    }

    private void OnRootKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && OpenSide != SwipeActionSide.None)
        {
            e.Handled = true;
            Close();
        }
    }

    #endregion

    #region 提交

    /// <summary>
    /// Runs the action at <paramref name="index"/> on <paramref name="side"/>: it stretches over the row; an action that keeps
    /// the row runs now while the row springs home, any other one slides the row away in its direction and runs once the
    /// row has collapsed (the list then closes the gap). If the item is still there after 1.4s the row comes back.
    /// </summary>
    internal void Commit(SwipeActionSide side, int index, double velocity)
    {
        var actions = ActionsOf(side);
        if (index < 0 || index >= actions.Count || _leaving)
        {
            return;
        }
        var action = actions[index];
        var direction = Direction(side);
        _armed = false;
        Covering = (side, index);
        StretchTo(1);
        var item = List?.ItemFromContainer(this) ?? this;
        if (action.KeepsRow)
        {
            Run(action, side, item);
            Settle(0, velocity);
            return;
        }
        _leaving = true;
        PseudoClasses.Set(LeavingPseudoClass, true);
        List?.OnRowLeavingChanged();
        _x.Stop();
        _ = MomentumProjector.SettleTo(_x, direction * (RowWidth + 2), Math.Sign(velocity) == direction ? velocity : 0, ArcSpring.Smooth);
        SetOpenSide(SwipeActionSide.None);
        var level = IsInVisualTree() ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        var gone = level switch
        {
            ArcMotionLevel.Full => Task.WhenAll(
                _presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, CollapseDelay)),
                _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard, FadeDelay))),
            ArcMotionLevel.Reduced => _fade.AnimateTo(0, MotionSpec.Tween(ReducedFade, ArcEasings.Standard)),
            _ => Task.CompletedTask
        };
        var token = ++_leaveToken;
        _restoreTimer?.Dispose();
        _restoreTimer = DispatcherTimer.RunOnce(() => Restore(token), RestoreDelay);
        _ = RunWhenGone(gone, action, side, item);
    }

    private bool IsInVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private async Task RunWhenGone(Task gone, SwipeAction action, SwipeActionSide side, object? item)
    {
        await gone;
        Run(action, side, item);
    }

    private void Run(SwipeAction action, SwipeActionSide side, object? item)
    {
        action.Run(new SwipeActionInvokedEventArgs(SwipeActions.ActionInvokedEvent, action, side, this, item) { Source = this });
        RaiseEvent(new SwipeActionInvokedEventArgs(SwipeActions.ActionInvokedEvent, action, side, this, item));
    }

    /// <summary>A list that keeps the item gets its row back instead of an empty band.</summary>
    private void Restore(int token)
    {
        _restoreTimer = null;
        if (!_leaving || token != _leaveToken)
        {
            return;
        }
        _leaving = false;
        PseudoClasses.Set(LeavingPseudoClass, false);
        List?.OnRowLeavingChanged();
        _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
        _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
        Settle(0);
    }

    #endregion

    #region 菜单

    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        var focus = _menuKeyboard ? MenuOpenFocus.First : MenuOpenFocus.Surface;
        _menuKeyboard = false;
        if (_menu?.IsOpen == true)
        {
            _menu.Close(returnFocus: false);
            return;
        }
        OpenMenu(focus);
    }

    private void OnMoreKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
                e.Handled = true;
                if (_menu?.IsOpen != true)
                {
                    OpenMenu(e.Key == Key.Down ? MenuOpenFocus.First : MenuOpenFocus.Last);
                }
                break;
            case Key.Enter:
            case Key.Space:
                _menuKeyboard = true;
                break;
        }
    }

    /// <summary>Opens the More actions menu (created on first use, rebuilt only when the actions changed).</summary>
    internal void OpenMenu(MenuOpenFocus focus)
    {
        if (_more is null || !HasActions || _leaving)
        {
            return;
        }
        var menu = EnsureMenu();
        menu.FocusReturnTarget = null;
        menu.Open(_more, focus);
    }

    internal void CloseMenu() => _menu?.Close();

    private MenuSurface EnsureMenu()
    {
        if (_menu is null)
        {
            _menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End, AnchorGap = 6 };
            _menu.ItemClicked += OnMenuItemClicked;
            _menu.PropertyChanged += OnMenuPropertyChanged;
        }
        _menu.SetValue(MinWidthProperty, MenuMinWidth, BindingPriority.Template);
        AutomationProperties.SetName(_menu, MoreName);
        if (_menuDirty)
        {
            _menuDirty = false;
            _menu.Items.Clear();
            foreach (var side in new[] { SwipeActionSide.Leading, SwipeActionSide.Trailing })
            {
                var actions = ActionsOf(side);
                for (var i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    _menu.Items.Add(new MenuEntry
                    {
                        Header = action.Label,
                        Icon = action.Icon,
                        IsDestructive = action.Tone == SwipeActionTone.Danger,
                        Tag = new SwipeActionsMenuTag(side, i)
                    });
                }
            }
        }
        return _menu;
    }

    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MenuSurface.IsOpenProperty)
        {
            _more?.SetMenuOpen(e.GetNewValue<bool>());
        }
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry.Tag is not SwipeActionsMenuTag tag || _menu is null)
        {
            return;
        }
        var actions = ActionsOf(tag.Side);
        if (tag.Index >= actions.Count)
        {
            return;
        }
        // A removed row hands focus to its neighbour's More button (or the list) once the menu closes.
        _menu.FocusReturnTarget = actions[tag.Index].KeepsRow ? null : List?.NeighbourFocusTarget(this);
        Commit(tag.Side, tag.Index, 0);
    }

    private sealed record SwipeActionsMenuTag(SwipeActionSide Side, int Index);

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new SwipeActionsRowAutomationPeer(this);

    private sealed class SwipeActionsRowAutomationPeer : ContentControlAutomationPeer
    {
        public SwipeActionsRowAutomationPeer(SwipeActionsRow owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    }
}
