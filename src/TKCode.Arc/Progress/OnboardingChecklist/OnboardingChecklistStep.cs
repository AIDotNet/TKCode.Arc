// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): the step description.
using System.Windows.Input;
using Avalonia;

namespace TKCode.Arc;

/// <summary>One step of an <see cref="OnboardingChecklist"/> (Arc <c>steps[]</c> entry).</summary>
public class OnboardingChecklistStep : AvaloniaObject
{
    /// <summary>The stable key of the step, used by <see cref="OnboardingChecklist.Completed"/>; the title when unset.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, string?>(nameof(Id));

    /// <summary>The name of the step, always visible.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, string?>(nameof(Title));

    /// <summary>A sentence shown while the step is open.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, string?>(nameof(Description));

    /// <summary>The label of the button that does the step. Without it, the step is checked off by hand.</summary>
    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, string?>(nameof(ActionLabel));

    /// <summary>Runs the step. Completing checks it off; throwing keeps it open and shows the exception's message.</summary>
    public static readonly StyledProperty<Func<Task>?> ActionProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, Func<Task>?>(nameof(Action));

    /// <summary>A command that runs the step when no Action is set; it checks the step off unless it throws.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to the command.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<OnboardingChecklistStep, object?>(nameof(CommandParameter));

    public OnboardingChecklistStep()
    {
    }

    public OnboardingChecklistStep(string id, string title, string? description = null)
    {
        Id = id;
        Title = title;
        Description = description;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="ActionLabelProperty"/>
    public string? ActionLabel { get => GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }

    /// <inheritdoc cref="ActionProperty"/>
    public Func<Task>? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    internal string Key => Id ?? Title ?? string.Empty;
}
