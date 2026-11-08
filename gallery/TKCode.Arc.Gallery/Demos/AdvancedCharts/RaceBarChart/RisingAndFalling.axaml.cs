using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RaceBarChartRisingAndFallingDemo : UserControl
{
    // Made-up shows. Each week stands alone, so a show that loses listeners falls down the ranking.
    private static readonly (string Id, string Label, double[] Weekly)[] s_shows =
    [
        ("dawn", "Dawn Shift", [8200, 8000, 7600, 7100, 6900, 6400, 6800, 7300, 7900, 8400]),
        ("static", "Pleasant Static", [5100, 5600, 6300, 7400, 8800, 9600, 9100, 8200, 7000, 6100]),
        ("low-tide", "Low Tide Hour", [6600, 6200, 5900, 5500, 5300, 5600, 6200, 7100, 8300, 9400]),
        ("night", "Night Ferry", [4300, 4900, 5200, 6100, 6000, 5200, 4700, 4400, 4100, 3900]),
        ("garden", "The Quiet Garden", [3900, 3700, 4200, 4100, 4800, 5400, 5900, 5700, 5300, 5000]),
        ("market", "Market Square", [7000, 7300, 6900, 6300, 5500, 4900, 4300, 3900, 3700, 3500]),
        ("relay", "Relay Station", [2100, 2600, 3300, 3900, 4600, 6200, 7500, 6900, 5800, 4700]),
        ("almanac", "Tin Almanac", [3000, 2800, 2600, 2900, 3400, 3200, 3000, 3300, 3600, 4300])
    ];

    public RaceBarChartRisingAndFallingDemo()
    {
        InitializeComponent();
        Chart.Items = s_shows.Select(s => new RaceBarItem(s.Id, s.Label)).ToList();
        Chart.Frames = Enumerable.Range(0, 10)
                                 .Select(week => new RaceBarFrame($"Week {week + 1}", s_shows.ToDictionary(s => s.Id, s => s.Weekly[week])))
                                 .ToList();
    }
}
