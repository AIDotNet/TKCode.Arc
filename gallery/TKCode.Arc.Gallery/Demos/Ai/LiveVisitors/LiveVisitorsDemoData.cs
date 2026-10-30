namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented site the LiveVisitors demos simulate: a SaaS marketing site with about 128 people on it, visitors from
/// 21 cities, eight pages and three kinds of conversion. The traffic is invented; nothing is fetched or tracked.
/// </summary>
internal static class LiveVisitorsDemoData
{
    /// <summary>About how many people are on the site.</summary>
    public const int TypicalCount = 128;

    /// <summary>The cities people come from, weighted.</summary>
    public static IReadOnlyList<LiveVisitorsSimulationCity> Cities { get; } =
    [
        new("San Francisco", "US", 37.77, -122.42, 5), new("New York", "US", 40.71, -74.01, 5), new("Austin", "US", 30.27, -97.74, 3),
        new("Toronto", "CA", 43.65, -79.38, 3), new("Mexico City", "MX", 19.43, -99.13, 2), new("São Paulo", "BR", -23.55, -46.63, 3),
        new("London", "GB", 51.51, -0.13, 5), new("Dublin", "IE", 53.35, -6.26, 2), new("Paris", "FR", 48.86, 2.35, 3),
        new("Amsterdam", "NL", 52.37, 4.90, 3), new("Berlin", "DE", 52.52, 13.40, 4), new("Zurich", "CH", 47.38, 8.54, 2),
        new("Stockholm", "SE", 59.33, 18.07, 2), new("Madrid", "ES", 40.42, -3.70, 2), new("Lagos", "NG", 6.52, 3.38, 2),
        new("Nairobi", "KE", -1.29, 36.82, 1), new("Bengaluru", "IN", 12.97, 77.59, 4), new("Singapore", "SG", 1.35, 103.82, 3),
        new("Seoul", "KR", 37.57, 126.98, 2), new("Tokyo", "JP", 35.68, 139.69, 4), new("Sydney", "AU", -33.87, 151.21, 3),
    ];

    /// <summary>The site's pages, weighted by their share of the people on it.</summary>
    public static IReadOnlyList<LiveVisitorsSimulationPage> SitePages { get; } =
    [
        new("/", 34), new("/pricing", 22), new("/docs/quickstart", 17), new("/blog/launch-week", 14), new("/changelog", 9),
        new("/signup", 8), new("/customers", 6), new("/docs/api/webhooks", 5),
    ];

    /// <summary>The conversions the ticker shows, weighted by how often they happen.</summary>
    public static IReadOnlyList<LiveVisitorsSimulationConversion> Conversions { get; } =
    [
        new("Signed up", 62), new("Upgraded to Pro", 28), new("Upgraded to Team", 10),
    ];
}
