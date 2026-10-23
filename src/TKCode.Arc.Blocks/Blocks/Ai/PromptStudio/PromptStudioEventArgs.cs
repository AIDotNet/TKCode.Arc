using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <c>PromptStudio.TemplateChanged</c>.</summary>
public class PromptTemplateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptTemplateChangedEventArgs(RoutedEvent routedEvent, object source, PromptTemplate template)
        : base(routedEvent, source)
    {
        Template = template;
    }

    /// <summary>The template as it is after the edit.</summary>
    public PromptTemplate Template { get; }
}

/// <summary>Arguments of <c>PromptStudio.RunStarted</c>.</summary>
public class PromptRunStartedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptRunStartedEventArgs(RoutedEvent routedEvent, object source, PromptRunRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What is being run.</summary>
    public PromptRunRequest Request { get; }
}

/// <summary>Arguments of <c>PromptStudio.RunCompleted</c>.</summary>
public class PromptRunCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptRunCompletedEventArgs(RoutedEvent routedEvent, object source, PromptRunState state, PromptRunResult? result)
        : base(routedEvent, source)
    {
        State = state;
        Result = result;
    }

    /// <summary><see cref="PromptRunState.Done"/>, <see cref="PromptRunState.Stopped"/> or <see cref="PromptRunState.Failed"/>.</summary>
    public PromptRunState State { get; }

    /// <summary>What the run produced; null when it failed before any text.</summary>
    public PromptRunResult? Result { get; }
}

/// <summary>Arguments of <c>PromptStudio.RunFailed</c>: change <see cref="Message"/> to word the error yourself.</summary>
public class PromptRunFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptRunFailedEventArgs(RoutedEvent routedEvent, object source, Exception exception)
        : base(routedEvent, source)
    {
        Exception = exception;
        Message = exception.Message;
    }

    /// <summary>The exception the handler threw.</summary>
    public Exception Exception { get; }

    /// <summary>The text shown in the error box; starts as the exception's message.</summary>
    public string Message { get; set; }
}

/// <summary>Arguments of <c>PromptStudio.Promoted</c>: the production version changed; set <see cref="Cancel"/> to roll it back.</summary>
public class PromptPromotedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptPromotedEventArgs(RoutedEvent routedEvent, object source, PromptVersion version, PromptVersion? previous, bool isUndo)
        : base(routedEvent, source)
    {
        Version = version;
        Previous = previous;
        IsUndo = isUndo;
    }

    /// <summary>The version that is in production now.</summary>
    public PromptVersion Version { get; }

    /// <summary>The version that was in production before.</summary>
    public PromptVersion? Previous { get; }

    /// <summary>True when this undoes the last promotion (the arguments are swapped).</summary>
    public bool IsUndo { get; }

    /// <summary>Set to true to roll the change back (the studio updates first and waits for you to say no).</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <c>PromptStudio.VariableAdded</c>: a new name appeared in the template.</summary>
public class PromptVariableAddedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PromptVariableAddedEventArgs(RoutedEvent routedEvent, object source, string name)
        : base(routedEvent, source)
    {
        Name = name;
    }

    /// <summary>The variable's name.</summary>
    public string Name { get; }
}
