using Avalonia.Animation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>Full, Reduced and Off: the border color, the text area's height, the lists and the drag cover.</summary>
public class AiComposerMotionTests
{
    private static TimeSpan BorderDuration(AiComposer composer) =>
        composer.Transitions?.OfType<ArcBrushTransition>().FirstOrDefault(t => t.Property == Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty)?.Duration
        ?? TimeSpan.Zero;

    [AvaloniaFact]
    public void The_Border_Color_Moves_In_160ms_At_Full_120ms_Reduced_And_Jumps_Off()
    {
        using (var full = new AiComposerRig(level: ArcMotionLevel.Full))
        {
            BorderDuration(full.Composer).ShouldBe(TimeSpan.FromMilliseconds(160));
        }
        using (var reduced = new AiComposerRig(level: ArcMotionLevel.Reduced))
        {
            BorderDuration(reduced.Composer).ShouldBe(TimeSpan.FromMilliseconds(120));
        }
        using var off = new AiComposerRig();
        BorderDuration(off.Composer).ShouldBe(TimeSpan.Zero);
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_New_Line_Grows_The_Box_On_A_160ms_Ease()
    {
        using var rig = new AiComposerRig(c => c.Placeholder = "Ask", level: ArcMotionLevel.Full);
        rig.Focus();
        rig.Type("one");
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Editor.TargetHeight.ShouldBe(64, 0.5);
        rig.Editor.Bounds.Height.ShouldBe(40, 0.5);
        rig.Run(0.016);
        rig.Editor.Bounds.Height.ShouldBeGreaterThan(40.5);
        rig.Editor.Bounds.Height.ShouldBeLessThan(63.5);
        rig.Run(0.2);
        rig.Editor.Bounds.Height.ShouldBe(64, 0.5);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Height_And_Fades_The_List_Without_Moving_It()
    {
        using var rig = new AiComposerRig(level: ArcMotionLevel.Reduced, useFrameClock: true);
        rig.Focus();
        rig.Type("one");
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        rig.Editor.Bounds.Height.ShouldBe(64, 0.5);
        rig.Type("/");
        rig.ListOpen.ShouldBeTrue();
        rig.Suggest!.CurrentPose.ShouldBe(new Avalonia.Point(0, 1));
        rig.Suggest.CurrentOpacity.ShouldBeLessThan(1);
        rig.Run(0.2);
        rig.Suggest.CurrentOpacity.ShouldBe(1, 0.01);
    }

    [AvaloniaFact]
    public void With_Motion_Off_The_List_And_The_Drag_Cover_Appear_At_Once()
    {
        using var rig = new AiComposerRig();
        rig.Focus();
        rig.Type("@");
        rig.Suggest!.CurrentOpacity.ShouldBe(1);
        rig.Composer.SetDragging(true);
        rig.Pump();
        rig.Composer.DropHost!.Progress.ShouldBe(1);
        rig.Composer.SetDragging(false);
        rig.Pump();
        rig.Composer.DropHost.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void At_Full_Motion_The_Drag_Cover_Fades_In()
    {
        using var rig = new AiComposerRig(level: ArcMotionLevel.Full);
        rig.Composer.SetDragging(true);
        rig.Pump();
        rig.Composer.DropHost!.Progress.ShouldBeLessThan(1);
        rig.Run(0.6);
        rig.Composer.DropHost.Progress.ShouldBe(1, 0.01);
    }
}
