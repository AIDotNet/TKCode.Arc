using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An enrich column restored from saved cells: two values, one failure, the rest not run.</summary>
public partial class EnrichColumnSavedValuesDemo : UserControl
{
    public EnrichColumnSavedValuesDemo()
    {
        InitializeComponent();
        Table.InitialCells = new Dictionary<string, EnrichColumnCell>
        {
            ["lumen"] = EnrichColumnCell.Done("240 people · Series B, $38M",
                new EnrichColumnSource("lumenfreight.example/about", "https://lumenfreight.example/about")),
            ["harbor"] = EnrichColumnCell.Done("85 people · Seed, $6M",
                new EnrichColumnSource("harboranalytics.example/news", "https://harboranalytics.example/news")),
            ["kestrel"] = EnrichColumnCell.Failed("No source found"),
        };
    }
}
