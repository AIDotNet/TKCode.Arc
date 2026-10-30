using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The state of the realtime stream behind a <see cref="LiveVisitors"/> card.</summary>
public enum LiveVisitorsConnection
{
    /// <summary>Connected: the status reads Live and its dot pulses.</summary>
    Live,

    /// <summary>Not connected yet: placeholders hold the space of the count and the lists.</summary>
    Connecting,

    /// <summary>The connection dropped: the last data stays and the status shows a warning dot with Reconnecting.</summary>
    Reconnecting
}

/// <summary>What kind of arrival a <see cref="LiveVisitorsPing"/> is.</summary>
public enum LiveVisitorsPingKind
{
    /// <summary>A visitor arriving: a dot with one soft ring.</summary>
    Visit,

    /// <summary>A conversion (a signup or an upgrade): a larger dot with a stronger ring.</summary>
    Event
}

/// <summary>A page with people on it, listed by <see cref="LiveVisitors"/> with a bar scaled to the busiest page.</summary>
public sealed class LiveVisitorsPage
{
    /// <summary>Creates an empty page; set <see cref="Path"/> and <see cref="Visitors"/>.</summary>
    public LiveVisitorsPage()
    {
    }

    /// <summary>Creates a page with its path and the number of people on it.</summary>
    public LiveVisitorsPage(string path, int visitors)
    {
        Path = path;
        Visitors = visitors;
    }

    /// <summary>The page's path ("/pricing"). It identifies the row, so a row keeps its place while its count changes.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The people on the page right now.</summary>
    public int Visitors { get; set; }
}

/// <summary>
/// A recent arrival drawn on the <see cref="LiveVisitors"/> map. A new <see cref="Id"/> pings in; an id that is no longer
/// passed fades out. Send arrivals as a rolling window of the last few seconds.
/// </summary>
public sealed class LiveVisitorsPing
{
    /// <summary>Creates an empty ping; set its id and position.</summary>
    public LiveVisitorsPing()
    {
    }

    /// <summary>Creates a ping with its id, latitude, longitude and kind.</summary>
    public LiveVisitorsPing(string id, double latitude, double longitude, LiveVisitorsPingKind kind = LiveVisitorsPingKind.Visit)
    {
        Id = id;
        Latitude = latitude;
        Longitude = longitude;
        Kind = kind;
    }

    /// <summary>Identifies the arrival across updates.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Latitude in degrees, north positive. The map covers 80°N to 56°S; latitudes beyond sit on its edge.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in degrees, east positive.</summary>
    public double Longitude { get; set; }

    /// <summary>A visit, or a conversion with a stronger ring.</summary>
    public LiveVisitorsPingKind Kind { get; set; }
}

/// <summary>
/// A recent conversion in the <see cref="LiveVisitors"/> ticker: what happened (past tense, "Signed up"), where and when.
/// Never put personal data such as an email address in it.
/// </summary>
public sealed class LiveVisitorsEvent
{
    /// <summary>Creates an empty event; set its id, label, place and time.</summary>
    public LiveVisitorsEvent()
    {
    }

    /// <summary>Creates an event with its id, label, city, country code and time.</summary>
    public LiveVisitorsEvent(string id, string label, string city, string country, DateTimeOffset at)
    {
        Id = id;
        Label = label;
        City = city;
        Country = country;
        At = at;
    }

    /// <summary>Identifies the event across updates; a new id slides in at the top of the ticker.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>What happened, short and in the past tense ("Signed up", "Upgraded to Pro").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The city it came from.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>The ISO 3166 alpha-2 country code ("DE"). It shows as a small code chip and is spelled out for screen readers.</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>When it happened; the ticker shows how long ago.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The latitude of the city. With <see cref="Longitude"/>, the newest event is labelled on the map for 2s.</summary>
    public double? Latitude { get; set; }

    /// <summary>The longitude of the city.</summary>
    public double? Longitude { get; set; }

    /// <summary>An image of your own in place of the country code chip (a flag, for example). It is decorative.</summary>
    public IImage? Flag { get; set; }
}

/// <summary>Arguments of <see cref="LiveVisitors.PausedChanged"/>.</summary>
public sealed class LiveVisitorsPausedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LiveVisitorsPausedChangedEventArgs(RoutedEvent routedEvent, bool isPaused)
        : base(routedEvent)
    {
        IsPaused = isPaused;
    }

    /// <summary>Whether the card is now paused. Pause or close your own subscription to match.</summary>
    public bool IsPaused { get; }
}
