using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Expand;

public class ResizablePanelsTests
{
    private static ResizablePanels Mail(bool foldersCollapsed = false)
    {
        var group = new ResizablePanels { Width = 802, Height = 300 };
        group.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 25, MinSize = 120, IsCollapsible = true, IsCollapsed = foldersCollapsed });
        group.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 25, MinSize = 150 });
        group.Panels.Add(new ResizablePanel { Id = "reading", Label = "Reading", DefaultSize = 50, MinSize = 200 });
        return group;
    }

    private static (ResizablePanels Group, Window Window) Show(ResizablePanels group)
    {
        var window = TestHelpers.Show(group);
        window.Width = 900;
        window.Height = 400;
        Dispatcher.UIThread.RunJobs();
        return (group, window);
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Panes_Share_The_Room_By_Their_Default_Sizes()
    {
        var (group, window) = Show(Mail());
        try
        {
            var widths = group.ActualWidths;
            // 802 − 2 one-pixel dividers = 800 px of room.
            widths[0].ShouldBe(200, 0.5);
            widths[1].ShouldBe(200, 0.5);
            widths[2].ShouldBe(400, 0.5);
            group.Handles.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Step_And_Home_End_Jump_To_The_Limits()
    {
        var (group, window) = Show(Mail());
        var layouts = new List<IReadOnlyList<double>>();
        group.LayoutChanged += (_, e) => layouts.Add(e.Sizes);
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            group.ActualWidths[0].ShouldBe(216, 0.5);
            group.ActualWidths[1].ShouldBe(184, 0.5);
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            group.ActualWidths[0].ShouldBe(250, 0.5);
            group.ActualWidths[1].ShouldBe(150, 0.5);
            Key(window, PhysicalKey.Home);
            group.ActualWidths[0].ShouldBe(120, 0.5);
            layouts.Count.ShouldBe(3);
            group.Sizes!.Sum().ShouldBe(100, 0.01);
            group.Sizes![0].ShouldBe(15, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Collapses_And_Restores_A_Collapsible_Neighbour()
    {
        var (group, window) = Show(Mail());
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            group.ActualWidths[0].ShouldBe(0, 0.5);
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.Handles[0].Mode.ShouldBe(ResizablePanelsMode.Before);
            group.Panels[0].IsHitTestVisible.ShouldBeFalse();
            Key(window, PhysicalKey.Enter);
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualWidths[0].ShouldBe(200, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Past_The_Minimum_Collapses_A_Collapsible_Pane()
    {
        var (group, window) = Show(Mail());
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Home);
            Key(window, PhysicalKey.ArrowLeft);
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            Key(window, PhysicalKey.ArrowRight);
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualWidths[0].ShouldBe(120, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_A_Divider_Follows_The_Pointer_And_Snaps_Closed_Past_The_Minimum()
    {
        var (group, window) = Show(Mail());
        try
        {
            var handle = group.Handles[0];
            var start = handle.TranslatePoint(new Point(handle.LineOffset, 150), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(40, 0));
            Dispatcher.UIThread.RunJobs();
            group.Classes.Contains(":resizing").ShouldBeTrue();
            group.ActualWidths[0].ShouldBe(240, 0.5);
            window.MouseUp(start + new Point(40, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Classes.Contains(":resizing").ShouldBeFalse();
            group.ActualWidths[0].ShouldBe(240, 0.5);

            var again = handle.TranslatePoint(new Point(handle.LineOffset, 150), window)!.Value;
            window.MouseDown(again, MouseButton.Left);
            window.MouseMove(again - new Point(200, 0));
            window.MouseUp(again - new Point(200, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.ActualWidths[0].ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapsed_By_Default_Restores_On_Press()
    {
        var (group, window) = Show(Mail(foldersCollapsed: true));
        try
        {
            group.ActualWidths[0].ShouldBe(0, 0.5);
            var handle = group.Handles[0];
            handle.Mode.ShouldBe(ResizablePanelsMode.Before);
            var point = handle.TranslatePoint(new Point(10, 150), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualWidths[0].ShouldBeGreaterThanOrEqualTo(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Restore_A_Saved_Layout()
    {
        var (group, window) = Show(Mail());
        try
        {
            group.Sizes = [50, 25, 25];
            Dispatcher.UIThread.RunJobs();
            group.ActualWidths[0].ShouldBe(400, 0.5);
            group.ActualWidths[2].ShouldBe(200, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
