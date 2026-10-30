using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="LiveVisitors"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts, times and sentences are format functions.
/// </summary>
public sealed class LiveVisitorsLabels
{
    private static readonly Dictionary<string, string> s_countries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AR"] = "Argentina", ["AU"] = "Australia", ["AT"] = "Austria", ["BE"] = "Belgium", ["BR"] = "Brazil",
        ["CA"] = "Canada", ["CH"] = "Switzerland", ["CL"] = "Chile", ["CN"] = "China", ["CO"] = "Colombia",
        ["CZ"] = "Czechia", ["DE"] = "Germany", ["DK"] = "Denmark", ["EG"] = "Egypt", ["ES"] = "Spain",
        ["FI"] = "Finland", ["FR"] = "France", ["GB"] = "United Kingdom", ["GR"] = "Greece", ["HK"] = "Hong Kong",
        ["ID"] = "Indonesia", ["IE"] = "Ireland", ["IL"] = "Israel", ["IN"] = "India", ["IT"] = "Italy",
        ["JP"] = "Japan", ["KE"] = "Kenya", ["KR"] = "South Korea", ["MX"] = "Mexico", ["MY"] = "Malaysia",
        ["NG"] = "Nigeria", ["NL"] = "Netherlands", ["NO"] = "Norway", ["NZ"] = "New Zealand", ["PH"] = "Philippines",
        ["PL"] = "Poland", ["PT"] = "Portugal", ["SE"] = "Sweden", ["SG"] = "Singapore", ["TH"] = "Thailand",
        ["TR"] = "Türkiye", ["TW"] = "Taiwan", ["UA"] = "Ukraine", ["AE"] = "United Arab Emirates", ["US"] = "United States",
        ["VN"] = "Vietnam", ["ZA"] = "South Africa",
    };

    /// <summary>The English defaults.</summary>
    public static LiveVisitorsLabels Default { get; } = new();

    /// <summary>The accessible name of the card.</summary>
    public string Title { get; init; } = "Live visitors";

    /// <summary>The status while connected.</summary>
    public string Live { get; init; } = "Live";

    /// <summary>The status while paused.</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>The status before the stream connects.</summary>
    public string Connecting { get; init; } = "Connecting";

    /// <summary>The status after the connection dropped.</summary>
    public string Reconnecting { get; init; } = "Reconnecting";

    /// <summary>The status word when the card has no data and no simulation.</summary>
    public string NoData { get; init; } = "No data";

    /// <summary>The words after the number, pluralized from the count ("people on the site now").</summary>
    public Func<int, string> People { get; init; } = count => count == 1 ? "person on the site now" : "people on the site now";

    /// <summary>The heading of the pages list.</summary>
    public string PagesHeading { get; init; } = "Top pages";

    /// <summary>The heading of the conversions ticker.</summary>
    public string EventsHeading { get; init; } = "Signups and upgrades";

    /// <summary>The pages list when no page has anyone on it.</summary>
    public string NoPages { get; init; } = "No one on a page yet";

    /// <summary>The ticker before the first conversion.</summary>
    public string NoEvents { get; init; } = "No signups yet";

    /// <summary>The pause button's name and tooltip while live (it names the action).</summary>
    public string Pause { get; init; } = "Pause live updates";

    /// <summary>The pause button's name and tooltip while paused.</summary>
    public string Resume { get; init; } = "Resume live updates";

    /// <summary>Said politely when the person pauses.</summary>
    public string PausedAnnouncement { get; init; } = "Live updates paused";

    /// <summary>Said politely when the person resumes.</summary>
    public string ResumedAnnouncement { get; init; } = "Live updates resumed";

    /// <summary>Said politely when the card plays a simulated stream.</summary>
    public string SimulatedAnnouncement { get; init; } = "Showing a simulated stream of visitors";

    /// <summary>A page row as read by screen readers ("/pricing, 24 people").</summary>
    public Func<string, int, string> PageSentence { get; init; } =
        (path, count) => path + ", " + count.ToString("N0", CultureInfo.InvariantCulture) + (count == 1 ? " person" : " people");

    /// <summary>The short time in the ticker's right column ("now", "12s", "3m", "2h", "4d").</summary>
    public Func<TimeSpan, string> Ago { get; init; } = ago =>
    {
        var seconds = (long)Math.Max(0, Math.Floor(ago.TotalSeconds));
        return seconds switch
        {
            < 5 => "now",
            < 60 => seconds.ToString(CultureInfo.InvariantCulture) + "s",
            < 3600 => (seconds / 60).ToString(CultureInfo.InvariantCulture) + "m",
            < 86400 => (seconds / 3600).ToString(CultureInfo.InvariantCulture) + "h",
            _ => (seconds / 86400).ToString(CultureInfo.InvariantCulture) + "d",
        };
    };

    /// <summary>The time in words for screen readers ("just now", "12 seconds ago", "3 minutes ago").</summary>
    public Func<TimeSpan, string> AgoSpoken { get; init; } = ago =>
    {
        var seconds = (long)Math.Max(0, Math.Floor(ago.TotalSeconds));
        static string Unit(long n, string unit) => n.ToString(CultureInfo.InvariantCulture) + " " + unit + (n == 1 ? string.Empty : "s") + " ago";
        return seconds switch
        {
            < 5 => "just now",
            < 60 => Unit(seconds, "second"),
            < 3600 => Unit(seconds / 60, "minute"),
            < 86400 => Unit(seconds / 3600, "hour"),
            _ => Unit(seconds / 86400, "day"),
        };
    };

    /// <summary>A country spelled out from its ISO alpha-2 code ("DE" → "Germany"); unknown codes are read as given.</summary>
    public Func<string, string> CountryName { get; init; } =
        code => s_countries.TryGetValue(code ?? string.Empty, out var name) ? name : code ?? string.Empty;

    /// <summary>
    /// An event row as one sentence, from its label, city, spelled-out country and spoken time
    /// ("Signed up, Berlin, Germany, 12 seconds ago").
    /// </summary>
    public Func<string, string, string, string, string> EventSentence { get; init; } = (label, city, country, ago) =>
        string.Join(", ", new[] { label, city, country, ago }.Where(part => !string.IsNullOrEmpty(part)));
}
