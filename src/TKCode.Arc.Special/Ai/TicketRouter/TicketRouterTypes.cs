using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where a <see cref="TicketMessage"/> came in. Each channel has a neutral glyph and a label; no brand marks.</summary>
public enum TicketChannel
{
    /// <summary>An email to the support address.</summary>
    Email,

    /// <summary>A live chat on the website or in the app.</summary>
    Chat,

    /// <summary>A message in a team messaging workspace.</summary>
    Messages,

    /// <summary>A ticket filed in the help desk.</summary>
    HelpDesk
}

/// <summary>What the customer wants, as the classifier reads it.</summary>
public enum TicketIntent
{
    /// <summary>A question about an invoice, a plan or a charge.</summary>
    Billing,

    /// <summary>A request for money back.</summary>
    Refund,

    /// <summary>Something is broken.</summary>
    Bug,

    /// <summary>Something the product does not do yet.</summary>
    Feature,

    /// <summary>A how-to question or anything else.</summary>
    Question
}

/// <summary>How the customer sounds, as the classifier reads it.</summary>
public enum TicketSentiment
{
    /// <summary>Happy or thankful.</summary>
    Positive,

    /// <summary>Matter of fact.</summary>
    Neutral,

    /// <summary>Upset or frustrated.</summary>
    Negative
}

/// <summary>Where a <see cref="TicketRouter"/> is with the current ticket.</summary>
internal enum TicketRouterPhase
{
    /// <summary>No tickets were given and the queue does not loop.</summary>
    Empty,

    /// <summary>The ticket card crossfades in (220ms).</summary>
    Arriving,

    /// <summary>The classifier is reading the ticket.</summary>
    Classifying,

    /// <summary>The labels fade in with a 50ms stagger and hold.</summary>
    Labeled,

    /// <summary>The pulse draws along the connector to the target lane (240ms).</summary>
    Routing,

    /// <summary>The row flies into its lane and lands.</summary>
    Landing,

    /// <summary>The classifier threw: Retry or Send to review.</summary>
    Failed,

    /// <summary>The queue ran out and does not loop.</summary>
    CaughtUp
}

/// <summary>
/// One incoming support message for a <see cref="TicketRouter"/>. Data only, safe to read from any thread (your classifier
/// may run off the UI thread).
/// </summary>
public sealed class TicketMessage
{
    /// <summary>Creates an empty message; set at least <see cref="Id"/> and <see cref="Subject"/>.</summary>
    public TicketMessage()
    {
    }

    /// <summary>Creates a message.</summary>
    /// <param name="id">Your identifier for the message.</param>
    /// <param name="channel">Where it came in.</param>
    /// <param name="customer">The person who wrote it.</param>
    /// <param name="company">Their company, or empty.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="preview">The first words of the message.</param>
    public TicketMessage(string id, TicketChannel channel, string customer, string company, string subject, string preview)
    {
        Id = id;
        Channel = channel;
        Customer = customer;
        Company = company;
        Subject = subject;
        Preview = preview;
    }

    /// <summary>Your identifier for the message.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Where the message came in.</summary>
    public TicketChannel Channel { get; set; }

    /// <summary>The person who wrote it.</summary>
    public string Customer { get; set; } = string.Empty;

    /// <summary>Their company, or empty.</summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>The subject line (one line, cut with an ellipsis).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The first words of the message (one line, cut with an ellipsis).</summary>
    public string Preview { get; set; } = string.Empty;

    /// <summary>An optional label for when it arrived ("2m ago"), shown as given.</summary>
    public string? ReceivedAt { get; set; }

    /// <summary>
    /// What the simulated classifier answers for this message when no <see cref="TicketRouter.Classifier"/> is set. A
    /// message without one goes to the review lane.
    /// </summary>
    public TicketClassification? Sample { get; set; }

    /// <inheritdoc />
    public override string ToString() => Subject;
}

/// <summary>What the classifier read in a <see cref="TicketMessage"/>: the intent, the sentiment and how sure it is.</summary>
/// <param name="Intent">What the customer wants.</param>
/// <param name="Sentiment">How the customer sounds.</param>
/// <param name="Confidence">How sure the classifier is, from 0 to 1. Below <see cref="TicketRouter.ReviewThreshold"/> the ticket waits for review.</param>
public sealed record TicketClassification(TicketIntent Intent, TicketSentiment Sentiment, double Confidence);

/// <summary>A team lane of a <see cref="TicketRouter"/>, or the review lane that collects low confidence tickets.</summary>
public sealed class TicketLane
{
    /// <summary>Creates an empty lane; set <see cref="Id"/> and <see cref="Label"/>.</summary>
    public TicketLane()
    {
    }

    /// <summary>Creates a lane.</summary>
    /// <param name="id">The id your route function returns for this lane.</param>
    /// <param name="label">The lane's name.</param>
    /// <param name="assignees">The people who take tickets in turn (round robin); empty for the review lane.</param>
    public TicketLane(string id, string label, params string[] assignees)
    {
        Id = id;
        Label = label;
        Assignees = assignees;
    }

    /// <summary>The id your route function returns for this lane.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The lane's name.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The people who take the lane's tickets in turn (round robin), shown as avatar initials.</summary>
    public IReadOnlyList<string> Assignees { get; set; } = [];

    /// <summary>Tickets routed here earlier that are not in the history; added to the lane's count.</summary>
    public int Count { get; set; }

    /// <summary>Marks the lane that collects tickets below the review threshold. One lane at most should set it.</summary>
    public bool IsReview { get; set; }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>A ticket that landed in a lane of a <see cref="TicketRouter"/>.</summary>
public sealed class RoutedTicket
{
    private static int s_serial;

    /// <summary>Creates a routed ticket (for <see cref="TicketRouter.InitialRouted"/>).</summary>
    /// <param name="message">The message.</param>
    /// <param name="classification">What the classifier read, or null when it failed and the ticket was sent to review.</param>
    /// <param name="laneId">The lane it is in.</param>
    /// <param name="assignee">Who has it, or null in the review lane.</param>
    /// <param name="suggestedLaneId">In the review lane: the lane the classifier picked.</param>
    public RoutedTicket(TicketMessage message, TicketClassification? classification, string laneId, string? assignee = null,
        string? suggestedLaneId = null)
    {
        Message = message;
        Classification = classification;
        LaneId = laneId;
        Assignee = assignee;
        SuggestedLaneId = suggestedLaneId;
        Key = "ticket-router-" + Interlocked.Increment(ref s_serial).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The message.</summary>
    public TicketMessage Message { get; }

    /// <summary>What the classifier read, or null when it failed and the ticket was sent to review.</summary>
    public TicketClassification? Classification { get; }

    /// <summary>The id of the lane the ticket is in.</summary>
    public string LaneId { get; }

    /// <summary>Who has the ticket (round robin in its lane), or null in the review lane.</summary>
    public string? Assignee { get; }

    /// <summary>In the review lane: the lane the classifier picked, which a click confirms.</summary>
    public string? SuggestedLaneId { get; }

    /// <summary>A key unique to this landing (the same message can land twice when a simulation loops).</summary>
    internal string Key { get; }

    /// <inheritdoc />
    public override string ToString() => Message.Subject;
}

/// <summary>Arguments of <see cref="TicketRouter.TicketRouted"/>: the ticket that landed in a lane.</summary>
public sealed class TicketRoutedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TicketRoutedEventArgs(RoutedEvent routedEvent, RoutedTicket ticket) : base(routedEvent)
    {
        Ticket = ticket;
    }

    /// <summary>The ticket with its classification, lane, assignee and, in the review lane, the suggested lane.</summary>
    public RoutedTicket Ticket { get; }
}

/// <summary>Arguments of <see cref="TicketRouter.ReviewConfirmed"/>: a review ticket sent to its suggested lane.</summary>
public sealed class TicketReviewConfirmedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TicketReviewConfirmedEventArgs(RoutedEvent routedEvent, RoutedTicket ticket, string laneId) : base(routedEvent)
    {
        Ticket = ticket;
        LaneId = laneId;
    }

    /// <summary>The ticket as it now is: in its new lane with an assignee.</summary>
    public RoutedTicket Ticket { get; }

    /// <summary>The lane it was sent to.</summary>
    public string LaneId { get; }
}

/// <summary>Arguments of <see cref="TicketRouter.ClassificationFailed"/>: the classifier threw for a message.</summary>
public sealed class TicketClassificationFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TicketClassificationFailedEventArgs(RoutedEvent routedEvent, TicketMessage message, Exception error) : base(routedEvent)
    {
        Message = message;
        Error = error;
    }

    /// <summary>The message the classifier could not read.</summary>
    public TicketMessage Message { get; }

    /// <summary>What the classifier threw.</summary>
    public Exception Error { get; }
}

/// <summary>Arguments of <see cref="TicketRouter.PausedChanged"/>.</summary>
public sealed class TicketRouterPausedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TicketRouterPausedChangedEventArgs(RoutedEvent routedEvent, bool isPaused) : base(routedEvent)
    {
        IsPaused = isPaused;
    }

    /// <summary>Whether the flow is now paused.</summary>
    public bool IsPaused { get; }
}
