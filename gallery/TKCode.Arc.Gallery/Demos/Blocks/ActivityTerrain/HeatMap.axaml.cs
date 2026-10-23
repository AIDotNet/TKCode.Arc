using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityTerrainHeatMapDemo : UserControl
{
    public ActivityTerrainHeatMapDemo()
    {
        InitializeComponent();
        Terrain.Metrics.AddRange(ActivityTerrainDemoData.Metrics());
    }
}
