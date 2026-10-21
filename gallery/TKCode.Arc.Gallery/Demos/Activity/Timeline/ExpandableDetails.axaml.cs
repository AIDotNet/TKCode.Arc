using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TimelineExpandableDetailsDemo : UserControl
{
    public TimelineExpandableDetailsDemo()
    {
        InitializeComponent();
        var now = TimelineDemoData.Now();
        Feed.Now = now;
        Feed.ItemsSource = new List<TimelineEvent>
        {
            new("build-418", now.AddMinutes(-12), "deployed web to production", "Ana Ruiz")
            {
                Meta = "Build #418",
                Avatar = "https://i.pravatar.cc/56?img=47",
                Detail = "All 312 checks passed in 4m 12s. Rolled out to 100% of traffic in three waves, with no error-rate change."
            },
            new("pr-212", now.AddMinutes(-58), "merged Checkout redesign into main", "Theo Walsh")
            {
                Meta = "PR #212 · 14 files",
                Avatar = "https://i.pravatar.cc/56?img=12",
                Detail = "Squashed 9 commits. Reviewed by Mina Park and Lee Chen."
            },
            new("note", now.AddHours(-2), "renamed the project to Atlas", "Lee Chen") { Avatar = "https://i.pravatar.cc/56?img=33" },
            new("migrate", now.AddHours(-30), "Database migration completed")
            {
                Meta = "0042_add_invoices",
                Icon = ArcIcons.Check,
                Tone = TimelineTone.Success,
                Detail = "Applied in 38s on the primary; replicas caught up within a minute."
            }
        };
    }
}
