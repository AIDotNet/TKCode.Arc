using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Shows what the card raises: the picked image and the downloads the app saves.</summary>
public partial class ImageGenerationEventsDemo : UserControl
{
    public ImageGenerationEventsDemo()
    {
        InitializeComponent();
        Card.SelectionChanged += (_, e) => Log.Text = e.Image is { } image
            ? $"Picked image {e.Index + 1} ({image.Width} × {image.Height})"
            : "Selection cleared";
        // A real app would save e.Image.Source here, for example through the window's storage provider.
        Card.DownloadRequested += (_, e) => Log.Text = $"Download requested: {e.FileName}";
    }
}
