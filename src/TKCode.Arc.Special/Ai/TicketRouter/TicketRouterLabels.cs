using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="TicketRouter"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; words built from data are format functions.
/// </summary>
public sealed class TicketRouterLabels
{
    /// <summary>The English defaults.</summary>
    public static TicketRouterLabels Default { get; } = new();

    /// <summary>The status while tickets flow.</summary>
    public string Routing { get; init; } = "Routing";

    /// <summary>The status while paused.</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>The status and the stage's message when the queue ran out.</summary>
    public string CaughtUp { get; init; } = "All caught up";

    /// <summary>The line under "All caught up".</summary>
    public string CaughtUpHint { get; init; } = "New messages are routed as they arrive.";

    /// <summary>The stage's message when no tickets were given.</summary>
    public string Empty { get; init; } = "No tickets yet";

    /// <summary>The line under "No tickets yet".</summary>
    public string EmptyHint { get; init; } = "Incoming messages appear here one at a time.";

    /// <summary>The Pause button.</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>The Resume button.</summary>
    public string Resume { get; init; } = "Resume";

    /// <summary>The shimmering label while the classifier reads, from the model's name ("Classifying with" and the model).</summary>
    public Func<string, string> ClassifyingWith { get; init; } = model =>
        string.IsNullOrEmpty(model) ? "Classifying" : "Classifying with " + model;

    /// <summary>The name of the default Billing lane.</summary>
    public string BillingLane { get; init; } = "Billing";

    /// <summary>The name of the default Engineering lane.</summary>
    public string EngineeringLane { get; init; } = "Engineering";

    /// <summary>The name of the default Success lane.</summary>
    public string SuccessLane { get; init; } = "Success";

    /// <summary>The short label while the classifier reads, in narrow cards.</summary>
    public string Classifying { get; init; } = "Classifying";

    /// <summary>The message when the classifier threw.</summary>
    public string Failed { get; init; } = "Couldn't classify";

    /// <summary>The Retry button of a failed classification.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that sends a failed ticket to the review lane.</summary>
    public string SendToReview { get; init; } = "Send to review";

    /// <summary>The route target of a low confidence ticket.</summary>
    public string NeedsReview { get; init; } = "Needs review";

    /// <summary>A review ticket's suggestion, from the lane's name ("Suggested Billing").</summary>
    public Func<string, string> Suggested { get; init; } = lane => "Suggested " + lane;

    /// <summary>A ticket sent to review without a classification.</summary>
    public string Unclassified { get; init; } = "Unclassified";

    /// <summary>The words of the intents.</summary>
    public Func<TicketIntent, string> Intent { get; init; } = intent => intent switch
    {
        TicketIntent.Billing => "Billing",
        TicketIntent.Refund => "Refund",
        TicketIntent.Bug => "Bug report",
        TicketIntent.Feature => "Feature request",
        _ => "Question",
    };

    /// <summary>The words of the sentiments.</summary>
    public Func<TicketSentiment, string> Sentiment { get; init; } = sentiment => sentiment switch
    {
        TicketSentiment.Positive => "Positive",
        TicketSentiment.Negative => "Negative",
        _ => "Neutral",
    };

    /// <summary>The words of the channels, read with the channel glyph.</summary>
    public Func<TicketChannel, string> Channel { get; init; } = channel => channel switch
    {
        TicketChannel.Chat => "Chat",
        TicketChannel.Messages => "Messages",
        TicketChannel.HelpDesk => "Help desk",
        _ => "Email",
    };

    /// <summary>A confidence as a percentage ("92%").</summary>
    public Func<double, string> Percent { get; init; } = confidence =>
        Math.Round(Math.Clamp(confidence, 0, 1) * 100).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The name of the confidence meter, from the percentage ("92% confident").</summary>
    public Func<string, string> Confident { get; init; } = percent => percent + " confident";

    /// <summary>The name of a lane's list, from its name and ticket count ("Billing, 14 tickets").</summary>
    public Func<string, int, string> LaneName { get; init; } = (lane, count) =>
        lane + ", " + count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " ticket" : " tickets");

    /// <summary>The name of a review ticket's button, from its subject, confidence and target lane.</summary>
    public Func<string, string, string, string> ReviewButton { get; init; } = (subject, percent, lane) =>
        "Route " + subject + " to " + lane + ", " + percent + " confident";

    /// <summary>Said when a ticket lands, from its subject, lane and assignee.</summary>
    public Func<string, string, string?, string> AnnounceRouted { get; init; } = (subject, lane, assignee) =>
        assignee is null ? subject + " routed to " + lane : subject + " routed to " + lane + ", assigned to " + assignee;

    /// <summary>Said when a ticket waits for review, from its subject, confidence and suggested lane.</summary>
    public Func<string, string, string, string> AnnounceReview { get; init; } = (subject, percent, lane) =>
        subject + " needs review, " + percent + " confident, suggested " + lane;

    /// <summary>Said when the classifier throws, from the subject.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = subject => "Couldn't classify " + subject;

    /// <summary>Said when someone confirms a review ticket, from its subject, lane and assignee.</summary>
    public Func<string, string, string?, string> AnnounceConfirmed { get; init; } = (subject, lane, assignee) =>
        assignee is null ? "Confirmed, " + subject + " routed to " + lane
                         : "Confirmed, " + subject + " routed to " + lane + ", assigned to " + assignee;

    /// <summary>Said when the flow pauses.</summary>
    public string AnnouncePaused { get; init; } = "Routing paused";

    /// <summary>Said when the flow resumes.</summary>
    public string AnnounceResumed { get; init; } = "Routing resumed";

    /// <summary>Added to what is said while the built-in simulation runs (no classifier set).</summary>
    public string Simulated { get; init; } = "Simulated, nothing was sent.";
}
