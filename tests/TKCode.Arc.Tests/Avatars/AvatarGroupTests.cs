using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Avatars;

public class AvatarGroupTests
{
    private static AvaloniaList<AvatarGroupMember> Team() =>
    [
        new("Ada Lovelace", status: AvatarStatus.Online), new("Grace Hopper"), new("Alan Turing"),
        new("Katherine Johnson"), new("Linus Torvalds"), new("Margaret Hamilton")
    ];

    [Fact]
    public void Hover_Fans_From_The_Center_And_Lifts_The_Person()
    {
        AvatarGroup.ComputeOffset(0, 5, false, -1).ShouldBe(default);
        AvatarGroup.ComputeOffset(0, 5, true, -1).ShouldBe(new Vector(-8, 0));
        AvatarGroup.ComputeOffset(0, 5, true, 2).ShouldBe(new Vector(-11, 0));
        AvatarGroup.ComputeOffset(2, 5, true, 2).ShouldBe(new Vector(0, -2));
        AvatarGroup.ComputeOffset(4, 5, true, 2).ShouldBe(new Vector(11, 0));
    }

    [AvaloniaFact]
    public void Shows_The_First_People_And_Folds_The_Rest()
    {
        var team = Team();
        var group = new AvatarGroup { ItemsSource = team };
        var window = TestHelpers.Show(group);
        try
        {
            group.Slots.Count.ShouldBe(5);
            group.OverflowCount.ShouldBe(2);
            group.Slots[4].IsOverflow.ShouldBeTrue();
            AutomationProperties.GetName(group.Slots[4].Content).ShouldBe("2 more team members");
            AutomationProperties.GetName(group).ShouldBe("Team members");
            // 6px padding plus 5 slots of (36 − 6).
            group.Bounds.Width.ShouldBe(156);
            group.Bounds.Height.ShouldBe(36);
            ((Avatar)group.Slots[0].Content).Status.ShouldBe(AvatarStatus.Online);

            team.RemoveAt(1);
            Dispatcher.UIThread.RunJobs();
            group.OverflowCount.ShouldBe(1);
            ((Avatar)group.Slots[1].Content).DisplayName.ShouldBe("Alan Turing");

            team.RemoveAt(0);
            team.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            group.OverflowCount.ShouldBe(0);
            group.Slots.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_And_Plain_Items()
    {
        var group = new AvatarGroup { Size = AvatarSize.Small, MaxVisibleCount = 2, Items = { "Ada Lovelace", new Avatar { DisplayName = "Grace Hopper" }, "Alan Turing" } };
        var window = TestHelpers.Show(group);
        try
        {
            group.Slots.Count.ShouldBe(3);
            group.Bounds.Height.ShouldBe(28);
            group.Bounds.Width.ShouldBe(6 + 3 * 22);
            ((Avatar)group.Slots[1].Content).DisplayName.ShouldBe("Grace Hopper");
            ((Avatar)group.Slots[1].Content).Bounds.Width.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointing_Lifts_The_Person_Under_The_Pointer()
    {
        var group = new AvatarGroup { ItemsSource = Team(), Margin = new Thickness(20, 40, 0, 0) };
        var window = TestHelpers.Show(group);
        try
        {
            var target = group.Slots[1];
            var point = target.TranslatePoint(new Point(26, 18), window)!.Value;
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            target.ZIndex.ShouldBe(1);
            target.Emphasis.ShouldBe(1);
            target.TipOpacity.ShouldBe(1);
            group.Slots[0].ZIndex.ShouldBe(0);
            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();
            target.Emphasis.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Members_Open_Their_Slot_On_A_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var team = Team();
        var group = new AvatarGroup { ItemsSource = team, MaxVisibleCount = 8 };
        var window = TestHelpers.Show(group, ArcMotionLevel.Full);
        try
        {
            team.Add(new AvatarGroupMember("Barbara Liskov"));
            Dispatcher.UIThread.RunJobs();
            var joined = group.Slots.Last();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            joined.Presence.ShouldBeGreaterThan(0);
            joined.Presence.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromSeconds(2));
            joined.Presence.ShouldBe(1, 0.01);

            team.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            var leaving = group.Slots[0];
            leaving.IsLeaving.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            group.Slots.Count.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }
}
