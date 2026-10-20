using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Activity;

public class ActivitySharedTests
{
    private static readonly TimeZoneInfo s_utc = TimeZoneInfo.Utc;

    [AvaloniaFact]
    public void Relative_Labels_Follow_Arc()
    {
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        ActivityTime.Relative(now.AddSeconds(-30), now).ShouldBe(new ActivityRelativeTime("Now", "just now"));
        ActivityTime.Relative(now.AddMinutes(5), now).Short.ShouldBe("Now");
        ActivityTime.Relative(now.AddMinutes(-1), now).ShouldBe(new ActivityRelativeTime("1m", "1 minute ago"));
        ActivityTime.Relative(now.AddMinutes(-59), now).ShouldBe(new ActivityRelativeTime("59m", "59 minutes ago"));
        ActivityTime.Relative(now.AddMinutes(-60), now).ShouldBe(new ActivityRelativeTime("1h", "1 hour ago"));
        ActivityTime.Relative(now.AddHours(-5).AddMinutes(-20), now).ShouldBe(new ActivityRelativeTime("5h", "5 hours ago"));
    }

    [AvaloniaFact]
    public void Dates_Format_Like_Intl_In_En_Us()
    {
        var at = new DateTimeOffset(2026, 10, 19, 21, 41, 0, TimeSpan.Zero);
        var en = CultureInfo.GetCultureInfo("en-US");
        Normalize(ActivityTime.Clock(at, s_utc, en)).ShouldBe("9:41 PM");
        ActivityTime.WeekdayMonthDay(at, s_utc, en).ShouldBe("Monday, October 19");
        Normalize(ActivityTime.Full(at, s_utc, en)).ShouldBe("Monday, October 19, 9:41 PM");
        ActivityTime.ShortDate(at, s_utc, en, withYear: false).ShouldBe("Mon, Oct 19");
        ActivityTime.ShortDate(at, s_utc, en, withYear: true).ShouldBe("Mon, Oct 19, 2026");
        ActivityTime.Weekday(at, s_utc, en).ShouldBe("Monday");

        // Day keys follow the time zone: 21:41 UTC is already the next day in Tokyo.
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        ActivityTime.DayOf(at, s_utc).ShouldBe(new DateOnly(2026, 10, 19));
        ActivityTime.DayOf(at, tokyo).ShouldBe(new DateOnly(2026, 10, 20));
        ActivityTime.DaysBefore(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20)).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Bytes_Format_Like_Arc()
    {
        ActivityTime.FormatBytes(0).ShouldBe("0 B");
        ActivityTime.FormatBytes(1023).ShouldBe("1023 B");
        ActivityTime.FormatBytes(18 * 1024 + 300).ShouldBe("18 KB");
        ActivityTime.FormatBytes((long)(2.44 * 1024 * 1024)).ShouldBe("2.4 MB");
    }

    [AvaloniaFact]
    public void Live_Region_Repeats_The_Same_Text()
    {
        var region = new ActivityLiveRegion();
        AutomationProperties.GetLiveSetting(region).ShouldBe(AutomationLiveSetting.Polite);
        region.Announce("Sam is typing");
        AutomationProperties.GetName(region).ShouldBe("Sam is typing");
        region.Announce("Sam is typing");
        AutomationProperties.GetName(region).ShouldNotBe("Sam is typing");
        region.LastAnnouncement.ShouldBe("Sam is typing");
        region.Announce(null);
        AutomationProperties.GetName(region).ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Avatar_Uses_The_Quiet_Activity_Look()
    {
        var avatar = ActivityAvatar.Create(28, "Ada Lovelace");
        var window = TestHelpers.Show(avatar);
        try
        {
            avatar.Bounds.Width.ShouldBe(28);
            avatar.Initials.ShouldBe("AL");
            avatar.FontSize.ShouldBe(11);
            avatar.BorderThickness.Left.ShouldBe(0);
            AutomationProperties.GetAccessibilityView(avatar).ShouldBe(AccessibilityView.Raw);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reaction_Bar_Keeps_Chips_By_Emoji()
    {
        var bar = new ActivityReactionBar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SetReactions([new("👍", 2, false), new("🎉", 1, true)], animate: false);
            Dispatcher.UIThread.RunJobs();
            var thumbs = bar.ChipOf("👍").ShouldNotBeNull();
            var party = bar.ChipOf("🎉").ShouldNotBeNull();
            thumbs.Bounds.Height.ShouldBe(26);
            thumbs.IsChecked.ShouldBe(false);
            party.IsChecked.ShouldBe(true);
            AutomationProperties.GetName(party).ShouldBe("🎉 1, including you");
            party.Bounds.Left.ShouldBeGreaterThan(thumbs.Bounds.Right);

            string? toggled = null;
            bar.Toggled += (_, e) => toggled = e.Emoji;
            thumbs.Focus();
            window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Enter, Avalonia.Input.RawInputModifiers.None);
            toggled.ShouldBe("👍");
            thumbs.IsChecked.ShouldBe(false); // the data owns the state

            // Same emoji, new count: the chip is kept and its count rolls.
            bar.SetReactions([new("👍", 3, true), new("🎉", 1, true)], animate: true);
            bar.ChipOf("👍").ShouldBeSameAs(thumbs);
            thumbs.Count.ShouldBe(3);
            thumbs.IsChecked.ShouldBe(true);

            // Motion off: a removed chip leaves at once.
            bar.SetReactions([new("🎉", 1, true)], animate: true);
            Dispatcher.UIThread.RunJobs();
            bar.ChipOf("👍").ShouldBeNull();
            bar.Children.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reaction_Chips_Pop_In_And_Out_Under_Full_Motion()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var bar = new ActivityReactionBar { Variant = ActivityReactionChipVariant.Chat };
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            bar.SetReactions([new("👍", 1, false)], animate: true);
            Dispatcher.UIThread.RunJobs();
            bar.SetReactions([new("❤️", 1, true), new("👍", 1, false)], animate: true);
            Dispatcher.UIThread.RunJobs();
            var heart = bar.ChipOf("❤️").ShouldNotBeNull();
            heart.Variant.ShouldBe(ActivityReactionChipVariant.Chat);
            heart.PresenceScale.ShouldBe(0.6, 0.01);
            heart.Opacity.ShouldBe(0, 0.01);
            var thumbs = bar.ChipOf("👍")!;
            // The chip that moved right glides from where it was.
            ((Avalonia.Media.MatrixTransform)thumbs.RenderTransform!).Matrix.M31.ShouldBeLessThan(0);
            frames.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            heart.PresenceScale.ShouldBe(1, 0.01);
            heart.Opacity.ShouldBe(1, 0.01);
            ((Avalonia.Media.MatrixTransform)thumbs.RenderTransform!).Matrix.M31.ShouldBe(0, 0.5);

            bar.SetReactions([new("👍", 1, false)], animate: true);
            heart.IsLeaving.ShouldBeTrue();
            bar.Children.Count.ShouldBe(2);
            frames.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            bar.Children.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Without_Scaling()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var bar = new ActivityReactionBar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Reduced);
        try
        {
            bar.SetReactions([new("👍", 1, false)], animate: true);
            Dispatcher.UIThread.RunJobs();
            var chip = bar.ChipOf("👍")!;
            chip.PresenceScale.ShouldBe(1);
            chip.Opacity.ShouldBe(0, 0.01);
            frames.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            chip.Opacity.ShouldBe(1, 0.01);
            chip.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().First().Text.ShouldBe("👍");
        }
        finally
        {
            window.Close();
        }
    }

    private static string Normalize(string text) => text.Replace(' ', ' ').Replace(' ', ' ');
}
