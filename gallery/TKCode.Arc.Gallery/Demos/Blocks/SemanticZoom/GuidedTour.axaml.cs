using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SemanticZoomGuidedTourDemo : UserControl
{
    public SemanticZoomGuidedTourDemo()
    {
        InitializeComponent();
        var org = SemanticZoomDemoData.Shared;
        Map.Organization = org;
        // Every team in turn, then the whole map.
        var stops = new AvaloniaList<SemanticZoomNode>();
        stops.AddRange(org.Teams);
        stops.Add(org);
        Map.TourStops = stops;
        FleetButton.CommandParameter = org.Teams[1];
        FleetButton.Command = Map.FlyToCommand;
        Map.ScopeChanged += (_, e) => Status.Text = e.NewScope is { } node ? $"Showing {node.Title}." : string.Empty;
    }
}
