using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusAllGreenDemo : UserControl
{
    public UptimeStatusAllGreenDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
    }
}
