namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="Integrations"/> shows or announces, in English by default. Replace the instance to translate or
/// reword; texts with an app's name, a count or a query are functions so that word order can change.
/// </summary>
public sealed class IntegrationLabels
{
    /// <summary>The heading when <see cref="Integrations.Title"/> is null.</summary>
    public string Title { get; init; } = "Integrations";

    /// <summary>The line under the heading when <see cref="Integrations.Subtitle"/> is null.</summary>
    public string Subtitle { get; init; } = "Link the services your workspace relies on.";

    /// <summary>The search field's placeholder.</summary>
    public string SearchPlaceholder { get; init; } = "Search apps or categories";

    /// <summary>The search field's accessible name.</summary>
    public string SearchName { get; init; } = "Search integrations";

    /// <summary>The accessible name of the filter group.</summary>
    public string FilterGroupName { get; init; } = "Filter integrations";

    /// <summary>The filter that shows every app.</summary>
    public string FilterAll { get; init; } = "All";

    /// <summary>The filter that shows the connected apps.</summary>
    public string FilterConnected { get; init; } = "Connected";

    /// <summary>A filter's accessible name: its text and its count ("Files, 3").</summary>
    public Func<string, int, string> FilterName { get; init; } = (label, count) => $"{label}, {count}";

    /// <summary>The accessible name of the list.</summary>
    public string ListName { get; init; } = "Integrations";

    /// <summary>The connect pill while idle.</summary>
    public string Connect { get; init; } = "Connect";

    /// <summary>The connect pill while the connect action runs.</summary>
    public string Connecting { get; init; } = "Connecting";

    /// <summary>The pill once connected (a status, not a button).</summary>
    public string Connected { get; init; } = "Connected";

    /// <summary>The pill after a failed attempt.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The button that opens a connected app's settings.</summary>
    public string Configure { get; init; } = "Settings";

    /// <summary>The idle pill's accessible name.</summary>
    public Func<string, string> ConnectName { get; init; } = app => $"Connect {app}";

    /// <summary>The busy pill's accessible name.</summary>
    public Func<string, string> ConnectingName { get; init; } = app => $"Connecting {app}";

    /// <summary>The connected pill's accessible name.</summary>
    public Func<string, string> ConnectedName { get; init; } = app => $"{app} connected";

    /// <summary>The failed pill's accessible name.</summary>
    public Func<string, string> RetryName { get; init; } = app => $"Retry connecting {app}";

    /// <summary>The settings button's accessible name.</summary>
    public Func<string, string> ConfigureName { get; init; } = app => $"Settings for {app}";

    /// <summary>The drawer's status line while idle.</summary>
    public string NotConnected { get; init; } = "Not linked yet";

    /// <summary>The drawer's status line while connecting.</summary>
    public Func<string, string> Waiting { get; init; } = app => $"Waiting for {app} to grant access";

    /// <summary>The drawer's status line when someone else connected it: their name and the date (already formatted; may be empty).</summary>
    public Func<string, string, string> ConnectedBy { get; init; } =
        (person, date) => string.IsNullOrEmpty(date) ? $"Linked by {person}" : $"Linked by {person} · {date}";

    /// <summary>The drawer's status line when the person using the block connected it.</summary>
    public string ConnectedByYou { get; init; } = "Linked by you just now";

    /// <summary>The drawer's status line when nobody is known.</summary>
    public string ConnectedUnknown { get; init; } = "Linked";

    /// <summary>The date format of <see cref="ConnectedBy"/> (current culture).</summary>
    public string DateFormat { get; init; } = "MMM d";

    /// <summary>The drawer's status line after a failure without a reason.</summary>
    public Func<string, string> ConnectFailed { get; init; } = app => $"{app} did not finish linking. Try again.";

    /// <summary>The permissions heading.</summary>
    public string AccessTitle { get; init; } = "Permissions";

    /// <summary>The lead of the permissions while not connected.</summary>
    public Func<string, string> AccessLeadFuture { get; init; } = app => $"{app} will be able to";

    /// <summary>The lead of the permissions while connected.</summary>
    public Func<string, string> AccessLeadPresent { get; init; } = app => $"{app} can";

    /// <summary>The label of the sync frequency menu.</summary>
    public string FrequencyLabel { get; init; } = "Sync frequency";

    /// <summary>The hold-to-disconnect button.</summary>
    public string HoldToDisconnect { get; init; } = "Hold to unlink";

    /// <summary>The same button once the hold completed.</summary>
    public string Disconnected { get; init; } = "Unlinked";

    /// <summary>The line under the button.</summary>
    public Func<string, string> DisconnectNote { get; init; } = app => $"Stops syncing and removes {app}'s access.";

    /// <summary>The empty list's title when the search matches nothing.</summary>
    public Func<string, string> EmptySearchTitle { get; init; } = query => $"Nothing matches “{query}”";

    /// <summary>The empty list's hint when the search matches nothing.</summary>
    public string EmptySearchHint { get; init; } = "Try an app name or a category.";

    /// <summary>The button that clears the search.</summary>
    public string ClearSearch { get; init; } = "Clear search";

    /// <summary>The empty list's title under the Connected filter.</summary>
    public string EmptyConnectedTitle { get; init; } = "No apps linked yet";

    /// <summary>The empty list's hint under the Connected filter.</summary>
    public string EmptyConnectedHint { get; init; } = "Link an app and it will appear here.";

    /// <summary>The button that returns to every app.</summary>
    public string BrowseAll { get; init; } = "Show all apps";

    /// <summary>The empty list's title when a category has no apps.</summary>
    public string EmptyCategoryTitle { get; init; } = "No apps here yet";

    /// <summary>Announced when a connect starts.</summary>
    public Func<string, string> AnnounceConnecting { get; init; } = app => $"Linking {app}";

    /// <summary>Announced when a connect succeeds.</summary>
    public Func<string, string> AnnounceConnected { get; init; } = app => $"{app} linked";

    /// <summary>Announced when a connect fails.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = app => $"{app} could not be linked";

    /// <summary>Announced when an app is disconnected.</summary>
    public Func<string, string> AnnounceDisconnected { get; init; } = app => $"{app} unlinked";

    /// <summary>Announced when the disconnect action failed and the app stays connected.</summary>
    public Func<string, string> AnnounceDisconnectFailed { get; init; } = app => $"{app} is still linked";

    /// <summary>Announced after a sync frequency change: the app and the option's phrase.</summary>
    public Func<string, string, string> AnnounceSync { get; init; } = (app, phrase) => $"{app} now syncs {phrase}";

    /// <summary>Announced after a filter change under All or Connected: the count.</summary>
    public Func<int, string> AnnounceShown { get; init; } = count => count == 1 ? "1 app shown" : $"{count} apps shown";

    /// <summary>Announced after a filter change to a category: the count and the category.</summary>
    public Func<int, string, string> AnnounceShownIn { get; init; } =
        (count, category) => count == 1 ? $"1 {category} app shown" : $"{count} {category} apps shown";

    /// <summary>Announced after a search change: the count and the (trimmed, lower-case) query.</summary>
    public Func<int, string, string> AnnounceMatches { get; init; } =
        (count, query) => count == 1 ? $"1 match for “{query}”" : $"{count} matches for “{query}”";
}
