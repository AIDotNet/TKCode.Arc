using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ProviderStatus"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; numbers, dates and sentences that carry values are format functions.
/// </summary>
public sealed class ProviderStatusLabels
{
    /// <summary>The English defaults.</summary>
    public static ProviderStatusLabels Default { get; } = new();

    /// <summary>The health label of a working provider.</summary>
    public string Operational { get; init; } = "Operational";

    /// <summary>The health label of a slow or partly failing provider.</summary>
    public string Degraded { get; init; } = "Degraded";

    /// <summary>The health label of a failing provider.</summary>
    public string Outage { get; init; } = "Outage";

    /// <summary>A day without data.</summary>
    public string NoData { get; init; } = "No data";

    /// <summary>The last day of the bar.</summary>
    public string Today { get; init; } = "Today";

    /// <summary>The incident stage "Investigating".</summary>
    public string Investigating { get; init; } = "Investigating";

    /// <summary>The incident stage "Identified".</summary>
    public string Identified { get; init; } = "Identified";

    /// <summary>The incident stage "Monitoring".</summary>
    public string Monitoring { get; init; } = "Monitoring";

    /// <summary>The incident stage "Resolved".</summary>
    public string Resolved { get; init; } = "Resolved";

    /// <summary>The column over the names (wide cards).</summary>
    public string ProviderColumn { get; init; } = "Provider";

    /// <summary>The column over the health labels.</summary>
    public string StatusColumn { get; init; } = "Status";

    /// <summary>The column over the median latency.</summary>
    public string LatencyColumn { get; init; } = "p50";

    /// <summary>The column over the uptime bars.</summary>
    public string HistoryColumn { get; init; } = "Last 30 days";

    /// <summary>The column over the 30 day percentage.</summary>
    public string UptimeColumn { get; init; } = "Uptime";

    /// <summary>The header's summary from the counts of operational, degraded and failing providers.</summary>
    public Func<int, int, int, string> Summary { get; init; } = (operational, degraded, outage) =>
    {
        if (degraded == 0 && outage == 0)
        {
            return operational == 1 ? "Operational" : "All systems operational";
        }
        var parts = new List<string>(3);
        if (outage > 0)
        {
            parts.Add(outage == 1 ? "1 outage" : $"{outage} outages");
        }
        if (degraded > 0)
        {
            parts.Add($"{degraded} degraded");
        }
        if (operational > 0)
        {
            parts.Add($"{operational} operational");
        }
        return string.Join(" · ", parts);
    };

    /// <summary>The countdown beside the ring, from the seconds left ("Next check in 6s").</summary>
    public Func<int, string> NextCheck { get; init; } = seconds => $"Next check in {seconds}s";

    /// <summary>The countdown on narrow cards ("6s").</summary>
    public Func<int, string> NextCheckShort { get; init; } = seconds => $"{seconds}s";

    /// <summary>The countdown after a failed check ("Retry in 6s").</summary>
    public Func<int, string> RetryIn { get; init; } = seconds => $"Retry in {seconds}s";

    /// <summary>The label while a check runs.</summary>
    public string Checking { get; init; } = "Checking";

    /// <summary>The refresh button's name.</summary>
    public string CheckNow { get; init; } = "Check now";

    /// <summary>The refresh button's name after a failed check.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>Said after a successful check, from the new summary.</summary>
    public Func<string, string> Checked { get; init; } = summary => $"Checked. {summary}.";

    /// <summary>Shown when a check fails with an exception that has no message.</summary>
    public string RefreshFailed { get; init; } = "Couldn’t reach the status feeds. Showing the last results.";

    /// <summary>The link at the end of an open incident.</summary>
    public string StatusPage { get; init; } = "Status page";

    /// <summary>Said when a simulation stands in for a real action, such as opening a status page.</summary>
    public string Simulated { get; init; } = "Simulated, no requests were sent";

    /// <summary>The footnote while a simulation plays and no <see cref="ProviderStatus.Footnote"/> is set.</summary>
    public string SampleFootnote { get; init; } = "Sample data from a simulation, not live status.";

    /// <summary>The header's summary while the card has no providers.</summary>
    public string NoProviders { get; init; } = "No providers";

    /// <summary>The name of the skeleton shown while loading.</summary>
    public string Loading { get; init; } = "Loading providers";

    /// <summary>The median latency: "412 ms" under a second, "1.92 s" from a second.</summary>
    public Func<double, string> Latency { get; init; } = ms => ms < 1000
        ? Math.Round(ms).ToString("0", CultureInfo.InvariantCulture) + " ms"
        : (ms / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s";

    /// <summary>The latency on narrow cards, where it sits under the name ("p50 412 ms").</summary>
    public Func<string, string> LatencyNarrow { get; init; } = latency => "p50 " + latency;

    /// <summary>Shown for a missing latency or uptime.</summary>
    public string Missing { get; init; } = "–";

    /// <summary>An uptime percentage: "99.61%", "100%".</summary>
    public Func<double, string> Percent { get; init; } = value => value >= 100
        ? "100%"
        : (Math.Floor(value * 100) / 100).ToString("0.00", CultureInfo.InvariantCulture) + "%";

    /// <summary>A day's uptime in its tooltip ("99.94% uptime").</summary>
    public Func<string, string> DayUptime { get; init; } = percent => percent + " uptime";

    /// <summary>A day's date: "Oct 28".</summary>
    public Func<DateOnly, string> Date { get; init; } = date => date.ToString("MMM d", CultureInfo.InvariantCulture);

    /// <summary>An update's time from <see cref="ProviderStatusUpdate.At"/>: "14:32 UTC".</summary>
    public Func<DateTimeOffset, string> Time { get; init; } = at => at.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) + " UTC";

    /// <summary>
    /// The uptime bar's name, from the provider, its 30 day uptime (already formatted, or <see cref="Missing"/>) and the
    /// counts of degraded and failing days.
    /// </summary>
    public Func<string, string, int, int, string> BarName { get; init; } = (name, uptime, degraded, outage) =>
    {
        var text = $"{name}, last 30 days: {uptime} uptime";
        if (degraded > 0)
        {
            text += degraded == 1 ? ", 1 degraded day" : $", {degraded} degraded days";
        }
        if (outage > 0)
        {
            text += outage == 1 ? ", 1 day with an outage" : $", {outage} days with an outage";
        }
        return text + ". Use the arrow keys to read each day.";
    };

    /// <summary>What a day says to a screen reader, from its date, health and uptime (and note, or null).</summary>
    public Func<string, string, string, string?, string> Day { get; init; } = (date, health, uptime, note) =>
        string.IsNullOrEmpty(note) ? $"{date}: {health}, {uptime}" : $"{date}: {health}, {uptime}. {note}";

    /// <summary>The name of an incident row's button, from the provider, its health and the incident's title.</summary>
    public Func<string, string, string, string> IncidentRow { get; init; } = (name, health, title) => $"{name}, {health}, {title}";

    /// <summary>The name of the list of updates.</summary>
    public string Updates { get; init; } = "Incident updates";

    /// <summary>The health word of <paramref name="health"/>.</summary>
    public string HealthText(ProviderStatusHealth health) => health switch
    {
        ProviderStatusHealth.Degraded => Degraded,
        ProviderStatusHealth.Outage => Outage,
        _ => Operational
    };

    /// <summary>The word of <paramref name="stage"/>.</summary>
    public string StageText(ProviderStatusStage stage) => stage switch
    {
        ProviderStatusStage.Identified => Identified,
        ProviderStatusStage.Monitoring => Monitoring,
        ProviderStatusStage.Resolved => Resolved,
        _ => Investigating
    };
}
