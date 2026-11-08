using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SankeyFlowCityBudgetDemo : UserControl
{
    public SankeyFlowCityBudgetDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = millions => $"${millions:0.#}M";
        Chart.Columns = ["Revenue", "Department", "Spent on"];
        // Node labels are the ids here: a link end with no node gets a node named after its id.
        Chart.Links =
        [
            new("Property tax", "Public safety", 18),
            new("Property tax", "Schools", 20),
            new("Property tax", "Streets and transit", 6),
            new("Property tax", "Parks and libraries", 2),
            new("Sales tax", "Public safety", 9),
            new("Sales tax", "Schools", 4),
            new("Sales tax", "Streets and transit", 6),
            new("Sales tax", "Parks and libraries", 3),
            new("State grants", "Public safety", 2),
            new("State grants", "Schools", 3),
            new("State grants", "Streets and transit", 7),
            new("State grants", "Parks and libraries", 2),
            new("State grants", "Water works", 4),
            new("Utility fees", "Public safety", 2),
            new("Utility fees", "Parks and libraries", 2),
            new("Utility fees", "Water works", 10),
            new("Public safety", "Salaries", 24),
            new("Public safety", "Equipment", 5),
            new("Public safety", "Contracts", 2),
            new("Schools", "Salaries", 21),
            new("Schools", "Equipment", 2),
            new("Schools", "Contracts", 4),
            new("Streets and transit", "Salaries", 5),
            new("Streets and transit", "Equipment", 3),
            new("Streets and transit", "Contracts", 11),
            new("Parks and libraries", "Salaries", 6),
            new("Parks and libraries", "Contracts", 3),
            new("Water works", "Salaries", 5),
            new("Water works", "Equipment", 4),
            new("Water works", "Contracts", 2),
            new("Water works", "Debt service", 3),
        ];
    }
}
