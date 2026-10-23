using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityTerrainNarrowDemo : UserControl
{
    public ActivityTerrainNarrowDemo()
    {
        InitializeComponent();
        Terrain.Metrics.AddRange(ActivityTerrainDemoData.Metrics());
    }
}
