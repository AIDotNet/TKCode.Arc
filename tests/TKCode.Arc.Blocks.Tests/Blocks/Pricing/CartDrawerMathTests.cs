using System.Globalization;
using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>The cart's numbers against the values observed on Arc's Cart drawer (design reference, ADR 0039).</summary>
public class CartDrawerMathTests
{
    private static CartDrawerLine Line(decimal price, int quantity) => new(new CartDrawerProduct { Name = "P" + price, Price = price }, quantity);

    [Fact]
    public void Subtotal_Sums_Price_Times_Quantity()
    {
        CartDrawerMath.Subtotal([Line(64, 1)]).ShouldBe(64m);
        CartDrawerMath.Subtotal([Line(64, 1), Line(186, 1), Line(48, 1), Line(118, 1)]).ShouldBe(416m);
        CartDrawerMath.Subtotal([Line(186, 2)]).ShouldBe(372m);
        CartDrawerMath.Subtotal([]).ShouldBe(0m);
    }

    [Theory]
    [InlineData(64, 6)]      // 6.4
    [InlineData(186, 19)]    // 18.6: rounds, does not floor
    [InlineData(250, 25)]
    [InlineData(372, 37)]    // 37.2
    [InlineData(65, 7)]      // 6.5: halves away from zero
    public void Discount_Rounds_Ten_Percent_To_Whole_Units(decimal subtotal, decimal expected)
        => CartDrawerMath.Discount(subtotal, 0.10m, 0).ShouldBe(expected);

    [Fact]
    public void Discount_Keeps_Minor_Units_When_Asked()
    {
        CartDrawerMath.Discount(64.5m, 0.10m, 2).ShouldBe(6.45m);
        CartDrawerMath.Discount(0m, 0.10m, 0).ShouldBe(0m);
        CartDrawerMath.Discount(100m, 0m, 0).ShouldBe(0m);
    }

    [Fact]
    public void Shipping_Uses_The_Subtotal_Before_The_Discount()
    {
        CartDrawerMath.Shipping(64, 250, 12).ShouldBe(12m);
        CartDrawerMath.Shipping(250, 250, 12).ShouldBe(0m);
        CartDrawerMath.Shipping(256, 250, 12).ShouldBe(0m);
        CartDrawerMath.Shipping(0, 250, 12).ShouldBe(0m);
        CartDrawerMath.Shipping(10, 0, 12).ShouldBe(0m);
        // S = 250 with the code: the total drops to 225 and shipping stays free.
        var discount = CartDrawerMath.Discount(250, 0.10m, 0);
        CartDrawerMath.Total(250, discount, CartDrawerMath.Shipping(250, 250, 12)).ShouldBe(225m);
    }

    [Theory]
    [InlineData(64, 0, 12, 76)]
    [InlineData(64, 6, 12, 70)]
    [InlineData(128, 0, 12, 140)]
    [InlineData(192, 0, 12, 204)]
    [InlineData(186, 19, 12, 179)]
    [InlineData(372, 37, 0, 335)]
    public void Total_Is_Subtotal_Minus_Discount_Plus_Shipping(decimal s, decimal d, decimal ship, decimal expected)
        => CartDrawerMath.Total(s, d, ship).ShouldBe(expected);

    [Fact]
    public void Free_Shipping_Progress_And_Remaining_Match_The_Preview()
    {
        CartDrawerMath.Progress(64, 250).ShouldBe(0.256, 1e-9);
        CartDrawerMath.Progress(128, 250).ShouldBe(0.512, 1e-9);
        CartDrawerMath.Progress(192, 250).ShouldBe(0.768, 1e-9);
        CartDrawerMath.Progress(186, 250).ShouldBe(0.744, 1e-9);
        CartDrawerMath.Progress(256, 250).ShouldBe(1);
        CartDrawerMath.Progress(10, 0).ShouldBe(1);
        CartDrawerMath.Remaining(64, 250).ShouldBe(186m);
        CartDrawerMath.Remaining(128, 250).ShouldBe(122m);
        CartDrawerMath.Remaining(192, 250).ShouldBe(58m);
        CartDrawerMath.Remaining(300, 250).ShouldBe(0m);
        // Our sample store: $58 in the cart against $240.
        CartDrawerMath.Remaining(58, 240).ShouldBe(182m);
        CartDrawerMath.Progress(58, 240).ShouldBe(58.0 / 240, 1e-9);
    }

    [Fact]
    public void Quantity_Is_Capped_Per_Row()
    {
        CartDrawerMath.ClampQuantity(10, 9).ShouldBe(9);
        CartDrawerMath.ClampQuantity(-1, 9).ShouldBe(0);
        CartDrawerMath.ClampQuantity(4, 9).ShouldBe(4);
    }

    [Theory]
    [InlineData(5, 0.0625)]
    [InlineData(10, 0.125)]
    [InlineData(20, 0.25)]
    [InlineData(30, 0.3616)]
    [InlineData(50, 0.4777)]
    [InlineData(100, 0.7679)]
    [InlineData(130, 0.942)]
    [InlineData(140, 1)]
    [InlineData(400, 1)]
    public void Swipe_Backdrop_Opacity_Follows_The_Measured_Curve(double x, double expected)
        => CartDrawerMath.SwipeBackdropOpacity(-x).ShouldBe(expected, 0.001);

    [Theory]
    [InlineData(30, 0.7)]
    [InlineData(60, 0.775)]
    [InlineData(100, 0.925)]
    [InlineData(115, 0.98125)]
    [InlineData(200, 1)]
    public void Swipe_Label_Scale_Follows_The_Measured_Curve(double x, double expected)
        => CartDrawerMath.SwipeLabelScale(-x).ShouldBe(expected, 1e-6);

    [Fact]
    public void Swipe_Arms_Past_110px_And_Flicks_Remove()
    {
        CartDrawerMath.IsSwipeArmed(-110).ShouldBeFalse();
        CartDrawerMath.IsSwipeArmed(-111).ShouldBeTrue();
        CartDrawerMath.ShouldRemove(-111, 0).ShouldBeTrue();
        CartDrawerMath.ShouldRemove(-80, 0).ShouldBeFalse();
        CartDrawerMath.ShouldRemove(-60, -1250).ShouldBeTrue();
        CartDrawerMath.ShouldRemove(-15, -2000).ShouldBeFalse();
        CartDrawerMath.ShouldRemove(-60, -400).ShouldBeFalse();
    }

    [Fact]
    public void Flight_Follows_A_Quadratic_Bezier_With_The_Measured_Control_Point()
    {
        var from = new Point(200, 400);
        var to = new Point(1200, 60);
        var control = CartDrawerMath.FlightControl(from, to);
        control.X.ShouldBe(200 + 0.55 * 1000, 1e-9);
        control.Y.ShouldBe(30, 1e-9);
        CartDrawerMath.FlightPoint(from, control, to, 0).ShouldBe(from);
        CartDrawerMath.FlightPoint(from, control, to, 1).ShouldBe(to);
        var mid = CartDrawerMath.FlightPoint(from, control, to, 0.5);
        mid.X.ShouldBe(0.25 * 200 + 0.5 * 750 + 0.25 * 1200, 1e-9);
        mid.Y.ShouldBe(0.25 * 400 + 0.5 * 30 + 0.25 * 60, 1e-9);
    }

    [Fact]
    public void Flight_Shrinks_To_26_By_08_And_Fades_After_086()
    {
        var start = new Size(410, 513);
        CartDrawerMath.FlightSize(start, 0).ShouldBe(start);
        CartDrawerMath.FlightSize(start, 0.8).ShouldBe(new Size(26, 26));
        CartDrawerMath.FlightSize(start, 0.95).ShouldBe(new Size(26, 26));
        CartDrawerMath.FlightSize(start, 0.4).Width.ShouldBe((410 + 26) / 2.0, 1e-9);
        CartDrawerMath.FlightRadius(0).ShouldBe(16);
        CartDrawerMath.FlightRadius(1).ShouldBe(13);
        CartDrawerMath.FlightRotation(0.5).ShouldBe(-10, 1e-9);
        CartDrawerMath.FlightRotation(0).ShouldBe(0, 1e-9);
        CartDrawerMath.FlightOpacity(0.86).ShouldBe(1);
        CartDrawerMath.FlightOpacity(0.93).ShouldBe(0.5, 1e-9);
        CartDrawerMath.FlightOpacity(1).ShouldBe(0);
        var (y0, blur0, a0) = CartDrawerMath.FlightShadow(0);
        (y0, blur0, a0).ShouldBe((4, 10, 0.08));
        var (y, blur, a) = CartDrawerMath.FlightShadow(0.5);
        y.ShouldBe(18, 1e-9);
        blur.ShouldBe(36, 1e-9);
        a.ShouldBe(0.2, 1e-9);
        CartDrawerMath.FlightDuration.ShouldBe(TimeSpan.FromMilliseconds(780));
        CartDrawerMath.FlightEasing.Ease(0.5).ShouldBeGreaterThan(0.5);
    }

    [Fact]
    public void Bump_And_Shake_Hit_Their_Keyframes()
    {
        CartDrawerMath.BumpScale(0).ShouldBe(1);
        CartDrawerMath.BumpScale(0.33).ShouldBe(1.09, 1e-9);
        CartDrawerMath.BumpScale(0.70).ShouldBe(0.98, 1e-9);
        CartDrawerMath.BumpScale(1).ShouldBe(1);
        CartDrawerMath.BumpDuration.TotalMilliseconds.ShouldBe(430);
        CartDrawerMath.ShakeOffset(0.2).ShouldBe(-6, 1e-9);
        CartDrawerMath.ShakeOffset(0.4).ShouldBe(5, 1e-9);
        CartDrawerMath.ShakeOffset(0.6).ShouldBe(-3, 1e-9);
        CartDrawerMath.ShakeOffset(0.8).ShouldBe(2, 1e-9);
        CartDrawerMath.ShakeOffset(1).ShouldBe(0);
        CartDrawerMath.Ripple(0).ShouldBe((1, 0.45));
        var (scale, opacity) = CartDrawerMath.Ripple(1);
        scale.ShouldBe(1.45, 1e-9);
        opacity.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Arrival_Window_Is_Three_To_Five_Days_Out()
        => CartDrawerMath.ArrivalWindow(new DateTime(2026, 11, 8), CultureInfo.GetCultureInfo("en-US")).ShouldBe("Nov 11 – Nov 13");

    [Fact]
    public async Task Samples_Accept_The_Code_In_Any_Case()
    {
        var ct = TestContext.Current.CancellationToken;
        (await CartDrawerSamples.ValidatePromoAsync("workshop10", ct))!.PercentOff.ShouldBe(0.10m);
        (await CartDrawerSamples.ValidatePromoAsync(" WORKSHOP10 ", ct))!.Code.ShouldBe("WORKSHOP10");
        (await CartDrawerSamples.ValidatePromoAsync("SPRING", ct)).ShouldBeNull();
        var products = CartDrawerSamples.CreateProducts();
        products.Count.ShouldBe(6);
        products.Sum(p => p.InitialQuantity).ShouldBe(1);
        products.Count(p => p.Image is null).ShouldBe(0);
    }
}
