using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CatchUpOpenedDemo : UserControl
{
    public CatchUpOpenedDemo()
    {
        InitializeComponent();
        Recap.ItemInvoked += (_, e) => Log.Text = $"Open {e.Item.Link}";
    }
}
