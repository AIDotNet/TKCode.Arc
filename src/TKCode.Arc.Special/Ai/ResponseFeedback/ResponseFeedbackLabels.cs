namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="FeedbackActions"/> and <see cref="ResponseFeedback"/> show or announce, in English by default.
/// Replace the whole object to localize or reword. The reasons, the note's placeholder, the privacy note and the second line
/// of the thank-you state are properties of the control.
/// </summary>
public sealed class ResponseFeedbackLabels
{
    /// <summary>The English defaults.</summary>
    public static ResponseFeedbackLabels Default { get; } = new();

    /// <summary>The thumbs up button's name and tooltip.</summary>
    public string GoodResponse { get; init; } = "Good response";

    /// <summary>The thumbs down button's name and tooltip.</summary>
    public string BadResponse { get; init; } = "Bad response";

    /// <summary>The panel's title after a thumbs down; it also names the reasons group.</summary>
    public string NegativeTitle { get; init; } = "What went wrong?";

    /// <summary>The follow-up row's title after a thumbs up; it also names the reasons group.</summary>
    public string PositiveTitle { get; init; } = "What was good?";

    /// <summary>The reason that moves focus to the note and changes its placeholder (matched against the reason's text).</summary>
    public string Other { get; init; } = "Other";

    /// <summary>The note's placeholder while <see cref="Other"/> is selected.</summary>
    public string OtherPlaceholder { get; init; } = "Tell us what went wrong";

    /// <summary>Sends the reasons and the note after a thumbs down.</summary>
    public string Submit { get; init; } = "Submit";

    /// <summary>Sends the chips of the follow-up row after a thumbs up.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>Closes the follow-up row after a thumbs up without sending.</summary>
    public string Skip { get; init; } = "Skip";

    /// <summary>Closes the panel without sending.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The name and tooltip of the panel's close button.</summary>
    public string Close { get; init; } = "Close";

    /// <summary>Takes the place of Submit or Send after a failed send, and retries a failed rating in the row.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The note's name.</summary>
    public string Note { get; init; } = "Details";

    /// <summary>The first line of the thank-you state.</summary>
    public string Thanks { get; init; } = "Thanks for your feedback";

    /// <summary>The second line of the thank-you state when the feedback was sent.</summary>
    public string ThanksMessage { get; init; } = "It helps improve future replies.";

    /// <summary>The second line of the thank-you state when the send was simulated (no handler).</summary>
    public string SimulatedThanks { get; init; } = "Preview only: nothing was sent.";

    /// <summary>The error next to the actions when the details could not be sent.</summary>
    public string SendFailed { get; init; } = "Couldn't send feedback.";

    /// <summary>The error in the row when the rating could not be saved.</summary>
    public string RatingFailed { get; init; } = "Couldn't save rating.";

    /// <summary>Announced while a send is in flight.</summary>
    public string Sending { get; init; } = "Sending feedback";

    /// <summary>Announced after a thumbs up.</summary>
    public string RatedUp { get; init; } = "Rated as a good response";

    /// <summary>Announced after a thumbs down.</summary>
    public string RatedDown { get; init; } = "Rated as a bad response";

    /// <summary>Announced when the rating is cleared.</summary>
    public string RatingCleared { get; init; } = "Rating cleared";

    /// <summary>Added to an announcement when the send was simulated.</summary>
    public string Simulated { get; init; } = "Nothing was sent: this is a preview.";

    /// <summary>The copy action of <see cref="ResponseFeedback"/>.</summary>
    public string CopyReply { get; init; } = "Copy reply";

    /// <summary>The regenerate action of <see cref="ResponseFeedback"/>.</summary>
    public string Regenerate { get; init; } = "Regenerate";
}
