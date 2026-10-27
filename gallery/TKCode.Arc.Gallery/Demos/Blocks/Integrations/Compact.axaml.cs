using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class IntegrationsCompactDemo : UserControl
{
    public IntegrationsCompactDemo()
    {
        InitializeComponent();
        IntegrationsDemoData.Fill(Directory);
    }
}
