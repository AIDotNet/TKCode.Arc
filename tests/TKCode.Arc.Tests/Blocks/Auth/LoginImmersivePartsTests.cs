using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class LoginImmersivePartsTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Host(Control content, ArcMotionLevel level, double width = 500, double height = 500)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = height;
        LoginImmersiveTests.Settle();
        return window;
    }

    private static Border View(double height) => new() { Height = height, Background = Avalonia.Media.Brushes.White };

    [AvaloniaFact]
    public void Step_Host_Shows_The_First_Child_And_Swaps_At_Once_When_Motion_Is_Off()
    {
        var host = new LoginImmersiveStepHost { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var a = View(100);
        var b = View(160);
        host.Children.Add(a);
        host.Children.Add(b);
        var window = Host(host, ArcMotionLevel.Off);
        try
        {
            host.Current.ShouldBe(a);
            b.IsVisible.ShouldBeFalse();
            host.Bounds.Height.ShouldBe(100);
            host.Show(b);
            LoginImmersiveTests.Settle();
            a.IsVisible.ShouldBeFalse();
            b.IsVisible.ShouldBeTrue();
            host.ExitingCount.ShouldBe(0);
            host.Bounds.Height.ShouldBe(160);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Step_Host_Crosses_Views_And_Springs_Its_Height_Under_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var host = new LoginImmersiveStepHost { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var a = View(100);
        var b = View(200);
        host.Children.Add(a);
        host.Children.Add(b);
        var window = Host(host, ArcMotionLevel.Full);
        try
        {
            host.Show(b);
            LoginImmersiveTests.Settle();
            host.ExitingCount.ShouldBe(1);
            a.IsVisible.ShouldBeTrue();
            b.Opacity.ShouldBe(0);
            ((Avalonia.Media.TranslateTransform)b.RenderTransform!).Y.ShouldBe(12, 0.01);
            host.IsGliding.ShouldBeTrue();

            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            LoginImmersiveTests.Settle();
            // The leaving view is clear after 120 ms and up 8 after 180 ms.
            a.Opacity.ShouldBeLessThan(0.2);
            ((Avalonia.Media.TranslateTransform)a.RenderTransform!).Y.ShouldBeLessThan(-6);
            host.Bounds.Height.ShouldBeGreaterThan(100);
            host.Bounds.Height.ShouldBeLessThan(200);

            clock.Advance(TimeSpan.FromMilliseconds(1200), Frame);
            LoginImmersiveTests.Settle();
            host.ExitingCount.ShouldBe(0);
            a.IsVisible.ShouldBeFalse();
            b.Opacity.ShouldBe(1, 0.001);
            ((Avalonia.Media.TranslateTransform)b.RenderTransform!).Y.ShouldBe(0, 0.01);
            host.IsGliding.ShouldBeFalse();
            host.Bounds.Height.ShouldBe(200, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Step_Host_Only_Fades_Under_Reduced_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var host = new LoginImmersiveStepHost { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        var a = View(100);
        var b = View(200);
        host.Children.Add(a);
        host.Children.Add(b);
        var window = Host(host, ArcMotionLevel.Reduced);
        try
        {
            host.Show(b);
            LoginImmersiveTests.Settle();
            a.IsVisible.ShouldBeFalse();
            host.ExitingCount.ShouldBe(0);
            host.IsGliding.ShouldBeFalse();
            ((Avalonia.Media.TranslateTransform)b.RenderTransform!).Y.ShouldBe(0);
            b.Opacity.ShouldBe(0);
            host.Bounds.Height.ShouldBe(200);
            clock.Advance(TimeSpan.FromMilliseconds(130), Frame);
            b.Opacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Envelope_Drops_The_Letter_Then_Folds_The_Flap()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var envelope = new LoginImmersiveEnvelope { InkBrush = Avalonia.Media.Brushes.Black, PaperBrush = Avalonia.Media.Brushes.White };
        var window = Host(envelope, ArcMotionLevel.Full);
        try
        {
            envelope.Flap.ShouldBe(1);
            envelope.Replay();
            envelope.Flap.ShouldBe(-1);
            envelope.LetterOffset.ShouldBe(-30);
            envelope.LetterOpacity.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(150), Frame);
            envelope.LetterOffset.ShouldBe(-30);
            clock.Advance(TimeSpan.FromMilliseconds(250), Frame);
            // About 400 ms in: the letter is nearly in and the flap has not moved.
            envelope.LetterOffset.ShouldBeGreaterThan(-8);
            envelope.LetterOpacity.ShouldBe(1, 0.001);
            envelope.Flap.ShouldBe(-1);
            clock.Advance(TimeSpan.FromMilliseconds(320), Frame);
            // 720 ms: the flap crosses flat.
            envelope.Flap.ShouldBeGreaterThan(-0.4);
            envelope.Flap.ShouldBeLessThan(0.4);
            clock.Advance(TimeSpan.FromMilliseconds(1200), Frame);
            envelope.Flap.ShouldBe(1, 0.01);
            envelope.LetterOffset.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Envelope_Shows_Closed_Under_Reduced_Motion()
    {
        var envelope = new LoginImmersiveEnvelope { InkBrush = Avalonia.Media.Brushes.Black };
        var window = Host(envelope, ArcMotionLevel.Reduced);
        try
        {
            envelope.Replay();
            envelope.Flap.ShouldBe(1);
            envelope.LetterOffset.ShouldBe(0);
            envelope.LetterOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Mark_Draws_The_Ring_Then_The_Check()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var mark = new LoginImmersiveDoneMark { Foreground = Avalonia.Media.Brushes.Green };
        var window = Host(mark, ArcMotionLevel.Full);
        try
        {
            mark.Replay();
            mark.Ring.ShouldBe(0);
            mark.Check.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(130), Frame);
            mark.Ring.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            mark.Ring.ShouldBeGreaterThan(0.2);
            mark.Ring.ShouldBeLessThan(0.9);
            mark.Check.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
            mark.Ring.ShouldBe(1, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(600), Frame);
            mark.Check.ShouldBe(1, 0.01);
            mark.CheckOpacity.ShouldBe(1, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Done_Mark_Is_Drawn_At_Once_Under_Reduced_Motion()
    {
        var mark = new LoginImmersiveDoneMark { Foreground = Avalonia.Media.Brushes.Green };
        var window = Host(mark, ArcMotionLevel.Reduced);
        try
        {
            mark.Replay();
            mark.Ring.ShouldBe(1);
            mark.Check.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Formats_Minutes_And_Seconds()
    {
        LoginImmersiveCountdown.Format(30).ShouldBe("0:30");
        LoginImmersiveCountdown.Format(5).ShouldBe("0:05");
        LoginImmersiveCountdown.Format(125).ShouldBe("2:05");
        LoginImmersiveCountdown.Format(-3).ShouldBe("0:00");
    }

    [AvaloniaFact]
    public void Countdown_Rolls_Only_The_Changed_Columns()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var countdown = new LoginImmersiveCountdown { Seconds = 30 };
        var window = Host(countdown, ArcMotionLevel.Full);
        try
        {
            countdown.Text.ShouldBe("0:30");
            countdown.Children.Count.ShouldBe(4);
            countdown.Seconds = 29;
            LoginImmersiveTests.Settle();
            countdown.Text.ShouldBe("0:29");
            // "3" and "0" leave; "2" and "9" arrive; "0" and ":" stay.
            countdown.LeavingCount.ShouldBe(2);
            countdown.Children.Count.ShouldBe(6);
            clock.Advance(TimeSpan.FromMilliseconds(800), Frame);
            LoginImmersiveTests.Settle();
            countdown.LeavingCount.ShouldBe(0);
            countdown.Children.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Changes_At_Once_Under_Reduced_Motion()
    {
        var countdown = new LoginImmersiveCountdown { Seconds = 30 };
        var window = Host(countdown, ArcMotionLevel.Reduced);
        try
        {
            countdown.Seconds = 29;
            LoginImmersiveTests.Settle();
            countdown.LeavingCount.ShouldBe(0);
            countdown.Children.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Blur_Box_Sizes_Match_The_Gaussian_Variance()
    {
        var sizes = LoginImmersiveBlur.BoxSizes(7);
        sizes.Length.ShouldBe(3);
        sizes.ShouldAllBe(s => s % 2 == 1);
        // Each box of width w adds (w² − 1) / 12 to the variance; three of them make σ².
        var variance = sizes.Sum(w => (w * w - 1) / 12.0);
        variance.ShouldBe(49, 6);
    }

    [AvaloniaFact]
    public void Blur_Keeps_Flat_Colour_And_Spreads_A_Point_Evenly()
    {
        const int Size = 21;
        var flat = new byte[Size * Size * 4];
        for (var i = 0; i < flat.Length; i += 4)
        {
            flat[i] = 40;
            flat[i + 1] = 80;
            flat[i + 2] = 120;
            flat[i + 3] = 255;
        }
        LoginImmersiveBlur.Apply(flat, Size, Size, Size * 4, 3, 1, redFirst: false);
        flat[(10 * Size + 10) * 4].ShouldBe((byte)40);
        flat[(0 * Size + 20) * 4 + 2].ShouldBe((byte)120);

        var point = new byte[Size * Size * 4];
        point[(10 * Size + 10) * 4 + 1] = 255;
        point[(10 * Size + 10) * 4 + 3] = 255;
        LoginImmersiveBlur.Apply(point, Size, Size, Size * 4, 2, 1, redFirst: false);
        var center = point[(10 * Size + 10) * 4 + 1];
        center.ShouldBeLessThan((byte)255);
        point[(10 * Size + 13) * 4 + 1].ShouldBe(point[(10 * Size + 7) * 4 + 1]);
        point[(13 * Size + 10) * 4 + 1].ShouldBe(point[(7 * Size + 10) * 4 + 1]);
        point[(10 * Size + 13) * 4 + 1].ShouldBeLessThan(center);
    }

    [AvaloniaFact]
    public void Saturate_Leaves_Grey_Alone_And_Strengthens_Colour()
    {
        var pixels = new byte[] { 100, 100, 100, 255, 90, 120, 160, 255 };
        LoginImmersiveBlur.Apply(pixels, 2, 1, 8, 0, 1.6, redFirst: true);
        pixels[0].ShouldBe((byte)100);
        pixels[1].ShouldBe((byte)100);
        pixels[2].ShouldBe((byte)100);
        // Red stays below, blue rises: the spread between them grows.
        (pixels[6] - pixels[4]).ShouldBeGreaterThan(160 - 90);
    }

    [AvaloniaFact]
    public void Cover_Transform_Fills_And_Aligns_Like_Object_Position()
    {
        var m = LoginImmersiveScene.CoverTransform(new Size(1600, 1000), new Rect(0, 0, 800, 800), new Point(0.5, 0.58));
        m.M11.ShouldBe(0.8, 1e-9);
        m.M22.ShouldBe(0.8, 1e-9);
        m.M31.ShouldBe((800 - 1280) * 0.5, 1e-9);
        m.M32.ShouldBe(0, 1e-9);
        var wide = LoginImmersiveScene.CoverTransform(new Size(1600, 1000), new Rect(0, 0, 3200, 1000), new Point(0.5, 0.58));
        wide.M11.ShouldBe(2, 1e-9);
        wide.M32.ShouldBe((1000 - 2000) * 0.58, 1e-9);
    }

    [AvaloniaFact]
    public void Scene_Is_Built_Once_With_Sky_Ridges_And_Trees()
    {
        var ops = LoginImmersiveScene.Ops;
        ops.Count.ShouldBeGreaterThan(10);
        ReferenceEquals(ops, LoginImmersiveScene.Ops).ShouldBeTrue();
        ops[0].Geometry.Bounds.ShouldBe(new Rect(LoginImmersiveScene.DesignSize));
        LoginImmersiveScene.Hash(3, 7).ShouldBe(LoginImmersiveScene.Hash(3, 7));
        LoginImmersiveScene.Fbm(1.25, 11, 4).ShouldBeInRange(0, 1);
    }

    [AvaloniaFact]
    public void Footer_Puts_The_Links_On_Their_Own_Line_When_The_Note_Cannot_Fit()
    {
        var panel = new LoginImmersiveFooterPanel();
        panel.Children.Add(new TextBlock { Text = "Demo note", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(new Border { Width = 100, Height = 32 });
        panel.Measure(new Size(600, double.PositiveInfinity));
        panel.IsWrapped.ShouldBeFalse();
        panel.Arrange(new Rect(0, 0, 600, panel.DesiredSize.Height));
        panel.Children[1].Bounds.X.ShouldBe(500);
        panel.Measure(new Size(330, double.PositiveInfinity));
        panel.IsWrapped.ShouldBeTrue();
        panel.Arrange(new Rect(0, 0, 330, panel.DesiredSize.Height));
        panel.Children[1].Bounds.X.ShouldBe(0);
        panel.Children[1].Bounds.Y.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Equal_Row_Splits_The_Width_Into_Equal_Cells()
    {
        var row = new LoginImmersiveEqualRow { Gap = 8 };
        for (var i = 0; i < 3; i++)
        {
            row.Children.Add(new Border { Height = 44 });
        }
        row.Measure(new Size(354, 100));
        row.Arrange(new Rect(0, 0, 354, 44));
        // Layout rounding snaps the cells to whole pixels.
        row.Children[0].Bounds.Width.ShouldBe(112.667, 1);
        row.Children[1].Bounds.X.ShouldBe(120.667, 1);
        row.Children[2].Bounds.Right.ShouldBe(354, 1);
    }

    [AvaloniaFact]
    public void Switch_Crosses_Its_Two_Sentences()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var toggle = new LoginImmersiveSwitch { SignUpPrompt = "No account?", SignUpAction = "Sign up", SignInPrompt = "Have an account?", SignInAction = "Sign in" };
        var window = Host(toggle, ArcMotionLevel.Full);
        try
        {
            toggle.GroupOpacity.ShouldBe((1, 0));
            toggle.IsSignUp = true;
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            toggle.GroupOpacity.SignIn.ShouldBeLessThan(1);
            toggle.GroupOpacity.SignUp.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            toggle.GroupOpacity.ShouldBe((0, 1));
            Avalonia.Automation.AutomationProperties.GetName(toggle).ShouldBe("Have an account? Sign in");
        }
        finally
        {
            window.Close();
        }
    }
}
