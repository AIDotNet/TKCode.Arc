using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DataFlowOwnSourcesDemo : UserControl
{
    public DataFlowOwnSourcesDemo()
    {
        InitializeComponent();
        Flow.Sources = [new DataFlowSource("Checkout", "orders"), new DataFlowSource("CRM", "contacts")];
    }
}
