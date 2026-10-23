using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiFollowScroller"/>: the first content lands at the bottom; growth while following chases the bottom 22% of
/// the remaining distance per frame (jumps at Reduced and Off) without measuring the thread again; the person's scroll up
/// (wheel, keys, a drag) releases even by 20px while programmatic offsets never do; coming back within 6px or
/// <see cref="AiFollowScroller.ScrollToLatest"/> follows again; the scroll-to-latest button (fade 160 in / 100 out, name,
/// click); offsets saved and restored per key. Frames run on a manual clock.
/// </summary>
public class AiFollowScrollerTests
{
    private const string JumpName = "Scroll to latest message";

    /// <summary>A thread stand-in that counts its measures.</summary>
    private sealed class CountingBorder : Border
    {
        public int Measures { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public ManualFrameClock Clock { get; } = new();
        public AiFollowScroller Scroller { get; }
        public CountingBorder Thread { get; }
        public Window Window { get; }
        public int FollowingChanges { get; private set; }

        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double height = 1000)
        {
            _clockInstall = Clock.Install();
            Thread = new CountingBorder { Height = height, Background = Brushes.Transparent, Child = new Button { Content = "Copy" } };
            Scroller = new AiFollowScroller { Content = Thread, JumpButtonName = JumpName };
            Scroller.FollowingChanged += (_, _) => FollowingChanges++;
            Window = TestHelpers.Show(Scroller, level);
            Layout();
        }

        public ScrollArea Scroll => Scroller.Scroll!;
        public double Offset => Scroll.Offset.Y;
        public double Bottom => Scroller.BottomOffset;
        public AiFollowJumpButton Jump => Scroller.JumpButton!;

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Runs <paramref name="count"/> frames of 1/60 s and the layout they cause.</summary>
        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                CoreFrames.Run(Clock, 1);
                Layout();
            }
        }

        public void Grow(double by)
        {
            Thread.Height += by;
            Layout();
        }

        public Point Center => Scroller.TranslatePoint(new Point(Scroller.Bounds.Width / 2, 60), Window) ?? default;

        /// <summary>The wheel over the thread: positive moves up (toward older content), 50px a notch.</summary>
        public void Wheel(double notches)
        {
            Window.MouseWheel(Center, new Vector(0, notches));
            Layout();
        }

        /// <summary>A press and release on the thread (stands in for grabbing the scrollbar).</summary>
        public void Touch()
        {
            Window.MouseDown(Center, MouseButton.Left);
            Window.MouseUp(Center, MouseButton.Left);
            Layout();
        }

        public void SetOffset(double y)
        {
            Scroll.Offset = new Vector(0, y);
            Layout();
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    #region Following

    [AvaloniaFact]
    public void The_First_Content_Lands_At_The_Bottom_Without_Motion()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(700, rig.Bottom, 1);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.False(rig.Scroller.IsChasing);
        Assert.Contains(":following", rig.Scroller.Classes);
        Assert.False(rig.Scroller.IsJumpButtonShown);
        Assert.False(rig.Jump.IsVisible);
    }

    [AvaloniaFact]
    public void Growth_While_Following_Chases_22_Percent_Of_The_Rest_Per_Frame()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        var start = rig.Offset;
        rig.Grow(100);
        Assert.Equal(start, rig.Offset, 1);
        Assert.True(rig.Scroller.IsChasing);
        for (var n = 1; n <= 6; n++)
        {
            rig.Frames(1);
            var expected = rig.Bottom - 100 * Math.Pow(0.78, n);
            Assert.Equal(expected, rig.Offset, 1);
        }
        rig.Frames(20);
        Assert.Equal(rig.Bottom, rig.Offset, 3);
        Assert.False(rig.Scroller.IsChasing);
        Assert.True(rig.Scroller.IsFollowing);
    }

    [AvaloniaFact]
    public void The_Chase_Settles_Within_About_18_Frames_And_Snaps_The_Last_Pixel()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Grow(79);
        var frames = 0;
        while (rig.Scroller.IsChasing && frames < 60)
        {
            rig.Frames(1);
            frames++;
        }
        // 79 × 0.78ⁿ < 1 after 18 frames (Arc: 16 frames at whole-pixel scrollTop); the last step lands exactly.
        Assert.InRange(frames, 16, 19);
        Assert.Equal(rig.Bottom, rig.Offset, 3);
    }

    [AvaloniaFact]
    public void A_Target_That_Keeps_Moving_Is_Followed_Every_Frame()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        for (var i = 0; i < 10; i++)
        {
            rig.Grow(20);
            rig.Frames(1);
            Assert.True(rig.Scroller.IsFollowing);
        }
        Assert.True(rig.Bottom - rig.Offset < 100);
        rig.Frames(40);
        Assert.Equal(rig.Bottom, rig.Offset, 3);
    }

    [AvaloniaFact]
    public void Reduced_And_Off_Jump_To_The_Bottom()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = new Rig(level);
            rig.Grow(300);
            Assert.Equal(rig.Bottom, rig.Offset, 1);
            Assert.False(rig.Scroller.IsChasing);
        }
    }

    [AvaloniaFact]
    public void A_Smaller_Viewport_Is_Followed_Too()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Window.Height = 200;
        rig.Layout();
        Assert.Equal(800, rig.Bottom, 1);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
    }

    [AvaloniaFact]
    public void The_Chase_Only_Moves_The_Offset_And_Does_Not_Measure_The_Thread()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Grow(200);
        var measures = rig.Thread.Measures;
        rig.Frames(10);
        Assert.True(rig.Scroller.IsChasing || Math.Abs(rig.Bottom - rig.Offset) < 0.5);
        Assert.Equal(measures, rig.Thread.Measures);
    }

    #endregion

    #region Release and re-attach

    [AvaloniaFact]
    public void The_Wheel_Up_Releases_And_Growth_Then_Stays_Put()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Wheel(1);
        Assert.False(rig.Scroller.IsFollowing);
        Assert.Equal(1, rig.FollowingChanges);
        Assert.DoesNotContain(":following", rig.Scroller.Classes);
        var offset = rig.Offset;
        Assert.True(offset < rig.Bottom - 6);
        rig.Grow(200);
        rig.Frames(20);
        Assert.Equal(offset, rig.Offset, 1);
        Assert.False(rig.Scroller.IsChasing);
    }

    [AvaloniaFact]
    public void The_Wheel_Up_During_A_Chase_Wins_Over_The_Chase()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Grow(400);
        rig.Frames(2);
        Assert.True(rig.Scroller.IsChasing);
        rig.Wheel(1);
        var offset = rig.Offset;
        Assert.False(rig.Scroller.IsFollowing);
        rig.Frames(10);
        Assert.Equal(offset, rig.Offset, 1);
        Assert.False(rig.Scroller.IsChasing);
    }

    [AvaloniaFact]
    public void The_Persons_Scroll_Of_20px_Releases()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Touch();
        rig.SetOffset(rig.Bottom - 20);
        Assert.False(rig.Scroller.IsFollowing);
        Assert.True(rig.Scroller.IsJumpButtonShown);
    }

    [AvaloniaFact]
    public void A_Key_That_Scrolls_Up_Releases()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Scroll.Focus();
        rig.Window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        rig.Window.KeyReleaseQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
        rig.Layout();
        Assert.True(rig.Offset < rig.Bottom - 6);
        Assert.False(rig.Scroller.IsFollowing);
    }

    [AvaloniaFact]
    public void Programmatic_Offsets_Never_Release()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.SetOffset(100);
        Assert.True(rig.Scroller.IsFollowing);
        // Following means pinned: the scroller goes back to the bottom.
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.False(rig.Scroller.IsJumpButtonShown);
        Assert.Equal(0, rig.FollowingChanges);
    }

    [AvaloniaFact]
    public void Input_Long_Ago_Does_Not_Make_A_Later_Offset_The_Persons()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        var now = DateTime.UtcNow;
        rig.Scroller.Now = () => now;
        rig.Touch();
        now += TimeSpan.FromSeconds(1);
        rig.SetOffset(100);
        Assert.True(rig.Scroller.IsFollowing);
    }

    [AvaloniaFact]
    public void A_Scroll_Down_While_Following_Keeps_Following()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Grow(600);
        rig.Frames(1);
        Assert.True(rig.Scroller.IsChasing);
        rig.Wheel(-1);
        Assert.True(rig.Scroller.IsFollowing);
        rig.Frames(40);
        Assert.Equal(rig.Bottom, rig.Offset, 3);
    }

    [AvaloniaFact]
    public void Coming_Back_Within_6px_Follows_Again_And_8px_Does_Not()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Wheel(2);
        Assert.False(rig.Scroller.IsFollowing);
        rig.Touch();
        rig.SetOffset(rig.Bottom - 8);
        Assert.False(rig.Scroller.IsFollowing);
        Assert.True(rig.Scroller.IsJumpButtonShown);
        rig.Touch();
        rig.SetOffset(rig.Bottom - 5);
        Assert.True(rig.Scroller.IsFollowing);
        Assert.False(rig.Scroller.IsJumpButtonShown);
        rig.Grow(100);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
    }

    [AvaloniaFact]
    public void The_Wheel_Back_To_The_Bottom_Follows_Again()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Wheel(2);
        Assert.False(rig.Scroller.IsFollowing);
        rig.Wheel(-3);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.True(rig.Scroller.IsFollowing);
    }

    [AvaloniaFact]
    public void ScrollToLatest_Follows_Again_And_Chases_At_Full()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Wheel(4);
        Assert.False(rig.Scroller.IsFollowing);
        var from = rig.Offset;
        rig.Scroller.ScrollToLatest();
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(from, rig.Offset, 1);
        rig.Frames(1);
        Assert.Equal(rig.Bottom - (rig.Bottom - from) * 0.78, rig.Offset, 1);
        rig.Frames(40);
        Assert.Equal(rig.Bottom, rig.Offset, 3);
    }

    [AvaloniaFact]
    public void ScrollToLatest_Jumps_At_Off_Or_When_Asked()
    {
        using (var rig = new Rig(ArcMotionLevel.Off))
        {
            rig.Wheel(4);
            rig.Scroller.ScrollToLatest();
            Assert.Equal(rig.Bottom, rig.Offset, 1);
        }
        using (var rig = new Rig(ArcMotionLevel.Full))
        {
            rig.Wheel(4);
            rig.Scroller.ScrollToLatest(animate: false);
            Assert.Equal(rig.Bottom, rig.Offset, 1);
            Assert.False(rig.Scroller.IsChasing);
        }
    }

    [AvaloniaFact]
    public void Content_That_Shrinks_Clamps_And_Keeps_Following()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Grow(-200);
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
    }

    #endregion

    #region Scroll-to-latest button

    [AvaloniaFact]
    public void The_Button_Is_A_Named_34px_Circle_Centered_10px_Above_The_Bottom()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Wheel(2);
        Assert.True(rig.Jump.IsVisible);
        Assert.Equal(1, rig.Jump.Opacity);
        Assert.Equal(34, rig.Jump.Bounds.Width);
        Assert.Equal(34, rig.Jump.Bounds.Height);
        Assert.Equal(JumpName, AutomationProperties.GetName(rig.Jump));
        var center = rig.Jump.Bounds.Center;
        Assert.Equal(rig.Scroller.Bounds.Width / 2, center.X, 1);
        Assert.Equal(rig.Scroller.Bounds.Height - 10, rig.Jump.Bounds.Bottom, 1);
    }

    [AvaloniaFact]
    public void The_Button_Shows_Only_While_Released_With_Content_Below()
    {
        using var rig = new Rig(ArcMotionLevel.Off, height: 200);
        // Nothing to scroll: the wheel does nothing and the button never shows.
        rig.Wheel(2);
        Assert.True(rig.Scroller.IsFollowing);
        Assert.False(rig.Jump.IsVisible);
    }

    [AvaloniaFact]
    public void Clicking_The_Button_Scrolls_To_The_Latest()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Wheel(4);
        var point = rig.Jump.TranslatePoint(new Point(17, 17), rig.Window) ?? default;
        rig.Window.MouseDown(point, MouseButton.Left);
        rig.Window.MouseUp(point, MouseButton.Left);
        rig.Layout();
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.False(rig.Jump.IsVisible);
    }

    [AvaloniaFact]
    public void Enter_On_The_Focused_Button_Scrolls_And_Leaves_Focus_In_The_Thread()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.Wheel(4);
        rig.Jump.Focus(NavigationMethod.Tab);
        rig.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        rig.Layout();
        Assert.True(rig.Scroller.IsFollowing);
        Assert.False(rig.Jump.IsVisible);
        Assert.True(rig.Scroll.IsFocused);
    }

    [AvaloniaFact]
    public void The_Button_Fades_In_In_160ms_And_Out_In_100ms()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.Wheel(4);
        Assert.True(rig.Jump.IsVisible);
        Assert.True(rig.Jump.Opacity < 0.05);
        rig.Frames(5);
        Assert.InRange(rig.Jump.Opacity, 0.3, 0.99);
        rig.Frames(6);
        Assert.Equal(1, rig.Jump.Opacity, 3);

        rig.Scroller.ScrollToLatest(animate: false);
        Assert.False(rig.Jump.IsHitTestVisible);
        Assert.True(rig.Jump.IsVisible);
        rig.Frames(3);
        Assert.InRange(rig.Jump.Opacity, 0.01, 0.9);
        Assert.True(rig.Jump.IsVisible);
        rig.Frames(5);
        Assert.False(rig.Jump.IsVisible);
        Assert.Equal(0, rig.Jump.Opacity, 3);
    }

    [AvaloniaFact]
    public void Reduced_Fades_The_Button_In_120ms()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced);
        rig.Wheel(4);
        rig.Frames(4);
        Assert.InRange(rig.Jump.Opacity, 0.2, 0.9);
        rig.Frames(5);
        Assert.Equal(1, rig.Jump.Opacity, 3);
    }

    #endregion

    #region Offsets per conversation

    [AvaloniaFact]
    public void Offsets_Are_Kept_Per_Key_And_Unknown_Keys_Land_At_The_Bottom()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        var first = new object();
        var second = new object();
        rig.Touch();
        rig.SetOffset(120);
        Assert.False(rig.Scroller.IsFollowing);
        rig.Scroller.SaveOffset(first);

        // Another conversation that was never saved: its bottom, at once.
        rig.Scroller.Content = new Border { Height = 1500 };
        rig.Scroller.RestoreOffset(second);
        rig.Layout();
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(1200, rig.Bottom, 1);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.False(rig.Scroller.IsChasing);
        rig.Scroller.SaveOffset(second);

        // Back to the first: its offset, not following, the button shown.
        rig.Scroller.Content = rig.Thread;
        rig.Scroller.RestoreOffset(first);
        rig.Layout();
        Assert.Equal(120, rig.Offset, 1);
        Assert.False(rig.Scroller.IsFollowing);
        Assert.True(rig.Scroller.IsJumpButtonShown);

        // The second was following: it lands at its (grown) bottom.
        rig.Scroller.Content = new Border { Height = 1800 };
        rig.Scroller.RestoreOffset(second);
        rig.Layout();
        Assert.True(rig.Scroller.IsFollowing);
        Assert.Equal(rig.Bottom, rig.Offset, 1);
        Assert.False(rig.Scroller.IsChasing);
    }

    [AvaloniaFact]
    public void A_Saved_Offset_Past_The_New_Bottom_Is_Clamped()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        var key = "conversation";
        rig.Touch();
        rig.SetOffset(400);
        rig.Scroller.SaveOffset(key);
        rig.Scroller.Content = new Border { Height = 500 };
        rig.Scroller.RestoreOffset(key);
        rig.Layout();
        Assert.Equal(200, rig.Bottom, 1);
        Assert.Equal(200, rig.Offset, 1);
        Assert.True(rig.Scroller.IsFollowing);
    }

    [AvaloniaFact]
    public void Restoring_The_Same_Content_Works_Without_Another_Change()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        var key = new object();
        rig.Touch();
        rig.SetOffset(300);
        rig.Scroller.SaveOffset(key);
        rig.Touch();
        rig.SetOffset(50);
        rig.Scroller.RestoreOffset(key);
        rig.Layout();
        Assert.Equal(300, rig.Offset, 1);
    }

    #endregion
}
