using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class IntegrationsPreviewDemo : UserControl
{
    public IntegrationsPreviewDemo()
    {
        InitializeComponent();
        // No ConnectAction: each connect waits SimulatedConnectDuration (1.4 s) and succeeds.
        IntegrationsDemoData.Fill(Directory);
    }
}
