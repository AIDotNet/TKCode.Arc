using Avalonia;
using TKCode.Arc.Gestures;

namespace TKCode.Arc.Core.Tests.Gestures;

public class PanZoomTransformTests
{
    private static void ShouldBeNear(Point actual, Point expected, double tolerance = 1e-9)
    {
        actual.X.ShouldBe(expected.X, tolerance);
        actual.Y.ShouldBe(expected.Y, tolerance);
    }

    [Fact]
    public void World_And_Screen_Round_Trip()
    {
        var view = new PanZoomTransform(new Point(40, -20), 1.5);
        var world = new Point(100, 60);
        var screen = view.WorldToScreen(world);
        ShouldBeNear(screen, new Point(90, 120));
        ShouldBeNear(view.ScreenToWorld(screen), world);
        view.WorldToScreen(new Rect(40, -20, 10, 20)).ShouldBe(new Rect(0, 0, 15, 30));
        view.VisibleWorld(new Size(300, 150)).ShouldBe(new Rect(40, -20, 200, 100));
    }

    [Fact]
    public void Matrix_Matches_WorldToScreen()
    {
        var view = new PanZoomTransform(new Point(12, 34), 0.75);
        var world = new Point(-50, 210);
        ShouldBeNear(view.Matrix.Transform(world), view.WorldToScreen(world));
    }

    [Fact]
    public void Zooming_Keeps_The_Point_Under_The_Anchor()
    {
        var view = new PanZoomTransform(new Point(10, 20), 1);
        var anchor = new Point(120, 80);
        var under = view.ScreenToWorld(anchor);
        var zoomed = view.ZoomBy(1.246, anchor);
        zoomed.Zoom.ShouldBe(1.246, 1e-12);
        ShouldBeNear(zoomed.WorldToScreen(under), anchor);
    }

    [Fact]
    public void Zoom_Is_Clamped_And_The_Anchor_Still_Holds()
    {
        var view = new PanZoomTransform(default, 1.8);
        var anchor = new Point(50, 50);
        var under = view.ScreenToWorld(anchor);
        var zoomed = view.ZoomBy(4, anchor, 0.25, 2);
        zoomed.Zoom.ShouldBe(2);
        ShouldBeNear(zoomed.WorldToScreen(under), anchor);
        view.ZoomTo(0.01, anchor, 0.25, 2).Zoom.ShouldBe(0.25);
    }

    [Fact]
    public void Pan_Moves_The_Content_By_Screen_Pixels()
    {
        var view = new PanZoomTransform(new Point(0, 0), 2);
        var world = new Point(30, 40);
        var before = view.WorldToScreen(world);
        var panned = view.PanBy(new Vector(-50, 20));
        ShouldBeNear(panned.WorldToScreen(world), before + new Vector(-50, 20));
    }

    [Fact]
    public void Fit_Centers_The_Content_In_The_Padded_Viewport()
    {
        // Arc's 640 × 524 graph in a 400 wide column: shrinks to 0.5, centered.
        var content = new Rect(0, 0, 640, 524);
        var view = PanZoomTransform.Fit(content, new Size(368, 600), new Thickness(24));
        view.Zoom.ShouldBe(0.5, 1e-12);
        var screen = view.WorldToScreen(content);
        screen.Center.X.ShouldBe(184, 1e-9);
        screen.Center.Y.ShouldBe(300, 1e-9);
    }

    [Fact]
    public void Shrink_To_Fit_Never_Enlarges_Small_Content()
    {
        var content = new Rect(100, 100, 200, 100);
        var view = PanZoomTransform.Fit(content, new Size(1000, 800), default, maxZoom: 1);
        view.Zoom.ShouldBe(1);
        view.WorldToScreen(content).Center.ShouldBe(new Point(500, 400));
    }

    [Fact]
    public void Fit_Handles_Empty_And_Flat_Content()
    {
        PanZoomTransform.Fit(new Rect(10, 10, 0, 0), new Size(200, 100), default).ShouldBe(
            new PanZoomTransform(new Point(-90, -40), 1));
        PanZoomTransform.Fit(new Rect(0, 0, 400, 0), new Size(200, 100), default).Zoom.ShouldBe(0.5);
    }

    [Fact]
    public void EnsureVisible_Pans_Only_As_Far_As_Needed()
    {
        var view = PanZoomTransform.Identity;
        var viewport = new Size(400, 300);
        var margin = new Thickness(16);
        view.EnsureVisible(new Rect(100, 100, 50, 50), viewport, margin).ShouldBe(view);
        // Past the right edge: the node's right side lands 16px inside.
        var right = view.EnsureVisible(new Rect(380, 100, 50, 30), viewport, margin);
        right.WorldToScreen(new Point(430, 100)).X.ShouldBe(384, 1e-9);
        right.Origin.Y.ShouldBe(0);
        // Above the top edge.
        var up = view.EnsureVisible(new Rect(10, -60, 50, 30), viewport, margin);
        up.WorldToScreen(new Point(10, -60)).Y.ShouldBe(16, 1e-9);
        // Larger than the viewport: the top-left corner wins.
        var big = view.EnsureVisible(new Rect(500, 0, 600, 40), viewport, margin);
        big.WorldToScreen(new Point(500, 0)).X.ShouldBe(16, 1e-9);
    }

    [Fact]
    public void Interpolation_Holds_A_Zoom_Anchor_Still_Throughout()
    {
        var from = new PanZoomTransform(new Point(-30, 15), 0.8);
        var anchor = new Point(210, 140);
        var to = from.ZoomBy(2.5, anchor);
        var world = from.ScreenToWorld(anchor);
        foreach (var t in new[] { 0, 0.1, 0.37, 0.5, 0.9, 1 })
        {
            ShouldBeNear(PanZoomTransform.Interpolate(from, to, t).WorldToScreen(world), anchor, 1e-9);
        }
        var start = PanZoomTransform.Interpolate(from, to, 0);
        start.Zoom.ShouldBe(from.Zoom);
        ShouldBeNear(start.Origin, from.Origin, 1e-9);
        var end = PanZoomTransform.Interpolate(from, to, 1);
        end.Zoom.ShouldBe(to.Zoom, 1e-12);
        ShouldBeNear(end.Origin, to.Origin, 1e-9);
    }
}
