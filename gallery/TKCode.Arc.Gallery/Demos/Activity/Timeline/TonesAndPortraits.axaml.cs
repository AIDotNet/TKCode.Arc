using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimelineTonesAndPortraitsDemo : UserControl
{
    public TimelineTonesAndPortraitsDemo()
    {
        InitializeComponent();
        var now = TimelineDemoData.Now();
        Feed.Now = now;
        Feed.ItemsSource = new List<TimelineEvent>
        {
            new("t1", now.AddMinutes(-5), "Deploy succeeded") { Meta = "web · production", Icon = ArcIcons.Check, Tone = TimelineTone.Success },
            new("t2", now.AddMinutes(-18), "Deploy failed: health check timed out") { Meta = "api · staging", Icon = ArcIcons.X, Tone = TimelineTone.Danger },
            new("t3", now.AddMinutes(-26), "Rollback started") { Meta = "api · staging", Icon = ArcIcons.RotateCcw },
            new("t4", now.AddMinutes(-40), "approved the release", "Ivy Brooks") { Meta = "v2.8.0", Avatar = "https://i.pravatar.cc/56?img=45" },
            new("t5", now.AddMinutes(-52), "tagged v2.8.0", "Kai Moreno") { Avatar = "https://i.pravatar.cc/56?img=15" }
        };
    }
}
