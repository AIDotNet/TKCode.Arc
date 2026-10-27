using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusPreviewDemo : UserControl
{
    public UptimeStatusPreviewDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
        Status.Incidents.AddRange(UptimeStatusDemoData.Incidents());
        var simulation = new UptimeIncidentSimulation(Status);
        Simulate.Click += (_, _) =>
        {
            simulation.Advance();
            Simulate.Content = simulation.ActionText;
        };
    }
}
