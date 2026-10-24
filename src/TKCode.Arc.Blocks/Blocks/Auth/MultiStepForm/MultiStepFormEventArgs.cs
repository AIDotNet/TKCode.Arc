using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="MultiStepForm.StepChanged"/>: the step that was shown, the one shown now and the way it went.</summary>
public sealed class MultiStepFormStepChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MultiStepFormStepChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex) : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }

    /// <summary>The zero-based index of the step that was shown.</summary>
    public int OldIndex { get; }

    /// <summary>The zero-based index of the step shown now.</summary>
    public int NewIndex { get; }

    /// <summary>1 when the form moved forward, -1 when it moved back.</summary>
    public int Direction => NewIndex >= OldIndex ? 1 : -1;
}

/// <summary>
/// Arguments of <see cref="MultiStepForm.StepValidating"/>, raised when a step is submitted and before its
/// <see cref="MultiStepFormStep.ValidateAsync"/> runs. Set <see cref="Cancel"/> to stay on the step.
/// </summary>
public sealed class MultiStepFormStepValidatingEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MultiStepFormStepValidatingEventArgs(RoutedEvent routedEvent, MultiStepFormStep step, int index) : base(routedEvent)
    {
        Step = step;
        Index = index;
    }

    /// <summary>The step being submitted.</summary>
    public MultiStepFormStep Step { get; }

    /// <summary>Its zero-based index.</summary>
    public int Index { get; }

    /// <summary>Set to true to keep the form on this step.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <see cref="MultiStepForm.Completed"/>, raised once the last step was submitted and the success state is shown.</summary>
public sealed class MultiStepFormCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MultiStepFormCompletedEventArgs(RoutedEvent routedEvent, IReadOnlyList<MultiStepFormStep> steps) : base(routedEvent)
    {
        Steps = steps;
    }

    /// <summary>The form's steps, in order (their content still holds what was entered).</summary>
    public IReadOnlyList<MultiStepFormStep> Steps { get; }
}

/// <summary>Arguments of <see cref="MultiStepForm.CompleteFailed"/>: <see cref="MultiStepForm.CompleteAsync"/> threw, so the form stayed on the last step.</summary>
public sealed class MultiStepFormCompleteFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MultiStepFormCompleteFailedEventArgs(RoutedEvent routedEvent, Exception exception) : base(routedEvent)
    {
        Exception = exception;
    }

    /// <summary>What the completion handler threw.</summary>
    public Exception Exception { get; }
}
