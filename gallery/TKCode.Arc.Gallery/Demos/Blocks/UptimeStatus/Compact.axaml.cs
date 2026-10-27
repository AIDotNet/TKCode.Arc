using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusCompactDemo : UserControl
{
    public UptimeStatusCompactDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
        Status.Incidents.AddRange(UptimeStatusDemoData.Incidents());
        Status.ActionContent = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = "Subscribe" };
    }
}
