using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimelineScrollingFeedDemo : UserControl
{
    public TimelineScrollingFeedDemo()
    {
        InitializeComponent();
        Feed.Now = TimelineDemoData.Now();
        Feed.ItemsSource = TimelineDemoData.History(60, 140);
    }
}
