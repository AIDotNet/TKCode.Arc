using System.Windows.Input;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One action of a <see cref="PageHeader"/>: a button in the header row (<see cref="PageHeader.Actions"/>,
/// <see cref="PageHeader.PrimaryAction"/>) or a row of its "More actions" menu (<see cref="PageHeader.MoreActions"/>,
/// and the secondary actions once they fold away). Changing <see cref="Label"/> or <see cref="Icon"/> swaps the button
/// content in place (Follow becomes Following) while the button width springs to fit.
/// </summary>
public class PageHeaderAction : AvaloniaObject
{
    /// <summary>Button text, and the accessible name of the button and its menu row.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PageHeaderAction, string?>(nameof(Label));

    /// <summary>Leading icon: a 24×24 Lucide <see cref="Geometry"/> (drawn as a 16px glyph) or a control (button only).</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<PageHeaderAction, object?>(nameof(Icon));

    /// <summary>Text of the menu row when it differs from the button (Arc: "Following" on the button, "Unfollow" in the menu).</summary>
    public static readonly StyledProperty<string?> MenuLabelProperty =
        AvaloniaProperty.Register<PageHeaderAction, string?>(nameof(MenuLabel));

    /// <summary>Icon of the menu row when it differs from the button; falls back to a <see cref="Geometry"/> <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<object?> MenuIconProperty =
        AvaloniaProperty.Register<PageHeaderAction, object?>(nameof(MenuIcon));

    /// <summary>Command run when the action is chosen.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<PageHeaderAction, ICommand?>(nameof(Command));

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<PageHeaderAction, object?>(nameof(CommandParameter));

    /// <summary>False disables the button and dims the menu row. Defaults to true.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<PageHeaderAction, bool>(nameof(IsEnabled), true);

    /// <summary>Shows the button spinner and ignores clicks while true; the menu row is disabled meanwhile.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<PageHeaderAction, bool>(nameof(IsLoading));

    /// <summary>Draws the menu row in the danger color (destructive actions in <see cref="PageHeader.MoreActions"/>).</summary>
    public static readonly StyledProperty<bool> IsDestructiveProperty =
        AvaloniaProperty.Register<PageHeaderAction, bool>(nameof(IsDestructive));

    /// <summary>Free application data (for example an id to tell actions apart in one handler).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<PageHeaderAction, object?>(nameof(Tag));

    /// <summary>Button text, and the accessible name of the button and its menu row.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Leading icon: a 24×24 Lucide <see cref="Geometry"/> (drawn as a 16px glyph) or a control (button only).</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Text of the menu row when it differs from the button (Arc: "Following" on the button, "Unfollow" in the menu).</summary>
    public string? MenuLabel
    {
        get => GetValue(MenuLabelProperty);
        set => SetValue(MenuLabelProperty, value);
    }

    /// <summary>Icon of the menu row when it differs from the button; falls back to a <see cref="Geometry"/> <see cref="Icon"/>.</summary>
    public object? MenuIcon
    {
        get => GetValue(MenuIconProperty);
        set => SetValue(MenuIconProperty, value);
    }

    /// <summary>Command run when the action is chosen.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Parameter passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>False disables the button and dims the menu row. Defaults to true.</summary>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }

    /// <summary>Shows the button spinner and ignores clicks while true; the menu row is disabled meanwhile.</summary>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>Draws the menu row in the danger color (destructive actions in <see cref="PageHeader.MoreActions"/>).</summary>
    public bool IsDestructive
    {
        get => GetValue(IsDestructiveProperty);
        set => SetValue(IsDestructiveProperty, value);
    }

    /// <summary>Free application data (for example an id to tell actions apart in one handler).</summary>
    public object? Tag
    {
        get => GetValue(TagProperty);
        set => SetValue(TagProperty, value);
    }

    /// <summary>Raised when the action is chosen from its button or its menu row (before <see cref="Command"/> runs).</summary>
    public event EventHandler? Click;

    private ICommand? _subscribedCommand;
    private EventHandler? _stateChanged;

    /// <summary>
    /// Raised when <see cref="CanExecute"/> may have changed (command state, enabled, loading). The command is followed
    /// only while someone listens here (a header that shows the action): a command that outlives the view would otherwise
    /// keep the action alive, and with it an <see cref="Icon"/> control and the header around it.
    /// </summary>
    internal event EventHandler? StateChanged
    {
        add
        {
            _stateChanged += value;
            FollowCommand();
        }
        remove
        {
            _stateChanged -= value;
            FollowCommand();
        }
    }

    /// <summary>True when the action can be chosen now: enabled, not loading, and its command can execute.</summary>
    internal bool CanExecute => IsEnabled && !IsLoading && (Command is not { } command || command.CanExecute(CommandParameter));

    /// <summary>The label of the menu row.</summary>
    internal string? EffectiveMenuLabel => MenuLabel ?? Label;

    /// <summary>The icon of the menu row: <see cref="MenuIcon"/>, else <see cref="Icon"/> when it is a geometry (a control can only live in the button).</summary>
    internal object? EffectiveMenuIcon => MenuIcon ?? (Icon as Geometry);

    /// <summary>Chooses the action: raises <see cref="Click"/> and runs <see cref="Command"/> when it can execute.</summary>
    public void Invoke()
    {
        if (!IsEnabled || IsLoading)
        {
            return;
        }
        Click?.Invoke(this, EventArgs.Empty);
        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandProperty)
        {
            FollowCommand();
            _stateChanged?.Invoke(this, EventArgs.Empty);
        }
        else if (change.Property == IsEnabledProperty || change.Property == IsLoadingProperty || change.Property == CommandParameterProperty)
        {
            _stateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void FollowCommand()
    {
        var wanted = _stateChanged is null ? null : Command;
        if (ReferenceEquals(wanted, _subscribedCommand))
        {
            return;
        }
        if (_subscribedCommand is not null)
        {
            _subscribedCommand.CanExecuteChanged -= OnCanExecuteChanged;
        }
        _subscribedCommand = wanted;
        if (_subscribedCommand is not null)
        {
            _subscribedCommand.CanExecuteChanged += OnCanExecuteChanged;
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e) => _stateChanged?.Invoke(this, EventArgs.Empty);
}
