using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvoiceStudioPreviewDemo : UserControl
{
    public InvoiceStudioPreviewDemo()
    {
        InitializeComponent();
        InvoiceStudioSamples.Apply(Studio);
        Studio.SendAsync = InvoiceStudioSamples.SendAsync;
    }
}
