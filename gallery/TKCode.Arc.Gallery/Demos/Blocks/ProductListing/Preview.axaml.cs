using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProductListingPreviewDemo : UserControl
{
    public ProductListingPreviewDemo()
    {
        InitializeComponent();
        Listing.Title = ProductListingSamples.Title;
        Listing.ProductsSource = ProductListingSamples.CreateProducts();
    }
}
