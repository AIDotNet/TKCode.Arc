using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Auth;

public class LoginSplitPartsTests
{
    [Theory]
    [InlineData("  Abc.Def@Example.COM  ", "abc.def@example.com")]
    [InlineData("rowan@fieldnote.app", "rowan@fieldnote.app")]
    [InlineData(null, "")]
    public void Addresses_Are_Trimmed_And_Lower_Cased(string? email, string expected) =>
        LoginSplitIdentity.Normalize(email).ShouldBe(expected);

    [Theory]
    [InlineData("abcx.iod@x.io", "Abcx Iod", "AI", "Abcx")]
    [InlineData("abc.def@example.com", "Abc Def", "AD", "Abc")]
    [InlineData("maya@x.io", "Maya", "M", "Maya")]
    [InlineData("ada_lovelace+work@x.io", "Ada Lovelace Work", "AL", "Ada")]
    [InlineData("jean-luc@x.io", "Jean Luc", "JL", "Jean")]
    [InlineData("..@x.io", "Member", "M", "Member")]
    public void Unknown_Addresses_Become_Names(string email, string name, string initials, string greeting)
    {
        var account = LoginSplitAccount.FromEmail(email);
        account.Name.ShouldBe(name);
        account.Email.ShouldBe(email);
        Avatar.ComputeInitials(account.Name).ShouldBe(initials);
        account.GreetingName.ShouldBe(greeting);
    }

    [Fact]
    public void Known_Accounts_Are_Found_By_Email()
    {
        var known = new[] { new LoginSplitAccount("Rowan Hale", "Rowan@Fieldnote.app"), new LoginSplitAccount("Iris Vance", "iris@fieldnote.app") };
        LoginSplitIdentity.Resolve(" rowan@fieldnote.APP", known).ShouldBeSameAs(known[0]);
        LoginSplitIdentity.Resolve("iris@fieldnote.app", known).ShouldBeSameAs(known[1]);
        LoginSplitIdentity.Resolve("teo.marsh@fieldnote.app", known).Name.ShouldBe("Teo Marsh");
    }

    [Theory]
    [InlineData(30, "0:30")]
    [InlineData(29, "0:29")]
    [InlineData(9, "0:09")]
    [InlineData(1, "0:01")]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(-3, "0:00")]
    public void Countdown_Reads_Minutes_And_Two_Digit_Seconds(int seconds, string expected) =>
        LoginSplitIdentity.Countdown(seconds).ShouldBe(expected);

    [Theory]
    [InlineData("a@b.c", false)]
    [InlineData("a@b.co", true)]
    [InlineData("a b@c.de", false)]
    [InlineData("a@b@c.de", false)]
    [InlineData("  a@b.de  ", true)]
    [InlineData("x@gmial.com", true)]
    [InlineData("", false)]
    [InlineData("ada@", false)]
    public void Email_Rule_Matches_The_Reference(string email, bool valid) => SiteEmail.IsValid(email).ShouldBe(valid);

    [Fact]
    public void Default_Timings_Are_The_Measured_Ones()
    {
        var timings = new LoginSplitTimings();
        timings.Lookup.TotalMilliseconds.ShouldBe(650);
        timings.PasswordCheck.TotalMilliseconds.ShouldBe(800);
        timings.AcceptedHold.TotalMilliseconds.ShouldBe(775);
        timings.Signing.TotalMilliseconds.ShouldBe(1700);
        timings.SigningReduced.TotalMilliseconds.ShouldBe(400);
        timings.Provider.TotalMilliseconds.ShouldBe(1000);
        timings.ResetSend.TotalMilliseconds.ShouldBe(900);
        timings.ProgressDelay.TotalMilliseconds.ShouldBe(260);
        timings.ProgressDuration.TotalMilliseconds.ShouldBe(1400);
    }

    [Fact]
    public void Springs_Have_No_Bounce()
    {
        LoginSplitMotion.Smooth.DampingRatio.ShouldBe(1, 0.0001);
        LoginSplitMotion.Morph.DampingRatio.ShouldBe(1, 0.0001);
        LoginSplitMotion.Snappy.DampingRatio.ShouldBe(1, 0.0001);
        LoginSplitMotion.Smooth.NaturalFrequency.ShouldBe(13.09, 0.05);
        LoginSplitMotion.Morph.NaturalFrequency.ShouldBe(12.47, 0.05);
        LoginSplitMotion.Snappy.NaturalFrequency.ShouldBe(20.14, 0.05);
    }

    [Fact]
    public void Default_Labels_Format_Their_Values()
    {
        var labels = new LoginSplitLabels();
        labels.ResendAvailableIn(1).ShouldBe("Resend link, available in 1 second");
        labels.ResendAvailableIn(12).ShouldBe("Resend link, available in 12 seconds");
        labels.DoneTitle("Rowan").ShouldBe("All set, Rowan");
        labels.SigningTitle("Fieldnote").ShouldBe("Opening Fieldnote…");
        labels.DemoPasswordHint!("fieldnote-demo").ShouldBe("Demo password: fieldnote-demo.");
        labels.ChangeEmail("a@b.co").ShouldBe("a@b.co, change email");
    }

    [Theory]
    [InlineData(1000, 1500, 400, 900, 0.5, 0.58, 166.6667, 0, 666.6667, 1500)]
    [InlineData(1600, 900, 400, 900, 0.5, 0.58, 600, 0, 400, 900)]
    [InlineData(1000, 1000, 800, 400, 0.5, 0.58, 0, 290, 1000, 500)]
    public void Cover_Crops_Around_The_Focal_Point(double iw, double ih, double tw, double th, double ax, double ay,
                                                   double x, double y, double w, double h)
    {
        var rect = LoginSplitBackdrop.CoverSource(new Size(iw, ih), new Size(tw, th), new Point(ax, ay));
        rect.X.ShouldBe(x, 0.01);
        rect.Y.ShouldBe(y, 0.01);
        rect.Width.ShouldBe(w, 0.01);
        rect.Height.ShouldBe(h, 0.01);
        (rect.Width / rect.Height).ShouldBe(tw / th, 0.001);
    }

    [AvaloniaFact]
    public void Viewport_Steps_Enter_From_Below_And_The_Old_One_Leaves_Up()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var viewport = new LoginSplitViewport { Width = 384 };
        var window = TestHelpers.Show(viewport, ArcMotionLevel.Full);
        try
        {
            var first = new Border { Height = 300 };
            viewport.Show(first, 0);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            viewport.ShownHeight.ShouldBe(300, 0.01);

            var second = new Border { Height = 200 };
            viewport.Show(second, 1);
            Dispatcher.UIThread.RunJobs();
            viewport.LeavingCount.ShouldBe(1);
            first.IsHitTestVisible.ShouldBeFalse();
            ((Avalonia.Media.TranslateTransform)second.RenderTransform!).Y.ShouldBe(14, 0.01);
            second.Opacity.ShouldBe(0);
            viewport.IsGliding.ShouldBeTrue();

            clock.Advance(TimeSpan.FromMilliseconds(40));
            second.Opacity.ShouldBe(0, 0.001);
            clock.Advance(TimeSpan.FromMilliseconds(260));
            second.Opacity.ShouldBe(1, 0.05);
            first.Opacity.ShouldBe(0, 0.001);
            ((Avalonia.Media.TranslateTransform)first.RenderTransform!).Y.ShouldBeLessThan(-5);
            window.UpdateLayout();
            viewport.ShownHeight.ShouldBeLessThan(300);
            viewport.ShownHeight.ShouldBeGreaterThan(200);

            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            viewport.LeavingCount.ShouldBe(0);
            viewport.IsGliding.ShouldBeFalse();
            ((Avalonia.Media.TranslateTransform)second.RenderTransform!).Y.ShouldBe(0, 0.01);
            viewport.ShownHeight.ShouldBe(200, 0.01);

            var third = new Border { Height = 250 };
            viewport.Show(third, -1);
            Dispatcher.UIThread.RunJobs();
            ((Avalonia.Media.TranslateTransform)third.RenderTransform!).Y.ShouldBe(-14, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            ((Avalonia.Media.TranslateTransform)second.RenderTransform!).Y.ShouldBeGreaterThan(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Viewport_Under_Reduced_Motion_Drops_The_Old_Step_And_Fades_The_New_One()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var viewport = new LoginSplitViewport { Width = 384 };
        var window = TestHelpers.Show(viewport, ArcMotionLevel.Reduced);
        try
        {
            viewport.Show(new Border { Height = 300 }, 0);
            Dispatcher.UIThread.RunJobs();
            var next = new Border { Height = 200 };
            viewport.Show(next, 1);
            Dispatcher.UIThread.RunJobs();
            viewport.LeavingCount.ShouldBe(0);
            viewport.IsGliding.ShouldBeFalse();
            ((Avalonia.Media.TranslateTransform)next.RenderTransform!).Y.ShouldBe(0);
            next.Opacity.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(130));
            next.Opacity.ShouldBe(1, 0.001);
            next.Effect.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Rolls_Only_The_Changed_Columns()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var countdown = new LoginSplitCountdown { Seconds = 30 };
        var window = TestHelpers.Show(countdown, ArcMotionLevel.Full);
        try
        {
            countdown.Text.ShouldBe("0:30");
            countdown.LeavingCount.ShouldBe(0);
            countdown.Seconds = 29;
            countdown.Text.ShouldBe("0:29");
            countdown.LeavingCount.ShouldBe(2);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            countdown.LeavingCount.ShouldBe(0);
            countdown.Seconds = 28;
            countdown.LeavingCount.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Countdown_Changes_At_Once_Without_Full_Motion()
    {
        var countdown = new LoginSplitCountdown { Seconds = 10 };
        var window = TestHelpers.Show(countdown, ArcMotionLevel.Reduced);
        try
        {
            countdown.Seconds = 9;
            countdown.Text.ShouldBe("0:09");
            countdown.LeavingCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Submit_Slot_Narrows_To_A_Circle_On_The_Morph_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var button = new Button { Content = "Sign in", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        var slot = new LoginSplitSubmitSlot { Child = button, Width = 360 };
        var window = TestHelpers.Show(slot, ArcMotionLevel.Full);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            button.Bounds.Width.ShouldBe(360, 0.01);
            slot.Collapse(44);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            Dispatcher.UIThread.RunJobs();
            slot.ShownWidth.ShouldBeLessThan(360);
            slot.ShownWidth.ShouldBeGreaterThan(44);
            clock.Advance(TimeSpan.FromMilliseconds(900));
            Dispatcher.UIThread.RunJobs();
            slot.ShownWidth.ShouldBe(44, 0.5);
            button.Bounds.Width.ShouldBe(44, 0.5);
            button.Bounds.X.ShouldBe((360 - 44) / 2.0, 0.5);
            button.CornerRadius.TopLeft.ShouldBe(22, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collapse_Opens_And_Closes_Its_Row()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var row = new LoginSplitCollapse { IsOpen = false, Child = new Border { Height = 52 } };
        var window = TestHelpers.Show(new StackPanel { Children = { row } }, ArcMotionLevel.Full);
        try
        {
            row.Bounds.Height.ShouldBe(0);
            row.Child!.IsVisible.ShouldBeFalse();
            row.IsOpen = true;
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Dispatcher.UIThread.RunJobs();
            row.Fraction.ShouldBeGreaterThan(0.1);
            row.Fraction.ShouldBeLessThan(0.99);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            row.Bounds.Height.ShouldBe(52, 0.01);
            row.IsOpen = false;
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            row.Bounds.Height.ShouldBe(0, 0.01);
            row.Child.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Footer_Wraps_The_Links_Under_A_Long_Status()
    {
        var status = new Border { Width = 220, Height = 16.8 };
        var links = new Border { Width = 90, Height = 16.8 };
        var footer = new LoginSplitFooterPanel { Children = { status, links } };
        var window = TestHelpers.Show(footer);
        try
        {
            window.Width = 400;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeFalse();
            links.Bounds.Right.ShouldBe(400, 0.5);
            window.Width = 320;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeTrue();
            links.Bounds.X.ShouldBe(0);
            links.Bounds.Y.ShouldBe(16.8 + 8, 0.5);
            status.Width = 0;
            Dispatcher.UIThread.RunJobs();
            footer.IsWrapped.ShouldBeFalse();
            links.Bounds.Right.ShouldBe(320, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
