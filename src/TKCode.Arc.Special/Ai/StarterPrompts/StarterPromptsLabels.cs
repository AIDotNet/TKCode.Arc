namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="StarterPrompts"/> shows or says, in English by default (the greeting, description and placeholder
/// are properties of the control). Replace the whole object to localize or reword; sentences that carry a value are
/// format functions.
/// </summary>
public sealed class StarterPromptsLabels
{
    /// <summary>The English defaults.</summary>
    public static StarterPromptsLabels Default { get; } = new();

    /// <summary>The first tab, holding every starter.</summary>
    public string All { get; init; } = "All";

    /// <summary>The shuffle button.</summary>
    public string Shuffle { get; init; } = "Shuffle";

    /// <summary>The shuffle button's accessible name.</summary>
    public string ShuffleName { get; init; } = "Shuffle starters";

    /// <summary>The name of the starter cards while they load.</summary>
    public string Loading { get; init; } = "Loading starters";

    /// <summary>A card's accessible name, from its title, its app's name (or null) and its category's label (or null).</summary>
    public Func<string, string?, string?, string> CardName { get; init; } = (title, app, category) =>
    {
        var name = title;
        if (!string.IsNullOrEmpty(app))
        {
            name += ", uses " + app;
        }
        if (!string.IsNullOrEmpty(category))
        {
            name += ", " + category;
        }
        return name;
    };

    /// <summary>The composer's accessible name.</summary>
    public string Composer { get; init; } = "Message";

    /// <summary>The send button's name.</summary>
    public string Send { get; init; } = "Send message";

    /// <summary>The send button's name while a message is in flight.</summary>
    public string StopSending { get; init; } = "Stop sending";

    /// <summary>The footer status while sending.</summary>
    public string Sending { get; init; } = "Sending";

    /// <summary>The footer status once sent.</summary>
    public string Sent { get; init; } = "Sent";

    /// <summary>The footer status once a simulated send finished.</summary>
    public string SentSimulated { get; init; } = "Sent (simulated)";

    /// <summary>The footer status after the person stopped a send.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The footer status of a failed send whose error has no message.</summary>
    public string Failed { get; init; } = "Couldn't send. Try again.";

    /// <summary>The button that sends a failed message again.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The name of the model chip in the footer, from the model's name.</summary>
    public Func<string, string> ModelName { get; init; } = model => "Model: " + model;

    /// <summary>Announced when a card is picked, from its title.</summary>
    public Func<string, string> Picked { get; init; } = title => "“" + title + "” added to the composer";

    /// <summary>Announced after a shuffle, from the number of cards on screen.</summary>
    public Func<int, string> Shuffled { get; init; } = count => count == 1 ? "Showing a new starter" : $"Showing {count} new starters";

    /// <summary>Announced when a send starts.</summary>
    public string SendingAnnouncement { get; init; } = "Sending message";

    /// <summary>Announced when a send finished.</summary>
    public string SentAnnouncement { get; init; } = "Message sent";

    /// <summary>Announced when a simulated send finished.</summary>
    public string SentSimulatedAnnouncement { get; init; } = "Message sent. This send was simulated.";

    /// <summary>Announced when the person stopped a send.</summary>
    public string StoppedAnnouncement { get; init; } = "Sending stopped";

    /// <summary>Announced when a send failed, from the error text.</summary>
    public Func<string, string> FailedAnnouncement { get; init; } = error => "Couldn't send: " + error;
}
