using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc.Mobile;

/// <summary>The visual emphasis of a <see cref="SwipeRowAction"/>.</summary>
public enum SwipeRowActionRole
{
    /// <summary>A neutral action on a muted surface.</summary>
    Normal,

    /// <summary>An emphasized action filled with the theme's accent color.</summary>
    Primary,

    /// <summary>A destructive action filled with the theme's danger color (for example Delete).</summary>
    Destructive,
}

/// <summary>Which side of a <see cref="SwipeableRow"/> is currently open.</summary>
public enum SwipeRowSide
{
    /// <summary>The row is closed and centered.</summary>
    None,

    /// <summary>The leading (left) actions are revealed.</summary>
    Leading,

    /// <summary>The trailing (right) actions are revealed.</summary>
    Trailing,
}

/// <summary>Event data of <see cref="SwipeableRow.ActionInvoked"/>.</summary>
public sealed class SwipeRowActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public SwipeRowActionEventArgs(RoutedEvent routedEvent, SwipeRowAction action, SwipeRowSide side)
        : base(routedEvent)
    {
        Action = action;
        Side = side;
    }

    /// <summary>The action that was tapped or triggered by a full swipe.</summary>
    public SwipeRowAction Action { get; }

    /// <summary>The side that hosted <see cref="Action"/>.</summary>
    public SwipeRowSide Side { get; }
}

/// <summary>
/// One action revealed behind a <see cref="SwipeableRow"/> when the person swipes it horizontally.
/// </summary>
public class SwipeRowAction : AvaloniaObject
{
    /// <summary>The short label shown inside the action button.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SwipeRowAction, string?>(nameof(Label));

    /// <summary>An optional glyph geometry shown above <see cref="Label"/> (for example a Lucide icon).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SwipeRowAction, Geometry?>(nameof(Icon));

    /// <summary>How the action is colored: neutral, accent or destructive.</summary>
    public static readonly StyledProperty<SwipeRowActionRole> RoleProperty =
        AvaloniaProperty.Register<SwipeRowAction, SwipeRowActionRole>(nameof(Role));

    /// <summary>The command run when the action is invoked.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SwipeRowAction, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SwipeRowAction, object?>(nameof(CommandParameter));

    /// <summary>Whether invoking this action closes the <see cref="SwipeableRow"/>: true by default.</summary>
    public static readonly StyledProperty<bool> ClosesOnInvokeProperty =
        AvaloniaProperty.Register<SwipeRowAction, bool>(nameof(ClosesOnInvoke), true);

    /// <summary>Whether the action can be invoked: true by default.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<SwipeRowAction, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the action is shown in the tray: true by default.</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<SwipeRowAction, bool>(nameof(IsVisible), true);

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="RoleProperty"/>
    public SwipeRowActionRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <inheritdoc cref="ClosesOnInvokeProperty"/>
    public bool ClosesOnInvoke
    {
        get => GetValue(ClosesOnInvokeProperty);
        set => SetValue(ClosesOnInvokeProperty, value);
    }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }
}
