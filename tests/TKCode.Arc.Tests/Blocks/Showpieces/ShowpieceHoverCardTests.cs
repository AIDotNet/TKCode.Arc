using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceHoverCard"/>: placement and flips (measured per block), and its motion.</summary>
public class ShowpieceHoverCardTests
{
    [Fact]
    public void Galaxy_Cards_Sit_Right_Of_The_Pointer_And_Flip_Left()
    {
        var p = ShowpieceCardPlacement.Galaxy;
        var stage = new Size(663, 360);
        var card = new Size(248, 120);
        p.Place(new Point(100, 150), card, stage, out var flipped, out _).ShouldBe(new Point(118, 90));
        flipped.ShouldBeFalse();
        // 500 + 18 + 248 > 663: the card goes to the left of the pointer.
        p.Place(new Point(500, 150), card, stage, out flipped, out _).ShouldBe(new Point(234, 90));
        flipped.ShouldBeTrue();
        // Clamped 8px inside vertically.
        p.Place(new Point(100, 20), card, stage, out _, out _).Y.ShouldBe(8);
    }

    [Fact]
    public void Semantic_Cards_Flip_Left_And_Up_And_Stay_Under_The_Breadcrumb()
    {
        var p = ShowpieceCardPlacement.SemanticZoom;
        var stage = new Size(663, 400);
        var card = new Size(252, 140);
        p.Place(new Point(100, 100), card, stage, out _, out _).ShouldBe(new Point(118, 118));
        p.Place(new Point(100, 300), card, stage, out _, out var up).ShouldBe(new Point(118, 142));
        up.ShouldBeTrue();
        p.Place(new Point(600, 100), card, stage, out var left, out _).X.ShouldBe(600 - 18 - 252);
        left.ShouldBeTrue();
        p.Place(new Point(100, 10), card, stage, out _, out _).Y.ShouldBe(58);
    }

    [Fact]
    public void Terrain_Cards_Match_The_Measured_Positions()
    {
        // Activity terrain 5.3 on a 623×292 plot, card 208 × 158.8.
        var p = ShowpieceCardPlacement.Terrain;
        var stage = new Size(623, 292);
        var card = new Size(208, 158.8);
        p.Place(new Point(411, 190), card, stage, out _, out _).X.ShouldBe(181);
        p.Place(new Point(505, 190), card, stage, out _, out _).X.ShouldBe(275);
        p.Place(new Point(353, 190), card, stage, out _, out _).X.ShouldBe(375);
        p.Place(new Point(353, 190), card, stage, out _, out _).Y.ShouldBe(14.8, 0.5);
        p.Place(new Point(353, 50), card, stage, out _, out _).Y.ShouldBe(8);
    }

    [Fact]
    public void Globe_Cards_Center_Above_The_City()
    {
        var p = ShowpieceCardPlacement.Globe;
        p.Place(new Point(300, 200), new Size(216, 100), new Size(663, 437), out _, out var below).ShouldBe(new Point(192, 84));
        below.ShouldBeFalse();
        p.Place(new Point(300, 60), new Size(216, 100), new Size(663, 437), out _, out below).Y.ShouldBe(76);
        below.ShouldBeTrue();
    }

    private static (Window Window, ShowpieceHoverCard Card) Show(ArcMotionLevel level)
    {
        var card = new ShowpieceHoverCard
        {
            Placement = ShowpieceCardPlacement.Galaxy,
            Width = 200,
            Content = new TextBlock { Text = "Northwind Labs" }
        };
        var stage = new Panel { Width = 400, Height = 300, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Children = { card } };
        var window = TestHelpers.Show(stage, level);
        return (window, card);
    }

    [AvaloniaFact]
    public void Opens_At_The_Anchor_And_Flips_At_The_Edge()
    {
        var (window, card) = Show(ArcMotionLevel.Off);
        try
        {
            card.IsVisible.ShouldBeFalse();
            card.ShowAt(new Point(50, 150));
            Dispatcher.UIThread.RunJobs();
            card.IsVisible.ShouldBeTrue();
            card.Opacity.ShouldBe(1);
            var height = card.Bounds.Height;
            height.ShouldBeGreaterThan(0);
            card.CurrentPosition.ShouldBe(new Point(68, 150 - height / 2));
            card.IsFlipped.ShouldBeFalse();
            card.Anchor = new Point(300, 150);
            card.CurrentPosition.X.ShouldBe(300 - 18 - 200);
            card.IsFlipped.ShouldBeTrue();
            card.Classes.Contains(ShowpieceHoverCard.FlippedPseudoClass).ShouldBeTrue();
            // Scale grows from the corner next to the anchor.
            card.RenderTransformOrigin.Point.X.ShouldBe(1);
            card.IsOpen = false;
            card.Opacity.ShouldBe(0);
            card.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fades_And_Scales_In_Without_Sliding_Then_Follows_On_Its_Spring()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(ArcMotionLevel.Full);
        try
        {
            card.PositionSpring = ShowpieceSprings.CardFollow;
            Dispatcher.UIThread.RunJobs();
            card.ShowAt(new Point(50, 150));
            Dispatcher.UIThread.RunJobs();
            // The first appearance lands in place.
            card.CurrentPosition.ShouldBe(card.TargetPosition);
            card.CurrentOpacity.ShouldBeLessThan(0.5);
            card.CurrentScale.ShouldBe(0.96, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            card.CurrentOpacity.ShouldBe(1, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            card.CurrentScale.ShouldBe(1, 1e-3);

            // A moving anchor is followed on the spring.
            card.Anchor = new Point(60, 120);
            card.CurrentPosition.ShouldNotBe(card.TargetPosition);
            clock.Advance(TimeSpan.FromMilliseconds(50));
            card.CurrentPosition.X.ShouldBeInRange(68, 78);
            clock.Advance(TimeSpan.FromSeconds(1));
            card.CurrentPosition.X.ShouldBe(card.TargetPosition.X, 0.01);

            // Closing fades where it is, then hides.
            var position = card.CurrentPosition;
            card.IsOpen = false;
            clock.Advance(TimeSpan.FromMilliseconds(250));
            card.CurrentOpacity.ShouldBe(0, 1e-9);
            card.CurrentPosition.ShouldBe(position);
            card.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
