using TKCode.Arc.Gestures;

namespace TKCode.Arc.Core.Tests.Gestures;

public class RubberBandTests
{
    [Fact]
    public void Follows_The_Finger_At_First_Then_Approaches_The_Dimension()
    {
        // Slope at zero is factor (0.55): a small pull moves about half as far.
        RubberBand.Apply(1, 120).ShouldBe(0.55, 0.01);
        RubberBand.Apply(120, 120).ShouldBe((1 - 1 / 1.55) * 120, 1e-9);
        RubberBand.Apply(100_000, 120).ShouldBeLessThan(120);
        RubberBand.Apply(100_000, 120).ShouldBeGreaterThan(119);
    }

    [Fact]
    public void Keeps_The_Sign_And_Handles_Degenerate_Input()
    {
        RubberBand.Apply(-40, 120).ShouldBe(-RubberBand.Apply(40, 120), 1e-12);
        RubberBand.Apply(0, 120).ShouldBe(0);
        RubberBand.Apply(40, 0).ShouldBe(0);
        RubberBand.Apply(double.NaN, 120).ShouldBe(0);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(80)]
    [InlineData(-300)]
    public void Invert_Round_Trips(double distance)
    {
        var stretch = RubberBand.Apply(distance, 120);
        RubberBand.Invert(stretch, 120).ShouldBe(distance, 1e-6);
    }

    [Fact]
    public void Clamp_Is_Free_Inside_And_Resists_Outside()
    {
        RubberBand.Clamp(50, 0, 100, 120).ShouldBe(50);
        RubberBand.Clamp(-30, 0, 100, 120).ShouldBe(RubberBand.Apply(-30, 120), 1e-12);
        RubberBand.Clamp(130, 0, 100, 120).ShouldBe(100 + RubberBand.Apply(30, 120), 1e-12);
    }

    [Fact]
    public void Sqrt_Decay_Matches_Arc_Drawer()
    {
        RubberBand.Sqrt(-49).ShouldBe(-7);
        RubberBand.Sqrt(16).ShouldBe(4);
        RubberBand.Sqrt(0).ShouldBe(0);
    }
}
