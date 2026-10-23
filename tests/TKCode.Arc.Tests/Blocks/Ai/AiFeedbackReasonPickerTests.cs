using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiFeedbackReasonPicker"/>: the popover above the thumbs down, its question and pills, focus on open and Tab
/// between the reasons, a choice (event at once, the thanks in place, the height spring with the bottom edge kept, closing by
/// itself after 1.5s with focus back), Escape and outside presses without a reason, motion levels and automation.
/// </summary>
public class AiFeedbackReasonPickerTests
{
    private sealed class Host
    {
        public Host(Action<AiFeedbackReasonPicker>? configure = null)
        {
            Target = new Button { Content = "Bad", Width = 36, Height = 36, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            Picker = new AiFeedbackReasonPicker
            {
                PlacementTarget = Target,
                Title = "What went wrong?",
                Reasons = AiAnswerActionsSamples.Reasons,
                ThanksTitle = "Thanks for the feedback",
                ThanksDescription = "It helps make answers better.",
                Time = Time
            };
            configure?.Invoke(Picker);
            Picker.ReasonChosen += (_, e) => Chosen.Add(e);
            Root = new Panel { Children = { Target, Picker } };
        }

        public Button Target { get; }
        public AiFeedbackReasonPicker Picker { get; }
        public Panel Root { get; }
        public AiAnswerFakeTime Time { get; } = new();
        public List<AiFeedbackReasonEventArgs> Chosen { get; } = new();
    }

    /// <summary>Words short enough for the headless test font (its advances are wider than a real font's): three rows of pills, one-line thanks.</summary>
    private static void ShortWords(AiFeedbackReasonPicker picker)
    {
        picker.Reasons = ["Wrong", "Old", "Source", "Long", "Off"];
        picker.ThanksTitle = "Thanks";
        picker.ThanksDescription = "Noted";
    }

    private static (AiAnswerActionsRig Rig, Host Host) Open(bool keyboard = false, Action<AiFeedbackReasonPicker>? configure = null,
        ArcMotionLevel level = ArcMotionLevel.Off, ManualFrameClock? clock = null)
    {
        var host = new Host(configure);
        var rig = new AiAnswerActionsRig(host.Root, level, clock);
        if (keyboard)
        {
            host.Target.Focus(NavigationMethod.Tab);
        }
        host.Picker.Open(keyboard);
        rig.Settle();
        rig.Frames(700);
        return (rig, host);
    }

    [AvaloniaFact]
    public void The_Popover_Sits_8px_Above_Left_Aligned_280_Wide()
    {
        var (rig, host) = Open(configure: ShortWords);
        using var _ = rig;
        var panel = host.Picker.Panel!;
        var target = rig.BoundsOf(host.Target);
        var surface = rig.BoundsOf(panel);
        surface.X.ShouldBe(target.X, 0.5);
        (target.Y - surface.Bottom).ShouldBe(8, 0.5);
        surface.Width.ShouldBe(280);
        panel.Padding.ShouldBe(new Thickness(16));
        panel.CornerRadius.ShouldBe(ArcTokens.Get<CornerRadius>(panel, ArcDesignTokenKind.RadiusPanel));
        TestHelpers.ColorOf(panel.Background).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.SurfaceRaised));
        var title = TestHelpers.Descendant<TextBlock>(host.Picker.ReasonsView!, "PART_Title");
        title.Text.ShouldBe("What went wrong?");
        title.FontWeight.ShouldBe(Avalonia.Media.FontWeight.Medium);
        // The pills start 10px under the question and wrap 6px apart.
        var pills = host.Picker.ReasonButtons;
        pills.Select(p => p.Reason).ShouldBe(["Wrong", "Old", "Source", "Long", "Off"]);
        pills.ShouldAllBe(p => p.Bounds.Height == 32);
        (rig.BoundsOf(pills[0]).Y - rig.BoundsOf(title).Bottom).ShouldBe(10, 0.5);
        (rig.BoundsOf(pills[1]).X - rig.BoundsOf(pills[0]).Right).ShouldBe(6, 0.5);
        var secondRow = pills.First(p => rig.BoundsOf(p).Y > rig.BoundsOf(pills[0]).Y + 1);
        (rig.BoundsOf(secondRow).Y - rig.BoundsOf(pills[0]).Bottom).ShouldBe(6, 0.5);
        pills[0].Padding.ShouldBe(new Thickness(12, 0));
        TestHelpers.ColorOf(pills[0].Foreground).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.TextSecondary));
        TestHelpers.ColorOf(pills[0].Background).ShouldBe(ArcTokens.GetColor(panel, ArcDesignTokenKind.Surface));
    }

    [AvaloniaFact]
    public void A_Pill_Hover_Is_SurfaceMuted_BorderStrong_Foreground()
    {
        var (rig, host) = Open();
        using var _ = rig;
        var pill = host.Picker.ReasonButtons[1];
        rig.Hover(pill);
        TestHelpers.ColorOf(pill.Background).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.SurfaceMuted));
        TestHelpers.ColorOf(pill.BorderBrush).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.BorderStrong));
        TestHelpers.ColorOf(pill.Foreground).ShouldBe(ArcTokens.GetColor(pill, ArcDesignTokenKind.Foreground));
    }

    [AvaloniaFact]
    public void Opening_Focuses_The_First_Reason_And_Tab_Goes_Round_The_Reasons()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        var pills = host.Picker.ReasonButtons;
        pills[0].IsFocused.ShouldBeTrue();
        pills[0].Classes.ShouldContain(":focus-visible");
        for (var i = 1; i < pills.Count; i++)
        {
            rig.Key(PhysicalKey.Tab);
            pills[i].IsFocused.ShouldBeTrue();
        }
        rig.Key(PhysicalKey.Tab);
        pills[0].IsFocused.ShouldBeTrue();
        rig.Key(PhysicalKey.Tab, RawInputModifiers.Shift);
        pills[^1].IsFocused.ShouldBeTrue();
        host.Picker.IsOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Pointer_Open_Focuses_The_First_Reason_Without_A_Ring()
    {
        var (rig, host) = Open();
        using var _ = rig;
        host.Picker.ReasonButtons[0].IsFocused.ShouldBeTrue();
        host.Picker.ReasonButtons[0].Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Reason_Counts_At_Once_Then_The_Thanks_Show_And_Close_After_1_5s()
    {
        var (rig, host) = Open(keyboard: true, configure: p => p.ThanksDescriptionFor = r => $"Noted: {r.ToLowerInvariant()}");
        using var _ = rig;
        var picker = host.Picker;
        rig.Key(PhysicalKey.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.Key(PhysicalKey.Enter);
        host.Chosen.Single().Reason.ShouldBe("Needs a source");
        host.Chosen.Single().Index.ShouldBe(2);
        picker.IsConfirming.ShouldBeTrue();
        picker.IsOpen.ShouldBeTrue();
        picker.Panel!.ShownContent.ShouldBeSameAs(picker.ThanksView);
        picker.ThanksView!.Title.ShouldBe("Thanks for the feedback");
        picker.ThanksView.Description.ShouldBe("Noted: needs a source");
        picker.Panel.IsFocused.ShouldBeTrue();

        host.Time.Now += TimeSpan.FromMilliseconds(1400);
        picker.Tick();
        picker.IsOpen.ShouldBeTrue();
        host.Time.Now += TimeSpan.FromMilliseconds(100);
        picker.Tick();
        picker.IsOpen.ShouldBeFalse();
        picker.IsConfirming.ShouldBeFalse();
        host.Target.IsFocused.ShouldBeTrue();
        host.Target.Classes.ShouldContain(":focus-visible");
        host.Chosen.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Clicked_Reason_Returns_Focus_Without_A_Ring_And_The_Plain_Description()
    {
        var (rig, host) = Open();
        using var _ = rig;
        rig.Click(host.Picker.ReasonButtons[0]);
        host.Picker.ThanksView!.Description.ShouldBe("It helps make answers better.");
        host.Time.Now += TimeSpan.FromSeconds(1.5);
        host.Picker.Tick();
        host.Target.IsFocused.ShouldBeTrue();
        host.Target.Classes.ShouldNotContain(":focus-visible");
    }

    [AvaloniaFact]
    public void Escape_Closes_Without_A_Reason_And_Focus_Goes_Back()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.Escape);
        host.Picker.IsOpen.ShouldBeFalse();
        host.Chosen.ShouldBeEmpty();
        host.Target.IsFocused.ShouldBeTrue();
        host.Target.Classes.ShouldContain(":focus-visible");
    }

    [AvaloniaFact]
    public void A_Press_Outside_Closes_Without_A_Reason()
    {
        var (rig, host) = Open();
        using var _ = rig;
        rig.Click(rig.After);
        host.Picker.IsOpen.ShouldBeFalse();
        host.Chosen.ShouldBeEmpty();
        host.Target.IsFocused.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Escape_During_The_Thanks_Closes_At_Once()
    {
        var (rig, host) = Open(keyboard: true);
        using var _ = rig;
        rig.Key(PhysicalKey.Enter);
        host.Picker.IsConfirming.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        host.Picker.IsOpen.ShouldBeFalse();
        host.Chosen.Count.ShouldBe(1);
        // A later tick finds nothing to close.
        host.Time.Now += TimeSpan.FromSeconds(2);
        host.Picker.Tick();
        host.Picker.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Popover_Shrinks_172_To_83_On_The_Smooth_Spring_Bottom_Edge_Kept()
    {
        var clock = new ManualFrameClock();
        var (rig, host) = Open(level: ArcMotionLevel.Full, clock: clock, configure: ShortWords);
        using var _ = rig;
        var panel = host.Picker.Panel!;
        var before = rig.BoundsOf(panel);
        before.Height.ShouldBe(171.6, 2);
        rig.Click(host.Picker.ReasonButtons[3]);
        rig.Frames(100);
        var mid = rig.BoundsOf(panel);
        mid.Height.ShouldBeLessThan(before.Height - 5);
        mid.Height.ShouldBeGreaterThan(90);
        mid.Bottom.ShouldBe(before.Bottom, 0.5);
        rig.Frames(900);
        var after = rig.BoundsOf(panel);
        after.Height.ShouldBe(82.6, 2);
        after.Bottom.ShouldBe(before.Bottom, 0.5);
        // The reasons have faded away; only the thanks are left.
        rig.Drain(() => host.Picker.Panel!.Swap!.GetVisualChildren().Count() == 1).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Height_And_Off_Swaps_At_Once()
    {
        var clock = new ManualFrameClock();
        var (rig, host) = Open(level: ArcMotionLevel.Reduced, clock: clock, configure: ShortWords);
        using (rig)
        {
            rig.Click(host.Picker.ReasonButtons[0]);
            rig.Frames(17);
            rig.BoundsOf(host.Picker.Panel!).Height.ShouldBe(82.6, 2);
        }
        var (offRig, off) = Open(configure: ShortWords);
        using (offRig)
        {
            offRig.Click(off.Picker.ReasonButtons[0]);
            offRig.BoundsOf(off.Picker.Panel!).Height.ShouldBe(82.6, 2);
            off.Picker.Panel!.Swap!.GetVisualChildren().Count().ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void The_Thanks_Are_A_Green_Check_And_Two_Lines()
    {
        var (rig, host) = Open();
        using var _ = rig;
        rig.Click(host.Picker.ReasonButtons[0]);
        var thanks = host.Picker.ThanksView!;
        var check = TestHelpers.Descendant<ArcGlyph>(thanks, "PART_Check");
        check.Data.ShouldBeSameAs(ArcIcons.Check);
        check.Size.ShouldBe(18);
        TestHelpers.ColorOf(check.Foreground).ShouldBe(ArcTokens.GetColor(thanks, ArcDesignTokenKind.Success));
        var title = TestHelpers.Descendant<TextBlock>(thanks, "PART_Title");
        var description = TestHelpers.Descendant<TextBlock>(thanks, "PART_Description");
        (rig.BoundsOf(title).X - rig.BoundsOf(check.Parent as Visual ?? check).Right).ShouldBe(10, 0.5);
        TestHelpers.ColorOf(description.Foreground).ShouldBe(ArcTokens.GetColor(thanks, ArcDesignTokenKind.TextSecondary));
        title.LineHeight.ShouldBe(20.3);
    }

    [AvaloniaFact]
    public void The_Popover_The_Reasons_And_The_Thanks_Are_Named()
    {
        var (rig, host) = Open();
        using var _ = rig;
        ControlAutomationPeer.CreatePeerForElement(host.Picker.Panel!).GetName().ShouldBe("What went wrong?");
        AutomationProperties.GetName(host.Picker.ReasonsView!.Reasons).ShouldBe("What went wrong?");
        AutomationProperties.GetName(host.Picker.ReasonButtons[4]).ShouldBe("Didn't answer my question");
        rig.Click(host.Picker.ReasonButtons[0]);
        var thanks = host.Picker.ThanksView!;
        AutomationProperties.GetLiveSetting(thanks).ShouldBe(AutomationLiveSetting.Polite);
        AutomationProperties.GetName(thanks).ShouldBe("Thanks for the feedback. It helps make answers better.");
    }

    [AvaloniaFact]
    public void Opening_Again_Shows_The_Reasons()
    {
        var (rig, host) = Open();
        using var _ = rig;
        rig.Click(host.Picker.ReasonButtons[0]);
        host.Picker.Close(returnFocus: false);
        rig.Settle();
        host.Picker.Open();
        rig.Settle();
        host.Picker.IsConfirming.ShouldBeFalse();
        host.Picker.Panel!.ShownContent.ShouldBeSameAs(host.Picker.ReasonsView);
        host.Picker.ReasonButtons[0].IsFocused.ShouldBeTrue();
    }
}
