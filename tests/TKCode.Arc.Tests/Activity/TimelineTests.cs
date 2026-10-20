using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Activity;

public class TimelineTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private static List<TimelineEvent> Sample() =>
    [
        new("deploy", s_now.AddMinutes(-9), "deployed web to production", "Ana Ruiz") { Meta = "Build #418", Detail = "All checks passed in 4m 12s.", Tone = TimelineTone.Success, Icon = ArcIcons.Check },
        new("merge", s_now.AddHours(-2).AddMinutes(-5), "merged Checkout redesign into main", "Theo Walsh") { Meta = "PR #212" },
        new("fail", s_now.AddHours(-26), "Build failed on main", null) { Tone = TimelineTone.Danger, Icon = ArcIcons.X, Detail = "Step test:e2e exited with code 1." },
        new("review", s_now.AddHours(-27), "requested changes on Pricing page", "Mina Park") { Meta = "PR #207" },
        new("old", s_now.AddDays(-4), "created the project", "Lee Chen")
    ];

    private static List<TimelineEvent> Many(int count)
    {
        var list = new List<TimelineEvent>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new TimelineEvent($"e{i}", s_now.AddMinutes(-37 * i), $"shipped change {i}", "Ana Ruiz")
            {
                Meta = $"Build #{1000 - i}",
                Detail = i % 3 == 0 ? $"Detail {i}" : null
            });
        }
        return list;
    }

    private static Window Show(Timeline timeline, ArcMotionLevel level = ArcMotionLevel.Off, double width = 560, double height = 700)
    {
        var window = TestHelpers.Show(timeline, level);
        window.Width = width;
        window.Height = height;
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<TimelineRow> Rows(Timeline timeline) => timeline.RealizedContainers.OfType<TimelineRow>().Where(r => r.IsVisible);

    private static TimelineRow Row(Timeline timeline, string id) => Rows(timeline).First(r => r.Model!.Item.Id == id);

    private static TimelineDayHeader Header(Timeline timeline, string label) =>
        timeline.RealizedContainers.OfType<TimelineDayHeader>().First(h => h.IsVisible && h.LabelText == label);

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var timeline = new Timeline();
        timeline.Label.ShouldBe("Activity");
        timeline.TimeZone.ShouldBe(TimeZoneInfo.Utc);
        timeline.Culture.ShouldBeNull();
        timeline.IsScrollToNewEnabled.ShouldBeTrue();
        timeline.HeadingLevel.ShouldBe(3);
        timeline.ExpandedIds.ShouldBeNull();
        timeline.HeadingLevel = 9;
        timeline.HeadingLevel.ShouldBe(6);
        timeline.HeadingLevel = 1;
        timeline.HeadingLevel.ShouldBe(2);
        AutomationProperties.GetLandmarkType(timeline).ShouldBe(AutomationLandmarkType.Region);
    }

    [AvaloniaFact]
    public void Groups_Newest_First_By_Day_With_Labels()
    {
        var events = Sample();
        events.Reverse(); // any order
        var timeline = new Timeline { ItemsSource = events, Now = s_now, Label = "Project activity" };
        var window = Show(timeline);
        try
        {
            var keys = timeline.Rows.Select(r => r.Key).ToArray();
            keys.ShouldBe(["day:2026-10-20", "ev:deploy", "ev:merge", "day:2026-10-19", "ev:fail", "ev:review", "day:2026-10-16", "ev:old"]);
            Header(timeline, "Today").CountValue.ShouldBe(2);
            Header(timeline, "Yesterday").CountValue.ShouldBe(2);
            var older = Header(timeline, "Friday, October 16");
            older.CountValue.ShouldBe(1);
            AutomationProperties.GetName(older).ShouldBe("Friday, October 16, 1 update");
            AutomationProperties.GetHeadingLevel(older).ShouldBe(3);
            ControlAutomationPeer.CreatePeerForElement(older).GetAutomationControlType().ShouldBe(AutomationControlType.Header);
            ControlAutomationPeer.CreatePeerForElement(timeline).GetName().ShouldBe("Project activity");

            // Today within 12 hours: relative; older: clock time.
            Row(timeline, "deploy").TimeText.ShouldBe("9m");
            Row(timeline, "deploy").TimeFull.ShouldStartWith("9 minutes ago, Tuesday, October 20, ");
            Row(timeline, "merge").TimeText.ShouldBe("2h");
            Row(timeline, "fail").TimeText.Replace(' ', ' ').ShouldBe("1:00 PM");

            // Last row of each day has no line.
            timeline.Rows.OfType<TimelineEventModel>().Where(m => m.IsLastOfDay).Select(m => m.Item.Id).ShouldBe(["merge", "review", "old"]);
            Row(timeline, "merge").Classes.ShouldContain(":last");
            Row(timeline, "deploy").Classes.ShouldNotContain(":last");
            Row(timeline, "deploy").Classes.ShouldContain(":success");
            Row(timeline, "fail").Classes.ShouldContain(":danger");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Geometry_Follows_Arc()
    {
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
        var window = Show(timeline);
        try
        {
            var row = Row(timeline, "deploy");
            var marker = TestHelpers.Descendant<Panel>(row, TimelineRow.MarkerPart);
            marker.Bounds.Width.ShouldBe(28);
            marker.TranslatePoint(default, row)!.Value.Y.ShouldBe(8);
            var trigger = row.Trigger!;
            trigger.TranslatePoint(default, row)!.Value.X.ShouldBe(36);
            trigger.Padding.ShouldBe(new Thickness(12));
            var title = TestHelpers.Descendant<TextBlock>(row, TimelineRow.TitlePart);
            var meta = TestHelpers.Descendant<TextBlock>(row, TimelineRow.MetaPart);
            (title.Bounds.Height % 20).ShouldBe(0, 0.6); // 20px title lines
            meta.Bounds.Top.ShouldBe(title.Bounds.Bottom + 2, 0.6);
            trigger.Bounds.Height.ShouldBe(12 + title.Bounds.Height + 2 + meta.Bounds.Height + 12, 0.6);
            var segment = TestHelpers.Descendant<Border>(row, TimelineRow.SegmentPart);
            segment.Bounds.Left.ShouldBe(13.5, 0.5);
            segment.Bounds.Top.ShouldBe(40);
            segment.Bounds.Bottom.ShouldBe(row.Bounds.Height + 4, 0.5);
            var header = Header(timeline, "Today");
            header.Bounds.Height.ShouldBe(8 + 19.6 + 8, 0.6);
            // The day gap follows the last row of a day.
            Row(timeline, "merge").Padding.Bottom.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Feeds_Tighten_Paddings()
    {
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
        var window = Show(timeline, width: 360);
        try
        {
            timeline.Classes.ShouldContain(":narrow");
            var row = Row(timeline, "deploy");
            row.Classes.ShouldContain(":narrow");
            row.Trigger!.Padding.ShouldBe(new Thickness(8, 12));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Expand_With_Click_Keyboard_And_Automation()
    {
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now, ExpandedIds = ["fail"] };
        var window = Show(timeline);
        try
        {
            Row(timeline, "fail").IsExpanded.ShouldBeTrue();
            Row(timeline, "fail").Detail!.Bounds.Height.ShouldBeGreaterThan(10);
            var deploy = Row(timeline, "deploy");
            deploy.IsExpanded.ShouldBeFalse();
            deploy.Detail!.Bounds.Height.ShouldBe(0);
            deploy.Trigger!.Focusable.ShouldBeTrue();
            Row(timeline, "merge").Trigger!.Focusable.ShouldBeFalse();
            ControlAutomationPeer.CreatePeerForElement(Row(timeline, "merge").Trigger!).GetAutomationControlType().ShouldBe(AutomationControlType.Text);

            var changes = new List<(string, bool)>();
            timeline.ExpandedChanged += (_, e) => changes.Add((e.Item.Id, e.IsExpanded));
            deploy.Trigger.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            deploy.IsExpanded.ShouldBeTrue();
            deploy.ChevronAngle.ShouldBe(180);
            timeline.ExpandedIds.ShouldBe(["fail", "deploy"], ignoreOrder: true);
            changes.ShouldBe([("deploy", true)]);

            var peer = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(deploy.Trigger);
            peer.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            peer.Collapse();
            Settle();
            deploy.IsExpanded.ShouldBeFalse();
            AutomationProperties.GetName(deploy.Trigger).ShouldStartWith("Ana Ruiz deployed web to production, Build #418, 9 minutes ago");

            // ↓ skips rows without detail; End / Home jump; Up from the first stays.
            deploy.Trigger.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Settle();
            Row(timeline, "fail").Trigger!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Settle();
            deploy.Trigger.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Settle();
            Row(timeline, "fail").Trigger!.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Settle();
            deploy.Trigger.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            deploy.Trigger.IsFocused.ShouldBeTrue();

            // Programmatic control.
            timeline.Toggle("fail");
            Settle();
            Row(timeline, "fail").IsExpanded.ShouldBeFalse();
            timeline.Expand("merge"); // no detail: stays closed
            timeline.IsExpanded("merge").ShouldBeFalse();
            timeline.ExpandedIds = ["deploy"];
            Settle();
            deploy.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Thousand_Events_Realize_A_Bounded_Number_Of_Rows()
    {
        var events = Many(1200);
        var timeline = new Timeline { ItemsSource = events, Now = s_now, MaxHeight = 480 };
        var window = Show(timeline);
        try
        {
            timeline.Rows.Count.ShouldBeGreaterThan(1200);
            var realized = timeline.RealizedContainers.Count();
            realized.ShouldBeLessThan(40);
            timeline.Bounds.Height.ShouldBe(480);
            var scroller = timeline.Scroller!;
            scroller.Extent.Height.ShouldBeGreaterThan(20000);

            scroller.Offset = new Vector(0, scroller.Extent.Height / 2);
            Settle();
            timeline.RealizedContainers.Count().ShouldBeLessThan(40);
            scroller.Offset = new Vector(0, scroller.Extent.Height);
            Settle();
            timeline.RealizedContainers.Count().ShouldBeLessThan(40);
            Rows(timeline).ShouldContain(r => r.Model!.Item.Id == "e1199");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Growing_With_The_Page_Still_Virtualizes()
    {
        var timeline = new Timeline { ItemsSource = Many(1000), Now = s_now };
        var page = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 200 }, timeline } } };
        var window = TestHelpers.Show(page);
        window.Width = 560;
        window.Height = 700;
        Settle();
        try
        {
            timeline.Bounds.Height.ShouldBeGreaterThan(20000);
            timeline.Scroller!.Offset.Y.ShouldBe(0);
            timeline.RealizedContainers.Count().ShouldBeLessThan(50);
            page.Offset = new Vector(0, 15000);
            Settle();
            timeline.RealizedContainers.Count().ShouldBeLessThan(50);
            // The pinned label sits at the top of the page's viewport.
            var sticky = timeline.StickyHeader!;
            sticky.Opacity.ShouldBe(1);
            var top = sticky.TranslatePoint(default, page)!.Value.Y;
            top.ShouldBeInRange(-0.5 - 40, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Now_Refreshes_Time_Labels_Without_Rebuilding_Rows()
    {
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
        var window = Show(timeline);
        try
        {
            var before = timeline.RealizedContainers.ToList();
            var deploy = Row(timeline, "deploy");
            deploy.TimeText.ShouldBe("9m");
            timeline.Now = s_now.AddMinutes(1);
            Settle();
            timeline.RealizedContainers.ToList().ShouldBe(before);
            Row(timeline, "deploy").ShouldBeSameAs(deploy);
            deploy.TimeText.ShouldBe("10m");

            // Past midnight, Today becomes Yesterday in place.
            var today = Header(timeline, "Today");
            timeline.Now = new DateTimeOffset(2026, 10, 21, 0, 30, 0, TimeSpan.Zero);
            Settle();
            today.LabelText.ShouldBe("Yesterday");
            deploy.TimeText.Replace(' ', ' ').ShouldBe("2:51 PM");
            timeline.RealizedContainers.ToList().ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Events_Insert_Without_Rebuilding_And_Are_Announced()
    {
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Many(300));
        var timeline = new Timeline { ItemsSource = events, Now = s_now, MaxHeight = 500 };
        var window = Show(timeline);
        try
        {
            var before = Rows(timeline).ToDictionary(r => r.Model!.Item.Id);
            events.Add(new TimelineEvent("new", s_now.AddSeconds(10), "opened issue #9", "Kai Moreno"));
            Settle();
            timeline.Rows[1].Key.ShouldBe("ev:new");
            foreach (var (id, row) in before)
            {
                if (Rows(timeline).FirstOrDefault(r => r.Model!.Item.Id == id) is { } now)
                {
                    now.ShouldBeSameAs(row);
                }
            }
            timeline.Announcement.ShouldBe("New update: Kai Moreno opened issue #9");
            timeline.LiveRegion!.LastAnnouncement.ShouldBe("New update: Kai Moreno opened issue #9");
            Header(timeline, "Today").CountValue.ShouldBe(events.Count(e => e.At.UtcDateTime.Date == s_now.UtcDateTime.Date));

            // Scrolled down, a new newest update brings the feed back to the top (instantly with motion off).
            timeline.Scroller!.Offset = new Vector(0, 3000);
            Settle();
            events.Add(new TimelineEvent("newer", s_now.AddSeconds(20), "merged #9", "Kai Moreno"));
            Settle();
            timeline.Scroller.Offset.Y.ShouldBe(0);

            // Removing an event drops its row (motion off: at once).
            events.Remove(events.First(e => e.Id == "new"));
            Settle();
            timeline.Rows.ShouldNotContain(r => r.Key == "ev:new");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Current_Day_Label_Stays_Pinned_And_Is_Pushed_By_The_Next()
    {
        var timeline = new Timeline { ItemsSource = Many(200), Now = s_now, MaxHeight = 400 };
        var window = Show(timeline);
        try
        {
            var sticky = timeline.StickyHeader!;
            sticky.Opacity.ShouldBe(0); // the first label is in place
            var scroller = timeline.Scroller!;
            // Scroll into the second day (Yesterday).
            var yesterdayIndex = timeline.Rows.ToList().FindIndex(r => r.Key == "day:2026-10-19");
            yesterdayIndex.ShouldBeGreaterThan(0);
            timeline.List!.ScrollIntoView(yesterdayIndex);
            Settle();
            var header = Header(timeline, "Yesterday");
            scroller.Offset = new Vector(0, scroller.Offset.Y + header.TranslatePoint(default, timeline)!.Value.Y + 120);
            Settle();
            sticky.Opacity.ShouldBe(1);
            sticky.LabelText.ShouldBe("Yesterday");
            sticky.TranslatePoint(default, timeline)!.Value.Y.ShouldBe(0, 0.5);
            AutomationProperties.GetAccessibilityView(sticky).ShouldBe(AccessibilityView.Raw);

            // Just above the next day's label, the pinned label is pushed up.
            var dayAfter = timeline.Rows.ToList().FindIndex(yesterdayIndex + 1, r => r is TimelineDayModel);
            timeline.List.ScrollIntoView(dayAfter);
            Settle();
            var next = timeline.List.ContainerFromIndex(dayAfter)!;
            var nextTop = next.TranslatePoint(default, timeline)!.Value.Y;
            scroller.Offset = new Vector(0, scroller.Offset.Y + nextTop - 20);
            Settle();
            sticky.LabelText.ShouldBe("Yesterday");
            var stickyBottom = sticky.TranslatePoint(new Point(0, sticky.Bounds.Height), timeline)!.Value.Y;
            stickyBottom.ShouldBe(20 - 12, 0.6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Reveal_When_Added_To_A_Page_That_Has_Already_Settled()
    {
        // A gallery or app page: a scrolling host laid out before the timeline arrives, and no resize afterwards.
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var host = new StackPanel();
        var window = TestHelpers.Show(new ScrollViewer { Content = host }, ArcMotionLevel.Full);
        try
        {
            Settle();
            var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
            host.Children.Add(timeline);
            Settle();
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            var deploy = Row(timeline, "deploy");
            deploy.Model!.Revealed.ShouldBeTrue();
            deploy.MarkerProgress.ShouldBe(1, 0.01);
            deploy.LineProgress.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Reveals_Markers_And_Lines_Then_Rests()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Full);
        try
        {
            var deploy = Row(timeline, "deploy");
            deploy.MarkerScale.ShouldBeLessThan(1);
            deploy.LineProgress.ShouldBeLessThan(1);
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            deploy.MarkerScale.ShouldBe(1, 0.01);
            deploy.MarkerProgress.ShouldBe(1, 0.01);
            deploy.LineProgress.ShouldBe(1, 0.01);
            deploy.Model!.Revealed.ShouldBeTrue();

            // Expanding springs the detail open.
            timeline.Expand("deploy");
            Settle();
            frames.Advance(TimeSpan.FromMilliseconds(60));
            Settle();
            var mid = deploy.Detail!.Bounds.Height;
            deploy.ChevronAngle.ShouldBeGreaterThan(0);
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            deploy.Detail.Bounds.Height.ShouldBeGreaterThan(mid);
            deploy.ChevronAngle.ShouldBe(180, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Grows_A_Fresh_Row_In()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Sample());
        var timeline = new Timeline { ItemsSource = events, Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Full);
        try
        {
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            var mergeTop = Row(timeline, "merge").Bounds.Top;
            events.Add(new TimelineEvent("fresh", s_now.AddMinutes(-2), "commented on PR #212", "Ivy Brooks"));
            Settle();
            var fresh = Row(timeline, "fresh");
            fresh.Model!.Fresh.ShouldBeTrue();
            fresh.ContentOffset.ShouldBeLessThan(0);
            frames.Advance(TimeSpan.FromMilliseconds(80));
            Settle();
            fresh.Bounds.Height.ShouldBeLessThan(fresh.Trigger!.Bounds.Height);
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            fresh.ContentOffset.ShouldBe(0, 0.1);
            Row(timeline, "merge").Bounds.Top.ShouldBe(mergeTop + fresh.Bounds.Height, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Without_Growing()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Sample());
        var timeline = new Timeline { ItemsSource = events, Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Reduced);
        try
        {
            var deploy = Row(timeline, "deploy");
            deploy.MarkerScale.ShouldBe(1);
            deploy.LineProgress.ShouldBe(1);
            frames.Advance(TimeSpan.FromSeconds(1));
            Settle();
            deploy.MarkerProgress.ShouldBe(1, 0.01);

            events.Add(new TimelineEvent("fresh", s_now.AddMinutes(-2), "commented", "Ivy Brooks"));
            Settle();
            var fresh = Row(timeline, "fresh");
            fresh.ContentOffset.ShouldBe(0);
            fresh.Grow!.Progress.ShouldBe(1);

            timeline.Expand("deploy");
            Settle();
            deploy.Detail!.Progress.ShouldBe(1);
            deploy.ChevronAngle.ShouldBe(180);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Feather_Shows_While_More_Waits_Below()
    {
        var timeline = new Timeline { ItemsSource = Many(60), Now = s_now, MaxHeight = 300 };
        var window = Show(timeline);
        try
        {
            timeline.Classes.ShouldContain(":scrolls");
            timeline.FeatherValue.ShouldBe(36);
            timeline.Scroller!.OpacityMask.ShouldNotBeNull();
            timeline.Scroller.Offset = new Vector(0, timeline.Scroller.Extent.Height);
            Settle();
            timeline.FeatherValue.ShouldBe(0);
            timeline.Scroller.OpacityMask.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Revealed_Together_Are_Staggered()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var timeline = new Timeline { ItemsSource = Many(8), Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Full);
        try
        {
            frames.Advance(TimeSpan.FromMilliseconds(120));
            Settle();
            var first = Row(timeline, "e0");
            var fourth = Row(timeline, "e3");
            first.MarkerProgress.ShouldBeGreaterThan(fourth.MarkerProgress);
            first.LineProgress.ShouldBeGreaterThanOrEqualTo(fourth.LineProgress);
            frames.Advance(TimeSpan.FromSeconds(2));
            Settle();
            Rows(timeline).ShouldAllBe(r => Math.Abs(r.MarkerScale - 1) < 0.01 && Math.Abs(r.LineProgress - 1) < 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Off_Shows_The_End_State_At_Once()
    {
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Sample());
        var timeline = new Timeline { ItemsSource = events, Now = s_now };
        var window = Show(timeline);
        try
        {
            Rows(timeline).ShouldAllBe(r => r.MarkerScale == 1 && r.MarkerProgress == 1 && r.LineProgress == 1);
            events.Add(new TimelineEvent("fresh", s_now.AddMinutes(-1), "commented", "Ivy Brooks"));
            Settle();
            var fresh = Row(timeline, "fresh");
            fresh.Grow!.Progress.ShouldBe(1);
            fresh.ContentOffset.ShouldBe(0);
            fresh.MarkerScale.ShouldBe(1);
            timeline.Expand("deploy");
            Row(timeline, "deploy").Detail!.Progress.ShouldBe(1);
            Row(timeline, "deploy").ChevronAngle.ShouldBe(180);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removed_Rows_Close_Then_Leave_Under_Full_Motion()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Sample());
        var timeline = new Timeline { ItemsSource = events, Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Full);
        try
        {
            frames.Advance(TimeSpan.FromSeconds(1));
            Settle();
            var merge = Row(timeline, "merge");
            var height = merge.Bounds.Height;
            events.Remove(events.First(e => e.Id == "merge"));
            Settle();
            merge.Model!.Leaving.ShouldBeTrue();
            timeline.Rows.ShouldContain(r => r.Key == "ev:merge");
            // The new last row of the day already lost its line.
            Row(timeline, "deploy").Classes.ShouldContain(":last");
            frames.Advance(TimeSpan.FromMilliseconds(60));
            Settle();
            merge.Bounds.Height.ShouldBeLessThan(height);
            frames.Advance(TimeSpan.FromSeconds(1));
            Settle();
            timeline.Rows.ShouldNotContain(r => r.Key == "ev:merge");
            Header(timeline, "Today").CountValue.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Day_Opens_From_Nothing()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var events = new System.Collections.ObjectModel.ObservableCollection<TimelineEvent>(Sample().Skip(2));
        var timeline = new Timeline { ItemsSource = events, Now = s_now };
        var window = Show(timeline, ArcMotionLevel.Full);
        try
        {
            frames.Advance(TimeSpan.FromSeconds(1));
            Settle();
            timeline.Rows[0].Key.ShouldBe("day:2026-10-19");
            events.Add(new TimelineEvent("today", s_now.AddMinutes(-3), "opened the day", "Kai Moreno"));
            Settle();
            var today = Header(timeline, "Today");
            today.Grow!.IsMoving.ShouldBeTrue();
            today.Opacity.ShouldBeLessThan(1);
            frames.Advance(TimeSpan.FromSeconds(1.5));
            Settle();
            today.Grow.Progress.ShouldBe(1, 0.01);
            today.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Source_Is_Not_Announced()
    {
        var timeline = new Timeline { ItemsSource = Sample(), Now = s_now };
        var window = Show(timeline);
        try
        {
            timeline.ItemsSource = Many(20);
            Settle();
            timeline.Announcement.ShouldBeNull();
            timeline.Rows.OfType<TimelineEventModel>().ShouldAllBe(m => !m.Fresh);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void End_And_Home_Reach_Rows_Outside_The_Realized_Range()
    {
        var timeline = new Timeline { ItemsSource = Many(900), Now = s_now, MaxHeight = 400 };
        var window = Show(timeline);
        try
        {
            var first = Row(timeline, "e0");
            first.Trigger!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Settle();
            var focused = window.FocusManager!.GetFocusedElement() as Control;
            focused.ShouldNotBeNull();
            focused.FindAncestorOfType<TimelineRow>()!.Model!.Item.Id.ShouldBe("e897");
            timeline.RealizedContainers.Count().ShouldBeLessThan(40);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Settle();
            (window.FocusManager!.GetFocusedElement() as Control)!.FindAncestorOfType<TimelineRow>()!.Model!.Item.Id.ShouldBe("e894");
            timeline.RealizedContainers.Count().ShouldBeLessThan(40);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Settle();
            (window.FocusManager!.GetFocusedElement() as Control)!.FindAncestorOfType<TimelineRow>()!.Model!.Item.Id.ShouldBe("e0");
        }
        finally
        {
            window.Close();
        }
    }
}
