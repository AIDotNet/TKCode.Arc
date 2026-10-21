using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AnnouncementBarControlsAndCountdownDemo : UserControl
{
    public AnnouncementBarControlsAndCountdownDemo()
    {
        InitializeComponent();
        // The countdown ends a day and three hours from now, whenever the demo is opened.
        Bar.Messages[0].Countdown!.To = DateTimeOffset.Now.AddDays(1).AddHours(3);
    }
}
