using TKCode.Arc.Primitives;
using Avalonia;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Primitives;

public class SharedElementTests
{
    private static Rect Map(Size size, Matrix own, Matrix chain) => new Rect(size).TransformToAABB(own * chain);

    private static void ShouldBeNear(Rect actual, Rect expected)
    {
        actual.X.ShouldBe(expected.X, 1e-9);
        actual.Y.ShouldBe(expected.Y, 1e-9);
        actual.Width.ShouldBe(expected.Width, 1e-9);
        actual.Height.ShouldBe(expected.Height, 1e-9);
    }

    [Fact]
    public void Flight_Rect_Interpolates_Bounds_And_Keeps_Size_For_Position()
    {
        var source = new Rect(20, 300, 280, 340);
        var natural = new Rect(260, 80, 480, 600);
        SharedElementMath.FlightRect(SharedElementMode.Bounds, source, natural, 0).ShouldBe(source);
        SharedElementMath.FlightRect(SharedElementMode.Bounds, source, natural, 1).ShouldBe(natural);
        SharedElementMath.FlightRect(SharedElementMode.Bounds, source, natural, 0.5).ShouldBe(new Rect(140, 190, 380, 470));

        var position = SharedElementMath.FlightRect(SharedElementMode.Position, source, natural, 0.25);
        position.Position.ShouldBe(new Point(80, 245));
        position.Size.ShouldBe(natural.Size);
    }

    [Fact]
    public void Place_Lands_A_Box_On_The_Target_Through_Its_Chain()
    {
        var size = new Size(200, 100);
        var chain = Matrix.CreateTranslation(50, 40);
        var target = new Rect(10, 10, 100, 25);
        ShouldBeNear(Map(size, SharedElementMath.Place(size, chain, target), chain), target);
        // At its own place the transform is the identity.
        SharedElementMath.Place(size, chain, new Rect(50, 40, 200, 100)).ShouldBe(Matrix.Identity);
    }

    [Fact]
    public void Place_Corrects_For_A_Scaled_Parent()
    {
        // A child at (30, 20) inside a parent that is itself squeezed to half width and a third of its height.
        var parentFlight = Matrix.CreateScale(0.5, 1.0 / 3) * Matrix.CreateTranslation(100, 60);
        var chain = Matrix.CreateTranslation(30, 20) * parentFlight;
        var size = new Size(120, 40);
        var target = new Rect(300, 200, 120, 40);
        // Position mode keeps the child's own size even though its parent is scaled.
        ShouldBeNear(Map(size, SharedElementMath.Place(size, chain, target), chain), target);
    }

    [Fact]
    public void Place_Is_Identity_When_The_Chain_Cannot_Be_Inverted()
    {
        SharedElementMath.Place(new Size(10, 10), new Matrix(0, 0, 0, 0, 0, 0), new Rect(0, 0, 5, 5)).ShouldBe(Matrix.Identity);
        SharedElementMath.Place(new Size(0, 10), Matrix.Identity, new Rect(0, 0, 5, 5)).ShouldBe(Matrix.Identity);
    }

    [Fact]
    public void Radius_Compensation_Reads_Circular_After_Scaling()
    {
        var radii = SharedElementMath.CompensateRadius(26, 0.5, 0.25);
        radii.ShouldBe(new Vector(52, 104));
        (radii.X * 0.5).ShouldBe(26);
        (radii.Y * 0.25).ShouldBe(26);
        SharedElementMath.CompensateRadius(-3, 1, 1).ShouldBe(new Vector(0, 0));
    }

    [Fact]
    public void Corner_Radius_Morphs_Between_Card_And_Quick_Look()
    {
        SharedElementMath.Lerp(new CornerRadius(26), new CornerRadius(34), 0.5).ShouldBe(new CornerRadius(30));
        SharedElementMath.Lerp(new CornerRadius(26), new CornerRadius(34), 0).ShouldBe(new CornerRadius(26));
    }
}
