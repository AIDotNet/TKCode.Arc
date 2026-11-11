using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Avatars;

public class AvatarTests
{
    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("  grace   brewster hopper ", "GB")]
    [InlineData("Plato", "P")]
    [InlineData("", "")]
    public void Initials_Use_The_First_Two_Words(string name, string expected) => Avatar.ComputeInitials(name).ShouldBe(expected);

    [AvaloniaFact]
    public void Sizes_Follow_Arc()
    {
        var avatar = new Avatar { DisplayName = "Alan Turing", Size = AvatarSize.Large, Status = AvatarStatus.Online };
        var window = TestHelpers.Show(avatar);
        try
        {
            avatar.Bounds.Width.ShouldBe(48);
            avatar.Initials.ShouldBe("AT");
            avatar.Size = AvatarSize.XLarge;
            Dispatcher.UIThread.RunJobs();
            avatar.Bounds.Width.ShouldBe(88);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Online_Status_Uses_The_Success_Color()
    {
        var avatar = new Avatar { DisplayName = "Leah Fox", Status = AvatarStatus.Offline };
        var window = TestHelpers.Show(avatar);
        try
        {
            var dot = TestHelpers.Descendant<Avalonia.Controls.Border>(avatar, "PART_Status");
            var offline = TestHelpers.ColorOf(dot.Background);
            avatar.Status = AvatarStatus.Online;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(dot.Background).ShouldNotBe(offline);
            TestHelpers.ColorOf(dot.Background).ShouldBe(ArcTokens.GetColor(avatar, DesignTokens.ArcDesignTokenKind.Success));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Busy_Away_And_Connecting_Have_Their_Own_Look_And_Name()
    {
        // Issue #110: presence beyond online and offline.
        var avatar = new Avatar { DisplayName = "Leah Fox", Status = AvatarStatus.Busy };
        var window = TestHelpers.Show(avatar, Motion.ArcMotionLevel.Full);
        try
        {
            var dot = TestHelpers.Descendant<Avalonia.Controls.Border>(avatar, "PART_Status");
            var bar = TestHelpers.Descendant<Avalonia.Controls.Border>(dot, "PART_StatusBar");
            TestHelpers.ColorOf(dot.Background).ShouldBe(ArcTokens.GetColor(avatar, DesignTokens.ArcDesignTokenKind.Danger));
            bar.IsVisible.ShouldBeTrue();
            avatar.Classes.Contains(Avatar.BusyPseudoClass).ShouldBeTrue();
            Avalonia.Automation.AutomationProperties.GetName(avatar).ShouldBe("Leah Fox, busy");

            // Full motion: the fill crossfades, so wait for it to settle.
            bool Settles(Func<Avalonia.Media.IBrush?> brush, DesignTokens.ArcDesignTokenKind kind) => TestHelpers.PumpUntil(() =>
            {
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return TestHelpers.ColorOf(brush()) == ArcTokens.GetColor(avatar, kind);
            }, 5000);

            avatar.Status = AvatarStatus.Away;
            Dispatcher.UIThread.RunJobs();
            Settles(() => dot.Background, DesignTokens.ArcDesignTokenKind.Warning).ShouldBeTrue();
            bar.IsVisible.ShouldBeFalse();
            Avalonia.Automation.AutomationProperties.GetName(avatar).ShouldBe("Leah Fox, away");

            avatar.Status = AvatarStatus.Connecting;
            Dispatcher.UIThread.RunJobs();
            Settles(() => dot.Background, DesignTokens.ArcDesignTokenKind.Surface).ShouldBeTrue();
            TestHelpers.ColorOf(dot.BorderBrush).ShouldBe(ArcTokens.GetColor(avatar, DesignTokens.ArcDesignTokenKind.TextMuted));
            Avalonia.Automation.AutomationProperties.GetName(avatar).ShouldBe("Leah Fox, connecting");
            // The ring breathes in opacity while connecting ...
            TestHelpers.PumpUntil(() =>
            {
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return dot.Opacity < 0.95;
            }, 5000).ShouldBeTrue();

            // ... and rests once it is connected.
            avatar.Status = AvatarStatus.Online;
            Dispatcher.UIThread.RunJobs();
            dot.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Connecting_Rests_At_Full_Opacity_With_Motion_Off()
    {
        var avatar = new Avatar { DisplayName = "Leah Fox", Status = AvatarStatus.Connecting };
        var window = TestHelpers.Show(avatar);
        try
        {
            var dot = TestHelpers.Descendant<Avalonia.Controls.Border>(avatar, "PART_Status");
            for (var i = 0; i < 20; i++)
            {
                Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            dot.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LargePlus_Is_Appended_Between_Large_And_XLarge()
    {
        // Issue #110: nothing between 48 and 88. The new member is appended, so the earlier values keep their numbers.
        ((int)AvatarSize.Small).ShouldBe(0);
        ((int)AvatarSize.Medium).ShouldBe(1);
        ((int)AvatarSize.Large).ShouldBe(2);
        ((int)AvatarSize.XLarge).ShouldBe(3);
        ((int)AvatarSize.LargePlus).ShouldBe(4);

        var avatar = new Avatar { DisplayName = "Alan Turing", Size = AvatarSize.LargePlus };
        var window = TestHelpers.Show(avatar);
        try
        {
            avatar.Bounds.Width.ShouldBe(64);
            avatar.Bounds.Height.ShouldBe(64);
            avatar.Classes.ShouldContain(Avatar.LargePlusPseudoClass);
            avatar.Classes.ShouldNotContain(Avatar.LargePseudoClass);
            avatar.Classes.ShouldNotContain(Avatar.XLargePseudoClass);
            // The initials take the type step between Large's TypeSM and XLarge's TypeXL.
            avatar.FontSize.ShouldBe(ArcTokens.Get<double>(avatar, DesignTokens.ArcDesignTokenKind.TypeLG));
            avatar.FontSize.ShouldBe(18);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(AvatarSize.Small, 28, 8, 1.5, 3, 0)]
    [InlineData(AvatarSize.Medium, 36, 10, 2, 4, 0)]
    [InlineData(AvatarSize.Large, 48, 12, 2, 6, 0)]
    [InlineData(AvatarSize.LargePlus, 64, 13, 2, 7, 2)]
    [InlineData(AvatarSize.XLarge, 88, 14, 2, 8, 4)]
    public void The_Status_Dot_Follows_The_Size(AvatarSize size, double diameter, double dot, double ring, double bar, double inset)
    {
        var avatar = new Avatar { DisplayName = "Leah Fox", Size = size, Status = AvatarStatus.Busy };
        var window = TestHelpers.Show(avatar);
        try
        {
            AssertDot(avatar, diameter, dot, ring, bar, inset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Status_Dot_Follows_A_Size_Change()
    {
        var avatar = new Avatar { DisplayName = "Leah Fox", Size = AvatarSize.Small, Status = AvatarStatus.Busy };
        var window = TestHelpers.Show(avatar);
        try
        {
            AssertDot(avatar, 28, 8, 1.5, 3, 0);
            avatar.Size = AvatarSize.LargePlus;
            Dispatcher.UIThread.RunJobs();
            AssertDot(avatar, 64, 13, 2, 7, 2);
            avatar.Size = AvatarSize.Medium;
            Dispatcher.UIThread.RunJobs();
            AssertDot(avatar, 36, 10, 2, 4, 0);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertDot(Avatar avatar, double diameter, double dot, double ring, double bar, double inset)
    {
        avatar.Bounds.Width.ShouldBe(diameter);
        var status = TestHelpers.Descendant<Avalonia.Controls.Border>(avatar, "PART_Status");
        status.Bounds.Width.ShouldBe(dot);
        status.Bounds.Height.ShouldBe(dot);
        status.BorderThickness.ShouldBe(new Avalonia.Thickness(ring));
        TestHelpers.Descendant<Avalonia.Controls.Border>(status, "PART_StatusBar").Bounds.Width.ShouldBe(bar);
        // The dot sits in the lower-right corner, inset on the largest sizes so its center stays near the circle's edge.
        var host = TestHelpers.Descendant<Avalonia.Controls.Control>(avatar, "PART_StatusHost");
        host.Margin.ShouldBe(new Avalonia.Thickness(0, 0, inset, inset));
        host.Bounds.Right.ShouldBe(diameter - inset, 0.01);
        host.Bounds.Bottom.ShouldBe(diameter - inset, 0.01);
    }
}
