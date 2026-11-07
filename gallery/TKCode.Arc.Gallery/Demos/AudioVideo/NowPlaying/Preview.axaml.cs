using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NowPlayingPreviewDemo : UserControl
{
    public NowPlayingPreviewDemo()
    {
        InitializeComponent();
        NowPlayingSamples.Apply(Player);
    }
}
