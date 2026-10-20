using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Avatars;

public class BadgeTests
{
    [Fact]
    public void Strings_And_Numbers_Animate_In_Place()
    {
        Badge.IsTextual("Live").ShouldBeTrue();
        Badge.IsTextual(3).ShouldBeTrue();
        Badge.IsTextual(2.5).ShouldBeTrue();
        Badge.IsTextual(new object()).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Sizes_Tones_And_Icon()
    {
        var badge = new Badge { Content = "Live", Tone = BadgeTone.Success };
        var window = TestHelpers.Show(badge);
        try
        {
            badge.Bounds.Height.ShouldBe(26);
            badge.Classes.ShouldContain(Badge.TextPseudoClass);
            badge.Classes.ShouldNotContain(Badge.IconPseudoClass);
            AutomationProperties.GetName(badge).ShouldBe("Live");
            var n = ArcPalette.Neutrals(false);
            TestHelpers.ColorOf(badge.Background).ShouldBe(Oklch.MixOklab(n.Surface, n.Success, 0.10));
            TestHelpers.ColorOf(badge.Foreground).ShouldBe(n.Success);

            badge.Size = BadgeSize.Small;
            badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
            badge.Content = 12;
            Dispatcher.UIThread.RunJobs();
            badge.Bounds.Height.ShouldBe(22);
            badge.FontSize.ShouldBe(11);
            badge.Classes.ShouldContain(Badge.IconPseudoClass);
            AutomationProperties.GetName(badge).ShouldBe("12");

            badge.Tone = BadgeTone.Info;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(badge.Background).ShouldBe(ArcTokens.GetColor(badge, TKCode.Arc.DesignTokens.ArcDesignTokenKind.AccentSubtle));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Badge_Is_Not_Focusable()
    {
        var badge = new Badge { Content = "Draft" };
        var window = TestHelpers.Show(badge);
        try
        {
            badge.Focusable.ShouldBeFalse();
            TestHelpers.ColorOf(badge.Foreground).ShouldBe(ArcPalette.Neutrals(false).TextSecondary);
        }
        finally
        {
            window.Close();
        }
    }
}
