using System.Windows.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which way a <see cref="TransferBox"/> moved its items.</summary>
public enum TransferDirection
{
    /// <summary>From <see cref="TransferBox.SourceItems"/> to <see cref="TransferBox.TargetItems"/>.</summary>
    ToTarget,

    /// <summary>From <see cref="TransferBox.TargetItems"/> back to <see cref="TransferBox.SourceItems"/>.</summary>
    ToSource
}

/// <summary>One of the two lists of a <see cref="TransferBox"/>.</summary>
public enum TransferBoxSide
{
    /// <summary>The list of <see cref="TransferBox.SourceItems"/> (left).</summary>
    Source,

    /// <summary>The list of <see cref="TransferBox.TargetItems"/> (right).</summary>
    Target
}

/// <summary>Arguments of <see cref="TransferBox.ItemsTransferred"/>: one event per move, however many items it carried.</summary>
public sealed class TransferBoxTransferredEventArgs : RoutedEventArgs
{
    public TransferBoxTransferredEventArgs(RoutedEvent routedEvent, TransferDirection direction, IReadOnlyList<object?> items)
        : base(routedEvent)
    {
        Direction = direction;
        Items = items;
    }

    /// <summary>Which way the items moved.</summary>
    public TransferDirection Direction { get; }

    /// <summary>The moved items (the same object references), in the order they had in the list they left.</summary>
    public IReadOnlyList<object?> Items { get; }
}

/// <summary>A command of <see cref="TransferBox"/>; its state follows the selection and the two collections.</summary>
internal sealed class TransferBoxCommand : ICommand
{
    private readonly Func<bool> _canExecute;
    private readonly Action _execute;

    public TransferBoxCommand(Func<bool> canExecute, Action execute)
    {
        _canExecute = canExecute;
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();

    public void Execute(object? parameter)
    {
        if (_canExecute())
        {
            _execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
