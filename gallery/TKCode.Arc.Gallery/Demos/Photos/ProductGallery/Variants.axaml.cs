using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProductGalleryVariantsDemo : UserControl
{
    public ProductGalleryVariantsDemo()
    {
        InitializeComponent();
        Gallery.ItemsSource = PhotosDemoData.Mug(0);
        Gallery.PropertyChanged += (_, e) =>
        {
            if (e.Property == ProductGallery.SelectedImageProperty)
            {
                Readout.Text = Gallery.SelectedImage?.Description ?? string.Empty;
            }
        };
        Readout.Text = Gallery.SelectedImage?.Description ?? string.Empty;
    }

    private void OnGlaze(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag } && int.TryParse(tag, out var glaze))
        {
            var view = Gallery.SelectedIndex;
            Gallery.ItemsSource = PhotosDemoData.Mug(glaze);
            Gallery.SelectedIndex = view;
        }
    }
}
