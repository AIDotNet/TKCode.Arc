using System.Globalization;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/activity: one row, the workspace's audit log (<see cref="DemoAuditLog"/>, built for this page) beside
/// the team's updates (a Timeline of the last five days, scrolling inside 560px). Below 1180px they stack, the log first.
/// </summary>
public sealed class DemoActivityPage : DemoPageView
{
    public DemoActivityPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.MainSide, "log", new DemoAuditLog(), BuildTeamUpdates());
    }

    private static Avalonia.Controls.Control BuildTeamUpdates()
    {
        var timeline = new Timeline
        {
            Label = "Latest updates from the team",
            Now = new DateTimeOffset(DemoData.Now),
            MaxHeight = 560,
            HeadingLevel = 3,
            TimeZone = TimeZoneInfo.Utc,
            Culture = CultureInfo.GetCultureInfo("en-US"),
            ItemsSource = DemoActivityData.TeamUpdates()
        };
        return DemoUi.Panel("Team updates", "What the studio shipped and shared today", timeline);
    }
}
