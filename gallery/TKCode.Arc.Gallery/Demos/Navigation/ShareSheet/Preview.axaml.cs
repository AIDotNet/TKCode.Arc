using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareSheetPreviewDemo : UserControl
{
    public ShareSheetPreviewDemo()
    {
        InitializeComponent();
        // A pretend server: sending takes a moment and fails while "Server down" is chosen; channels take a little less.
        Sheet.SendHandler = async (_, token) =>
        {
            await Task.Delay(900, token);
            if (Outcome.SelectedValue as string == "fails")
            {
                throw new InvalidOperationException("The server refused the share.");
            }
        };
        Sheet.ChannelHandler = (_, token) => Task.Delay(550, token);
    }
}
