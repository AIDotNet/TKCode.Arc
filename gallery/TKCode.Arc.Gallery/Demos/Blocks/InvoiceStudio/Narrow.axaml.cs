using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvoiceStudioNarrowDemo : UserControl
{
    public InvoiceStudioNarrowDemo()
    {
        InitializeComponent();
        InvoiceStudioSamples.Apply(Studio);
    }
}
