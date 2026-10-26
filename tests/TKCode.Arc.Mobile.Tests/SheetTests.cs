using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile.Tests;

public class SheetTests
{
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 400, Height = 600 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static T Named<T>(Control root, string name) where T : Control
        => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static Point CenterOf(Control control, Visual to)
        => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), to)!.Value;

    private static Sheet DetentSheet(string detents = "0.4, 0.9")
        => new() { Title = "Filters", Detents = SheetDetents.Parse(detents), Content = new Border { Height = 900 }, IsOpen = true };

    /// <summary>The offset the panel rests at for a detent fraction when the tallest is 0.9 of the sheet.</summary>
    private static double Offset(Sheet sheet, double fraction)
    {
        var panel = Named<Border>(sheet, "PART_Panel");
        return panel.Bounds.Height * (1 - fraction / 0.9);
    }

    /// <summary>Waits until the panel stops moving (a spring in flight), so a drag starts from a known rest.</summary>
    private static void Settle(Sheet sheet)
    {
        var last = double.NaN;
        var still = 0;
        for (var i = 0; i < 200 && still < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
            still = Math.Abs(sheet.PanelOffset - last) < 0.01 ? still + 1 : 0;
            last = sheet.PanelOffset;
        }
    }

    private static void Drag(Window window, Point from, double dy, bool pause = true)
    {
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(0, Math.Sign(dy) * 12), RawInputModifiers.LeftMouseButton);
        window.MouseMove(from + new Point(0, dy), RawInputModifiers.LeftMouseButton);
        if (pause)
        {
            Thread.Sleep(80); // a finger that rests before lifting has no speed
        }
        window.MouseUp(from + new Point(0, dy), MouseButton.Left);
    }

    [AvaloniaFact]
    public void Detents_Parse_From_Text_And_Sort_Distinct()
    {
        SheetDetents.Parse("0.4, 0.9").ShouldBe([0.4, 0.9]);
        SheetDetents.Parse("0.6 0.3").ShouldBe([0.6, 0.3]);
        var sheet = new Sheet { Detents = SheetDetents.Parse("0.9, 0.4, 0.4, 0, 2") };
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.EffectiveDetents.ShouldBe([0.4, 0.9, 1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Detents_The_Sheet_Keeps_One_Content_Sized_Stop()
    {
        var sheet = new Sheet { Content = new Border { Height = 200 }, IsOpen = true };
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.EffectiveDetents.Count.ShouldBe(1);
            sheet.DetentIndex.ShouldBe(0);
            sheet.PanelOffset.ShouldBe(0);
            Named<Border>(sheet, "PART_Panel").Bounds.Height.ShouldBeLessThan(300);
            sheet.Expand();
            sheet.Collapse();
            sheet.PanelOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opens_At_The_Initial_Detent_And_The_Panel_Is_As_Tall_As_The_Tallest()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            sheet.InitialDetentIndex = 0;
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var panel = Named<Border>(sheet, "PART_Panel");
            panel.Bounds.Height.ShouldBe(sheet.Bounds.Height * 0.9, 1);
            sheet.DetentIndex.ShouldBe(0);
            sheet.PanelOffset.ShouldBe(Offset(sheet, 0.4), 1);
            sheet.PanelOffset.ShouldBeGreaterThan(0);

            sheet.IsOpen = false;
            sheet.InitialDetentIndex = 1;
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.DetentIndex.ShouldBe(1);
            sheet.PanelOffset.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SnapTo_Expand_And_Collapse_Move_The_Sheet_And_Raise_DetentChanged()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet });
        try
        {
            var events = new List<(int, double)>();
            sheet.DetentChanged += (_, e) => events.Add((e.Index, e.Fraction));
            sheet.Expand();
            sheet.DetentIndex.ShouldBe(1);
            sheet.PanelOffset.ShouldBe(0, 0.5);
            sheet.Collapse();
            sheet.DetentIndex.ShouldBe(0);
            sheet.PanelOffset.ShouldBe(Offset(sheet, 0.4), 1);
            sheet.SnapTo(9);
            sheet.DetentIndex.ShouldBe(1);
            events.ShouldBe([(1, 0.9), (0, 0.4), (1, 0.9)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrim_Follows_The_Position_Between_Detents()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet });
        try
        {
            var scrim = Named<Border>(sheet, "PART_Scrim");
            scrim.Opacity.ShouldBe(0.78, 0.01);
            sheet.Expand();
            scrim.Opacity.ShouldBe(1, 0.01);
            sheet.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
            scrim.Opacity.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Up_Snaps_To_The_Tallest_And_Down_Returns_Then_Closes()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            Settle(sheet);
            var handle = Named<Control>(sheet, "PART_Handle");
            var to = Offset(sheet, 0.4);
            Drag(window, CenterOf(handle, window), -(to - 10)); // almost the whole way up
            sheet.DetentIndex.ShouldBe(1);
            sheet.IsOpen.ShouldBeTrue();

            Settle(sheet);
            Drag(window, CenterOf(handle, window), to); // back down to the peek, held still
            sheet.DetentIndex.ShouldBe(0);
            sheet.IsOpen.ShouldBeTrue();

            Settle(sheet);
            Drag(window, CenterOf(handle, window), 400); // far past the peek
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Short_Pull_Springs_Back_To_The_Detent_It_Started_At()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            Settle(sheet);
            var handle = Named<Control>(sheet, "PART_Handle");
            Drag(window, CenterOf(handle, window), -30);
            sheet.DetentIndex.ShouldBe(0);
            sheet.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pulling_Past_The_Tallest_Detent_Resists_Like_A_Rubber_Band()
    {
        var sheet = DetentSheet();
        sheet.InitialDetentIndex = 1;
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            sheet.DetentIndex.ShouldBe(1);
            sheet.PanelOffset.ShouldBe(0, 0.5);
            var handle = Named<Control>(sheet, "PART_Handle");
            var start = CenterOf(handle, window);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(0, -12), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Point(0, -300), RawInputModifiers.LeftMouseButton);
            sheet.PanelOffset.ShouldBeLessThan(0);
            sheet.PanelOffset.ShouldBeGreaterThan(-80);
            Thread.Sleep(80);
            window.MouseUp(start + new Point(0, -300), MouseButton.Left);
            sheet.DetentIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Non_Dismissible_Sheet_Never_Closes_By_Dragging_Down()
    {
        var sheet = DetentSheet();
        sheet.IsDismissible = false;
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            Settle(sheet);
            var handle = Named<Control>(sheet, "PART_Handle");
            Drag(window, CenterOf(handle, window), 400);
            sheet.IsOpen.ShouldBeTrue();
            sheet.DetentIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Scrolls_Only_At_The_Tallest_Detent()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet });
        try
        {
            var body = Named<ScrollViewer>(sheet, "PART_Body");
            var at = body.TranslatePoint(new Point(body.Bounds.Width / 2, 20), window)!.Value;
            // The peek: a wheel step raises the sheet instead of scrolling the content.
            window.MouseWheel(at, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            sheet.DetentIndex.ShouldBe(1);
            body.Offset.Y.ShouldBe(0);
            // The tallest: the wheel scrolls the content.
            window.MouseWheel(at, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            body.Offset.Y.ShouldBeGreaterThan(0);
            sheet.DetentIndex.ShouldBe(1);
            // Back at the peek the content returns to its start.
            sheet.Collapse();
            body.Offset.Y.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detents_Changed_While_Open_Move_The_Sheet_To_Stay_On_Its_Detent()
    {
        var sheet = DetentSheet();
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.Detents!.Add(0.6);
            Dispatcher.UIThread.RunJobs();
            sheet.EffectiveDetents.ShouldBe([0.4, 0.6, 0.9]);
            sheet.DetentIndex.ShouldBe(0);
            sheet.Detents = null;
            Dispatcher.UIThread.RunJobs();
            sheet.EffectiveDetents.Count.ShouldBe(1);
            sheet.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Is_Animated_Under_Full_And_Jumps_Under_Off()
    {
        var animated = DetentSheet();
        var window = Show(new Page { Overlay = animated }, ArcMotionLevel.Full);
        var jumped = DetentSheet();
        var window2 = Show(new Page { Overlay = jumped }, ArcMotionLevel.Off);
        try
        {
            animated.Expand();
            animated.PanelOffset.ShouldBeGreaterThan(1); // still travelling on the first frame
            jumped.Expand();
            jumped.PanelOffset.ShouldBe(0, 0.5); // already there
        }
        finally
        {
            window.Close();
            window2.Close();
        }
    }

    [AvaloniaFact]
    public void SafeAreaBottom_Keeps_Space_Under_The_Content()
    {
        var sheet = new Sheet { Content = new Border { Height = 100 }, SafeAreaBottom = 34, IsOpen = true };
        var window = Show(new Page { Overlay = sheet });
        try
        {
            Named<Border>(sheet, "PART_SafeArea").Bounds.Height.ShouldBe(34);
        }
        finally
        {
            window.Close();
        }
    }

    private static ActionSheet NewActions(out List<string> log)
    {
        var invoked = new List<string>();
        log = invoked;
        var sheet = new ActionSheet
        {
            Title = "Photo",
            Message = "Choose what to do",
            Actions =
            {
                new ActionSheetItem { Content = "Edit" },
                new ActionSheetItem { Content = "Delete", IsDestructive = true },
            },
        };
        sheet.ActionInvoked += (_, e) => invoked.Add((string)e.Action.Content!);
        return sheet;
    }

    [AvaloniaFact]
    public void ActionSheet_Choosing_An_Action_Raises_ActionInvoked_Runs_The_Command_And_Closes()
    {
        var sheet = NewActions(out var log);
        var ran = 0;
        sheet.Actions[0].Command = new RelayCommand(() => ran++);
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var row = sheet.Actions[0];
            var at = CenterOf(row, window);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            log.ShouldBe(["Edit"]);
            ran.ShouldBe(1);
            sheet.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ActionSheet_Cancel_Closes_Without_Raising_ActionInvoked_And_Can_Be_Hidden()
    {
        var sheet = NewActions(out var log);
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var cancel = Named<ActionSheetItem>(sheet, "PART_Cancel");
            cancel.Content.ShouldBe("Cancel");
            var at = CenterOf(cancel, window);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            log.ShouldBeEmpty();
            sheet.IsOpen.ShouldBeFalse();

            sheet.CancelText = null;
            Dispatcher.UIThread.RunJobs();
            cancel.GetVisualParent()!.IsEffectivelyVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ActionSheet_Marks_Destructive_Rows_And_Shows_Title_And_Message()
    {
        var sheet = NewActions(out _);
        var window = Show(new Page { Overlay = sheet });
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.Actions[0].Classes.Contains(":destructive").ShouldBeFalse();
            sheet.Actions[1].Classes.Contains(":destructive").ShouldBeTrue();
            sheet.HasHeader.ShouldBeTrue();
            Named<TextBlock>(sheet, "PART_Message").Text.ShouldBe("Choose what to do");
            sheet.Actions.ShouldAllBe(a => a.Bounds.Height >= 56);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ActionSheet_Rises_With_The_Sheets_Motion()
    {
        var sheet = NewActions(out _);
        var window = Show(new Page { Overlay = sheet }, ArcMotionLevel.Full);
        try
        {
            sheet.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            sheet.PanelOffset.ShouldBeGreaterThan(1);
            sheet.IsOpen = false;
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommand(Action run) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => run();
    }
}
