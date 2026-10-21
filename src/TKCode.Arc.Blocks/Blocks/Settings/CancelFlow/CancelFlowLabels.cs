using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The built-in text of a <see cref="CancelFlow"/>. Replace the whole object (with an object initializer) to localize
/// or reword it; the flow rebuilds its text when <see cref="CancelFlow.Labels"/> changes. Dates use <see cref="Culture"/>.
/// </summary>
public sealed class CancelFlowLabels
{
    /// <summary>The culture of dates such as the end of access (default en-US).</summary>
    public CultureInfo Culture { get; init; } = SettingsFormat.DefaultCulture;

    /// <summary>The step counter beside the step bar: (step, total) ⇒ "Step 1 of 3".</summary>
    public Func<int, int, string> StepCounter { get; init; } = static (step, total) => $"Step {step} of {total}";

    /// <summary>The name and tooltip of the back button: "Back".</summary>
    public string Back { get; init; } = "Back";

    /// <summary>The title of the first step: "Why are you canceling?". Also the name of the reason list.</summary>
    public string ReasonTitle { get; init; } = "Why are you canceling?";

    /// <summary>The sentence under the first title: plan name (or null) ⇒ "Your answer helps us improve Studio.".</summary>
    public Func<string?, string> ReasonDescription { get; init; } = static plan =>
        string.IsNullOrWhiteSpace(plan) ? "Your answer helps us improve." : $"Your answer helps us improve {plan}.";

    /// <summary>The feedback field's label after most reasons: "Anything else we should know?".</summary>
    public string FeedbackLabel { get; init; } = "Anything else we should know?";

    /// <summary>The follow-up question for a reason that asks for details but has no prompt of its own.</summary>
    public string DetailsPrompt { get; init; } = "Tell us a little more";

    /// <summary>The placeholder of the feedback field: "Optional".</summary>
    public string FeedbackPlaceholder { get; init; } = "Optional";

    /// <summary>The button that leaves the flow without canceling: "Keep my plan".</summary>
    public string KeepPlan { get; init; } = "Keep my plan";

    /// <summary>The primary button of the first step: "Continue".</summary>
    public string Continue { get; init; } = "Continue";

    /// <summary>The label of an offer's accept button when the offer has none: "Accept offer".</summary>
    public string AcceptOffer { get; init; } = "Accept offer";

    /// <summary>The button that declines the offer: "Continue canceling".</summary>
    public string ContinueCanceling { get; init; } = "Continue canceling";

    /// <summary>The title of the confirmation step: plan name (or null) ⇒ "Cancel your Studio plan?".</summary>
    public Func<string?, string> ConfirmTitle { get; init; } = static plan =>
        string.IsNullOrWhiteSpace(plan) ? "Cancel your plan?" : $"Cancel your {plan} plan?";

    /// <summary>The first sentence of the confirmation step: formatted end date (or null) ⇒ "You keep access until Nov 1, 2026.".</summary>
    public Func<string?, string> AccessUntil { get; init; } = static date =>
        date is null ? "Your access ends when you confirm." : $"You keep access until {date}.";

    /// <summary>Added to the confirmation sentence when there are losses to list: "After that:".</summary>
    public string AfterThat { get; init; } = "After that:";

    /// <summary>The accessible name of the list of losses: "What ends".</summary>
    public string LossesName { get; init; } = "What ends";

    /// <summary>The hold-to-confirm button: "Hold to cancel plan".</summary>
    public string HoldToCancel { get; init; } = "Hold to cancel plan";

    /// <summary>The hold-to-confirm button once the hold completes: "Canceled".</summary>
    public string HoldConfirmed { get; init; } = "Canceled";

    /// <summary>The title of the canceled step: "Your plan is canceled".</summary>
    public string CanceledTitle { get; init; } = "Your plan is canceled";

    /// <summary>
    /// The sentence of the canceled step: (plan name or null, formatted end date or null) ⇒ "You can keep using Studio
    /// until Nov 1, 2026. We sent a confirmation to your email.".
    /// </summary>
    public Func<string?, string?, string> CanceledDescription { get; init; } = static (plan, date) =>
        date is null
            ? "We sent a confirmation to your email."
            : string.IsNullOrWhiteSpace(plan)
                ? $"You can keep using your plan until {date}. We sent a confirmation to your email."
                : $"You can keep using {plan} until {date}. We sent a confirmation to your email.";

    /// <summary>The button of the canceled step: "Reactivate plan".</summary>
    public string Reactivate { get; init; } = "Reactivate plan";

    /// <summary>The title after an offer is accepted, when the offer has no title of its own: offer title ⇒ "{title} is on".</summary>
    public Func<string, string> RetainedTitle { get; init; } = static offer => $"{offer} is on";

    /// <summary>The sentence after an offer is accepted, when the offer has none of its own.</summary>
    public string RetainedDescription { get; init; } = "Thanks for staying. You'll see the change on your next invoice.";

    /// <summary>The button that closes the flow after an offer is accepted: "Done".</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The reasons offered when <see cref="CancelFlow.Reasons"/> is empty (five common reasons, no offers).</summary>
    public IReadOnlyList<CancelFlowReason> DefaultReasons { get; init; } =
    [
        new("Too expensive", "The price is more than it's worth to me right now."),
        new("Not using it enough", "I don't use it often enough to keep paying for it."),
        new("Missing a feature I need", "Something I rely on isn't there yet.")
        {
            AsksForDetails = true,
            DetailsPrompt = "What were you missing?"
        },
        new("Switching to another tool", "I'm moving my work to a different product.")
        {
            AsksForDetails = true,
            DetailsPrompt = "Which tool are you moving to?"
        },
        new("Something else", "My reason isn't listed here.")
        {
            AsksForDetails = true,
            DetailsPrompt = "What made you decide to cancel?"
        },
    ];
}
