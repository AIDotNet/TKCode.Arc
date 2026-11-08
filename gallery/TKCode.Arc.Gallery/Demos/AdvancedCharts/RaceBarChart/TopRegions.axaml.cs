using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RaceBarChartTopRegionsDemo : UserControl
{
    // New rooftop installations per month in a set of made-up districts; the chart shows the running total.
    private static readonly (string Id, string Label, string Group, double[] Monthly)[] s_districts =
    [
        ("marrow", "Marrow Bay", "north", [120, 140, 150, 130, 90, 70, 60, 50, 60, 70, 80, 90]),
        ("kestrel", "Kestrel Point", "north", [60, 70, 90, 110, 130, 150, 140, 120, 90, 70, 60, 50]),
        ("pinehollow", "Pinehollow", "inland", [40, 40, 60, 80, 120, 180, 220, 240, 200, 150, 110, 80]),
        ("sandgate", "Sandgate", "south", [150, 120, 100, 90, 80, 80, 90, 100, 110, 120, 130, 140]),
        ("ironvale", "Ironvale", "inland", [90, 100, 110, 100, 100, 90, 90, 80, 90, 100, 110, 110]),
        ("larkfield", "Larkfield", "inland", [20, 30, 50, 90, 140, 190, 230, 250, 260, 240, 210, 190]),
        ("copper", "Copper Ridge", "inland", [70, 60, 60, 70, 70, 60, 60, 70, 60, 60, 50, 50]),
        ("fenwick", "Fenwick Downs", "south", [100, 110, 120, 140, 150, 130, 100, 80, 70, 60, 60, 60]),
        ("aster", "Aster Isle", "islands", [30, 50, 80, 120, 110, 90, 150, 200, 170, 130, 90, 60]),
        ("tallowmere", "Tallowmere", "north", [80, 70, 60, 50, 50, 60, 70, 80, 90, 100, 120, 140]),
        ("greywater", "Greywater", "south", [50, 60, 70, 60, 50, 60, 70, 60, 50, 40, 40, 30]),
        ("holloway", "Holloway Reach", "islands", [10, 20, 40, 60, 90, 120, 130, 120, 140, 170, 200, 230])
    ];

    private static readonly string[] s_months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public RaceBarChartTopRegionsDemo()
    {
        InitializeComponent();
        Chart.Groups =
        [
            new RaceBarGroup("north", "North coast"),
            new RaceBarGroup("inland", "Inland"),
            new RaceBarGroup("south", "South coast"),
            new RaceBarGroup("islands", "Islands")
        ];
        Chart.Items = s_districts.Select(d => new RaceBarItem(d.Id, d.Label, d.Group)).ToList();
        var totals = new Dictionary<string, double>();
        var frames = new List<RaceBarFrame>();
        for (var month = 0; month < s_months.Length; month++)
        {
            foreach (var district in s_districts)
            {
                totals[district.Id] = totals.GetValueOrDefault(district.Id) + district.Monthly[month];
            }
            frames.Add(new RaceBarFrame(s_months[month], totals));
        }
        Chart.Frames = frames;
    }
}
