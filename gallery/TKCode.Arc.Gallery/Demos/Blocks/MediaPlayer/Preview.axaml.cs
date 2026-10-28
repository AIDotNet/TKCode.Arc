using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MediaPlayerPreviewDemo : UserControl
{
    public MediaPlayerPreviewDemo()
    {
        InitializeComponent();
        Player.Tracks.AddRange(MediaPlayerDemoData.Tracks());
    }
}
