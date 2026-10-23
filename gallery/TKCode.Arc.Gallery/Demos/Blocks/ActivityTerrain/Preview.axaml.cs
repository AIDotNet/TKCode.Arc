using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityTerrainPreviewDemo : UserControl
{
    public ActivityTerrainPreviewDemo()
    {
        InitializeComponent();
        Terrain.Metrics.AddRange(ActivityTerrainDemoData.Metrics());
    }
}
