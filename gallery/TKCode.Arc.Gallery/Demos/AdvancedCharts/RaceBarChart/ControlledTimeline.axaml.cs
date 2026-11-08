using Avalonia.Controls;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

public partial class RaceBarChartControlledTimelineDemo : UserControl
{
    // Made-up library branches, total cards issued by the end of each quarter.
    private static readonly (string Id, string Label, double[] Totals)[] s_branches =
    [
        ("mill", "Mill Street", [310, 520, 760, 930, 1120, 1290, 1480, 1610]),
        ("orchard", "Orchard Lane", [280, 610, 840, 1180, 1400, 1520, 1590, 1660]),
        ("harbour", "Harbour Road", [190, 340, 610, 800, 1080, 1370, 1630, 1910]),
        ("station", "Station Hill", [400, 560, 690, 810, 900, 980, 1040, 1100]),
        ("foundry", "Foundry Yard", [120, 260, 380, 560, 830, 1010, 1190, 1450]),
        ("meadow", "Meadow Gate", [90, 150, 240, 300, 410, 590, 720, 840])
    ];

    private static readonly string[] s_quarters = ["Q1 2025", "Q2 2025", "Q3 2025", "Q4 2025", "Q1 2026", "Q2 2026", "Q3 2026", "Q4 2026"];

    public RaceBarChartControlledTimelineDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = value => value.ToString("N0");
        Chart.TickFormatter = value => ArcFormatting.Compact(value);
        Chart.Items = s_branches.Select(b => new RaceBarItem(b.Id, b.Label)).ToList();
        Chart.Frames = s_quarters.Select((quarter, q) => new RaceBarFrame(quarter, s_branches.ToDictionary(b => b.Id, b => b.Totals[q]))).ToList();
        First.Click += (_, _) => Chart.FrameIndex = 0;
        Previous.Click += (_, _) => Chart.FrameIndex = Math.Max(0, Chart.FrameIndex - 1);
        Next.Click += (_, _) => Chart.FrameIndex = Math.Min(s_quarters.Length - 1, Chart.FrameIndex + 1);
        Last.Click += (_, _) => Chart.FrameIndex = s_quarters.Length - 1;
        Chart.FrameChanged += (_, e) => Log.Text = $"{e.Period}: {e.Leader?.Label} leads";
        Log.Text = "Q1 2025: Station Hill leads";
    }
}
