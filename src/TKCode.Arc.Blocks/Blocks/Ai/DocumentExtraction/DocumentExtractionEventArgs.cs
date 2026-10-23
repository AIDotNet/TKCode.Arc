using System.Collections.Generic;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>DocumentExtraction.ExtractionCompleted</c>.</summary>
public class ExtractionCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ExtractionCompletedEventArgs(RoutedEvent routedEvent, object source, ExtractionState result, string? error)
        : base(routedEvent, source)
    {
        Result = result;
        Error = error;
    }

    /// <summary><see cref="ExtractionState.Done"/>, <see cref="ExtractionState.Stopped"/> or <see cref="ExtractionState.Failed"/>.</summary>
    public ExtractionState Result { get; }

    /// <summary>What went wrong, when it failed.</summary>
    public string? Error { get; }
}

/// <summary>Arguments of <c>DocumentExtraction.ValueChanged</c>.</summary>
public class ExtractionValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ExtractionValueChangedEventArgs(RoutedEvent routedEvent, object source, string path, string oldText, string newText, ExtractionOrigin origin)
        : base(routedEvent, source)
    {
        Path = path;
        OldText = oldText;
        NewText = newText;
        Origin = origin;
    }

    /// <summary>The path of the value.</summary>
    public string Path { get; }

    /// <summary>The text before.</summary>
    public string OldText { get; }

    /// <summary>The text after.</summary>
    public string NewText { get; }

    /// <summary>Whether the model or the person changed it.</summary>
    public ExtractionOrigin Origin { get; }
}

/// <summary>Arguments of <c>DocumentExtraction.ValueChecked</c> and <c>SelectedPathChanged</c>.</summary>
public class ExtractionPathEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ExtractionPathEventArgs(RoutedEvent routedEvent, object source, string? path)
        : base(routedEvent, source)
    {
        Path = path;
    }

    /// <summary>The path; null when the selection was cleared.</summary>
    public string? Path { get; }
}

/// <summary>
/// Arguments of <c>DocumentExtraction.Accepting</c>: save the output (take a deferral to do it asynchronously). Set
/// <see cref="Cancel"/> or throw from the handler to undo the accept; every field then goes back to unaccepted.
/// </summary>
public class ExtractionAcceptingEventArgs : RoutedEventArgs
{
    private readonly List<Task> _deferrals = new();

    /// <summary>Creates the arguments.</summary>
    public ExtractionAcceptingEventArgs(RoutedEvent routedEvent, object source, ExtractionOutput output, IReadOnlyList<ExtractionValue> values)
        : base(routedEvent, source)
    {
        Output = output;
        Values = values;
    }

    /// <summary>The typed output.</summary>
    public ExtractionOutput Output { get; }

    /// <summary>Every value.</summary>
    public IReadOnlyList<ExtractionValue> Values { get; }

    /// <summary>Set to refuse the accept.</summary>
    public bool Cancel { get; set; }

    /// <summary>Registers work the control waits for before it shows the fields as accepted; complete the task, or fault it to undo the accept.</summary>
    /// <param name="work">The work.</param>
    public void Defer(Task work) => _deferrals.Add(work);

    /// <summary>The registered work (used by the control).</summary>
    public IReadOnlyList<Task> Deferrals => _deferrals;
}
