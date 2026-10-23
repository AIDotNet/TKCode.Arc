using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityTerrainIncidentsDemo : UserControl
{
    public ActivityTerrainIncidentsDemo()
    {
        InitializeComponent();
        var incidents = ActivityTerrainDemoData.Metrics()[2];
        incidents.SummaryTitle = "Pages this week";
        incidents.Unit = "pages";
        Terrain.Metrics.Add(incidents);
    }
}
