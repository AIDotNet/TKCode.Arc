using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NowPlayingFullPlayerDemo : UserControl
{
    public NowPlayingFullPlayerDemo()
    {
        InitializeComponent();
        NowPlayingSamples.Apply(Player);
        Player.Index = 2;
    }
}
