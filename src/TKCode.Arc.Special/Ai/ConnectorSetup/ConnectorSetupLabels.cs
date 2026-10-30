using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ConnectorSetup"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; texts that hold a name, the assistant or a count are format functions.
/// </summary>
public sealed class ConnectorSetupLabels
{
    /// <summary>The English defaults.</summary>
    public static ConnectorSetupLabels Default { get; } = new();

    /// <summary>The card's heading when <see cref="ConnectorSetup.Title"/> is not set.</summary>
    public string Title { get; init; } = "Connect your tools";

    /// <summary>The line under the heading when <see cref="ConnectorSetup.Description"/> is not set, from the assistant's name.</summary>
    public Func<string, string> Description { get; init; } = assistant =>
        $"Let {(string.IsNullOrEmpty(assistant) ? "your assistant" : assistant)} search and act in the apps your team uses.";

    /// <summary>The search field's placeholder when <see cref="ConnectorSetup.SearchPlaceholder"/> is not set.</summary>
    public string SearchPlaceholder { get; init; } = "Search apps";

    /// <summary>The line shown when the search matches nothing, from the query.</summary>
    public Func<string, string> NoMatches { get; init; } = query => $"No apps match “{query}”";

    /// <summary>The footer note when <see cref="ConnectorSetup.Footer"/> is not set, from the assistant's name.</summary>
    public Func<string, string> Footer { get; init; } =
        assistant => $"{(string.IsNullOrEmpty(assistant) ? "Your assistant" : assistant)} only sees what your account can see. Disconnect an app at any time.";

    /// <summary>The Connect button.</summary>
    public string Connect { get; init; } = "Connect";

    /// <summary>The Cancel button while an app connects, and in the disconnect confirmation.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The Retry button of a failed connect.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button of a connected app (it opens the access details).</summary>
    public string Connected { get; init; } = "Connected";

    /// <summary>The first step's status, from the host or the app's name: "Opening chat.example.com…".</summary>
    public Func<string, string> Redirecting { get; init; } = host => $"Opening {host}…";

    /// <summary>The second step's status, from the app's name.</summary>
    public Func<string, string> Authorizing { get; init; } = name => $"Waiting for you to approve in {name}…";

    /// <summary>The word before the rolling count while the first sync runs ("Syncing 1,240 messages").</summary>
    public string Syncing { get; init; } = "Syncing";

    /// <summary>What the sync counts when the app names nothing.</summary>
    public string DefaultNoun { get; init; } = "items";

    /// <summary>The status of a connected app with a sync count, from the formatted count and its noun: "2,480 messages synced".</summary>
    public Func<string, string, string> Synced { get; init; } = (count, noun) => $"{count} {noun} synced";

    /// <summary>The status of a connected app with neither an account nor a count.</summary>
    public string ConnectedStatus { get; init; } = "Connected";

    /// <summary>The reason shown when a connect fails without a message.</summary>
    public string ConnectFailed { get; init; } = "Couldn’t connect. Try again.";

    /// <summary>The heading over the scopes, from the assistant's name ("Your assistant can" without one).</summary>
    public Func<string, string> ScopesHeading { get; init; } = assistant =>
        $"{(string.IsNullOrEmpty(assistant) ? "Your assistant" : assistant)} can";

    /// <summary>The line shown when an app granted no scopes.</summary>
    public string NoScopes { get; init; } = "No access was granted.";

    /// <summary>The Disconnect button in the details.</summary>
    public string Disconnect { get; init; } = "Disconnect";

    /// <summary>The confirmation's question, from the app's name and the assistant's: "Disconnect Chat? Your assistant loses access.".</summary>
    public Func<string, string, string> ConfirmDisconnect { get; init; } =
        (name, assistant) => $"Disconnect {name}? {(string.IsNullOrEmpty(assistant) ? "Your assistant" : assistant)} loses access.";

    /// <summary>The destructive button of the confirmation.</summary>
    public string ConfirmDisconnectButton { get; init; } = "Disconnect";

    /// <summary>The status while access is revoked.</summary>
    public string Disconnecting { get; init; } = "Disconnecting…";

    /// <summary>The name of a connected app's button, from the app's name, when the details are closed.</summary>
    public Func<string, string> ShowAccess { get; init; } = name => $"Connected. Show access for {name}";

    /// <summary>The name of a connected app's button, from the app's name, when the details are open.</summary>
    public Func<string, string> HideAccess { get; init; } = name => $"Connected. Hide access for {name}";

    /// <summary>The name of the Connect button, from the app's name.</summary>
    public Func<string, string> ConnectApp { get; init; } = name => $"Connect {name}";

    /// <summary>The name of the Cancel button, from the app's name.</summary>
    public Func<string, string> CancelApp { get; init; } = name => $"Cancel connecting {name}";

    /// <summary>The name of the Retry button, from the app's name.</summary>
    public Func<string, string> RetryApp { get; init; } = name => $"Retry connecting {name}";

    /// <summary>Announced when a connect starts.</summary>
    public Func<string, string> AnnounceConnecting { get; init; } = name => $"Connecting {name}.";

    /// <summary>Announced when a connect finishes, from the name and the status line ("2,480 messages synced").</summary>
    public Func<string, string, string> AnnounceConnected { get; init; } = (name, detail) => $"{name} connected, {detail}.";

    /// <summary>Announced when a connect fails, from the name and the reason.</summary>
    public Func<string, string, string> AnnounceFailed { get; init; } = (name, reason) => $"{name} failed: {reason}";

    /// <summary>Announced when the person cancels a connect.</summary>
    public Func<string, string> AnnounceCancelled { get; init; } = name => $"Cancelled connecting {name}.";

    /// <summary>Announced when access was revoked.</summary>
    public Func<string, string> AnnounceDisconnected { get; init; } = name => $"{name} disconnected.";

    /// <summary>Added to each announcement while the built-in simulation runs.</summary>
    public string AnnounceSimulated { get; init; } = "Simulated, nothing was sent.";

    /// <summary>Formats a count with group separators ("2,480").</summary>
    public Func<long, string> Count { get; init; } = FormatCount;

    private static string FormatCount(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("en-US"));
}
