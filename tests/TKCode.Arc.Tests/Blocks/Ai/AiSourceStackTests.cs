using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiSourceStack"/>: how many discs, their overlap and size, growth with the list, the fan (offsets, full
/// motion only, the spring transition in the disc theme), the ring and the disc colors in both themes, and that the stack is
/// decorative.
/// </summary>
public class AiSourceStackTests
{
    private static (AiSourceStack Stack, Window Window) Show(int count, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var stack = new AiSourceStack { Sources = AiSourcesSamples.Sources(count), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { stack } }, level);
        window.UpdateLayout();
        return (stack, window);
    }

    private static double TranslateX(Visual disc) =>
        disc.RenderTransform is TransformOperations ops ? ops.Value.M31 : 0;

    [AvaloniaTheory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 22)]
    [InlineData(2, 2, 38)]
    [InlineData(3, 3, 54)]
    [InlineData(4, 4, 70)]
    [InlineData(8, 4, 70)]
    public void Shows_Up_To_Four_Discs_Overlapping_By_Four(int sources, int discs, double width)
    {
        var (stack, window) = Show(sources);
        stack.Count.ShouldBe(discs);
        stack.Bounds.Width.ShouldBe(width, 0.01);
        stack.Bounds.Height.ShouldBe(discs == 0 ? 0 : 20, 0.01);
        for (var i = 0; i < discs; i++)
        {
            var disc = stack.Discs[i];
            disc.Bounds.ShouldBe(new Rect(1 + 16 * i, 0, 20, 20));
            disc.Source.ShouldBeSameAs(stack.Sources![i]);
            // Later discs are drawn over the earlier ones (Arc's DOM order).
            stack.GetVisualChildren().ToList().IndexOf(disc).ShouldBe(i);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void A_Disc_Holds_The_12px_Mark_In_Its_Middle()
    {
        var (stack, window) = Show(2);
        var disc = stack.Discs[0];
        disc.Mark.Size.ShouldBe(12);
        disc.Mark.Bounds.ShouldBe(new Rect(4, 4, 12, 12));
        disc.Mark.Source.ShouldBeSameAs(stack.Sources![0]);
        window.Close();
    }

    [AvaloniaFact]
    public void The_Stack_Follows_The_List_While_It_Grows_And_Shrinks()
    {
        var (stack, window) = Show(1);
        var sources = (Avalonia.Collections.AvaloniaList<AiSource>)stack.Sources!;
        var more = AiSourcesSamples.Sources();
        sources.Add(more[1]);
        sources.Add(more[2]);
        window.UpdateLayout();
        stack.Count.ShouldBe(3);
        stack.Bounds.Width.ShouldBe(54, 0.01);
        sources.Add(more[3]);
        sources.Add(more[4]);
        window.UpdateLayout();
        stack.Count.ShouldBe(4);
        // A new first source takes the first disc.
        sources.Insert(0, more[7]);
        stack.Discs[0].Source.ShouldBeSameAs(more[7]);
        sources.Clear();
        window.UpdateLayout();
        stack.Count.ShouldBe(0);
        stack.Bounds.Width.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Away_From_A_Window_The_List_Is_Not_Followed_Until_It_Comes_Back()
    {
        var (stack, window) = Show(1);
        var host = (StackPanel)stack.Parent!;
        host.Children.Remove(stack);
        stack.Sources!.Add(AiSourcesSamples.Sources()[1]);
        stack.Count.ShouldBe(1);
        host.Children.Add(stack);
        stack.Count.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Fan_Offsets_Spread_From_The_Middle_In_Steps_Of_2_5()
    {
        AiSourceStack.FanOffset(0, 1).ShouldBe(0);
        AiSourceStack.FanOffset(0, 2).ShouldBe(-1.25);
        AiSourceStack.FanOffset(1, 2).ShouldBe(1.25);
        new[] { 0, 1, 2 }.Select(i => AiSourceStack.FanOffset(i, 3)).ShouldBe([-2.5, 0, 2.5]);
        new[] { 0, 1, 2, 3 }.Select(i => AiSourceStack.FanOffset(i, 4)).ShouldBe([-3.75, -1.25, 1.25, 3.75]);
    }

    [AvaloniaFact]
    public void At_Full_Motion_Fanning_Moves_Each_Disc_By_Its_Offset_And_Back()
    {
        var (stack, window) = Show(4, ArcMotionLevel.Full);
        stack.IsFanned = true;
        stack.IsSpread.ShouldBeTrue();
        for (var i = 0; i < 4; i++)
        {
            // The base value is the target; the theme's transition animates toward it.
            var target = (TransformOperations)stack.Discs[i].GetBaseValue(Visual.RenderTransformProperty).Value!;
            target.Value.M31.ShouldBe(AiSourceStack.FanOffset(i, 4), 0.001);
        }
        stack.IsFanned = false;
        stack.IsSpread.ShouldBeFalse();
        foreach (var disc in stack.Discs)
        {
            ((TransformOperations)disc.GetBaseValue(Visual.RenderTransformProperty).Value!).Value.M31.ShouldBe(0);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void A_Disc_Added_While_Fanned_Joins_The_Fan()
    {
        var (stack, window) = Show(2, ArcMotionLevel.Full);
        stack.IsFanned = true;
        stack.Sources!.Add(AiSourcesSamples.Sources()[2]);
        for (var i = 0; i < 3; i++)
        {
            ((TransformOperations)stack.Discs[i].GetBaseValue(Visual.RenderTransformProperty).Value!).Value.M31.ShouldBe(AiSourceStack.FanOffset(i, 3), 0.001);
        }
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Below_Full_Motion_The_Stack_Stays_Still(ArcMotionLevel level)
    {
        var (stack, window) = Show(4, level);
        stack.IsFanned = true;
        stack.IsSpread.ShouldBeFalse();
        foreach (var disc in stack.Discs)
        {
            TranslateX(disc).ShouldBe(0);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void The_Fan_Is_A_Spring_Transition_Of_The_Disc_Theme_At_Full_Motion_Only()
    {
        var (stack, window) = Show(2, ArcMotionLevel.Full);
        var disc = stack.Discs[0];
        var transition = disc.Transitions.ShouldNotBeNull().OfType<ArcTransformOperationsTransition>().Single();
        transition.Property.ShouldBe(Visual.RenderTransformProperty);
        transition.Tween.ShouldBe(ArcTween.Spring);
        window.Close();

        var (reduced, window2) = Show(2, ArcMotionLevel.Reduced);
        (reduced.Discs[0].Transitions?.Count ?? 0).ShouldBe(0);
        window2.Close();
    }

    [AvaloniaFact]
    public void The_Ring_Is_The_Page_Background_With_The_Rows_Fill_Over_It()
    {
        var (stack, window) = Show(3);
        foreach (var disc in stack.Discs)
        {
            TestHelpers.ColorOf(disc.PageBrush).ShouldBe(ArcTokens.GetColor(stack, ArcDesignTokenKind.Background));
            disc.RingBrush.ShouldBeNull();
        }
        stack.RingBrush = Brushes.Gold;
        foreach (var disc in stack.Discs)
        {
            disc.RingBrush.ShouldBe(Brushes.Gold);
        }
        // A disc that arrives later takes the current ring.
        stack.Sources!.Add(AiSourcesSamples.Sources()[5]);
        stack.Discs[^1].RingBrush.ShouldBe(Brushes.Gold);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Discs_Use_Surface_And_The_Subtle_Hairline_In_Light_And_The_Raised_Surface_And_Border_In_Dark()
    {
        var (stack, window) = Show(2);
        var disc = stack.Discs[0];
        disc.Classes.ShouldNotContain(AiSourceStackDisc.DarkPseudoClass);
        TestHelpers.ColorOf(disc.Fill).ShouldBe(ArcTokens.GetColor(disc, ArcDesignTokenKind.Surface));
        TestHelpers.ColorOf(disc.Hairline).ShouldBe(ArcTokens.GetColor(disc, ArcDesignTokenKind.BorderSubtle));
        await TestHelpers.ApplyAsync(TestHelpers.Dark);
        try
        {
            disc.Classes.ShouldContain(AiSourceStackDisc.DarkPseudoClass);
            TestHelpers.ColorOf(disc.Fill).ShouldBe(ArcTokens.GetColor(disc, ArcDesignTokenKind.SurfaceRaised));
            TestHelpers.ColorOf(disc.Hairline).ShouldBe(ArcTokens.GetColor(disc, ArcDesignTokenKind.Border));
            TestHelpers.ColorOf(disc.PageBrush).ShouldBe(ArcTokens.GetColor(disc, ArcDesignTokenKind.Background));
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void It_Is_Decorative()
    {
        var (stack, window) = Show(4);
        stack.IsHitTestVisible.ShouldBeFalse();
        stack.Focusable.ShouldBeFalse();
        ControlAutomationPeer.CreatePeerForElement(stack).ShouldBeOfType<NoneAutomationPeer>();
        stack.Discs.ShouldAllBe(d => !d.IsHitTestVisible);
        window.Close();
    }
}
