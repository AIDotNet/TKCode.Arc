using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimelineThousandUpdatesDemo : UserControl
{
    public TimelineThousandUpdatesDemo()
    {
        InitializeComponent();
        Feed.Now = TimelineDemoData.Now();
        Feed.ItemsSource = TimelineDemoData.History(1000, 23);
    }
}
