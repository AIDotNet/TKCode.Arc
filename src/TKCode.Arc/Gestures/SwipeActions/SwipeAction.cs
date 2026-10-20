using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Fill of a revealed <see cref="SwipeAction"/>.</summary>
public enum SwipeActionTone
{
    /// <summary>A quiet neutral fill (foreground 20% into the surface).</summary>
    Neutral,

    /// <summary>The accent fill.</summary>
    Accent,

    /// <summary>The danger fill; pair it with a label that names the destructive result.</summary>
    Danger
}

/// <summary>Edge of a <see cref="SwipeActionsRow"/> an action hides behind.</summary>
public enum SwipeActionSide
{
    /// <summary>The row is closed.</summary>
    None,

    /// <summary>Under the left edge, revealed by swiping right.</summary>
    Leading,

    /// <summary>Under the right edge, revealed by swiping left.</summary>
    Trailing
}

/// <summary>Arguments of <see cref="SwipeAction.Invoked"/> and <see cref="SwipeActions.ActionInvokedEvent"/>.</summary>
public sealed class SwipeActionInvokedEventArgs : RoutedEventArgs
{
    public SwipeActionInvokedEventArgs(RoutedEvent? routedEvent, SwipeAction action, SwipeActionSide side, SwipeActionsRow row, object? item)
        : base(routedEvent)
    {
        Action = action;
        Side = side;
        Row = row;
        Item = item;
    }

    /// <summary>The action that ran.</summary>
    public SwipeAction Action { get; }

    /// <summary>The side it was revealed from.</summary>
    public SwipeActionSide Side { get; }

    /// <summary>The row it ran on.</summary>
    public SwipeActionsRow Row { get; }

    /// <summary>The row's data item (the row itself for rows declared inline). Remove it here unless the action keeps the row.</summary>
    public object? Item { get; }
}

/// <summary>
/// One action of a <see cref="SwipeActionsRow"/>: revealed under the row's edge by a swipe (icon over label), committed
/// by a full swipe on the outermost action, a tap on the revealed action, or the row's More actions menu.
/// <para>
/// An action that removes its item (the default) runs once the row has slid away and collapsed, so the handler can remove
/// the item without a jump; set <see cref="KeepsRow"/> for actions such as Mark as unread, which run at once while the row
/// springs home.
/// </para>
/// </summary>
public class SwipeAction : AvaloniaObject
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SwipeAction, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SwipeAction, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<SwipeActionTone> ToneProperty =
        AvaloniaProperty.Register<SwipeAction, SwipeActionTone>(nameof(Tone));

    public static readonly StyledProperty<bool> KeepsRowProperty =
        AvaloniaProperty.Register<SwipeAction, bool>(nameof(KeepsRow));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<SwipeAction, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<SwipeAction, object?>(nameof(CommandParameter));

    /// <summary>Shown under the icon and in the menu (Arc: required).</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>24×24 Lucide geometry drawn at 20px (Arc: required), for example <c>ArcIcons.Archive</c>.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    public SwipeActionTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The row springs home after the action instead of sliding away and collapsing.</summary>
    public bool KeepsRow { get => GetValue(KeepsRowProperty); set => SetValue(KeepsRowProperty, value); }

    /// <summary>Runs after <see cref="Invoked"/>; its parameter defaults to the row's data item.</summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Raised when the action runs (full swipe, tap on the revealed action, or the menu).</summary>
    public event EventHandler<SwipeActionInvokedEventArgs>? Invoked;

    internal void Run(SwipeActionInvokedEventArgs e)
    {
        Invoked?.Invoke(this, e);
        var parameter = CommandParameter ?? e.Item;
        if (Command is { } command && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }
}
