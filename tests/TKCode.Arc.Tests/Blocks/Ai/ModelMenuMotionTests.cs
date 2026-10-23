using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using AtomUI.MotionScene;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The model menu's motion, frame by frame on a manual clock.</summary>
public class ModelMenuMotionTests
{
    private static double Ease(Easing easing, double elapsed, double duration) => easing.Ease(Math.Min(1, elapsed / duration));

    private static Matrix FrameMatrix(ModelMenuPanel panel)
    {
        var actor = panel.FindAncestorOfType<BaseMotionActor>();
        var transform = (actor?.MotionTransform ?? panel.RenderTransform) as MatrixTransform;
        return transform?.Matrix ?? Matrix.Identity;
    }

    [AvaloniaFact]
    public void The_Menu_Fades_In_On_The_Enter_Curve_While_Settling_On_The_Css_Spring_Then_Leaves_In_130ms()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full);
        var closed = 0;
        host.Menu.Closed += (_, _) => closed++;
        host.Open();
        var presence = host.Menu.MainPresence!;
        presence.CurrentOpacity.ShouldBe(0);
        presence.CurrentPose.ShouldBe(new Point(6, 0.97));
        // It starts above its rest position (toward the pill below it), scaled about its top edge.
        FrameMatrix(host.Main).M32.ShouldBeLessThan(-5);

        var t = CoreFrames.Run(clock, 5);
        presence.CurrentOpacity.ShouldBe(Ease(ArcEasings.Enter, t, 0.16), 0.001);
        presence.CurrentPose.X.ShouldBe(6 * (1 - Ease(ArcEasings.CssSpring, t, 0.58)), 0.001);
        presence.CurrentPose.Y.ShouldBe(0.97 + 0.03 * Ease(ArcEasings.CssSpring, t, 0.58), 0.0001);
        t += CoreFrames.Run(clock, 5);
        presence.CurrentOpacity.ShouldBe(1);
        presence.CurrentPose.X.ShouldBeGreaterThan(0);
        CoreFrames.Run(clock, 30);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        FrameMatrix(host.Main).ShouldBe(Matrix.Identity);
        // Rows do not stagger in.
        host.Main.Rows.ShouldAllBe(r => r.Opacity == 1 && r.RenderTransform == null);

        host.Menu.Close();
        host.Pump();
        host.Menu.IsOpen.ShouldBeFalse();
        presence.Popup.IsOpen.ShouldBeTrue();
        closed.ShouldBe(0);
        t = CoreFrames.Run(clock, 4);
        presence.CurrentOpacity.ShouldBe(1 - Ease(ArcEasings.Standard, t, 0.13), 0.001);
        presence.CurrentPose.X.ShouldBe(3 * Ease(ArcEasings.Standard, t, 0.13), 0.001);
        presence.CurrentPose.Y.ShouldBe(1 - 0.015 * Ease(ArcEasings.Standard, t, 0.13), 0.0001);
        CoreFrames.Run(clock, 5);
        host.Pump();
        presence.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void A_Close_During_The_Enter_Continues_From_Where_The_Menu_Is()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full);
        host.Open();
        var presence = host.Menu.MainPresence!;
        CoreFrames.Run(clock, 8);
        var pose = presence.CurrentPose;
        var opacity = presence.CurrentOpacity;
        pose.X.ShouldBeGreaterThan(0.2);
        host.Menu.Close();
        host.Pump();
        CoreFrames.Run(clock, 1);
        var step = Ease(ArcEasings.Standard, 1 / 60.0, 0.13);
        presence.CurrentPose.X.ShouldBe(pose.X + (3 - pose.X) * step, 0.001);
        presence.CurrentOpacity.ShouldBe(opacity * (1 - step), 0.001);
    }

    [AvaloniaFact]
    public void Opening_During_The_Exit_Turns_It_Around_Without_Closing()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full);
        var opened = 0;
        var closed = 0;
        host.Menu.Opened += (_, _) => opened++;
        host.Menu.Closed += (_, _) => closed++;
        host.Open();
        CoreFrames.Run(clock, 40);
        host.Menu.Close();
        host.Pump();
        CoreFrames.Run(clock, 3);
        var opacity = host.Menu.MainPresence!.CurrentOpacity;
        opacity.ShouldBeLessThan(1);
        host.Open(ModelMenuOpenFocus.Selected);
        CoreFrames.Run(clock, 1);
        host.Menu.MainPresence.CurrentOpacity.ShouldBeGreaterThan(opacity);
        CoreFrames.Run(clock, 40);
        host.Pump();
        host.Menu.MainPresence.Popup.IsOpen.ShouldBeTrue();
        host.Menu.MainPresence.CurrentOpacity.ShouldBe(1);
        host.Menu.MainPresence.CurrentPose.ShouldBe(new Point(0, 1));
        opened.ShouldBe(1);
        closed.ShouldBe(0);
        host.Main.Active.ShouldBe(host.Main.Rows[0]);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_In_And_Out_In_120ms_Without_Moving()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Reduced);
        host.Open();
        var presence = host.Menu.MainPresence!;
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        var t = CoreFrames.Run(clock, 3);
        presence.CurrentOpacity.ShouldBe(t / 0.12, 0.001);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        CoreFrames.Run(clock, 5);
        presence.CurrentOpacity.ShouldBe(1);
        host.Menu.Close();
        host.Pump();
        t = CoreFrames.Run(clock, 3);
        presence.CurrentOpacity.ShouldBe(1 - t / 0.12, 0.001);
        presence.CurrentPose.ShouldBe(new Point(0, 1));
        CoreFrames.Run(clock, 5);
        host.Pump();
        presence.Popup.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Off_Shows_And_Hides_At_Once()
    {
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Off);
        host.Open();
        host.Menu.MainPresence!.CurrentOpacity.ShouldBe(1);
        host.Menu.MainPresence.CurrentPose.ShouldBe(new Point(0, 1));
        host.Menu.Close();
        host.Menu.MainPresence.Popup.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Submenu_Enters_From_The_Menu_Side()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full);
        host.Open();
        CoreFrames.Run(clock, 40);
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        var sub = host.Menu.EffortPresence!;
        sub.CurrentPose.ShouldBe(new Point(6, 0.97));
        // On the right of the menu it starts 6px to the left, scaled about its top-left corner.
        var matrix = FrameMatrix(sub.Panel);
        matrix.M31.ShouldBeLessThan(-5);
        matrix.M32.ShouldBe(0, 0.5);
        CoreFrames.Run(clock, 40);
        sub.CurrentPose.ShouldBe(new Point(0, 1));
        sub.CurrentOpacity.ShouldBe(1);
        // Leaving it plays the 130ms exit toward the menu.
        host.Key(PhysicalKey.ArrowUp);
        sub.IsClosing.ShouldBeTrue();
        CoreFrames.Run(clock, 9);
        host.Pump();
        sub.Popup.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Submenu_On_The_Left_Starts_To_The_Right()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full, anchorAt: HorizontalAlignment.Right);
        host.Open();
        CoreFrames.Run(clock, 40);
        host.Click(host.ActionRow(host.Main, ModelMenuRowKind.Effort));
        var sub = host.Menu.EffortPresence!;
        sub.Popup.OpensLeft.ShouldBeTrue();
        FrameMatrix(sub.Panel).M31.ShouldBeGreaterThan(5);
    }

    [AvaloniaFact]
    public void The_Highlight_Fades_In_Where_It_Lands_Glides_With_The_Pointer_And_Jumps_With_Keys()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(level: ArcMotionLevel.Full);
        host.Open();
        CoreFrames.Run(clock, 40);
        var highlight = host.Main.Highlight!;
        highlight.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(80));
        var rows = host.Main.Rows;

        // First appearance: in place, fading in over 80ms.
        host.Move(rows[0]);
        var first = host.Main.RectOf(rows[0])!.Value;
        highlight.CurrentRect.ShouldBe(first);
        highlight.CurrentOpacity.ShouldBe(0);
        var t = CoreFrames.Run(clock, 3);
        highlight.CurrentOpacity.ShouldBe(Ease(ArcEasings.Standard, t, 0.08), 0.001);
        CoreFrames.Run(clock, 3);
        highlight.CurrentOpacity.ShouldBe(1);

        // Pointer: glides on the snappy spring.
        host.Move(rows[1]);
        var second = host.Main.RectOf(rows[1])!.Value;
        t = CoreFrames.Run(clock, 4);
        var expected = second.Y + SpringSolver.Evaluate(ArcMotionPresets.Snappy, first.Y - second.Y, 0, t).Displacement;
        highlight.CurrentRect.Y.ShouldBe(expected, 0.01);
        highlight.CurrentRect.Y.ShouldBeGreaterThan(first.Y);
        highlight.CurrentRect.Y.ShouldBeLessThan(second.Y + 1);
        CoreFrames.Run(clock, 60);
        highlight.CurrentRect.ShouldBe(second);

        // The height follows the row (54 → 40).
        var effort = host.ActionRow(host.Main, ModelMenuRowKind.Effort);
        host.Move(effort);
        CoreFrames.Run(clock, 4);
        highlight.CurrentRect.Height.ShouldBeInRange(40.5, 53.5);
        CoreFrames.Run(clock, 60);
        highlight.CurrentRect.ShouldBe(host.Main.RectOf(effort)!.Value);

        // Keyboard: jumps in a single frame.
        host.Key(PhysicalKey.ArrowUp);
        highlight.CurrentRect.ShouldBe(host.Main.RectOf(rows[3])!.Value);

        // Resting on the separator: fades out over 80ms.
        host.Move(host.Center(host.InPanel<ModelMenuSeparator>(host.Main).Single()));
        t = CoreFrames.Run(clock, 2);
        highlight.CurrentOpacity.ShouldBe(1 - Ease(ArcEasings.Standard, t, 0.08), 0.001);
        CoreFrames.Run(clock, 4);
        highlight.CurrentOpacity.ShouldBe(0);

        // Back on a row: lands there (no slide from the old place) and fades in.
        host.Move(rows[1]);
        highlight.CurrentRect.ShouldBe(second);
        highlight.CurrentOpacity.ShouldBe(0);
        // A key after a hide shows it at once.
        host.Move(host.Center(host.InPanel<ModelMenuSeparator>(host.Main).Single()));
        CoreFrames.Run(clock, 10);
        host.Key(PhysicalKey.Home);
        highlight.CurrentOpacity.ShouldBe(1);
        highlight.CurrentRect.ShouldBe(first);
    }

    [AvaloniaFact]
    public void A_Page_Enters_From_The_Side_It_Comes_From_While_The_Height_Follows_On_The_Smooth_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(width: 390, height: 800, level: ArcMotionLevel.Full, anchorAt: HorizontalAlignment.Center);
        host.Open(ModelMenuOpenFocus.Selected);
        CoreFrames.Run(clock, 40);
        host.Pump();
        var panel = host.Main;
        var size = panel.SizeDecorator!;
        var from = size.Bounds.Height;
        from.ShouldBe(321, 0.5);

        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        panel.Page.ShouldBe(ModelMenuPage.More);
        panel.PageDirection.ShouldBe(1);
        panel.PageShift.X.ShouldBe(12);
        panel.List!.Opacity.ShouldBe(0);
        size.IsMoving.ShouldBeTrue();
        var t = CoreFrames.Run(clock, 5);
        host.Pump();
        panel.PageShift.X.ShouldBe(12 * (1 - Ease(ArcEasings.Standard, t, 0.16)), 0.001);
        panel.List.Opacity.ShouldBe(Ease(ArcEasings.Standard, t, 0.16), 0.001);
        var height = 420 + SpringSolver.Evaluate(ArcMotionPresets.Smooth, from - 420, 0, t).Displacement;
        size.Bounds.Height.ShouldBe(height, 0.6);
        CoreFrames.Run(clock, 60);
        host.Pump();
        panel.PageShift.X.ShouldBe(0);
        panel.List.Opacity.ShouldBe(1);
        size.Bounds.Height.ShouldBe(420, 0.5);
        // The popup stayed attached to the pill while it grew.
        host.BoundsOf(panel).Top.ShouldBe(host.BoundsOf(host.Anchor).Bottom + 8, 0.5);

        host.Key(PhysicalKey.ArrowLeft);
        panel.PageDirection.ShouldBe(-1);
        panel.PageShift.X.ShouldBe(-12);
        CoreFrames.Run(clock, 60);
        host.Pump();
        panel.PageShift.X.ShouldBe(0);
        size.Bounds.Height.ShouldBe(321, 0.5);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Pages_Only_Fade_And_The_Height_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = ModelMenuHost.Show(width: 390, height: 800, level: ArcMotionLevel.Reduced, anchorAt: HorizontalAlignment.Center);
        host.Open(ModelMenuOpenFocus.Selected);
        CoreFrames.Run(clock, 20);
        host.Pump();
        host.Key(PhysicalKey.End);
        host.Key(PhysicalKey.ArrowRight);
        var panel = host.Main;
        panel.PageShift.X.ShouldBe(0);
        panel.SizeDecorator!.Bounds.Height.ShouldBe(420, 0.5);
        var t = CoreFrames.Run(clock, 3);
        panel.PageShift.X.ShouldBe(0);
        panel.List!.Opacity.ShouldBe(t / 0.12, 0.001);
        CoreFrames.Run(clock, 10);
        panel.List.Opacity.ShouldBe(1);
    }
}
