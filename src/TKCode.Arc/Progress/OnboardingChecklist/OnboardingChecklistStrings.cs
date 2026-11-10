namespace TKCode.Arc;

/// <summary>
/// The fixed words of an <see cref="OnboardingChecklist"/>, visible and spoken, in English by default. Set a new instance
/// to translate them; formats use <c>{0}</c>, <c>{1}</c> and <c>{2}</c> as each property says.
/// </summary>
public sealed class OnboardingChecklistStrings
{
    internal static readonly OnboardingChecklistStrings Default = new();

    /// <summary>The label of the button that shows a hidden checklist.</summary>
    public string Show { get; set; } = "Show";

    /// <summary>The label of the button that hides the checklist.</summary>
    public string Hide { get; set; } = "Hide";

    /// <summary>The accessible name of the show button.</summary>
    public string ShowChecklist { get; set; } = "Show checklist";

    /// <summary>The accessible name of the hide button.</summary>
    public string HideChecklist { get; set; } = "Hide checklist";

    /// <summary>The text after the rolling count under the title; {0} is the number of steps.</summary>
    public string CountFormat { get; set; } = "of {0} done";

    /// <summary>The text after the rolling count on the completed row.</summary>
    public string Completed { get; set; } = "completed";

    /// <summary>The accessible name of the progress ring.</summary>
    public string Progress { get; set; } = "Setup progress";

    /// <summary>The spoken value of the progress ring; {0} is the steps done, {1} the number of steps.</summary>
    public string ProgressFormat { get; set; } = "{0} of {1} steps done";

    /// <summary>The accessible name of the list of open steps.</summary>
    public string OpenSteps { get; set; } = "Steps to do";

    /// <summary>The accessible name of the list of completed steps.</summary>
    public string CompletedSteps { get; set; } = "Completed steps";

    /// <summary>Announced when a step is checked; {0} is its title, {1} the steps done, {2} the number of steps.</summary>
    public string StepDoneFormat { get; set; } = "{0} done. {1} of {2} complete.";

    /// <summary>Announced when a step is unchecked; {0} is its title, {1} the steps done, {2} the number of steps.</summary>
    public string StepUndoneFormat { get; set; } = "{0} marked as not done. {1} of {2} complete.";

    /// <summary>Announced when the last step is checked; {0} is the number of steps.</summary>
    public string AllDoneFormat { get; set; } = "All {0} steps done.";

    /// <summary>Shown when an action fails with an exception that has no message.</summary>
    public string ActionFailed { get; set; } = "That didn’t work. Try again.";
}
