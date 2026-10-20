using TKCode.Arc.Motion;
using Shouldly;

namespace TKCode.Arc.Core.Tests.Motion;

public class SpringTests
{
    [Theory]
    [InlineData(0.4, 0.0, 171.3, 26.18, 1.00)]
    [InlineData(0.26, 0.12, 405.6, 35.44, 0.88)]
    [InlineData(0.42, 0.16, 155.4, 20.94, 0.84)]
    public void FromVisual_Matches_Motion_Physical_Conversion(double visualDuration, double bounce, double k, double c, double zeta)
    {
        var spec = SpringSpec.FromVisual(visualDuration, bounce);
        spec.Stiffness.ShouldBe(k, 0.1);
        spec.Damping.ShouldBe(c, 0.01);
        spec.DampingRatio.ShouldBe(zeta, 0.005);
    }

    [Theory]
    [InlineData(400, 40)]   // underdamped
    [InlineData(100, 20)]   // critical
    [InlineData(100, 40)]   // overdamped
    public void Closed_Form_Matches_Numerical_Integration(double k, double c)
    {
        var spec = SpringSpec.FromPhysical(k, c);
        double x = -1, v = 3;
        const double dt = 1e-5;
        for (var i = 0; i < 50_000; i++)
        {
            // Semi-implicit Euler with a tiny step as the reference.
            var a = -k * x - c * v;
            v += a * dt;
            x += v * dt;
        }
        var (cx, cv) = SpringSolver.Evaluate(spec, -1, 3, 0.5);
        cx.ShouldBe(x, 1e-3);
        cv.ShouldBe(v, 1e-2);
    }

    [Fact]
    public void Retargeting_Keeps_Position_And_Velocity_Continuous()
    {
        var spec = ArcMotionPresets.Morph;
        // Segment 1: 0 -> 100. At t = 0.1 the target changes to 40.
        var (d1, v1) = SpringSolver.Evaluate(spec, -100, 0, 0.1);
        var position = 100 + d1;
        // Segment 2 starts from the current position and velocity.
        var (d2, v2) = SpringSolver.Evaluate(spec, position - 40, v1, 0);
        (40 + d2).ShouldBe(position, 1e-9);
        v2.ShouldBe(v1, 1e-9);
    }

    [Fact]
    public void Smooth_Is_Critically_Damped_And_Never_Overshoots()
    {
        var spec = ArcMotionPresets.Smooth;
        for (var t = 0.0; t < 2; t += 0.005)
        {
            var (x, _) = SpringSolver.Evaluate(spec, -1, 0, t);
            x.ShouldBeLessThanOrEqualTo(1e-9);
        }
    }

    [Fact]
    public void Spring_Easing_Starts_At_Zero_And_Ends_At_One()
    {
        var easing = ArcSpringEasing.For(ArcSpring.Snappy);
        easing.Ease(0).ShouldBe(0);
        easing.Ease(1).ShouldBe(1);
        easing.SettleDuration.TotalSeconds.ShouldBeInRange(0.2, 1.0);
    }
}
