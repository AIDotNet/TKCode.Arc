using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MediaPlayerFlatDemo : UserControl
{
    public MediaPlayerFlatDemo()
    {
        InitializeComponent();
        Player.Tracks.AddRange(MediaPlayerDemoData.Tracks());
    }
}
