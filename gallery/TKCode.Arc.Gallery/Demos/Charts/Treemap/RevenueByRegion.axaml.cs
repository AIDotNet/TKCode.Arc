using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreemapRevenueByRegionDemo : UserControl
{
    private sealed record Plan(string Name, double Revenue, double Seats, double Growth);

    private sealed record Country(string Code, string Name, Plan[] Plans);

    private sealed record Region(string Code, string Name, Country[] Countries);

    private static readonly Region[] s_regions =
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

    private readonly TreemapNode _revenue = Build(false);
    private readonly TreemapNode _seats = Build(true);

    public TreemapRevenueByRegionDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = v => $"${v:#,0}K";
        Chart.ColorFormatter = v => $"{(v >= 0 ? "+" : "−")}{Math.Abs(v):0}%";
        Chart.Root = _revenue;
    }

    // Change the label, the formatter and the tree together; tiles with the same Id keep their identity.
    private void OnRevenueClick(object? sender, RoutedEventArgs e)
    {
        Chart.Label = "Revenue";
        Chart.ValueFormatter = v => $"${v:#,0}K";
        Chart.Root = _revenue;
    }

    private void OnSeatsClick(object? sender, RoutedEventArgs e)
    {
        Chart.Label = "Seats";
        Chart.ValueFormatter = v => $"{v:#,0}";
        Chart.Root = _seats;
    }

    private void OnFocusedNodeChanged(object? sender, TreemapFocusedNodeChangedEventArgs e)
        => FocusText.Text = $"In view: {e.Node?.Label}";

    private static TreemapNode Build(bool seats) => new("all", "All regions", children: s_regions.Select(region =>
        new TreemapNode(region.Code, region.Name, children: region.Countries.Select(country =>
            new TreemapNode(country.Code, country.Name, children: country.Plans.Select(plan =>
                new TreemapNode($"{country.Code}-{plan.Name.ToLowerInvariant()}", plan.Name, seats ? plan.Seats : plan.Revenue, plan.Growth)))))));
}
