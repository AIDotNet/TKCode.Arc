using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CountdownLaunchDemo : UserControl
{
    public CountdownLaunchDemo()
    {
        InitializeComponent();
        Launch.Target = DateTimeOffset.UtcNow + new TimeSpan(3, 7, 42, 18);
    }
}
