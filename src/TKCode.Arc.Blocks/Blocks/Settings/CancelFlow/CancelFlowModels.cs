using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The steps of a <see cref="CancelFlow"/>, in the order they are usually met.</summary>
public enum CancelFlowStep
{
    /// <summary>The first step: choose why you are canceling and, optionally, say more.</summary>
    Reason,

    /// <summary>An alternative that fits the reason, shown only when the reason or the flow has an offer.</summary>
    Offer,

    /// <summary>What ends and when, with a hold-to-confirm button.</summary>
    Confirm,

    /// <summary>The end of a confirmed cancellation, with a way to reactivate.</summary>
    Canceled,

    /// <summary>The end of a flow where an offer was accepted and the plan stays.</summary>
    Retained
}

/// <summary>How a <see cref="CancelFlow"/> is drawn.</summary>
public enum CancelFlowAppearance
{
    /// <summary>A card: surface, 1px border, panel radius, resting shadow and inner padding.</summary>
    Card,

    /// <summary>No frame, shadow or padding, for a flow that sits inside another surface such as a dialog.</summary>
    Plain
}

/// <summary>
/// One answer to "Why are you canceling?" in a <see cref="CancelFlow"/>, with an optional offer made to people who
/// choose it. Write reasons inline in <see cref="CancelFlow.Reasons"/> or build them in code.
/// </summary>
public sealed class CancelFlowReason
{
    /// <summary>Creates an empty reason (for XAML).</summary>
    public CancelFlowReason()
    {
    }

    /// <summary>Creates a reason with a title and an optional description.</summary>
    public CancelFlowReason(string title, string? description = null)
    {
        Title = title;
        Description = description;
    }

    /// <summary>The reason as the person would say it, such as "Too expensive".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>A short line under the title that makes the reason clearer.</summary>
    public string? Description { get; set; }

    /// <summary>The offer shown after this reason; null falls back to <see cref="CancelFlow.DefaultOffer"/>.</summary>
    public CancelFlowOffer? Offer { get; set; }

    /// <summary>
    /// When true, the feedback field asks a follow-up question (<see cref="DetailsPrompt"/>) instead of the general one.
    /// The answer stays optional.
    /// </summary>
    public bool AsksForDetails { get; set; }

    /// <summary>The follow-up question when <see cref="AsksForDetails"/> is true, such as "What were you missing?".</summary>
    public string? DetailsPrompt { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>An alternative to canceling that a <see cref="CancelFlow"/> offers before the confirmation step.</summary>
public sealed class CancelFlowOffer
{
    /// <summary>What is offered, such as "Take 40% off for 3 months".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>One or two sentences on what the offer means for the plan and the bill.</summary>
    public string? Description { get; set; }

    /// <summary>The label of the button that accepts the offer, such as "Claim 40% off"; null uses the labels' default.</summary>
    public string? AcceptLabel { get; set; }

    /// <summary>A 24×24 stroke icon for the tile above the title; null shows a gift.</summary>
    public Geometry? Icon { get; set; }

    /// <summary>The title of the last step after the offer is accepted, such as "40% off is on"; null uses the labels.</summary>
    public string? AcceptedTitle { get; set; }

    /// <summary>The sentence under <see cref="AcceptedTitle"/>; null uses the labels.</summary>
    public string? AcceptedDescription { get; set; }

    /// <inheritdoc />
    public override string ToString() => Title;
}
