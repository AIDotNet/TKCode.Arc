using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteFooterCustomDemo : UserControl
{
    public SiteFooterCustomDemo()
    {
        InitializeComponent();
        // Handled here, so the footer does not open the address: the log shows what the event carries.
        Footer.Navigate += (_, e) =>
        {
            e.Handled = true;
            Log.Text = $"Navigate: {e.Kind} \"{e.Link.Label}\" {e.Link.Href ?? "(no address)"}";
        };
    }
}
