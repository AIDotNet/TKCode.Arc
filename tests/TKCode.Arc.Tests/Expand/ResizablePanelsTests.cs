using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
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
            var widths = group.ActualSizes;
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
            group.ActualSizes[0].ShouldBe(216, 0.5);
            group.ActualSizes[1].ShouldBe(184, 0.5);
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            group.ActualSizes[0].ShouldBe(250, 0.5);
            group.ActualSizes[1].ShouldBe(150, 0.5);
            Key(window, PhysicalKey.Home);
            group.ActualSizes[0].ShouldBe(120, 0.5);
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
            group.ActualSizes[0].ShouldBe(0, 0.5);
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.Handles[0].Mode.ShouldBe(ResizablePanelsMode.Before);
            group.Panels[0].IsHitTestVisible.ShouldBeFalse();
            Key(window, PhysicalKey.Enter);
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualSizes[0].ShouldBe(200, 0.5);
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
            group.ActualSizes[0].ShouldBe(120, 0.5);
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
            group.ActualSizes[0].ShouldBe(240, 0.5);
            window.MouseUp(start + new Point(40, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Classes.Contains(":resizing").ShouldBeFalse();
            group.ActualSizes[0].ShouldBe(240, 0.5);

            var again = handle.TranslatePoint(new Point(handle.LineOffset, 150), window)!.Value;
            window.MouseDown(again, MouseButton.Left);
            window.MouseMove(again - new Point(200, 0));
            window.MouseUp(again - new Point(200, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.ActualSizes[0].ShouldBe(0, 0.5);
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
            group.ActualSizes[0].ShouldBe(0, 0.5);
            var handle = group.Handles[0];
            handle.Mode.ShouldBe(ResizablePanelsMode.Before);
            var point = handle.TranslatePoint(new Point(10, 150), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualSizes[0].ShouldBeGreaterThanOrEqualTo(120);
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
            group.ActualSizes[0].ShouldBe(400, 0.5);
            group.ActualSizes[2].ShouldBe(200, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    private static ResizablePanels VerticalMail(bool foldersCollapsed = false)
    {
        var group = new ResizablePanels { Width = 300, Height = 602, Orientation = Orientation.Vertical };
        group.Panels.Add(new ResizablePanel { Id = "folders", Label = "Folders", DefaultSize = 25, MinSize = 80, IsCollapsible = true, IsCollapsed = foldersCollapsed });
        group.Panels.Add(new ResizablePanel { Id = "messages", Label = "Messages", DefaultSize = 25, MinSize = 80 });
        group.Panels.Add(new ResizablePanel { Id = "reading", Label = "Reading", DefaultSize = 50, MinSize = 120 });
        return group;
    }

    private static (ResizablePanels Group, Window Window) ShowTall(ResizablePanels group)
    {
        var window = TestHelpers.Show(group);
        window.Width = 900;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return (group, window);
    }

    [AvaloniaFact]
    public void Vertical_Panes_Stack_Top_To_Bottom()
    {
        var (group, window) = ShowTall(VerticalMail());
        try
        {
            // 602 − 2 one-pixel dividers = 600 px of room, shared by height.
            group.ActualSizes[0].ShouldBe(150, 0.5);
            group.ActualSizes[1].ShouldBe(150, 0.5);
            group.ActualSizes[2].ShouldBe(300, 0.5);
            group.Panels[1].Bounds.ShouldBe(new Rect(0, 151, 300, 150));
            group.Panels[2].Bounds.ShouldBe(new Rect(0, 302, 300, 300));
            group.Handles[0].Bounds.Height.ShouldBe(13, 0.01);
            group.Handles[0].Bounds.Width.ShouldBe(300, 0.01);
            group.Handles[0].Bounds.Y.ShouldBe(150 - ResizablePanelsHandle.Reach, 0.01);
            group.Classes.Contains(":vertical").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Up_Down_Step_And_Left_Right_Are_Ignored()
    {
        var (group, window) = ShowTall(VerticalMail());
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.ArrowLeft);
            group.ActualSizes[0].ShouldBe(150, 0.5);
            Key(window, PhysicalKey.ArrowDown);
            group.ActualSizes[0].ShouldBe(166, 0.5);
            group.ActualSizes[1].ShouldBe(134, 0.5);
            Key(window, PhysicalKey.ArrowUp, RawInputModifiers.Shift);
            group.ActualSizes[0].ShouldBe(102, 0.5);
            Key(window, PhysicalKey.End);
            group.ActualSizes[0].ShouldBe(220, 0.5);
            group.ActualSizes[1].ShouldBe(80, 0.5);
            Key(window, PhysicalKey.Home);
            group.ActualSizes[0].ShouldBe(80, 0.5);
            Key(window, PhysicalKey.ArrowUp);
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            Key(window, PhysicalKey.ArrowDown);
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualSizes[0].ShouldBe(80, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Drag_Follows_The_Pointer_Along_Y_And_Collapses()
    {
        var (group, window) = ShowTall(VerticalMail());
        try
        {
            var handle = group.Handles[0];
            var start = handle.TranslatePoint(new Point(150, handle.LineOffset), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(0, 40), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            group.Classes.Contains(":resizing").ShouldBeTrue();
            group.ActualSizes[0].ShouldBe(190, 0.5);
            window.MouseUp(start + new Point(0, 40), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBe(190, 0.5);
            group.Sizes![0].ShouldBe(190 / 6.0, 0.01);

            // A sideways move does nothing in a vertical group.
            var side = handle.TranslatePoint(new Point(60, handle.LineOffset), window)!.Value;
            window.MouseDown(side, MouseButton.Left);
            window.MouseMove(side + new Point(60, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(side + new Point(60, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBe(190, 0.5);

            // Pressed well away from the last press, so it does not count as a double-click (which resets).
            var again = handle.TranslatePoint(new Point(240, handle.LineOffset), window)!.Value;
            window.MouseDown(again, MouseButton.Left);
            window.MouseMove(again - new Point(0, 180), RawInputModifiers.LeftMouseButton);
            window.MouseUp(again - new Point(0, 180), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.ActualSizes[0].ShouldBe(0, 0.5);
            handle.Mode.ShouldBe(ResizablePanelsMode.Before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Collapse_And_Restore()
    {
        var (group, window) = ShowTall(VerticalMail(foldersCollapsed: true));
        try
        {
            group.ActualSizes[0].ShouldBe(0, 0.5);
            var handle = group.Handles[0];
            handle.Mode.ShouldBe(ResizablePanelsMode.Before);
            handle.Bounds.Height.ShouldBe(ResizablePanelsHandle.CollapsedReach + 1, 0.01);
            var point = handle.TranslatePoint(new Point(150, 10), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualSizes[0].ShouldBeGreaterThanOrEqualTo(80);

            handle.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.ActualSizes[0].ShouldBe(0, 0.5);
            group.Panels[0].IsCollapsed = false;
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBeGreaterThanOrEqualTo(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Sizes_Round_Trip()
    {
        var (group, window) = ShowTall(VerticalMail());
        try
        {
            group.Sizes = [50, 25, 25];
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBe(300, 0.5);
            group.ActualSizes[2].ShouldBe(150, 0.5);
            group.Panels[2].Bounds.Height.ShouldBe(150, 0.5);
            group.Handles[1].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowDown);
            group.Sizes!.Sum().ShouldBe(100, 0.01);
            group.Sizes![1].ShouldBe(166 / 6.0, 0.01);
            group.Sizes![2].ShouldBe(134 / 6.0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Gap_Spaces_The_Panes_And_The_Grab_Area_Covers_It()
    {
        var group = Mail();
        group.Width = 814;
        group.Gap = 7;
        var (_, window) = Show(group);
        try
        {
            // 814 − 2 gaps of 7 = 800 px of room.
            group.ActualSizes[0].ShouldBe(200, 0.5);
            group.ActualSizes[1].ShouldBe(200, 0.5);
            group.ActualSizes[2].ShouldBe(400, 0.5);
            group.Panels[1].Bounds.X.ShouldBe(207, 0.01);
            group.Panels[2].Bounds.X.ShouldBe(414, 0.01);
            (group.ActualSizes.Sum() + 2 * 7).ShouldBe(814, 0.5);
            var handle = group.Handles[0];
            handle.Bounds.X.ShouldBeLessThanOrEqualTo(200);
            handle.Bounds.Right.ShouldBeGreaterThanOrEqualTo(207);

            // Pressing anywhere in the gap grabs the divider.
            var start = handle.TranslatePoint(new Point(handle.LineOffset + 6, 150), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(30, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(30, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBe(230, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Gap_Of_One_Matches_The_One_Pixel_Line_Layout()
    {
        var (group, window) = Show(Mail());
        try
        {
            group.Gap.ShouldBe(1);
            group.Panels[0].Bounds.ShouldBe(new Rect(0, 0, 200, 300));
            group.Panels[1].Bounds.ShouldBe(new Rect(201, 0, 200, 300));
            group.Panels[2].Bounds.ShouldBe(new Rect(402, 0, 400, 300));
            group.Handles[0].Bounds.ShouldBe(new Rect(200 - ResizablePanelsHandle.Reach, 0, 13, 300));
            group.Handles[1].Bounds.ShouldBe(new Rect(401 - ResizablePanelsHandle.Reach, 0, 13, 300));
        }
        finally
        {
            window.Close();
        }
    }

    private static ResizablePanels Four()
    {
        var group = new ResizablePanels { Width = 803, Height = 300 };
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            group.Panels.Add(new ResizablePanel { Id = id, Label = id, DefaultSize = 25, MinSize = 80 });
        }
        return group;
    }

    [AvaloniaFact]
    public void Removing_A_Pane_Gives_Its_Space_To_Its_Neighbours_And_Keeps_The_Rest()
    {
        var (group, window) = Show(Four());
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            group.ActualSizes[0].ShouldBe(216, 0.5);
            group.ActualSizes[1].ShouldBe(184, 0.5);
            var writes = 0;
            group.PropertyChanged += (_, e) =>
            {
                if (e.Property == ResizablePanels.SizesProperty)
                {
                    writes++;
                }
            };
            var a = group.Panels[0];
            var d = group.Panels[3];
            group.Panels.RemoveAt(2);
            writes.ShouldBe(1);
            Dispatcher.UIThread.RunJobs();
            group.Handles.Count.ShouldBe(2);
            // c (200 px + its 1 px divider) goes to b (184) and d (200) in proportion; a keeps its 216 px.
            group.ActualSizes[0].ShouldBe(216, 0.5);
            group.ActualSizes[1].ShouldBe(184 + 201 * 184 / 384.0, 0.5);
            group.ActualSizes[2].ShouldBe(200 + 201 * 200 / 384.0, 0.5);
            a.Bounds.Width.ShouldBe(216, 0.5);
            d.Bounds.Right.ShouldBe(803, 0.5);
            group.Sizes!.Count.ShouldBe(3);
            group.Sizes!.Sum().ShouldBe(100, 0.01);
            group.Sizes![0].ShouldBe(216 / 8.01, 0.05);

            group.Panels.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            // Only one neighbour: b takes all of a's space.
            group.ActualSizes[0].ShouldBe(184 + 201 * 184 / 384.0 + 217, 0.5);
            group.ActualSizes[1].ShouldBe(200 + 201 * 200 / 384.0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_A_Pane_Keeps_The_Survivors_In_Proportion()
    {
        var (group, window) = Show(Mail());
        try
        {
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            var writes = 0;
            group.PropertyChanged += (_, e) =>
            {
                if (e.Property == ResizablePanels.SizesProperty)
                {
                    writes++;
                }
            };
            // Survivors: 216, 184, 400 (average 266.67 px for an average DefaultSize of 33.33), so DefaultSize 25 asks for 200 px.
            group.Panels.Add(new ResizablePanel { Id = "preview", Label = "Preview", DefaultSize = 25, MinSize = 80 });
            writes.ShouldBe(1);
            Dispatcher.UIThread.RunJobs();
            var sizes = group.ActualSizes;
            sizes.Count.ShouldBe(4);
            (sizes.Sum() + 3).ShouldBe(802, 0.5);
            // Scaled to fit 799 px, Messages would drop below its 150 px minimum, so it holds there and the rest scale.
            var scale = (799 - 150) / 816.0;
            sizes[0].ShouldBe(216 * scale, 0.5);
            sizes[1].ShouldBe(150, 0.5);
            sizes[2].ShouldBe(400 * scale, 0.5);
            sizes[3].ShouldBe(200 * scale, 0.5);
            (sizes[0] / sizes[2]).ShouldBe(216 / 400.0, 0.001);
            group.Handles.Count.ShouldBe(3);
            group.Sizes![3].ShouldBe(20, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Runtime_Changes_Keep_A_Survivors_Collapsed_State()
    {
        var (group, window) = Show(Mail(foldersCollapsed: true));
        try
        {
            group.ActualSizes[0].ShouldBe(0, 0.5);
            group.Panels.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            group.Panels[0].IsCollapsed.ShouldBeTrue();
            group.ActualSizes[0].ShouldBe(0, 0.5);
            group.ActualSizes[1].ShouldBe(801, 0.5);
            group.Handles[0].Mode.ShouldBe(ResizablePanelsMode.Before);
            group.Handles[0].Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.Enter);
            group.Panels[0].IsCollapsed.ShouldBeFalse();
            group.ActualSizes[0].ShouldBeGreaterThanOrEqualTo(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_First_Pane_Content_Anchors_To_Its_Bottom_Edge_Below_The_Minimum()
    {
        var group = VerticalMail();
        group.Panels[0].Content = new Border();
        var (_, window) = ShowTall(group);
        try
        {
            var handle = group.Handles[0];
            var start = handle.TranslatePoint(new Point(150, handle.LineOffset), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start - new Point(0, 100), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            var height = group.ActualSizes[0];
            height.ShouldBeLessThan(80);
            height.ShouldBeGreaterThan(40);
            var content = (Border)group.Panels[0].Content!;
            var top = content.TranslatePoint(default, group.Panels[0])!.Value.Y;
            top.ShouldBe(height - 80, 0.5);
            content.Bounds.Height.ShouldBe(80, 0.5);
            window.MouseUp(start - new Point(0, 100), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            // Released in the rubber band it settles at the minimum, or closes when the move counts as a flick.
            (group.ActualSizes[0] is < 0.5 or > 79.5 and < 80.5).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Orientation_Keeps_The_Shares()
    {
        var group = Mail();
        group.Height = 802;
        var (_, window) = ShowTall(group);
        try
        {
            group.Sizes = [50, 25, 25];
            Dispatcher.UIThread.RunJobs();
            group.Orientation = Orientation.Vertical;
            Dispatcher.UIThread.RunJobs();
            group.ActualSizes[0].ShouldBe(400, 0.5);
            group.Panels[1].Bounds.ShouldBe(new Rect(0, 401, 802, 200));
            group.Handles[0].Bounds.Height.ShouldBe(13, 0.01);
            group.Orientation = Orientation.Horizontal;
            Dispatcher.UIThread.RunJobs();
            group.Panels[1].Bounds.ShouldBe(new Rect(401, 0, 200, 802));
        }
        finally
        {
            window.Close();
        }
    }
}
