using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Content;

/// <summary>ActivityFeed: the sample, kind and person filters, live and paused arrival, and the live dot's loop.</summary>
public class ActivityFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static (ActivityFeed Feed, Avalonia.Controls.Window Window) Show(Action<ActivityFeed>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var feed = new ActivityFeed { Clock = () => Now };
        configure?.Invoke(feed);
        var window = TestHelpers.Show(feed, level);
        window.Width = 720;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (feed, window);
    }

    private static ActivityFeedItem Item(string id, string? actor, ActivityKind kind) => new(id, Now.AddMinutes(-5), actor, "did " + id, kind);

    [AvaloniaFact]
    public void The_Sample_Shows_Every_Item_With_Kind_Icons()
    {
        var (feed, window) = Show();
        try
        {
            feed.Items.Count.ShouldBe(9);
            feed.ShownEvents.Count.ShouldBe(9);
            feed.ShownEvents.First(e => e.Id == "a2").Icon.ShouldBe(SupportKit.Rocket);
            feed.ShownEvents.First(e => e.Id == "a6").Tone.ShouldBe(TimelineTone.Danger);
            feed.ActorPicker.ItemsSource!.Cast<string>().First().ShouldBe("Everyone");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kind_And_Person_Filters_Combine()
    {
        var (feed, window) = Show(f => f.Items.AddRange([Item("1", "Ana", ActivityKind.Comment), Item("2", "Ben", ActivityKind.Comment), Item("3", "Ana", ActivityKind.Change)]));
        try
        {
            feed.KindFilter = ActivityKind.Comment;
            feed.ShownEvents.Select(e => e.Id).ShouldBe(["1", "2"], ignoreOrder: true);
            feed.ChipFor(ActivityKind.Comment).IsChecked.ShouldBe(true);
            feed.ActorFilter = "Ana";
            feed.ShownEvents.Select(e => e.Id).ShouldBe(["1"]);
            feed.ActorPicker.SelectedItem.ShouldBe("Ana");
            feed.KindFilter = null;
            feed.ShownEvents.Select(e => e.Id).ShouldBe(["1", "3"], ignoreOrder: true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Items_Arrive_At_Once_And_Paused_Ones_Wait_Behind_The_Pill()
    {
        var (feed, window) = Show(f => f.UsesSampleData = false);
        try
        {
            feed.Push(Item("1", "Ana", ActivityKind.Comment));
            feed.ShownEvents.Count.ShouldBe(1);
            feed.IsPillShown.ShouldBeFalse();

            feed.IsLive = false;
            feed.Push(Item("2", "Ben", ActivityKind.Change));
            feed.Push(Item("3", "Ben", ActivityKind.Change));
            feed.PendingCount.ShouldBe(2);
            feed.ShownEvents.Count.ShouldBe(1);
            feed.IsPillShown.ShouldBeTrue();
            feed.LastAnnouncement.ShouldBe("2 new updates");

            feed.ShowPending();
            feed.PendingCount.ShouldBe(0);
            feed.ShownEvents.Count.ShouldBe(3);
            feed.IsPillShown.ShouldBeFalse();

            feed.Push(Item("4", "Ana", ActivityKind.Deploy));
            feed.IsLive = true;
            feed.PendingCount.ShouldBe(0, "going live lets the waiting items in");
            feed.ShownEvents.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pushed_Item_Outside_The_Filter_Is_Kept_But_Not_Shown()
    {
        var (feed, window) = Show(f =>
        {
            f.UsesSampleData = false;
            f.KindFilter = ActivityKind.Deploy;
        });
        try
        {
            feed.Push(Item("1", "Ana", ActivityKind.Comment));
            feed.Items.Count.ShouldBe(1);
            feed.ShownEvents.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Live_Dot_Pulses_Only_With_Full_Motion_While_Live()
    {
        var (feed, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            feed.LiveDot.IsPulseActive.ShouldBeTrue();
            feed.IsLive = false;
            feed.LiveDot.IsPulseActive.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }

        var (reduced, other) = Show(level: ArcMotionLevel.Reduced);
        try
        {
            reduced.LiveDot.IsPulsing.ShouldBeFalse();
        }
        finally
        {
            other.Close();
        }
    }
}
