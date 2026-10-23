namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="RevenueGlobe"/> shows or announces, with English defaults. Replace the whole object (it is
/// immutable) to translate the block: <c>globe.Labels = new RevenueGlobeLabels { Replay = "Tag wiederholen" }</c>.
/// </summary>
public sealed class RevenueGlobeLabels
{
    /// <summary>The button that replays the day from midnight ("Replay today").</summary>
    public string Replay { get; init; } = "Replay today";

    /// <summary>Accessible name of the pause button while the day plays ("Pause").</summary>
    public string Pause { get; init; } = "Pause";

    /// <summary>Accessible name of the same button while paused ("Play").</summary>
    public string Play { get; init; } = "Play";

    /// <summary>The pill while the playhead is the present ("Live").</summary>
    public string Live { get; init; } = "Live";

    /// <summary>The pill while the playhead is in the past ("Go live").</summary>
    public string GoLive { get; init; } = "Go live";

    /// <summary>The pill under reduced motion, where the picture holds still ("Now").</summary>
    public string Now { get; init; } = "Now";

    /// <summary>The time zone after the clock ("UTC").</summary>
    public string TimeZone { get; init; } = "UTC";

    /// <summary>Accessible name of the timeline ("Time of day, UTC").</summary>
    public string Timeline { get; init; } = "Time of day, UTC";

    /// <summary>The hour marks under the timeline.</summary>
    public IReadOnlyList<string> HourMarks { get; init; } = ["00:00", "06:00", "12:00", "18:00", "24:00"];

    /// <summary>Label of the large figure ("Revenue today").</summary>
    public string Revenue { get; init; } = "Revenue today";

    /// <summary>Label of the payment count ("Payments"; also in the hover card).</summary>
    public string Payments { get; init; } = "Payments";

    /// <summary>Label of the revenue of the last 60 minutes ("Last hour").</summary>
    public string LastHour { get; init; } = "Last hour";

    /// <summary>Title of the region list ("Regions").</summary>
    public string Regions { get; init; } = "Regions";

    /// <summary>Note beside the region list's title ("Against forecast").</summary>
    public string RegionsNote { get; init; } = "Against forecast";

    /// <summary>A region's share of its forecast; gets the rounded percentage ("62% of forecast").</summary>
    public Func<double, string> OfForecast { get; init; } = static percent => $"{percent:0}% of forecast";

    /// <summary>Title of the top-cities list ("Top cities").</summary>
    public string TopCities { get; init; } = "Top cities";

    /// <summary>Note beside the top-cities list's title ("Today").</summary>
    public string TopCitiesNote { get; init; } = "Today";

    /// <summary>Hover card: label of the city's revenue ("Today").</summary>
    public string Today { get; init; } = "Today";

    /// <summary>Hover card: label of the city's share of all revenue ("Share").</summary>
    public string Share { get; init; } = "Share";

    /// <summary>Hover card: the line under the city name; gets the region and the local time ("EMEA · 16:20 local").</summary>
    public Func<string, string, string> CityLine { get; init; } = static (region, time) =>
        string.IsNullOrEmpty(region) ? $"{time} local" : $"{region} · {time} local";

    /// <summary>Accessible name of the globe ("Revenue globe").</summary>
    public string Globe { get; init; } = "Revenue globe";

    /// <summary>Added to the globe's accessible name: how to use it.</summary>
    public string GlobeHelp { get; init; } = "Arrow keys turn the globe, Escape clears the selection.";

    /// <summary>Spoken when the day pauses ("Paused").</summary>
    public string Paused { get; init; } = "Paused";

    /// <summary>Spoken when a replay starts ("Replaying today").</summary>
    public string Replaying { get; init; } = "Replaying today";

    /// <summary>Spoken when the playhead reaches the present ("Back to live").</summary>
    public string BackToLive { get; init; } = "Back to live";

    /// <summary>Spoken when the camera flies to a city or region; gets its name ("Camera on Lisbon").</summary>
    public Func<string, string> CameraOn { get; init; } = static name => $"Camera on {name}";

    /// <summary>Spoken when the selection is cleared ("Selection cleared").</summary>
    public string SelectionCleared { get; init; } = "Selection cleared";

    /// <summary>
    /// The globe's description for screen readers; gets the revenue, the payment count, the time, the regions
    /// ("Americas $1.2M, EMEA $900K") and the leading city.
    /// </summary>
    public Func<string, string, string, string, string, string> Summary { get; init; } =
        static (revenue, payments, time, regions, leader) =>
            $"{revenue} revenue today from {payments} payments as of {time} UTC." +
            (string.IsNullOrEmpty(regions) ? string.Empty : $" {regions}.") +
            (string.IsNullOrEmpty(leader) ? string.Empty : $" {leader} leads.");

    /// <summary>One line of the city table read by screen readers; gets the city, region, revenue and payment count.</summary>
    public Func<string, string, string, string, string> CityTableRow { get; init; } =
        static (city, region, revenue, payments) => $"{city}, {region}: {revenue} from {payments} payments";

    /// <summary>A region row's extra description; gets its share of today's revenue and its forecast.</summary>
    public Func<string, string, string> RegionDetail { get; init; } =
        static (share, forecast) => $"{share} of today's revenue, forecast {forecast}";
}
