using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Where an <see cref="AgentHandoff"/> conversation is.</summary>
public enum AgentHandoffStage
{
    /// <summary>The AI assistant answers; Talk to a person can be offered under its last answer.</summary>
    Assistant,

    /// <summary>The assistant has handed off: the handoff card shows the summary and the typical wait.</summary>
    Transferring,

    /// <summary>A person has accepted the transfer and takes over.</summary>
    Connected,

    /// <summary>The person has resolved the conversation.</summary>
    Resolved,

    /// <summary>Nobody picked the transfer up: the card says so and can offer Try again.</summary>
    Unavailable
}

/// <summary>Who wrote an <see cref="AgentHandoffMessage"/>.</summary>
public enum AgentHandoffSender
{
    /// <summary>The customer: a bubble on the right.</summary>
    Customer,

    /// <summary>The AI assistant: a bubble on the left.</summary>
    Assistant,

    /// <summary>The person who took over: a bubble on the left.</summary>
    Agent
}

/// <summary>Who is typing in an <see cref="AgentHandoff"/>.</summary>
public enum AgentHandoffTyping
{
    /// <summary>Nobody.</summary>
    None,

    /// <summary>The assistant: a short shimmering Thinking line.</summary>
    Assistant,

    /// <summary>The person: typing dots.</summary>
    Agent
}

/// <summary>Which story an <see cref="AgentHandoffSimulation"/> plays.</summary>
public enum AgentHandoffScenario
{
    /// <summary>The assistant hands off, a person joins, resolves the issue and the chat.</summary>
    Handoff,

    /// <summary>The assistant hands off but nobody picks the transfer up, so Try again shows.</summary>
    Unavailable
}

/// <summary>
/// One message of an <see cref="AgentHandoff"/> conversation. Change <see cref="Text"/> in place while it streams (with
/// <see cref="IsStreaming"/> set); the conversation fades each appended chunk in.
/// </summary>
public sealed class AgentHandoffMessage : AvaloniaObject
{
    /// <summary>Who wrote the message.</summary>
    public static readonly StyledProperty<AgentHandoffSender> FromProperty =
        AvaloniaProperty.Register<AgentHandoffMessage, AgentHandoffSender>(nameof(From));

    /// <summary>The message text; it wraps.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AgentHandoffMessage, string?>(nameof(Text));

    /// <summary>An optional time shown under the bubble, such as "14:05".</summary>
    public static readonly StyledProperty<string?> TimeProperty =
        AvaloniaProperty.Register<AgentHandoffMessage, string?>(nameof(Time));

    /// <summary>Whether the text is still streaming in; each appended chunk fades in while it is.</summary>
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<AgentHandoffMessage, bool>(nameof(IsStreaming));

    /// <summary>Creates an empty message.</summary>
    public AgentHandoffMessage()
    {
    }

    /// <summary>Creates a message from <paramref name="from"/> with <paramref name="text"/>.</summary>
    public AgentHandoffMessage(AgentHandoffSender from, string? text, string? time = null)
    {
        From = from;
        Text = text;
        Time = time;
    }

    /// <summary>Your own identifier for the message (not shown).</summary>
    public string? Id { get; set; }

    /// <inheritdoc cref="FromProperty"/>
    public AgentHandoffSender From { get => GetValue(FromProperty); set => SetValue(FromProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="TimeProperty"/>
    public string? Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <inheritdoc cref="IsStreamingProperty"/>
    public bool IsStreaming { get => GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }
}

/// <summary>One fact of an <see cref="AgentHandoffSummary"/>: a short label and its value.</summary>
public sealed class AgentHandoffField
{
    /// <summary>Creates an empty field.</summary>
    public AgentHandoffField()
    {
    }

    /// <summary>Creates a field.</summary>
    public AgentHandoffField(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>The label, such as "Order".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The value, such as "NW-48213".</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>What the assistant passes to the person: the issue in a sentence and a few facts.</summary>
public sealed class AgentHandoffSummary
{
    /// <summary>Creates an empty summary.</summary>
    public AgentHandoffSummary()
    {
    }

    /// <summary>Creates a summary with an issue and fields.</summary>
    public AgentHandoffSummary(string issue, params AgentHandoffField[] fields)
    {
        Issue = issue;
        Fields.AddRange(fields);
    }

    /// <summary>The issue in a sentence or two.</summary>
    public string Issue { get; set; } = string.Empty;

    /// <summary>Facts shown as label and value rows under the issue.</summary>
    public AvaloniaList<AgentHandoffField> Fields { get; } = [];
}

/// <summary>The person who takes over an <see cref="AgentHandoff"/> conversation.</summary>
public sealed class AgentHandoffAgent
{
    /// <summary>Creates an empty agent; set <see cref="Name"/> before showing it.</summary>
    public AgentHandoffAgent()
    {
    }

    /// <summary>Creates an agent.</summary>
    public AgentHandoffAgent(string name, string? role = null, string? joinedAt = null)
    {
        Name = name;
        Role = role;
        JoinedAt = joinedAt;
    }

    /// <summary>The person's name; the avatar shows its initials unless <see cref="Avatar"/> is set.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The person's role, such as "Support specialist".</summary>
    public string? Role { get; set; }

    /// <summary>A photo for the avatar (a URL, <c>avares://</c> path, file or bitmap); null shows the initials.</summary>
    public ImageSource? Avatar { get; set; }

    /// <summary>When the person joined, such as "14:05"; shown on the join step.</summary>
    public string? JoinedAt { get; set; }

    /// <summary>The first name, used in short labels such as "With Jordan".</summary>
    public string FirstName
    {
        get
        {
            var name = Name.Trim();
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }
}

/// <summary>Arguments of <see cref="AgentHandoff.SendRequested"/>: the trimmed text the customer sent.</summary>
public sealed class AgentHandoffSendEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AgentHandoffSendEventArgs(RoutedEvent routedEvent, string text)
        : base(routedEvent)
    {
        Text = text;
    }

    /// <summary>The trimmed text; append it to <see cref="AgentHandoff.Messages"/> or <see cref="AgentHandoff.AgentMessages"/>.</summary>
    public string Text { get; }
}
