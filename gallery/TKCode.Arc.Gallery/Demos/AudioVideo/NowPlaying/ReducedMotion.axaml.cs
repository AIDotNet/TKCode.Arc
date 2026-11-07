using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NowPlayingReducedMotionDemo : UserControl
{
    public NowPlayingReducedMotionDemo()
    {
        InitializeComponent();
        NowPlayingSamples.Apply(Player);
        Player.IsPlaying = true;
    }
}
