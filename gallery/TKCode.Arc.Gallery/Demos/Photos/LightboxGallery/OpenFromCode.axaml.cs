using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class LightboxGalleryOpenFromCodeDemo : UserControl
{
    public LightboxGalleryOpenFromCodeDemo()
    {
        InitializeComponent();
        Gallery.ItemsSource = PhotosDemoData.Landscapes.Take(8).ToArray();
        Gallery.PropertyChanged += (_, e) =>
        {
            if (e.Property == LightboxGallery.OpenIndexProperty)
            {
                Readout.Text = Gallery.OpenIndex < 0 ? "The lightbox is closed." : $"Showing photo {Gallery.OpenIndex + 1} of {Gallery.Count}.";
            }
        };
    }

    private void OnOpen(object? sender, RoutedEventArgs e) => Gallery.Open(4);
}
