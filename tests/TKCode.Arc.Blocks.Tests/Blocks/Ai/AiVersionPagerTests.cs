using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiVersionPager"/>: hidden with one version, the buttons and their ends, moving by pointer and keys, the event,
/// the group name, the rolling count and its wording, and keeping focus in the group at an end.
/// </summary>
public class AiVersionPagerTests
{
    private static AiVersionPager Pager(int index, int count, Func<int, int, string>? countText = null) => new()
    {
        Count = count,
        Index = index,
        GroupName = (n, c) => $"Version {n} of {c}",
        CountText = countText,
        PreviousText = "Previous version",
        NextText = "Next version"
    };

    [AvaloniaFact]
    public void One_Version_Or_None_Hides_It()
    {
        using var rig = new AiReplyRig(Pager(0, 1));
        var pager = (AiVersionPager)rig.Content;
        pager.IsVisible.ShouldBeFalse();
        pager.Count = 0;
        rig.Settle();
        pager.IsVisible.ShouldBeFalse();
        pager.Count = 2;
        rig.Settle();
        pager.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Buttons_Are_26_By_28_With_15px_Chevrons_And_Dim_At_The_Ends()
    {
        using var rig = new AiReplyRig(Pager(1, 2));
        var pager = (AiVersionPager)rig.Content;
        var previous = pager.PreviousButton.ShouldBeOfType<AiVersionPagerButton>();
        var next = pager.NextButton.ShouldBeOfType<AiVersionPagerButton>();
        previous.Bounds.Size.ShouldBe(new Size(26, 28));
        previous.CornerRadius.ShouldBe(new CornerRadius(8));
        var glyph = TestHelpers.Descendant<ArcGlyph>(previous, "PART_Glyph");
        glyph.Size.ShouldBe(15);
        glyph.Data.ShouldBeSameAs(ArcIcons.ChevronLeft);
        TestHelpers.Descendant<ArcGlyph>(next, "PART_Glyph").Data.ShouldBeSameAs(ArcIcons.ChevronRight);
        TestHelpers.ColorOf(previous.Foreground).ShouldBe(ArcTokens.GetColor(pager, ArcDesignTokenKind.TextSecondary));
        previous.IsEffectivelyEnabled.ShouldBeTrue();
        next.IsEffectivelyEnabled.ShouldBeFalse();
        next.Opacity.ShouldBe(0.35);
        previous.Opacity.ShouldBe(1);
        AutomationProperties.GetName(previous).ShouldBe("Previous version");
        AutomationProperties.GetName(next).ShouldBe("Next version");
        Tooltip.GetTip(next).ShouldBe("Next version");
    }

    [AvaloniaFact]
    public void The_Count_Rolls_Two_Numbers_Hidden_From_Automation()
    {
        using var rig = new AiReplyRig(Pager(1, 2));
        var pager = (AiVersionPager)rig.Content;
        var number = TestHelpers.Descendant<RollingDigits>(pager, "PART_Number");
        var total = TestHelpers.Descendant<RollingDigits>(pager, "PART_Total");
        number.Value.ShouldBe(2);
        total.Value.ShouldBe(2);
        number.WheelSpring.ShouldBe(ArcSpring.Snappy);
        number.GlyphPitch.ShouldBe(0.6);
        TestHelpers.Descendant<TextBlock>(pager, "PART_Separator").Text.ShouldBe(" / ");
        AutomationProperties.GetAccessibilityView(TestHelpers.Descendant<Panel>(pager, "PART_Count")).ShouldBe(AccessibilityView.Raw);
        pager.ShownCount.ShouldBe("2 / 2");
        number.FontSize.ShouldBe(14);
    }

    [AvaloniaFact]
    public void Count_Wording_Sets_The_Separator_Or_Falls_Back_To_Text()
    {
        AiVersionPager.SeparatorOf(null).ShouldBe(" / ");
        AiVersionPager.SeparatorOf((n, c) => $"{n}/{c}").ShouldBe("/");
        AiVersionPager.SeparatorOf((n, c) => $"{n} of {c}").ShouldBe(" of ");
        AiVersionPager.SeparatorOf((n, c) => $"第 {n} 个，共 {c} 个").ShouldBeNull();
        AiVersionPager.SeparatorOf((n, c) => $"{c}-{n}").ShouldBeNull();

        using var rig = new AiReplyRig(Pager(0, 3, (n, c) => $"({n}) {c}"));
        var pager = (AiVersionPager)rig.Content;
        pager.Classes.ShouldContain(AiVersionPager.TextCountPseudoClass);
        TestHelpers.Descendant<TextBlock>(pager, "PART_CountText").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(pager, "PART_CountText").Text.ShouldBe("(1) 3");
        TestHelpers.Descendant<StackPanel>(pager, "PART_Rolling").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pressing_Moves_Raises_IndexChanged_And_Renames_The_Group()
    {
        var pager = Pager(0, 3);
        var moves = new List<(int Index, int Count)>();
        pager.IndexChanged += (_, e) => moves.Add((e.Index, e.Count));
        using var rig = new AiReplyRig(pager);
        AutomationProperties.GetName(pager).ShouldBe("Version 1 of 3");
        ControlAutomationPeer.CreatePeerForElement(pager).GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        rig.Click(pager.NextButton!);
        pager.Index.ShouldBe(1);
        AutomationProperties.GetName(pager).ShouldBe("Version 2 of 3");
        TestHelpers.Descendant<RollingDigits>(pager, "PART_Number").Value.ShouldBe(2);
        rig.Click(pager.PreviousButton!);
        pager.Index.ShouldBe(0);
        moves.ShouldBe([(1, 3), (0, 3)]);
        // A change from the owner is not the person's move.
        pager.Index = 2;
        rig.Settle();
        moves.Count.ShouldBe(2);
        AutomationProperties.GetName(pager).ShouldBe("Version 3 of 3");
    }

    [AvaloniaFact]
    public void Index_Stays_Within_The_Versions()
    {
        using var rig = new AiReplyRig(Pager(5, 3));
        var pager = (AiVersionPager)rig.Content;
        pager.Index.ShouldBe(2);
        pager.Index = -1;
        pager.Index.ShouldBe(0);
        pager.Index = 2;
        pager.Count = 2;
        pager.Index.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Keys_Move_And_Focus_Stays_In_The_Group_At_An_End()
    {
        var pager = Pager(0, 2);
        using var rig = new AiReplyRig(pager);
        pager.NextButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        pager.Index.ShouldBe(1);
        // The next button is now disabled: focus moved to the previous one, with its ring.
        pager.NextButton.IsEffectivelyEnabled.ShouldBeFalse();
        pager.PreviousButton!.IsFocused.ShouldBeTrue();
        pager.PreviousButton.Classes.ShouldContain(":focus-visible");
        rig.Key(PhysicalKey.Space);
        pager.Index.ShouldBe(0);
        pager.NextButton.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Full_Motion_Rolls_The_Number_Toward_The_New_Version()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var pager = Pager(0, 3);
        using var rig = new AiReplyRig(pager, ArcMotionLevel.Full, clock);
        rig.Click(pager.NextButton!);
        rig.Frames(600);
        var number = TestHelpers.Descendant<RollingDigits>(pager, "PART_Number");
        number.DisplayText.ShouldBe("2");
        number.WheelTargets.ShouldBe([2.0]);
    }
}
