namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="PiiRedaction"/> shows or announces, in English by default. Replace the whole object to
/// translate or reword; sentences with a number or a name are functions so the word order can change.
/// </summary>
public sealed class PiiRedactionLabels
{
    /// <summary>The English defaults.</summary>
    public static PiiRedactionLabels Default { get; } = new();

    /// <summary>The card's title and the guard node of the flow.</summary>
    public string Title { get; init; } = "PII guard";

    /// <summary>The card's accessible name.</summary>
    public string CardName { get; init; } = "PII redaction";

    /// <summary>The tag beside the title while the reply is simulated.</summary>
    public string Simulated { get; init; } = "Simulated";

    /// <summary>Added to the announcement of a simulated reply.</summary>
    public string SimulatedNotice { get; init; } = "Simulated, nothing was sent";

    /// <summary>The message section's label before a scan.</summary>
    public string Message { get; init; } = "Message";

    /// <summary>The message section's label while it shows the safe prompt.</summary>
    public string SafePrompt { get; init; } = "Safe prompt";

    /// <summary>The message section's label while it shows the original message after a scan.</summary>
    public string Original { get; init; } = "Original message";

    /// <summary>The placeholder of the empty message field.</summary>
    public string Placeholder { get; init; } = "Paste a message with names, emails, phone numbers or cards";

    /// <summary>The label of the switch that flips between the safe prompt and the original.</summary>
    public string ShowOriginal { get; init; } = "Show original";

    /// <summary>The type cells' names.</summary>
    public string Name { get; init; } = "Name";

    /// <inheritdoc cref="Name"/>
    public string Email { get; init; } = "Email";

    /// <inheritdoc cref="Name"/>
    public string Phone { get; init; } = "Phone";

    /// <inheritdoc cref="Name"/>
    public string Card { get; init; } = "Card";

    /// <summary>A type cell before a scan.</summary>
    public string NotChecked { get; init; } = "Not checked";

    /// <summary>A type cell while the scan runs.</summary>
    public string Checking { get; init; } = "Checking";

    /// <summary>A type cell with nothing of its type.</summary>
    public string NoneFound { get; init; } = "None found";

    /// <summary>A type cell's value from its tokens: "EMAIL_1", or "EMAIL_1 +1" when there are more.</summary>
    public Func<IReadOnlyList<string>, string> Tokens { get; init; } =
        tokens => tokens.Count switch
        {
            0 => string.Empty,
            1 => tokens[0],
            _ => tokens[0] + " +" + (tokens.Count - 1),
        };

    /// <summary>The name of a mark in the original message, from the type's name and its token: "Email, redacted as EMAIL_1".</summary>
    public Func<string, string, string> MarkName { get; init; } = (type, token) => type + ", redacted as " + token;

    /// <summary>The person's node of the flow.</summary>
    public string You { get; init; } = "You";

    /// <summary>The model's step on the flow when no model name is given.</summary>
    public string Model { get; init; } = "Model";

    /// <summary>The reply section's text before a reply arrives.</summary>
    public string ReplyPlaceholder { get; init; } = "The reply shows here once the safe prompt is sent.";

    /// <summary>The primary action before a scan.</summary>
    public string Scan { get; init; } = "Scan";

    /// <summary>The primary action while something runs.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The primary action once the message is redacted.</summary>
    public string Send { get; init; } = "Send";

    /// <summary>The primary action once the reply is restored.</summary>
    public string RunAgain { get; init; } = "Run again";

    /// <summary>The primary action after a failure.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that returns to the editable message.</summary>
    public string Reset { get; init; } = "Reset";

    /// <summary>The status before a scan.</summary>
    public string Ready { get; init; } = "Ready to scan";

    /// <summary>The status while scanning.</summary>
    public string Scanning { get; init; } = "Scanning for personal data";

    /// <summary>The status once redacted, from the number of entities: "4 items redacted".</summary>
    public Func<int, string> Redacted { get; init; } = count => count == 1 ? "1 item redacted" : count + " items redacted";

    /// <summary>The status once a scan found nothing.</summary>
    public string NothingFound { get; init; } = "No personal data found";

    /// <summary>The status while the safe prompt travels to the model.</summary>
    public string Sending { get; init; } = "Sending the safe prompt";

    /// <summary>The status while the model works, from its name.</summary>
    public Func<string, string> Thinking { get; init; } = model => string.IsNullOrWhiteSpace(model) ? "Waiting for the model" : "Waiting for " + model;

    /// <summary>The status while the reply streams in, from the model's name.</summary>
    public Func<string, string> Streaming { get; init; } = model => string.IsNullOrWhiteSpace(model) ? "The model is replying" : model + " is replying";

    /// <summary>The status while the values are put back.</summary>
    public string Restoring { get; init; } = "Restoring original values";

    /// <summary>The status once the reply is restored.</summary>
    public string Done { get; init; } = "Reply restored";

    /// <summary>The status after Stop.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The status of a failure without a message of its own.</summary>
    public string Failed { get; init; } = "Something went wrong";
}
