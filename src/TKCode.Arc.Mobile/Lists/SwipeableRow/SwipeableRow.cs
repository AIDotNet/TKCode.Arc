using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Gestures;
using TKCode.Arc.Mobile.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Mobile;

/// <summary>
/// A mobile list row that slides horizontally to reveal <see cref="LeadingActions"/> (swipe right) and
/// <see cref="TrailingActions"/> (swipe left), such as Archive, Pin or Delete. Wrap a <see cref="ListItem"/>,
/// <see cref="ListCell"/> or custom row inside it; <see cref="ListGroup"/> treats it as a native row container.
/// <para>
/// A horizontal swipe follows the finger 1:1 with rubber-band resistance past the tray width and settles onto the tray
/// or back to closed with the finger's velocity. Swiping past the full-swipe threshold when <see cref="AllowFullSwipe"/>
/// is on triggers the primary action on that side with a haptic cue. Inside a <see cref="NavigationStack"/> that can go
/// back, a rightward swipe starting inside the left edge (<see cref="NavigationStack.EdgeWidth"/>) yields to the stack's
/// edge-back gesture. Under reduced motion the row jumps instead of springing.
/// </para>
/// </summary>
[TemplatePart("PART_Root", typeof(Control))]
[TemplatePart("PART_LeadingTray", typeof(StackPanel))]
[TemplatePart("PART_TrailingTray", typeof(StackPanel))]
[TemplatePart("PART_ContentHost", typeof(Border))]
public partial class SwipeableRow : ContentControl
{
    /// <summary>The limit a pull stretches past the revealed tray width.</summary>
    private const double Stretch = 64;

    /// <summary>A release coasts this long at its velocity to decide whether to open or close.</summary>
    private static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.18);

    /// <summary>A flick faster than this (px/s) opens or closes the row immediately.</summary>
    private const double FlickVelocity = 420;

    /// <summary>Share of the row's width past which a drag triggers a full-swipe action.</summary>
    private const double FullSwipeFraction = 0.55;

    /// <summary>Extra distance past the tray width required before a full swipe triggers.</summary>
    private const double FullSwipeExtra = 40;

    /// <summary>Whether either action tray is currently open. Setting it false closes the row; setting it true opens the trailing (or leading) tray.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<SwipeableRow, bool>(nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Which side's tray is open: <see cref="SwipeRowSide.None"/> while closed.</summary>
    public static readonly DirectProperty<SwipeableRow, SwipeRowSide> OpenSideProperty =
        AvaloniaProperty.RegisterDirect<SwipeableRow, SwipeRowSide>(nameof(OpenSide), o => o.OpenSide);

    /// <summary>Whether dragging far past the tray width automatically invokes the first visible enabled action on that side: true.</summary>
    public static readonly StyledProperty<bool> AllowFullSwipeProperty =
        AvaloniaProperty.Register<SwipeableRow, bool>(nameof(AllowFullSwipe), true);

    /// <summary>Raised when a revealed action button is pressed or triggered by a full swipe.</summary>
    public static readonly RoutedEvent<SwipeRowActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<SwipeableRow, SwipeRowActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    private readonly TranslateTransform _shift = new();
    private readonly List<SwipeRowAction> _watchedActions = [];
    private Control? _root;
    private StackPanel? _leadingTray;
    private StackPanel? _trailingTray;
    private Border? _contentHost;
    private MotionValue<double>? _x;
    private DragTracker? _drag;
    private double _dragOrigin;
    private bool _dragging;
    private bool _fullSwipeArmed;
    private SwipeRowSide _openSide;
    private bool _syncingOpen;

    /// <summary>Creates an empty swipeable row.</summary>
    public SwipeableRow()
    {
        LeadingActions.CollectionChanged += OnActionsCollectionChanged;
        TrailingActions.CollectionChanged += OnActionsCollectionChanged;
    }

    /// <summary>Actions revealed on the left side when swiping right.</summary>
    public AvaloniaList<SwipeRowAction> LeadingActions { get; } = [];

    /// <summary>Actions revealed on the right side when swiping left.</summary>
    public AvaloniaList<SwipeRowAction> TrailingActions { get; } = [];

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="OpenSideProperty"/>
    public SwipeRowSide OpenSide
    {
        get => _openSide;
        private set => SetAndRaise(OpenSideProperty, ref _openSide, value);
    }

    /// <inheritdoc cref="AllowFullSwipeProperty"/>
    public bool AllowFullSwipe
    {
        get => GetValue(AllowFullSwipeProperty);
        set => SetValue(AllowFullSwipeProperty, value);
    }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<SwipeRowActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <summary>Current horizontal offset of the row content in px (positive reveals leading actions, negative reveals trailing actions). For tests and diagnostics.</summary>
    public double SwipeOffset => _x?.Value ?? 0;

    protected override Type StyleKeyOverride => typeof(SwipeableRow);

    private bool Reduced => ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;

    private double ActionWidth =>
        this.TryFindResource(SwipeableRowTokens.Key(SwipeableRowTokenKind.ActionWidth), out var value) && value is double w && w > 0 ? w : 76;

    private double IconExtent =>
        this.TryFindResource(SwipeableRowTokens.Key(SwipeableRowTokenKind.IconExtent), out var value) && value is double s && s > 0 ? s : 20;

    private double IconGap =>
        this.TryFindResource(SwipeableRowTokens.Key(SwipeableRowTokenKind.IconGap), out var value) && value is double g && g >= 0 ? g : 4;

    private double LeadingWidth => CountVisible(LeadingActions) * ActionWidth;

    private double TrailingWidth => CountVisible(TrailingActions) * ActionWidth;

    /// <summary>Reveals <see cref="LeadingActions"/> if any are visible.</summary>
    public void OpenLeading()
    {
        if (LeadingWidth > 0)
        {
            SettleTo(SwipeRowSide.Leading, null);
        }
    }

    /// <summary>Reveals <see cref="TrailingActions"/> if any are visible.</summary>
    public void OpenTrailing()
    {
        if (TrailingWidth > 0)
        {
            SettleTo(SwipeRowSide.Trailing, null);
        }
    }

    /// <summary>Closes any open action tray.</summary>
    public void Close() => SettleTo(SwipeRowSide.None, null);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        _root = e.NameScope.Find<Control>("PART_Root");
        _leadingTray = e.NameScope.Find<StackPanel>("PART_LeadingTray");
        _trailingTray = e.NameScope.Find<StackPanel>("PART_TrailingTray");
        _contentHost = e.NameScope.Find<Border>("PART_ContentHost");
        if (_root is null || _contentHost is null)
        {
            return;
        }
        _contentHost.RenderTransform = _shift;
        _contentHost.AddHandler(PointerPressedEvent, OnContentPointerPressed, RoutingStrategies.Tunnel);
        _x = new MotionValue<double>(_contentHost, TargetOffsetFor(OpenSide), MotionVectors.Double);
        _x.Changed += _ => Apply();
        AttachGestures();
        RebuildTrays();
        Apply();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnwatchAllActions();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildTrays();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty && !_syncingOpen)
        {
            if (change.GetNewValue<bool>())
            {
                if (TrailingWidth > 0)
                {
                    SettleTo(SwipeRowSide.Trailing, null);
                }
                else if (LeadingWidth > 0)
                {
                    SettleTo(SwipeRowSide.Leading, null);
                }
                else
                {
                    SyncOpenState(SwipeRowSide.None);
                }
            }
            else
            {
                SettleTo(SwipeRowSide.None, null);
            }
        }
        else if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled && OpenSide != SwipeRowSide.None)
        {
            Close();
        }
    }

    private void Detach()
    {
        DetachGestures();
        _contentHost?.RemoveHandler(PointerPressedEvent, OnContentPointerPressed);
        _x?.Dispose();
        _x = null;
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildTrays();
        if (OpenSide == SwipeRowSide.Leading && LeadingWidth <= 0)
        {
            Close();
        }
        else if (OpenSide == SwipeRowSide.Trailing && TrailingWidth <= 0)
        {
            Close();
        }
        else if (OpenSide != SwipeRowSide.None && !_dragging && _x is not null)
        {
            _x.Jump(TargetOffsetFor(OpenSide));
        }
    }

    private void UnwatchAllActions()
    {
        foreach (var action in _watchedActions)
        {
            action.PropertyChanged -= OnActionPropertyChanged;
        }
        _watchedActions.Clear();
    }

    private void OnActionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RebuildTrays();

    private void RebuildTrays()
    {
        UnwatchAllActions();
        WatchCollection(LeadingActions);
        WatchCollection(TrailingActions);
        PopulateTray(_leadingTray, LeadingActions, SwipeRowSide.Leading);
        PopulateTray(_trailingTray, TrailingActions, SwipeRowSide.Trailing);
        Apply();
    }

    private void WatchCollection(AvaloniaList<SwipeRowAction> actions)
    {
        foreach (var action in actions)
        {
            action.PropertyChanged += OnActionPropertyChanged;
            _watchedActions.Add(action);
        }
    }

    private void PopulateTray(StackPanel? tray, AvaloniaList<SwipeRowAction> actions, SwipeRowSide side)
    {
        if (tray is null)
        {
            return;
        }
        tray.Children.Clear();
        foreach (var action in actions)
        {
            if (!action.IsVisible)
            {
                continue;
            }
            tray.Children.Add(CreateActionButton(action, side));
        }
    }

    private AvaloniaButton CreateActionButton(SwipeRowAction action, SwipeRowSide side)
    {
        var body = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = IconGap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (action.Icon is { } icon)
        {
            body.Children.Add(new ArcGlyph
            {
                Data = icon,
                Size = IconExtent,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }
        if (!string.IsNullOrEmpty(action.Label))
        {
            body.Children.Add(new TextBlock
            {
                Text = action.Label,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
            });
        }

        var button = new AvaloniaButton
        {
            Content = body,
            IsEnabled = action.IsEnabled,
            Focusable = true,
        };
        button.Classes.Add("arc-swipe-action");
        if (action.Role == SwipeRowActionRole.Primary)
        {
            button.Classes.Add("primary");
        }
        else if (action.Role == SwipeRowActionRole.Destructive)
        {
            button.Classes.Add("destructive");
        }
        if (!string.IsNullOrEmpty(action.Label))
        {
            AutomationProperties.SetName(button, action.Label);
        }
        button.Click += (_, _) => InvokeAction(action, side);
        return button;
    }

    private void InvokeAction(SwipeRowAction action, SwipeRowSide side)
    {
        if (!action.IsEnabled || !action.IsVisible)
        {
            return;
        }
        if (action.Role == SwipeRowActionRole.Destructive)
        {
            Haptics.Trigger(HapticKind.Warning);
        }
        RaiseEvent(new SwipeRowActionEventArgs(ActionInvokedEvent, action, side));
        if (action.Command is { } command && command.CanExecute(action.CommandParameter))
        {
            command.Execute(action.CommandParameter);
        }
        if (action.ClosesOnInvoke)
        {
            Close();
        }
    }

    private static int CountVisible(AvaloniaList<SwipeRowAction> actions)
    {
        var count = 0;
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i].IsVisible)
            {
                count++;
            }
        }
        return count;
    }

    private static SwipeRowAction? FirstEnabledAction(AvaloniaList<SwipeRowAction> actions)
    {
        for (var i = 0; i < actions.Count; i++)
        {
            if (actions[i].IsVisible && actions[i].IsEnabled)
            {
                return actions[i];
            }
        }
        return null;
    }

    private double TargetOffsetFor(SwipeRowSide side) => side switch
    {
        SwipeRowSide.Leading => LeadingWidth,
        SwipeRowSide.Trailing => -TrailingWidth,
        _ => 0,
    };

    private void SyncOpenState(SwipeRowSide side)
    {
        OpenSide = side;
        var open = side != SwipeRowSide.None;
        if (IsOpen != open)
        {
            _syncingOpen = true;
            try
            {
                SetCurrentValue(IsOpenProperty, open);
            }
            finally
            {
                _syncingOpen = false;
            }
        }
    }

    private void CloseSiblingRows()
    {
        if (this.GetVisualParent() is not Visual parent)
        {
            return;
        }
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is SwipeableRow sibling && !ReferenceEquals(sibling, this) && sibling.OpenSide != SwipeRowSide.None)
            {
                sibling.Close();
            }
        }
    }

    private void SettleTo(SwipeRowSide side, double? velocity)
    {
        if (side != SwipeRowSide.None)
        {
            CloseSiblingRows();
        }
        SyncOpenState(side);
        var target = TargetOffsetFor(side);
        if (_x is null)
        {
            return;
        }
        if (Reduced)
        {
            _x.Stop();
            _x.Jump(target);
            return;
        }
        _ = MomentumProjector.SettleTo(_x, target, velocity ?? 0, ArcSpring.Smooth);
    }

    private void Apply()
    {
        var x = _x?.Value ?? 0;
        _shift.X = x;
        if (_leadingTray is not null)
        {
            _leadingTray.IsVisible = x > 0.5 && LeadingWidth > 0;
        }
        if (_trailingTray is not null)
        {
            _trailingTray.IsVisible = x < -0.5 && TrailingWidth > 0;
        }
    }
}
