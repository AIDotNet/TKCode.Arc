namespace TKCode.Arc;

/// <summary>
/// Every word an <see cref="UptimeStatus"/> writes or speaks besides the data and the three header titles, with English
/// defaults. Placeholders are numbered as described on each property. Assign a new instance to
/// <see cref="UptimeStatus.Labels"/> to translate the block.
/// </summary>
public sealed class UptimeStatusLabels
{
    /// <summary>The Uptime section's heading.</summary>
    public string UptimeText { get; set; } = "Uptime";

    /// <summary>The Uptime section's hint: {0} the number of days.</summary>
    public string UptimeHintFormat { get; set; } = "Last {0} days · select a service to chart it";

    /// <summary>The legend entry and name of an operational day.</summary>
    public string OperationalText { get; set; } = "Operational";

    /// <summary>The legend entry and name of a degraded day.</summary>
    public string DegradedText { get; set; } = "Degraded";

    /// <summary>The legend entry and name of a day with an outage.</summary>
    public string OutageText { get; set; } = "Major outage";

    /// <summary>The badge of a degraded service.</summary>
    public string DegradedBadgeText { get; set; } = "Degraded";

    /// <summary>The badge of a service in outage.</summary>
    public string OutageBadgeText { get; set; } = "Outage";

    /// <summary>The word after a service's uptime figure.</summary>
    public string UptimeWordText { get; set; } = "uptime";

    /// <summary>The right end of the bars' date row.</summary>
    public string TodayText { get; set; } = "Today";

    /// <summary>The hover card's line when a day had no incident.</summary>
    public string NoIncidentsRecordedText { get; set; } = "No incidents recorded";

    /// <summary>The hover card's title: {0} the service, {1} the date.</summary>
    public string DayTitleFormat { get; set; } = "{0} · {1}";

    /// <summary>The line under the title while no summary is set: {0} the update time (a UTC date and time, so "{0:HH:mm}" works).</summary>
    public string UpdatedFormat { get; set; } = "Updated {0:MMM d} at {0:HH:mm} UTC";

    /// <summary>The Response time section's heading.</summary>
    public string ResponseTimeText { get; set; } = "Response time";

    /// <summary>The Response time section's subtitle: {0} the service.</summary>
    public string ResponseSubtitleFormat { get; set; } = "{0} · last 24 hours";

    /// <summary>The time at the section's top right: {0} the time of the newest (or inspected) sample.</summary>
    public string LatestFormat { get; set; } = "Latest, {0}";

    /// <summary>The time of a sample: {0} the formatted date and time.</summary>
    public string SampleTimeFormat { get; set; } = "{0} UTC";

    /// <summary>The 95th percentile's name.</summary>
    public string P95Text { get; set; } = "p95";

    /// <summary>The median's name.</summary>
    public string P50Text { get; set; } = "p50";

    /// <summary>The target's name.</summary>
    public string TargetText { get; set; } = "p95 target";

    /// <summary>The unit after a response time.</summary>
    public string MillisecondsText { get; set; } = "ms";

    /// <summary>The Incidents section's heading.</summary>
    public string IncidentsText { get; set; } = "Incidents";

    /// <summary>The count under the heading: {0} the number, {1} the days.</summary>
    public string IncidentsCountFormat { get; set; } = "{0} in the last {1} days";

    /// <summary>Shown instead of the list when there is no incident: {0} the days.</summary>
    public string NoIncidentsFormat { get; set; } = "No incidents in the last {0} days";

    /// <summary>The text of a resolved incident's status.</summary>
    public string ResolvedText { get; set; } = "Resolved";

    /// <summary>The badge of an incident that is still going.</summary>
    public string OngoingText { get; set; } = "Investigating";

    /// <summary>A duration: {0} the minutes.</summary>
    public string MinutesFormat { get; set; } = "{0} min";

    /// <summary>The duration of an incident that is not over: {0} the formatted duration.</summary>
    public string SoFarFormat { get; set; } = "{0} so far";

    /// <summary>An incident's start today: {0} the time.</summary>
    public string TodayAtFormat { get; set; } = "Today at {0} UTC";

    /// <summary>An incident's start on another day: {0} the date and time.</summary>
    public string DateAtFormat { get; set; } = "{0} UTC";

    /// <summary>The line under an incident's title: {0} the severity, {1} the start, {2} the duration, {3} the services.</summary>
    public string IncidentMetaFormat { get; set; } = "{0} · {1} · {2} · {3}";

    /// <summary>The line of the response lead while it is going: {0} the name.</summary>
    public string LeadingFormat { get; set; } = "{0} is leading the response";

    /// <summary>The line of the response lead once it is over: {0} the name.</summary>
    public string LedFormat { get; set; } = "{0} led the response";

    /// <summary>The name of an incident's timeline.</summary>
    public string TimelineText { get; set; } = "Updates, newest first";

    /// <summary>The name of an incident button: {0} the title, {1} "expanded" or "collapsed".</summary>
    public string IncidentButtonFormat { get; set; } = "{0}, {1}";

    /// <summary>"expanded" in a button's name.</summary>
    public string ExpandedText { get; set; } = "expanded";

    /// <summary>"collapsed" in a button's name.</summary>
    public string CollapsedText { get; set; } = "collapsed";

    /// <summary>The service button's name: {0} the service, {1} its uptime, {2} its status.</summary>
    public string ServiceButtonFormat { get; set; } = "{0}, {1} uptime, {2}";

    /// <summary>A day bar group's name: {0} the service, {1} the days.</summary>
    public string StripNameFormat { get; set; } = "{0}, daily status for the last {1} days";

    /// <summary>A day bar group's description: {0} the uptime, {1} the incidents ("none" or a list).</summary>
    public string StripHelpFormat { get; set; } = "{0} uptime. Incidents: {1}. Use the arrow keys to inspect a day, PageUp and PageDown to move a week.";

    /// <summary>"none" in the description.</summary>
    public string NoneText { get; set; } = "none";

    /// <summary>An incident in the description: {0} the date, {1} the severity, {2} the minutes.</summary>
    public string StripIncidentFormat { get; set; } = "{0} {1}, {2} minutes";

    /// <summary>The announcement of an inspected day: {0} the date, {1} the status, {2} the uptime, {3} the incidents.</summary>
    public string DayAnnouncementFormat { get; set; } = "{0}: {1}, {2} uptime. {3}.";

    /// <summary>The chart group's name: {0} the service.</summary>
    public string ChartNameFormat { get; set; } = "{0} response time chart. Use the arrow keys to inspect each half hour.";

    /// <summary>The chart's text summary: {0} the service, {1} the newest p50, {2} the newest p95, {3} the highest p95, {4} its time, {5} the target.</summary>
    public string ChartSummaryFormat { get; set; } = "{0}: latest p50 {1} ms, p95 {2} ms. Highest p95 {3} ms at {4}. Target {5} ms.";

    /// <summary>The announcement of an inspected sample: {0} the time, {1} the p50, {2} the p95.</summary>
    public string PointAnnouncementFormat { get; set; } = "{0}: p50 {1} ms, p95 {2} ms.";

    /// <summary>The data table's caption: {0} the service.</summary>
    public string TableCaptionFormat { get; set; } = "{0}: response time per half hour";

    /// <summary>One row of the data table: {0} the time, {1} the p50, {2} the p95.</summary>
    public string TableRowFormat { get; set; } = "{0}: p50 {1} ms, p95 {2} ms";

    /// <summary>The empty state of the block.</summary>
    public string NoServicesText { get; set; } = "No services to show";

    /// <summary>Announced when an incident appears: {0} the title.</summary>
    public string IncidentStartedFormat { get; set; } = "Incident: {0}";

    /// <summary>Announced when an incident is resolved: {0} the title.</summary>
    public string IncidentResolvedFormat { get; set; } = "Resolved: {0}";

    /// <summary>The name of the avatar: {0} the person.</summary>
    public string AvatarFormat { get; set; } = "{0}";
}
