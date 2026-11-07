using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class NowPlayingNarrowDemo : UserControl
{
    public NowPlayingNarrowDemo()
    {
        InitializeComponent();
        NowPlayingSamples.Apply(Mini);
        NowPlayingSamples.Apply(Full);
        Mini.Index = 1;
        Full.Index = 1;
    }
}
