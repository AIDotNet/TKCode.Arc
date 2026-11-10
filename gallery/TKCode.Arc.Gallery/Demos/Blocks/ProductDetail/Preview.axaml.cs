using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProductDetailPreviewDemo : UserControl
{
    public ProductDetailPreviewDemo()
    {
        InitializeComponent();
        ProductDetailSamples.Apply(Detail);
    }
}
