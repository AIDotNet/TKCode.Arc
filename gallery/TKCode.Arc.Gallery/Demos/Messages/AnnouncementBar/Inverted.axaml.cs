using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AnnouncementBarInvertedDemo : UserControl
{
    public AnnouncementBarInvertedDemo()
    {
        InitializeComponent();
        // The maintenance window starts five hours and twelve minutes from now.
        Bar.Messages[0].Countdown!.To = DateTimeOffset.Now.AddHours(5).AddMinutes(12);
    }
}
