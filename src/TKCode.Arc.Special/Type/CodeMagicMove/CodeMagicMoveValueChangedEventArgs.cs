using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="CodeMagicMove.ValueChanged"/>.</summary>
public sealed class CodeMagicMoveValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CodeMagicMoveValueChangedEventArgs(RoutedEvent routedEvent, int oldValue, int newValue)
        : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The step shown before.</summary>
    public int OldValue { get; }

    /// <summary>The step the walkthrough moves to.</summary>
    public int NewValue { get; }
}
