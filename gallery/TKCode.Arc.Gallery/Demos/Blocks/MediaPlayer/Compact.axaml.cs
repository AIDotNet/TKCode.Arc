using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MediaPlayerCompactDemo : UserControl
{
    public MediaPlayerCompactDemo()
    {
        InitializeComponent();
        Player.Tracks.AddRange(MediaPlayerDemoData.Tracks());
        Player.SelectedIndex = 2;
    }
}
