using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AvatarStatusDemo : UserControl
{
    public AvatarStatusDemo()
    {
        InitializeComponent();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        Presence.Status = Presence.Status switch
        {
            AvatarStatus.Online => AvatarStatus.Offline,
            AvatarStatus.Offline => AvatarStatus.None,
            _ => AvatarStatus.Online
        };
    }
}
