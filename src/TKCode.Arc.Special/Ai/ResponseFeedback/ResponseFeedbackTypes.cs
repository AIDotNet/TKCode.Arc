using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The rating a person gave an AI reply with <see cref="FeedbackActions"/>.</summary>
public enum FeedbackRating
{
    /// <summary>Not rated (or the rating was cleared by pressing the active thumb again).</summary>
    None,

    /// <summary>Thumbs up: a good response.</summary>
    Up,

    /// <summary>Thumbs down: a bad response.</summary>
    Down
}

/// <summary>Which part of the feedback a <see cref="FeedbackSubmission"/> carries.</summary>
public enum FeedbackStage
{
    /// <summary>A thumb press: the rating alone (<see cref="FeedbackRating.None"/> when it was cleared).</summary>
    Rating,

    /// <summary>The reasons and the note submitted from the panel, with the rating they belong to.</summary>
    Details
}

/// <summary>
/// One piece of feedback to store: sent on every thumb press (<see cref="FeedbackStage.Rating"/>) and when reasons or a note
/// are submitted (<see cref="FeedbackStage.Details"/>). Store the rating on the first stage and attach the reasons and the
/// note on the second, both against <see cref="MessageId"/>.
/// </summary>
public sealed class FeedbackSubmission
{
    /// <summary>Creates a submission.</summary>
    public FeedbackSubmission(string? messageId, FeedbackStage stage, FeedbackRating rating, IReadOnlyList<string> reasons,
        string? comment)
    {
        MessageId = messageId;
        Stage = stage;
        Rating = rating;
        Reasons = reasons;
        Comment = comment;
    }

    /// <summary>The message the feedback belongs to (<see cref="FeedbackActions.MessageId"/>).</summary>
    public string? MessageId { get; }

    /// <summary>Whether this is a thumb press or the details from the panel.</summary>
    public FeedbackStage Stage { get; }

    /// <summary>The rating; <see cref="FeedbackRating.None"/> means the person cleared it.</summary>
    public FeedbackRating Rating { get; }

    /// <summary>The selected reason chips, in the order they are offered (empty on the rating stage).</summary>
    public IReadOnlyList<string> Reasons { get; }

    /// <summary>The note, trimmed, or null when there is none.</summary>
    public string? Comment { get; }
}

/// <summary>Arguments of <see cref="FeedbackActions.FeedbackSubmitted"/>.</summary>
public sealed class FeedbackSubmittedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeedbackSubmittedEventArgs(RoutedEvent routedEvent, FeedbackSubmission submission, bool isSimulated)
        : base(routedEvent)
    {
        Submission = submission;
        IsSimulated = isSimulated;
    }

    /// <summary>What is being sent.</summary>
    public FeedbackSubmission Submission { get; }

    /// <summary>True when no <see cref="FeedbackActions.FeedbackHandler"/> is set, so nothing is persisted.</summary>
    public bool IsSimulated { get; }
}

/// <summary>Arguments of <see cref="FeedbackActions.RatingChanged"/>.</summary>
public sealed class FeedbackRatingChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public FeedbackRatingChangedEventArgs(RoutedEvent routedEvent, FeedbackRating oldRating, FeedbackRating newRating)
        : base(routedEvent)
    {
        OldRating = oldRating;
        NewRating = newRating;
    }

    /// <summary>The rating before the press.</summary>
    public FeedbackRating OldRating { get; }

    /// <summary>The rating after the press (<see cref="FeedbackRating.None"/> when the active thumb was pressed again).</summary>
    public FeedbackRating NewRating { get; }
}
