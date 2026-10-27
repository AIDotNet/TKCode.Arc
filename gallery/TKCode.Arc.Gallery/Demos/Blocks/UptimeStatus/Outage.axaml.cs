using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusOutageDemo : UserControl
{
    public UptimeStatusOutageDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
        Status.Incidents.AddRange(UptimeStatusDemoData.Incidents());
        Status.Incidents.Add(UptimeStatusDemoData.OngoingOutage());
        Status.SelectedService = Status.Services[1];
    }
}
