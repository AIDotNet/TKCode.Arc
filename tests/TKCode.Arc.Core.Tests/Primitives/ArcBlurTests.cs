using Avalonia.Media;
using Shouldly;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Core.Tests.Primitives;

public class ArcBlurTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.1)]
    [InlineData(-2)]
    [InlineData(double.NaN)]
    public void Nothing_To_Blur_Returns_Null(double radius) => ArcBlur.Get(radius).ShouldBeNull();

    [Theory]
    [InlineData(4, 4)]
    [InlineData(1.1, 1)]
    [InlineData(1.13, 1.25)]
    [InlineData(0.2, 0.25)]
    [InlineData(10, 10)]
    [InlineData(64, 10)]
    public void Radii_Are_Quantized_To_A_Quarter_Pixel_And_Capped(double radius, double expected)
        => ArcBlur.Get(radius).ShouldBeAssignableTo<IBlurEffect>()!.Radius.ShouldBe(expected);

    [Fact]
    public void The_Same_Step_Reuses_One_Effect()
    {
        // A running blur asks every frame; it must get the cached instance back, not a new one.
        ReferenceEquals(ArcBlur.Get(2), ArcBlur.Get(2.05)).ShouldBeTrue();
        ReferenceEquals(ArcBlur.Get(2), ArcBlur.Get(2.25)).ShouldBeFalse();
    }
}
