using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="AgentGallery"/> shows or reads out, with English defaults. Replace the object to translate or
/// reword the gallery; formatted texts are functions so the word order can change. <see cref="AgentGallery.Title"/> and
/// <see cref="AgentGallery.Subtitle"/> win over <see cref="Title"/> and <see cref="Subtitle"/> when set.
/// </summary>
public sealed class AgentGalleryLabels
{
    /// <summary>The English defaults.</summary>
    public static AgentGalleryLabels Default { get; } = new();

    /// <summary>The heading ("Agents").</summary>
    public string Title { get; init; } = "Agents";

    /// <summary>The line under the heading.</summary>
    public string Subtitle { get; init; } = "Ready-made assistants you can add to your workspace";

    /// <summary>The search field's placeholder and accessible name.</summary>
    public string SearchPlaceholder { get; init; } = "Search agents";

    /// <summary>The first filter pill, for every category.</summary>
    public string AllCategories { get; init; } = "All";

    /// <summary>The accessible name of the filter pills.</summary>
    public string CategoriesName { get; init; } = "Agent categories";

    /// <summary>The accessible name of the sort switch.</summary>
    public string SortName { get; init; } = "Sort agents";

    /// <summary>The name of <see cref="AgentGallerySort.Popular"/>.</summary>
    public string SortPopular { get; init; } = "Popular";

    /// <summary>The name of <see cref="AgentGallerySort.Newest"/>.</summary>
    public string SortNewest { get; init; } = "Newest";

    /// <summary>The name of <see cref="AgentGallerySort.TopRated"/>.</summary>
    public string SortTopRated { get; init; } = "Top rated";

    /// <summary>The count over the cards; the argument is the number shown ("12 agents").</summary>
    public Func<int, string> ResultCount { get; init; } = static count => count == 1 ? "1 agent" : $"{count:N0} agents";

    /// <summary>The accessible name of the card grid.</summary>
    public string GridName { get; init; } = "Agents";

    /// <summary>The empty state when a search finds nothing; the argument is the query.</summary>
    public Func<string, string> NoMatches { get; init; } = static query => $"No agents match “{query}”";

    /// <summary>The empty state when a category has no agents.</summary>
    public string EmptyCategory { get; init; } = "No agents in this category yet";

    /// <summary>The hint under an empty state.</summary>
    public string EmptyHint { get; init; } = "Try another word or look in every category.";

    /// <summary>The empty state's button that clears the search.</summary>
    public string ClearSearch { get; init; } = "Clear search";

    /// <summary>The empty state's button that goes back to every category.</summary>
    public string ShowAll { get; init; } = "Show all categories";

    /// <summary>The install button.</summary>
    public string Install { get; init; } = "Install";

    /// <summary>The install button while the install runs.</summary>
    public string Installing { get; init; } = "Installing";

    /// <summary>The install button of an installed agent.</summary>
    public string Installed { get; init; } = "Installed";

    /// <summary>The install button after a failed install.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The detail view's button that removes an installed agent.</summary>
    public string Uninstall { get; init; } = "Uninstall";

    /// <summary>The badge of a featured agent.</summary>
    public string Featured { get; init; } = "Featured";

    /// <summary>Shown instead of a rating for an agent nobody rated yet.</summary>
    public string NotRated { get; init; } = "New";

    /// <summary>The maker line; the argument is the maker ("by Fernhill Studio").</summary>
    public Func<string, string> ByMaker { get; init; } = static maker => $"by {maker}";

    /// <summary>The tooltip and accessible name of the verified check.</summary>
    public string Verified { get; init; } = "Verified maker";

    /// <summary>The installs figure; the argument is the compact count ("12.4K installs").</summary>
    public Func<string, string> Installs { get; init; } = static count => $"{count} installs";

    /// <summary>The spoken rating; the arguments are the rating and the number of ratings.</summary>
    public Func<double, int, string> RatingName { get; init; } =
        static (rating, count) => $"Rated {rating.ToString("0.0", CultureInfo.InvariantCulture)} out of 5 from {count:N0} ratings";

    /// <summary>The detail view's back button.</summary>
    public string Back { get; init; } = "All agents";

    /// <summary>The detail view's date line; the argument is the formatted date ("Added Oct 12, 2026").</summary>
    public Func<string, string> AddedOn { get; init; } = static date => $"Added {date}";

    /// <summary>The heading over the abilities.</summary>
    public string AbilitiesHeading { get; init; } = "What it can do";

    /// <summary>The heading over the permissions.</summary>
    public string PermissionsHeading { get; init; } = "Needs access to";

    /// <summary>Shown under the permissions heading for an agent that needs none.</summary>
    public string NoPermissions { get; init; } = "Nothing outside the conversation";

    /// <summary>The word after a sensitive permission (with a warning icon, not color alone).</summary>
    public string Sensitive { get; init; } = "Sensitive";

    /// <summary>The heading over the sample prompts.</summary>
    public string PromptsHeading { get; init; } = "Try asking";

    /// <summary>The accessible name of a card; the arguments are the agent and the state's word ("Inbox Sorter by Fernhill Studio, Installed").</summary>
    public Func<AgentGalleryItem, string?, string> CardName { get; init; } = static (agent, state) =>
    {
        var name = agent.Maker is { Length: > 0 } maker ? $"{agent.Name} by {maker}" : agent.Name;
        return state is null ? name : $"{name}, {state}";
    };

    /// <summary>The accessible name of an install button; the argument is the agent's name.</summary>
    public Func<string, string> InstallName { get; init; } = static name => $"Install {name}";

    /// <summary>The accessible name of an uninstall button; the argument is the agent's name.</summary>
    public Func<string, string> UninstallName { get; init; } = static name => $"Uninstall {name}";

    /// <summary>The announcement after the cards change; the arguments are the count and the query (null without a search).</summary>
    public Func<int, string?, string> ResultsAnnouncement { get; init; } = static (count, query) =>
    {
        var agents = count == 1 ? "1 agent" : $"{count:N0} agents";
        return query is null ? $"{agents} shown" : $"{agents} match “{query}”";
    };

    /// <summary>The announcement when an install starts; the argument is the agent's name.</summary>
    public Func<string, string> InstallingAnnouncement { get; init; } = static name => $"Installing {name}";

    /// <summary>The announcement when an install finished; the argument is the agent's name.</summary>
    public Func<string, string> InstalledAnnouncement { get; init; } = static name => $"{name} installed";

    /// <summary>The announcement when an agent was removed; the argument is the agent's name.</summary>
    public Func<string, string> UninstalledAnnouncement { get; init; } = static name => $"{name} uninstalled";

    /// <summary>The announcement when an install failed; the arguments are the agent's name and the message.</summary>
    public Func<string, string, string> FailedAnnouncement { get; init; } = static (name, message) => $"{name} did not install. {message}";

    /// <summary>The message shown when an install handler fails without a message of its own.</summary>
    public string InstallFailedFallback { get; init; } = "The install did not finish. Try again.";
}
