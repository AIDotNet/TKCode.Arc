using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Avatars;

/// <summary>The presence stack: order, the limit and the chip, joins and leaves, the list, keyboard and motion.</summary>
public class PresenceStackTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static ObservableCollection<PresenceStackViewer> People(int count)
    {
        var list = new ObservableCollection<PresenceStackViewer>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new PresenceStackViewer("Person " + (char)('A' + i)));
        }
        return list;
    }

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Shows_The_Newest_First_Up_To_The_Limit_And_Counts_The_Rest()
    {
        var people = People(6);
        var stack = new PresenceStack { ItemsSource = people, MaxVisibleCount = 4 };
        var window = TestHelpers.Show(stack);
        try
        {
            Settle();
            stack.ViewerCount.ShouldBe(6);
            var panel = stack.Panel.ShouldNotBeNull();
            panel.VisibleSlots.Select(s => s.Viewer!.Name).ShouldBe(["Person F", "Person E", "Person D", "Person C"]);
            panel.Overflow.IsVisible.ShouldBeTrue();
            panel.Overflow.Count.ShouldBe(2);
            AutomationProperties.GetName(panel.Overflow).ShouldBe("2 more viewers");
            stack.Classes.Contains(PresenceStack.OverflowingPseudoClass).ShouldBeTrue();
            AutomationProperties.GetName(stack).ShouldBe("Viewing now, 6 viewing");
            stack.IsNewestFirst = false;
            panel.VisibleSlots.First().Viewer!.Name.ShouldBe("Person A");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Joins_And_Leaves_Update_The_Row_And_Are_Announced()
    {
        var people = People(2);
        var stack = new PresenceStack { ItemsSource = people };
        var window = TestHelpers.Show(stack);
        try
        {
            Settle();
            var announcer = stack.Announcer.ShouldNotBeNull();
            announcer.History.ShouldBeEmpty("the first list is not news");
            people.Add(new PresenceStackViewer("Zoe"));
            Settle();
            stack.Panel!.VisibleSlots.First().Viewer!.Name.ShouldBe("Zoe");
            announcer.Announcement.ShouldBe("Zoe joined");
            people.RemoveAt(0);
            Settle();
            announcer.Announcement.ShouldBe("Person A left");
            stack.ViewerCount.ShouldBe(2);
            stack.Panel.Slots.Count.ShouldBe(2, "with motion off a leaver goes at once");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Idle_Viewers_Fade_And_Say_So()
    {
        var viewer = new PresenceStackViewer("Ida", detail: "Reading");
        var stack = new PresenceStack();
        stack.Viewers.Add(viewer);
        var window = TestHelpers.Show(stack);
        try
        {
            Settle();
            var slot = stack.Panel!.VisibleSlots.Single();
            AutomationProperties.GetName(slot).ShouldBe("Ida, Reading");
            viewer.IsIdle = true;
            AutomationProperties.GetName(slot).ShouldBe("Ida, idle, Reading");
            slot.Avatar.Opacity.ShouldBeLessThan(1);
            slot.Avatar.Initials.ShouldBe("I");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Chip_Opens_The_List_Of_Everyone()
    {
        var stack = new PresenceStack { ItemsSource = People(5), MaxVisibleCount = 2 };
        var window = TestHelpers.Show(stack);
        try
        {
            Settle();
            stack.Panel!.Overflow.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
            Settle();
            stack.IsListOpen.ShouldBeTrue();
            stack.ListPanel.ShouldNotBeNull().Children.Count.ShouldBe(5);
            stack.CloseList();
            stack.IsListOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrow_Keys_Move_Between_People_And_The_Chip()
    {
        var stack = new PresenceStack { ItemsSource = People(4), MaxVisibleCount = 2 };
        var window = TestHelpers.Show(stack);
        try
        {
            Settle();
            var panel = stack.Panel!;
            var slots = panel.VisibleSlots.ToList();
            slots[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            slots[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            panel.Overflow.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            slots[0].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Full_Motion_A_Leaver_Shrinks_Out_Before_It_Goes()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var people = People(3);
        var stack = new PresenceStack { ItemsSource = people };
        var window = TestHelpers.Show(stack, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
            Settle();
            people.Add(new PresenceStackViewer("Newcomer"));
            Settle();
            var newcomer = stack.Panel!.VisibleSlots.First();
            newcomer.Presence!.Value.ShouldBe(0, 0.001);
            newcomer.Pulse.ShouldNotBeNull();
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            Settle();
            newcomer.Presence.Value.ShouldBe(1, 0.01);

            people.RemoveAt(0);
            Settle();
            stack.Panel.Slots.Count.ShouldBe(4, "the leaver is still shrinking");
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            Settle();
            stack.Panel.Slots.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Name_Keeps_Its_Color()
    {
        PresenceStack.SeriesIndexOf("Ada").ShouldBe(PresenceStack.SeriesIndexOf("Ada"));
        Enumerable.Range(0, 40).Select(i => PresenceStack.SeriesIndexOf("Name " + i)).Distinct().Count().ShouldBeGreaterThan(1);
    }
}
