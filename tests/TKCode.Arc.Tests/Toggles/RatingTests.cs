using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class RatingTests
{
    private static RatingStars Stars(Rating rating) => TestHelpers.Descendant<RatingStars>(rating);

    private static Point CellCenter(Rating rating, int index, Avalonia.Controls.Window window)
    {
        var stars = Stars(rating);
        var (left, right) = stars.CellRange(index);
        return stars.TranslatePoint(new Point((left + right) / 2, stars.Bounds.Height / 2), window)!.Value;
    }

    private static void Press(Avalonia.Controls.Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Arrows_Move_By_A_Step_And_Home_End_Jump()
    {
        var rating = new Rating { Label = "Score", Value = 3 };
        var window = TestHelpers.Show(rating);
        try
        {
            rating.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            rating.Value.ShouldBe(4);
            Press(window, PhysicalKey.ArrowDown);
            rating.Value.ShouldBe(3);
            Press(window, PhysicalKey.End);
            rating.Value.ShouldBe(5);
            Press(window, PhysicalKey.ArrowRight);
            rating.Value.ShouldBe(5);
            Press(window, PhysicalKey.Home);
            rating.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Number_Keys_Choose_A_Star_And_Delete_Clears()
    {
        var committed = new List<double>();
        var rating = new Rating { Label = "Score" };
        rating.ValueChanged += (_, e) => committed.Add(e.Value);
        var window = TestHelpers.Show(rating);
        try
        {
            rating.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Digit4);
            Press(window, PhysicalKey.Digit9);
            rating.Value.ShouldBe(4);
            Press(window, PhysicalKey.Delete);
            rating.Value.ShouldBe(0);
            committed.ShouldBe([4, 0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Clearing_The_Value_Never_Reaches_Zero()
    {
        var rating = new Rating { Label = "Score", Value = 1, AllowClear = false };
        var window = TestHelpers.Show(rating);
        try
        {
            rating.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowLeft);
            rating.Value.ShouldBe(1);
            Press(window, PhysicalKey.Delete);
            rating.Value.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Half_Stars_Move_In_Halves_And_Fill_Half_A_Star()
    {
        var rating = new Rating { Label = "Score", Value = 2, AllowHalf = true };
        var window = TestHelpers.Show(rating);
        try
        {
            rating.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowRight);
            rating.Value.ShouldBe(2.5);
            Stars(rating).FillOf(1).ShouldBe(1);
            Stars(rating).FillOf(2).ShouldBe(0.5);
            Stars(rating).FillOf(3).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_A_Star_Commits_And_Clicking_It_Again_Clears()
    {
        var rating = new Rating { Label = "Score" };
        var window = TestHelpers.Show(rating);
        try
        {
            var at = CellCenter(rating, 2, window);
            window.MouseMove(at, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            rating.Value.ShouldBe(3);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            rating.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_Previews_Without_Committing_And_Leaving_Restores()
    {
        var rating = new Rating { Label = "Score", Value = 1 };
        var window = TestHelpers.Show(rating);
        try
        {
            window.MouseMove(CellCenter(rating, 3, window), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Stars(rating).FillOf(3).ShouldBe(1);
            rating.Value.ShouldBe(1);
            TestHelpers.Descendant<RatingRollingText>(rating).Text.ShouldBe("Great");
            window.MouseMove(new Point(window.Bounds.Width - 1, window.Bounds.Height - 1), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Stars(rating).FillOf(3).ShouldBe(0);
            TestHelpers.Descendant<RatingRollingText>(rating).Text.ShouldBe("Poor");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Across_The_Row_Commits_Where_It_Ends()
    {
        var committed = new List<double>();
        var rating = new Rating { Label = "Score" };
        rating.ValueChanged += (_, e) => committed.Add(e.Value);
        var window = TestHelpers.Show(rating);
        try
        {
            var start = CellCenter(rating, 0, window);
            var end = CellCenter(rating, 3, window);
            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            rating.Value.ShouldBe(0);
            Stars(rating).FillOf(3).ShouldBe(1);
            window.MouseUp(end, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            rating.Value.ShouldBe(4);
            committed.ShouldBe([4]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Takes_No_Input_Or_Focus()
    {
        var rating = new Rating { Label = "Average", Value = 3, IsReadOnly = true };
        var window = TestHelpers.Show(rating);
        try
        {
            rating.Focusable.ShouldBeFalse();
            var at = CellCenter(rating, 4, window);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            rating.Value.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Value_Text_Follows_Words_Numbers_And_The_Empty_Label()
    {
        var rating = new Rating { Label = "Score" };
        var window = TestHelpers.Show(rating);
        try
        {
            var text = TestHelpers.Descendant<RatingRollingText>(rating);
            text.Text.ShouldBe("Not rated");
            rating.Value = 5;
            Dispatcher.UIThread.RunJobs();
            text.Text.ShouldBe("Excellent");
            rating.Maximum = 10;
            rating.Value = 7;
            Dispatcher.UIThread.RunJobs();
            text.Text.ShouldBe("7 out of 10");
            rating.ValueLabels = ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j"];
            Dispatcher.UIThread.RunJobs();
            text.Text.ShouldBe("g");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Value_Past_The_Scale_Is_Shown_Within_It()
    {
        var rating = new Rating { Label = "Score", Value = 9 };
        var window = TestHelpers.Show(rating);
        try
        {
            Stars(rating).FillOf(4).ShouldBe(1);
            TestHelpers.Descendant<RatingRollingText>(rating).Text.ShouldBe("Excellent");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Set_The_Cell_And_Row_Width()
    {
        var rating = new Rating { Label = "Score", Size = ControlSize.Large };
        var window = TestHelpers.Show(rating);
        try
        {
            Stars(rating).CellSize.ShouldBe(44);
            Stars(rating).GlyphSize.ShouldBe(30);
            Stars(rating).Bounds.Width.ShouldBe(5 * 44 + 4 * 2);
            rating.Size = ControlSize.Small;
            Dispatcher.UIThread.RunJobs();
            Stars(rating).CellSize.ShouldBe(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Reports_A_Range_And_Speaks_The_Value()
    {
        var rating = new Rating { Label = "Score", Value = 4 };
        var window = TestHelpers.Show(rating);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(rating);
            peer.GetName().ShouldBe("Score");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.Maximum.ShouldBe(5);
            range.Value.ShouldBe(4);
            range.SetValue(2);
            rating.Value.ShouldBe(2);
            AutomationProperties.GetHelpText(rating).ShouldBe("Fair, 2 of 5 stars");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Settles_On_The_Target()
    {
        var rating = new Rating { Label = "Score", Value = 1 };
        var window = TestHelpers.Show(rating, Motion.ArcMotionLevel.Full);
        try
        {
            rating.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Digit4);
            TestHelpers.PumpUntil(() => Stars(rating).FillOf(3) == 1 && Math.Abs(Stars(rating).ScaleOf(3) - 1) < 0.001, 3000).ShouldBeTrue();
            rating.Value.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }
}
