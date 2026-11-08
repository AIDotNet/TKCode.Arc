using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SunburstCostBreakdownDemo : UserControl
{
    public SunburstCostBreakdownDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = v => $"${v:#,0}K";
        Chart.Root = new SunburstNode("all", "All costs", children:
        [
            new SunburstNode("fleet", "Fleet", children:
            [
                new SunburstNode("fuel", "Fuel", children: [Leaf("Diesel", 1240), Leaf("Shore power", 180)]),
                new SunburstNode("upkeep", "Upkeep", children: [Leaf("Dry dock", 640), Leaf("Engine overhaul", 410), Leaf("Spare parts", 155)]),
                new SunburstNode("cover", "Insurance", children: [Leaf("Hull", 220), Leaf("Liability", 130)])
            ]),
            new SunburstNode("people", "People", children:
            [
                new SunburstNode("crew", "Crew", children: [Leaf("Deck officers", 880), Leaf("Engineers", 720), Leaf("Stewards", 460)]),
                new SunburstNode("shore", "Shore staff", children: [Leaf("Ticketing", 240), Leaf("Dispatch", 190)]),
                new SunburstNode("training", "Training", children: [Leaf("Safety drills", 75), Leaf("Certification", 60)])
            ]),
            new SunburstNode("terminals", "Terminals", children:
            [
                new SunburstNode("harbour", "Harbour fees", children: [Leaf("Berthing", 340), Leaf("Pilotage", 120)]),
                new SunburstNode("buildings", "Buildings", children: [Leaf("Rent", 280), Leaf("Utilities", 95)])
            ]),
            new SunburstNode("digital", "Digital", children:
            [
                new SunburstNode("booking", "Booking system", children: [Leaf("Licences", 130), Leaf("Support", 70)]),
                new SunburstNode("network", "Network", children: [Leaf("Onboard Wi-Fi", 85), Leaf("Radio", 40)])
            ])
        ]);
    }

    private static SunburstNode Leaf(string label, double thousands) => new(label.ToLowerInvariant().Replace(' ', '-'), label, thousands);
}
