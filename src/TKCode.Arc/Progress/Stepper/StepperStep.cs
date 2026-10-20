using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Which steps of a <see cref="Stepper"/> show their description.</summary>
public enum StepperDetails
{
    /// <summary>Every step shows its description.</summary>
    All,

    /// <summary>Only the current step shows its description (tight spaces). Errors always show.</summary>
    Current
}

/// <summary>State of one step in a <see cref="Stepper"/>.</summary>
public enum StepperStepStatus
{
    Upcoming,
    Current,
    Complete,
    Error
}

/// <summary>One step of a <see cref="Stepper"/> (Arc <c>steps[]</c> entry).</summary>
public class StepperStep : AvaloniaObject
{
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<StepperStep, string?>(nameof(Id));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StepperStep, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<StepperStep, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<StepperStep, string?>(nameof(Error));

    public StepperStep()
    {
    }

    public StepperStep(string label, string? description = null)
    {
        Label = label;
        Description = description;
    }

    /// <summary>Stable key, so each step keeps its own motion when the list changes.</summary>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>A short hint under the label.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Marks the step as failed: the marker becomes an alert and this message replaces the description.</summary>
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    public override string ToString() => Label ?? string.Empty;
}

/// <summary>Arguments of <see cref="Stepper.StepSelectedEvent"/>.</summary>
public sealed class StepperStepSelectedEventArgs : RoutedEventArgs
{
    public StepperStepSelectedEventArgs(RoutedEvent routedEvent, int index, StepperStep step) : base(routedEvent)
    {
        Index = index;
        Step = step;
    }

    /// <summary>Index of the completed step that was chosen.</summary>
    public int Index { get; }

    public StepperStep Step { get; }
}
