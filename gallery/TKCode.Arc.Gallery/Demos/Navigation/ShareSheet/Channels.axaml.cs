using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ShareSheetChannelsDemo : UserControl
{
    public ShareSheetChannelsDemo()
    {
        InitializeComponent();
        Sheet.ChannelHandler = async (channel, token) =>
        {
            await Task.Delay(700, token);
            if (channel.Id == "wiki")
            {
                throw new InvalidOperationException("The wiki is read-only today.");
            }
        };
    }

    private void OnChannelShared(object? sender, ShareSheetChannelEventArgs e) => Log.Text = $"Posted to {e.Channel.Label}";
}
