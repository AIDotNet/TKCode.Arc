using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProductListingNarrowDemo : UserControl
{
    public ProductListingNarrowDemo()
    {
        InitializeComponent();
        Listing.Title = ProductListingSamples.Title;
        Listing.ProductsSource = ProductListingSamples.CreateProducts();
    }
}
