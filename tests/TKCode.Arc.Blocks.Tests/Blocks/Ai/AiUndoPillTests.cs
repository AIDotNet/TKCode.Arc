using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiUndoPill"/>: the pill's look and parts, the accessible name, the event from a press, Enter and Space,
/// entering and leaving at each motion level, swapping its label in place (the focus stays), and that it never leaves by
/// itself.
/// </summary>
public class AiUndoPillTests
{
    private const double Frame = 1000.0 / 60;

    private static AiUndoPill Pill(bool active = true) => new()
    {
        Text = "Summary updated",
        UndoText = "Undo",
        IsActive = active,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Color Token(Control owner, ArcDesignTokenKind kind) => ArcTokens.GetColor(owner, kind);

    [AvaloniaFact]
    public void The_Pill_Reads_Label_Line_Icon_And_Undo_On_A_Raised_Surface()
    {
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200);
        var pill = (AiUndoPill)rig.Content;
        pill.IsVisible.ShouldBeTrue();
        pill.Bounds.Height.ShouldBe(32);
        var frame = TestHelpers.Descendant<Border>(pill, "PART_Frame");
        frame.Padding.ShouldBe(new Thickness(12, 0));
        frame.BorderThickness.ShouldBe(new Thickness(1));
        TestHelpers.ColorOf(frame.Background).ShouldBe(Token(pill, ArcDesignTokenKind.SurfaceRaised));
        TestHelpers.ColorOf(frame.BorderBrush).ShouldBe(Token(pill, ArcDesignTokenKind.Border));
        frame.BoxShadow.ShouldBe(ArcTokens.Get<BoxShadows>(pill, ArcDesignTokenKind.ShadowRaised));
        var label = TestHelpers.Descendant<TextBlock>(pill, "PART_Label");
        label.Text.ShouldBe("Summary updated");
        label.FontSize.ShouldBe(12);
        TestHelpers.ColorOf(label.Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.TextMuted));
        var line = TestHelpers.Descendant<Border>(pill, "PART_Separator");
        line.Bounds.Size.ShouldBe(new Size(1, 14));
        TestHelpers.ColorOf(line.Background).ShouldBe(Token(pill, ArcDesignTokenKind.Border));
        var icon = TestHelpers.Descendant<ArcGlyph>(pill, "PART_Icon");
        icon.Size.ShouldBe(13);
        TestHelpers.ColorOf(icon.Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.Foreground));
        var undo = TestHelpers.Descendant<TextBlock>(pill, "PART_Undo");
        undo.Text.ShouldBe("Undo");
        undo.FontWeight.ShouldBe(FontWeight.Medium);
        TestHelpers.ColorOf(undo.Foreground).ShouldBe(Token(pill, ArcDesignTokenKind.Foreground));
        // 6px between the parts, 2px more around the line.
        var labelRight = label.TranslatePoint(new Point(label.Bounds.Width, 0), pill)!.Value.X;
        line.TranslatePoint(default, pill)!.Value.X.ShouldBe(labelRight + 6 + 2, 0.5);
        icon.TranslatePoint(default, pill)!.Value.X.ShouldBe(line.TranslatePoint(new Point(1, 0), pill)!.Value.X + 2 + 6, 0.5);
        label.TranslatePoint(default, pill)!.Value.X.ShouldBe(13, 0.5);
    }

    [AvaloniaFact]
    public void Its_Name_Is_The_Owners_Or_The_Action_And_The_Label()
    {
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200);
        var pill = (AiUndoPill)rig.Content;
        AutomationProperties.GetName(pill).ShouldBe("Undo: Summary updated");
        pill.AccessibleName = "Undo the summary change";
        AutomationProperties.GetName(pill).ShouldBe("Undo the summary change");
    }

    [AvaloniaFact]
    public void A_Press_Enter_And_Space_Raise_UndoRequested()
    {
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200);
        var pill = (AiUndoPill)rig.Content;
        var undos = 0;
        pill.UndoRequested += (_, _) => undos++;
        pill.Focus(NavigationMethod.Tab);
        pill.Classes.Contains(":focus-visible").ShouldBeTrue();
        TestHelpers.Descendant<Border>(pill, "PART_FocusRing").IsVisible.ShouldBeTrue();
        rig.Key(PhysicalKey.Enter);
        undos.ShouldBe(1);
        rig.Key(PhysicalKey.Space);
        undos.ShouldBe(2);
        rig.Click(pill);
        undos.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Hidden_It_Is_Not_There_And_Raises_Nothing()
    {
        using var rig = new AiPanelRig(Pill(active: false), width: 500, height: 200);
        var pill = (AiUndoPill)rig.Content;
        pill.IsVisible.ShouldBeFalse();
        pill.Opacity.ShouldBe(0);
        pill.IsActive = true;
        rig.Settle();
        pill.IsVisible.ShouldBeTrue();
        pill.Opacity.ShouldBe(1);
        pill.IsActive = false;
        rig.Settle();
        pill.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Full_Enters_In_160ms_Rising_4px_And_Leaves_In_100ms_Without_Moving()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Pill(active: false), width: 500, height: 200, level: ArcMotionLevel.Full, clock: clock);
        var pill = (AiUndoPill)rig.Content;
        pill.IsActive = true;
        rig.Settle();
        pill.IsVisible.ShouldBeTrue();
        rig.FrameCount(5);
        var p = ArcEasings.Standard.Ease(5 * Frame / 160);
        pill.Opacity.ShouldBe(p, 0.001);
        pill.RenderTransform.ShouldBeOfType<TranslateTransform>().Y.ShouldBe(4 * (1 - p), 0.01);
        rig.FrameCount(5);
        pill.Opacity.ShouldBe(1);
        ((TranslateTransform)pill.RenderTransform!).Y.ShouldBe(0);
        pill.IsActive = false;
        rig.FrameCount(3);
        pill.Opacity.ShouldBe(1 - new SplineEasing(0, 0, 0.58, 1).Ease(0.5), 0.001);
        ((TranslateTransform)pill.RenderTransform!).Y.ShouldBe(0);
        pill.IsVisible.ShouldBeTrue();
        rig.FrameCount(4);
        pill.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Reduced_Fades_120ms_Without_Rising()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Pill(active: false), width: 500, height: 200, level: ArcMotionLevel.Reduced, clock: clock);
        var pill = (AiUndoPill)rig.Content;
        pill.IsActive = true;
        rig.FrameCount(3);
        pill.Opacity.ShouldBe(3 * Frame / 120, 0.001);
        ((TranslateTransform)pill.RenderTransform!).Y.ShouldBe(0);
        rig.FrameCount(5);
        pill.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_New_Label_While_Shown_Swaps_In_Place_And_Keeps_The_Focus()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200, level: ArcMotionLevel.Full, clock: clock);
        var pill = (AiUndoPill)rig.Content;
        pill.Focus(NavigationMethod.Tab);
        pill.Text = "Storage table updated";
        rig.Settle();
        // The old label leaves first.
        pill.ShownText.ShouldBe("Summary updated");
        rig.FrameCount(3);
        pill.Opacity.ShouldBeLessThan(0.6);
        rig.FrameCount(4);
        pill.ShownText.ShouldBe("Storage table updated");
        AutomationProperties.GetName(pill).ShouldBe("Undo: Storage table updated");
        rig.FrameCount(12);
        pill.Opacity.ShouldBe(1);
        pill.IsVisible.ShouldBeTrue();
        pill.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_New_Label_At_Off_Or_While_Hidden_Shows_At_Once()
    {
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200);
        var pill = (AiUndoPill)rig.Content;
        pill.Text = "Scope updated";
        rig.Settle();
        pill.ShownText.ShouldBe("Scope updated");
        pill.IsActive = false;
        pill.Text = "Limits updated";
        pill.IsActive = true;
        rig.Settle();
        pill.ShownText.ShouldBe("Limits updated");
    }

    [AvaloniaFact]
    public void It_Never_Leaves_By_Itself()
    {
        var clock = new ManualFrameClock();
        using var rig = new AiPanelRig(Pill(), width: 500, height: 200, level: ArcMotionLevel.Full, clock: clock);
        var pill = (AiUndoPill)rig.Content;
        rig.Frames(10_000);
        pill.IsVisible.ShouldBeTrue();
        pill.Opacity.ShouldBe(1);
    }
}
