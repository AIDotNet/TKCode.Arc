using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Core.Tests.Motion;

/// <summary>The reduced-motion breath of busy indicators: 1 → 0.45 → 1 every 1.8 s on a sine ease in and out.</summary>
public class ArcBusyPulseTests
{
    [Fact]
    public void Breathes_From_One_To_The_Minimum_And_Back_Every_Period()
    {
        ArcBusyPulse.Opacity(0).ShouldBe(1);
        ArcBusyPulse.Opacity(ArcBusyPulse.Period / 2).ShouldBe(ArcBusyPulse.MinOpacity, 1e-9);
        ArcBusyPulse.Opacity(ArcBusyPulse.Period).ShouldBe(1, 1e-9);
        // Symmetric, and halfway down at a quarter period (the sine ease passes 0.5 at its middle).
        ArcBusyPulse.Opacity(0.45).ShouldBe(ArcBusyPulse.Opacity(1.35), 1e-9);
        ArcBusyPulse.Opacity(0.45).ShouldBe((1 + ArcBusyPulse.MinOpacity) / 2, 1e-6);
        // Eases: slow near the top.
        (1 - ArcBusyPulse.Opacity(0.09)).ShouldBeLessThan(0.55 * 0.1);
        ArcBusyPulse.Opacity(-1).ShouldBe(1);
        ArcBusyPulse.Opacity(double.NaN).ShouldBe(1);
    }

    [Fact]
    public void Advance_Keeps_The_Phase_Within_One_Period()
    {
        ArcBusyPulse.Advance(0, 0.5).ShouldBe(0.5, 1e-12);
        ArcBusyPulse.Advance(1.5, 0.5).ShouldBe(0.2, 1e-9);
        ArcBusyPulse.Advance(1.0, -3).ShouldBe(1.0);
        ArcBusyPulse.Advance(0, double.PositiveInfinity).ShouldBe(0);
    }
}
