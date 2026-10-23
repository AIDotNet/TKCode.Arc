using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Messages;

public class AnnouncementBarTests
{
    private static AnnouncementBar CreateBar(int count = 3)
    {
        var bar = new AnnouncementBar { Storage = new InMemoryMessageStorage() };
        for (var i = 0; i < count; i++)
        {
            bar.Messages.Add(new Announcement { Id = $"m{i}", Message = $"Message {i}" });
        }
        return bar;
    }

    private static (Window Window, AnnouncementBar Bar) Show(AnnouncementBar bar, double width = 900,
                                                             Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var root = new StackPanel { Children = { bar } };
        var window = TestHelpers.Show(root, level);
        window.Width = width;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return (window, bar);
    }

    private static Button Part(AnnouncementBar bar, string name)
        => bar.GetVisualDescendants().OfType<Button>().First(b => b.Name == name);

    [AvaloniaFact]
    public void Default_Bar_Shows_First_Message_With_Dismiss_Only()
    {
        var (window, bar) = Show(CreateBar());
        try
        {
            bar.Viewport!.CurrentFace!.MessageBlock.Text.ShouldBe("Message 0");
            Part(bar, "PART_Dismiss").IsVisible.ShouldBeTrue();
            Part(bar, "PART_Previous").IsVisible.ShouldBeFalse();
            bar.Bounds.Height.ShouldBe(45, 1);
            var side = bar.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PART_Side");
            side.Bounds.Width.ShouldBe(32);
            AutomationProperties.GetName(bar).ShouldBe("Announcements");
            AutomationProperties.GetName(Part(bar, "PART_Dismiss")).ShouldBe("Dismiss");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Previous_Wrap_Around()
    {
        var (window, bar) = Show(CreateBar());
        try
        {
            var changes = 0;
            bar.SelectedIndexChanged += (_, _) => changes++;
            bar.Next();
            bar.SelectedIndex.ShouldBe(1);
            bar.Viewport!.CurrentFace!.MessageBlock.Text.ShouldBe("Message 1");
            bar.Previous();
            bar.Previous();
            bar.SelectedIndex.ShouldBe(2);
            bar.Viewport.CurrentFace!.MessageBlock.Text.ShouldBe("Message 2");
            changes.ShouldBe(3);
            AutomationProperties.GetName(bar.Viewport.CurrentFace).ShouldBe("3 of 3");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Navigation_Shows_Controls_And_Keyboard_Activates_Next()
    {
        var bar = CreateBar();
        bar.IsNavigationVisible = true;
        var (window, _) = Show(bar);
        try
        {
            var next = Part(bar, "PART_Next");
            next.IsVisible.ShouldBeTrue();
            Part(bar, "PART_Previous").IsVisible.ShouldBeTrue();
            // Motion is off in tests, so messages do not rotate and the pause ring stays hidden.
            bar.GetVisualDescendants().OfType<AnnouncementBarRing>().First().IsVisible.ShouldBeFalse();
            next.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            bar.SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rotation_Runs_Under_Full_Motion_And_Pauses_On_Hover_And_Ring()
    {
        var bar = CreateBar();
        bar.IsNavigationVisible = true;
        var (window, _) = Show(bar, level: Motion.ArcMotionLevel.Full);
        try
        {
            bar.IsRotating.ShouldBeTrue();
            bar.IsRunning.ShouldBeTrue();
            var ring = bar.GetVisualDescendants().OfType<AnnouncementBarRing>().First();
            ring.IsVisible.ShouldBeTrue();
            AutomationProperties.GetName(ring).ShouldBe("Pause announcements");
            AutomationProperties.GetLiveSetting(bar.Viewport!).ShouldBe(AutomationLiveSetting.Off);
            window.MouseMove(new Point(450, 20));
            Dispatcher.UIThread.RunJobs();
            bar.IsRunning.ShouldBeFalse();
            AutomationProperties.GetLiveSetting(bar.Viewport!).ShouldBe(AutomationLiveSetting.Polite);
            window.MouseMove(new Point(450, 200));
            Dispatcher.UIThread.RunJobs();
            bar.IsRunning.ShouldBeTrue();
            ring.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            bar.IsPaused.ShouldBeTrue();
            bar.IsRunning.ShouldBeFalse();
            AutomationProperties.GetName(ring).ShouldBe("Resume announcements");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dismiss_Is_Remembered_By_Key()
    {
        var storage = new InMemoryMessageStorage();
        var bar = CreateBar();
        bar.Storage = storage;
        bar.DismissalKey = "launch";
        var (window, _) = Show(bar);
        try
        {
            var closed = 0;
            bar.Closed += (_, _) => closed++;
            var dismiss = Part(bar, "PART_Dismiss");
            dismiss.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            closed.ShouldBe(1);
            bar.IsOpen.ShouldBeFalse();
            bar.Bounds.Height.ShouldBe(0, 0.5);
            storage.Read("arc-announcement:launch").ShouldBe("dismissed");
        }
        finally
        {
            window.Close();
        }

        var again = CreateBar();
        again.Storage = storage;
        again.DismissalKey = "launch";
        var (second, _) = Show(again);
        try
        {
            again.IsOpen.ShouldBeTrue();
            again.Bounds.Height.ShouldBe(0, 0.5);
            AnnouncementBar.ClearDismissal("launch", storage);
            storage.Read("arc-announcement:launch").ShouldBeNull();
        }
        finally
        {
            second.Close();
        }
    }

    [AvaloniaFact]
    public void A_Timed_Dismissal_Expires()
    {
        var storage = new InMemoryMessageStorage();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bar = CreateBar();
        bar.Storage = storage;
        bar.DismissalKey = "timed";
        bar.DismissalDuration = TimeSpan.FromDays(1);

        // Closed an hour ago: still hidden. Closed two days ago, or before durations existed: shown again.
        storage.Write("arc-announcement:timed", $"dismissed:{now - 3600}");
        var (window, _) = Show(bar);
        try
        {
            bar.Bounds.Height.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }

        foreach (var stored in new[] { $"dismissed:{now - 2 * 86400}", "dismissed" })
        {
            storage.Write("arc-announcement:timed", stored);
            var again = CreateBar();
            again.Storage = storage;
            again.DismissalKey = "timed";
            again.DismissalDuration = TimeSpan.FromDays(1);
            var (second, _) = Show(again);
            try
            {
                again.Bounds.Height.ShouldBeGreaterThan(0, stored);
            }
            finally
            {
                second.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Countdown_And_Action_Raise_Events()
    {
        var bar = new AnnouncementBar { Storage = new InMemoryMessageStorage() };
        bar.Messages.Add(new Announcement
        {
            Id = "sale",
            Message = "Spring sale",
            Countdown = new AnnouncementCountdown { To = DateTimeOffset.Now.AddHours(26).AddMinutes(3).AddSeconds(30), Label = "Ends in" },
            Action = new AnnouncementAction { Label = "Shop now" }
        });
        bar.Messages.Add(new Announcement
        {
            Id = "over",
            Message = "Sale over",
            Countdown = new AnnouncementCountdown { To = DateTimeOffset.Now.AddSeconds(-5) }
        });
        var ended = new List<string>();
        var actions = new List<string>();
        bar.CountdownEnded += (_, e) => ended.Add(e.Announcement.Id);
        bar.ActionInvoked += (_, e) => actions.Add(e.Announcement.Id);
        var (window, _) = Show(bar);
        try
        {
            var face = bar.Viewport!.CurrentFace!;
            face.Countdown!.DisplayText.ShouldStartWith("10203");
            face.Countdown.Bounds.Width.ShouldBeGreaterThan(40);
            AutomationProperties.GetName(face.Countdown).ShouldStartWith("1 days 2 hours 3 minutes");
            face.Link!.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            actions.ShouldBe(new[] { "sale" });
            bar.Next();
            Dispatcher.UIThread.RunJobs();
            ended.ShouldBe(new[] { "over" });
            bar.Viewport.CurrentFace!.Countdown!.DisplayText.ShouldBe("000000");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Bar_Drops_The_Mirror_Column_And_Inverted_Tone_Flips_Colors()
    {
        var bar = CreateBar(1);
        bar.Tone = AnnouncementBarTone.Inverted;
        var (window, _) = Show(bar, width: 500);
        try
        {
            bar.Classes.ShouldContain(":narrow");
            var side = bar.GetVisualDescendants().OfType<Panel>().First(p => p.Name == "PART_Side");
            side.Bounds.Width.ShouldBe(0);
            bar.Viewport!.IsCentered.ShouldBeFalse();
            var border = bar.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Bar");
            TestHelpers.ColorOf(border.Background).ShouldBe(TestHelpers.ColorOf(bar.FindResource(DesignTokens.ArcDesignTokenKind.Foreground)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Source_Overrides_Inline_Messages()
    {
        var bar = CreateBar(2);
        bar.ItemsSource = new List<Announcement> { new() { Id = "x", Message = "From a binding" } };
        var (window, _) = Show(bar);
        try
        {
            bar.Count.ShouldBe(1);
            bar.Viewport!.CurrentFace!.MessageBlock.Text.ShouldBe("From a binding");
            bar.ItemsSource = null;
            Dispatcher.UIThread.RunJobs();
            bar.Viewport.CurrentFace!.MessageBlock.Text.ShouldBe("Message 0");
        }
        finally
        {
            window.Close();
        }
    }
}
