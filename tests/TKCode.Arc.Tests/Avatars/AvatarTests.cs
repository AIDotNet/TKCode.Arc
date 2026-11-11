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
}
