using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A controlled job with an estimated duration: the image arrives when the person says so.</summary>
public partial class ImageRevealEstimatedDemo : UserControl
{
    public ImageRevealEstimatedDemo()
    {
        InitializeComponent();
    }

    private void OnArrived(object? sender, RoutedEventArgs e)
    {
        Reveal.Source = ImageRevealDemoPicture.Get();
        Reveal.Status = ImageRevealStatus.Done;
    }

    private void OnStartOver(object? sender, RoutedEventArgs e)
    {
        Reveal.Status = ImageRevealStatus.Queued;
        Reveal.Source = null;
        Reveal.Status = ImageRevealStatus.Generating;
    }
}
