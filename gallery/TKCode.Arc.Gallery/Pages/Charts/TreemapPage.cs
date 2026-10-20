using TKCode.Arc.Gallery.Shell;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Pages;

public static class TreemapPage
{
    private sealed record Plan(string Name, double Revenue, double Seats, double Growth);

    private sealed record Country(string Code, string Name, Plan[] Plans);

    private sealed record Region(string Code, string Name, Country[] Countries);

    private static readonly Region[] Regions =
    [
        new("amer", "Americas",
        [
            new("us", "United States", [new("Enterprise", 420, 3100, 18), new("Team", 310, 5200, 9), new("Starter", 120, 9100, -4)]),
            new("ca", "Canada", [new("Enterprise", 80, 640, 12), new("Team", 55, 980, 6)]),
            new("br", "Brazil", [new("Team", 40, 820, 31), new("Starter", 25, 1900, 22)])
        ]),
        new("emea", "Europe",
        [
            new("de", "Germany", [new("Enterprise", 190, 1400, 7), new("Team", 120, 2100, 3)]),
            new("uk", "United Kingdom", [new("Enterprise", 150, 1200, -2), new("Team", 90, 1700, 1), new("Starter", 30, 2600, -9)]),
            new("fr", "France", [new("Team", 70, 1300, 5), new("Starter", 20, 1500, 2)])
        ]),
        new("apac", "Asia Pacific",
        [
            new("jp", "Japan", [new("Enterprise", 130, 900, 14), new("Team", 60, 1100, 10)]),
            new("au", "Australia", [new("Team", 50, 860, 4), new("Starter", 15, 1200, 0)]),
            new("in", "India", [new("Team", 35, 1600, 44), new("Starter", 30, 5200, 38)])
        ])
    ];

    private static TreemapNode Build(bool seats) => new("all", "All regions", children: Regions.Select(region =>
        new TreemapNode(region.Code, region.Name, children: region.Countries.Select(country =>
            new TreemapNode(country.Code, country.Name, children: country.Plans.Select(plan =>
                new TreemapNode($"{country.Code}-{plan.Name.ToLowerInvariant()}", plan.Name, seats ? plan.Seats : plan.Revenue, plan.Growth)))))));

    public static Control Create()
    {
        var revenue = Build(false);
        var seats = Build(true);
        var chart = new Treemap
        {
            Label = "Revenue",
            Root = revenue,
            ColorLabel = "Growth",
            ValueFormatter = v => $"${v:#,0}K",
            ColorFormatter = v => $"{(v >= 0 ? "+" : "−")}{Math.Abs(v):0}%"
        };
        var byRevenue = new Button { Content = "Revenue", Variant = ButtonVariant.Secondary };
        var bySeats = new Button { Content = "Seats", Variant = ButtonVariant.Secondary };
        byRevenue.Click += (_, _) =>
        {
            chart.Label = "Revenue";
            chart.ValueFormatter = v => $"${v:#,0}K";
            chart.Root = revenue;
        };
        bySeats.Click += (_, _) =>
        {
            chart.Label = "Seats";
            chart.ValueFormatter = v => $"{v:#,0}";
            chart.Root = seats;
        };
        var focus = GalleryUi.Text("Click a region to zoom in; Escape, Backspace or the breadcrumb zoom out.", TKCode.Arc.DesignTokens.ArcDesignTokenKind.TextMuted, 13);
        chart.FocusedNodeChanged += (_, e) => focus.Text = $"In view: {e.Node?.Label}";

        var flat = new Treemap { Label = "Storage by folder", PlotHeight = 260 };
        flat.Items.Add(new TreemapNode("photos", "Photos", 48));
        flat.Items.Add(new TreemapNode("video", "Video", 112));
        flat.Items.Add(new TreemapNode("docs", "Documents", 12));
        flat.Items.Add(new TreemapNode("music", "Music", 26));
        flat.Items.Add(new TreemapNode("apps", "Applications", 64));
        flat.Items.Add(new TreemapNode("other", "Other", 9));

        return GalleryUi.Column(20,
            GalleryUi.Card("Revenue by region, country and plan", "Tiles take their region's hue and deepen with growth; switching the measure morphs every tile in place.",
                GalleryUi.Column(16, chart, GalleryUi.Row(byRevenue, bySeats), focus)),
            GalleryUi.Card("One level, one depth", "Without a color measure every tile is drawn at one depth.", flat));
    }
}
