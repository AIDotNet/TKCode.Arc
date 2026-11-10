using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GlassMenuPopupDemo : UserControl
{
    public GlassMenuPopupDemo()
    {
        InitializeComponent();
        if (GlassMenu.GetMenu(Trigger) is { } menu)
        {
            menu.ItemInvoked += (_, e) => Log.Text = $"Ran \"{e.Container.Content}\"";
        }
    }
}
