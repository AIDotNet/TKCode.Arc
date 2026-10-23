using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;
using AtomUI.MotionScene;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The list's popup in a window: where it lands, opening and closing, and its motion.</summary>
public class AiSuggestionPopupTests
{
    private static Matrix FrameMatrix(AiSuggestionPop pop)
    {
        var actor = pop.FindAncestorOfType<BaseMotionActor>();
        var transform = (actor?.MotionTransform ?? pop.RenderTransform) as MatrixTransform;
        return transform?.Matrix ?? Matrix.Identity;
    }

    private static double SpringValue(SpringSpec spec, double t) => 1 + SpringSolver.Evaluate(spec, -1, 0, t).Displacement;

    [AvaloniaFact]
    public void The_Bottom_Edge_Sits_6px_Above_The_Line_And_The_Left_Edge_7px_Left_Of_The_Trigger()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows()).ShouldBeTrue();
        host.Popup.IsOpen.ShouldBeTrue();
        host.Popup.Placement.Above.ShouldBeTrue();
        var anchor = host.BoundsOf(host.Anchor);
        var surface = host.BoundsOf(host.Pop);
        // Arc: bottom 87 in a 100-high composer, i.e. 13px below its top (line top 19 − 6).
        surface.Bottom.ShouldBe(anchor.Y + 13, 0.5);
        surface.X.ShouldBe(anchor.X + 12, 0.5);
        surface.Width.ShouldBe(300, 0.01);
        // The prompt keeps focus.
        host.Focused.ShouldBe(host.Box);
    }

    [AvaloniaFact]
    public void Near_The_Right_Edge_The_List_Stays_Inside_The_Anchor()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows(), new Rect(560, 19, 9, 24));
        var anchor = host.BoundsOf(host.Anchor);
        var surface = host.BoundsOf(host.Pop);
        surface.Right.ShouldBe(anchor.Right - 6, 0.5);
        surface.Height.ShouldBe(280, 0.01);
    }

    [AvaloniaFact]
    public void A_Narrow_Anchor_Gets_A_Narrower_List()
    {
        using var host = AiSuggestionHost.Show(width: 400, anchorWidth: 286);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows(), new Rect(40, 19, 9, 24));
        var anchor = host.BoundsOf(host.Anchor);
        var surface = host.BoundsOf(host.Pop);
        surface.Width.ShouldBe(274, 0.01);
        surface.X.ShouldBe(anchor.X + 6, 0.5);
        // Rows follow the width; long text is cut with an ellipsis rather than overflowing.
        host.RowView(0).Bounds.Width.ShouldBe(262, 0.01);
    }

    [AvaloniaFact]
    public void Without_Room_Above_The_List_Opens_Below_The_Line()
    {
        using var host = AiSuggestionHost.Show(anchorAt: VerticalAlignment.Top);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Popup.Placement.Above.ShouldBeFalse();
        var anchor = host.BoundsOf(host.Anchor);
        var surface = host.BoundsOf(host.Pop);
        // Line bottom 19 + 24 = 43, then 6px.
        surface.Y.ShouldBe(anchor.Y + 49, 0.5);
        surface.X.ShouldBe(anchor.X + 12, 0.5);
    }

    [AvaloniaFact]
    public void A_Short_Window_Shrinks_The_List_To_The_Roomier_Side()
    {
        using var host = AiSuggestionHost.Show(height: 330, anchorAt: VerticalAlignment.Top, anchorHeight: 100);
        host.Window.Height = 330;
        host.Pump();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        var surface = host.BoundsOf(host.Pop);
        host.Popup.Placement.Above.ShouldBeFalse();
        // Room below: 330 − (32 + 43) − 6 − 6 = 243.
        host.Popup.Placement.MaxHeight.ShouldBe(243, 0.5);
        surface.Height.ShouldBe(243, 0.5);
        surface.Bottom.ShouldBeLessThanOrEqualTo(330 - 6 + 0.5);
        host.Scroll.Viewport.Height.ShouldBe(241, 0.5);
    }

    [AvaloniaFact]
    public void New_Rows_While_Shown_Choose_The_Side_Again_For_Their_Height()
    {
        // Line top at 32 + 160 + 19 = 211 in a 560 window: 199 above, so 4 commands (197.6) fit above and 6 mentions do not.
        using var host = AiSuggestionHost.Show(anchorAt: VerticalAlignment.Top);
        host.Anchor.Margin = new Thickness(32, 192, 32, 32);
        host.Pump();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        host.Popup.Placement.Above.ShouldBeTrue();
        host.Pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        host.Pump();
        host.Popup.Placement.Above.ShouldBeFalse();
        var anchor = host.BoundsOf(host.Anchor);
        host.BoundsOf(host.Pop).Y.ShouldBe(anchor.Y + 49, 0.5);
        host.Pop.Show(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        host.Pump();
        host.Popup.Placement.Above.ShouldBeTrue();
        host.BoundsOf(host.Pop).Bottom.ShouldBe(anchor.Y + 13, 0.5);
    }

    [AvaloniaFact]
    public void Open_Again_Moves_The_Open_List_To_The_New_Trigger()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows());
        var anchor = host.BoundsOf(host.Anchor);
        host.Popup.Open(host.Anchor, new Rect(120, 43, 9, 24));
        host.Pump();
        var surface = host.BoundsOf(host.Pop);
        surface.X.ShouldBe(anchor.X + 113, 0.5);
        surface.Bottom.ShouldBe(anchor.Y + 37, 0.5);
    }

    [AvaloniaFact]
    public void The_Popup_Takes_The_Anchor_As_Logical_Parent_Unless_The_Owner_Adopted_It()
    {
        using (var host = AiSuggestionHost.Show())
        {
            host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
            ((ILogical)host.Popup.Popup).LogicalParent.ShouldBe(host.Anchor);
            host.Popup.Dispose();
            ((ILogical)host.Popup.Popup).LogicalParent.ShouldBeNull();
        }
        using (var host = AiSuggestionHost.Show())
        {
            var owner = (Panel)host.Anchor.Parent!;
            owner.Children.Add(new Border());
            ((ISetLogicalParent)host.Popup.Popup).SetParent(owner);
            host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
            ((ILogical)host.Popup.Popup).LogicalParent.ShouldBe(owner);
            host.Popup.IsOpen.ShouldBeTrue();
            ((ISetLogicalParent)host.Popup.Popup).SetParent(null);
        }
    }

    [AvaloniaFact]
    public void Close_With_Motion_Off_Closes_At_Once()
    {
        using var host = AiSuggestionHost.Show();
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        var closed = 0;
        host.Popup.Closed += (_, _) => closed++;
        host.Popup.Close();
        host.Popup.IsOpen.ShouldBeFalse();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        // Closing again does nothing.
        host.Popup.Close();
        closed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void It_Rises_6px_From_The_Line_On_The_0_29s_Spring_And_Leaves_In_120ms_Ease_Out()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Full);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Popup.CurrentOpacity.ShouldBe(0);
        host.Popup.CurrentPose.ShouldBe(new Point(6, 0.98));
        // Above the line it starts lower (toward the line), scaled about its bottom edge.
        FrameMatrix(host.Pop).M32.ShouldBeGreaterThan(5);

        var t = CoreFrames.Run(clock, 4);
        var spring = AiSuggestionPopup.EnterSpring;
        host.Popup.CurrentOpacity.ShouldBe(SpringValue(spring, t), 0.002);
        host.Popup.CurrentPose.X.ShouldBe(6 * (1 - SpringValue(spring, t)), 0.002);
        host.Popup.CurrentPose.Y.ShouldBe(0.98 + 0.02 * SpringValue(spring, t), 0.0001);
        CoreFrames.Run(clock, 40);
        host.Popup.CurrentPose.ShouldBe(new Point(0, 1));
        host.Popup.CurrentOpacity.ShouldBe(1);
        FrameMatrix(host.Pop).ShouldBe(Matrix.Identity);

        var closed = 0;
        host.Popup.Closed += (_, _) => closed++;
        host.Popup.Close();
        host.Popup.IsOpen.ShouldBeFalse();
        host.Popup.IsClosing.ShouldBeTrue();
        t = CoreFrames.Run(clock, 3);
        var e = AiSuggestionPopup.EaseOut.Ease(t / 0.12);
        host.Popup.CurrentOpacity.ShouldBe(1 - e, 0.002);
        host.Popup.CurrentPose.X.ShouldBe(4 * e, 0.002);
        host.Popup.CurrentPose.Y.ShouldBe(1 - 0.01 * e, 0.0001);
        CoreFrames.Run(clock, 6);
        host.Pump();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Opening_During_The_Exit_Turns_Back_From_Where_It_Is()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Full);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        CoreFrames.Run(clock, 40);
        host.Popup.Close();
        CoreFrames.Run(clock, 3);
        var opacity = host.Popup.CurrentOpacity;
        opacity.ShouldBeInRange(0.05, 0.95);
        var closed = 0;
        host.Popup.Closed += (_, _) => closed++;
        // "/" erased and typed again before the list was gone.
        host.Popup.Open(host.Anchor, AiSuggestionHost.LineStart).ShouldBeTrue();
        host.Popup.IsOpen.ShouldBeTrue();
        host.Popup.CurrentOpacity.ShouldBe(opacity, 0.001);
        // The spring keeps the exit's velocity, so the fade turns around within a few frames instead of jumping.
        CoreFrames.Run(clock, 2);
        host.Popup.CurrentOpacity.ShouldBeGreaterThan(0.1);
        CoreFrames.Run(clock, 8);
        host.Popup.CurrentOpacity.ShouldBeGreaterThan(opacity);
        CoreFrames.Run(clock, 40);
        host.Pump();
        host.Popup.CurrentOpacity.ShouldBe(1);
        host.Popup.Popup.IsOpen.ShouldBeTrue();
        closed.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_In_And_Out_In_120ms_Without_Moving()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Reduced);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Popup.CurrentPose.ShouldBe(new Point(0, 1));
        host.Popup.CurrentOpacity.ShouldBe(0);
        var t = CoreFrames.Run(clock, 3);
        host.Popup.CurrentOpacity.ShouldBe(t / 0.12, 0.002);
        FrameMatrix(host.Pop).ShouldBe(Matrix.Identity);
        CoreFrames.Run(clock, 10);
        host.Popup.CurrentOpacity.ShouldBe(1);
        host.Popup.Close();
        t = CoreFrames.Run(clock, 3);
        host.Popup.CurrentOpacity.ShouldBe(1 - t / 0.12, 0.002);
        host.Popup.CurrentPose.ShouldBe(new Point(0, 1));
        CoreFrames.Run(clock, 10);
        host.Pump();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void New_Rows_Spring_The_Height_While_The_Bottom_Edge_Stays_On_The_Line()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Full);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        CoreFrames.Run(clock, 40);
        host.Pump();
        var anchor = host.BoundsOf(host.Anchor);
        host.BoundsOf(host.Pop).Height.ShouldBe(197.6, 0.5);

        host.Pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows("re"));
        host.Pump();
        host.Pop.SizeDecorator!.IsMoving.ShouldBeTrue();
        var heights = new List<double>();
        for (var i = 0; i < 6; i++)
        {
            CoreFrames.Run(clock, 2);
            host.Pump();
            var surface = host.BoundsOf(host.Pop);
            heights.Add(surface.Height);
            surface.Bottom.ShouldBe(anchor.Y + 13, 0.5, $"frame {i}");
        }
        heights.ShouldBeInOrder(SortDirection.Descending);
        heights[0].ShouldBeLessThan(197);
        heights[^1].ShouldBeGreaterThan(2 * 46.4 + 13);
        CoreFrames.Run(clock, 60);
        host.Pump();
        host.Pop.SizeDecorator.IsMoving.ShouldBeFalse();
        host.BoundsOf(host.Pop).Height.ShouldBe(2 * 46.4 + 12, 0.5);
        host.BoundsOf(host.Pop).Bottom.ShouldBe(anchor.Y + 13, 0.5);
    }

    [AvaloniaFact]
    public void New_Rows_Jump_To_Their_Height_Without_Full_Motion()
    {
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Reduced);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        host.Pop.Show(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows("re"));
        host.Pump();
        host.Pop.SizeDecorator!.IsMoving.ShouldBeFalse();
        host.Pop.Bounds.Height.ShouldBe(2 * 46.4 + 12, 0.5);
    }

    [AvaloniaFact]
    public void A_List_Reopened_With_Other_Rows_Starts_At_Their_Height()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        using var host = AiSuggestionHost.Show(level: ArcMotionLevel.Full);
        host.Open(AiSuggestionKind.Command, AiSuggestionSamples.CommandRows());
        CoreFrames.Run(clock, 40);
        host.Popup.Close();
        CoreFrames.Run(clock, 20);
        host.Pump();
        host.Popup.Popup.IsOpen.ShouldBeFalse();
        host.Open(AiSuggestionKind.Mention, AiSuggestionSamples.MentionRows("export"));
        host.Pop.SizeDecorator!.IsMoving.ShouldBeFalse();
        host.Pop.Bounds.Height.ShouldBe(2 * 26.8 + 2 * 46.4 + 12, 1);
    }
}
