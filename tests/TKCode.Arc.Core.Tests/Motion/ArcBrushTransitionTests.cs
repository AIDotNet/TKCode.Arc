using Avalonia.Media;
using Avalonia.Media.Immutable;
using Shouldly;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Core.Tests.Motion;

public class ArcBrushTransitionTests
{
    private sealed class Probe : ArcBrushTransition
    {
        public IBrush? At(double progress, IBrush? from, IBrush? to) => Interpolate(progress, from, to);
    }

    /// <summary>Composites a translucent color over an opaque surface (what the screen shows).</summary>
    private static int OnSurface(Color color, int surface) =>
        (int)Math.Round(color.R * color.A / 255.0 + surface * (1 - color.A / 255.0));

    [Fact]
    public void Fading_In_From_Transparent_Never_Passes_Through_A_Lighter_Gray()
    {
        // A ghost button's hover on a dark raised surface: Transparent (transparent white) to a dark fill.
        var probe = new Probe();
        var target = Color.Parse("#2C2C2C");
        const int surface = 0x1F;
        for (var p = 0.0; p <= 1.0; p += 0.05)
        {
            var color = ((ISolidColorBrush)probe.At(p, Brushes.Transparent, new ImmutableSolidColorBrush(target))!).Color;
            (color.R, color.G, color.B).ShouldBe((target.R, target.G, target.B));
            OnSurface(color, surface).ShouldBeInRange(surface, target.R);
        }
    }

    [Fact]
    public void Fading_Out_To_Transparent_Keeps_The_Hue()
    {
        var probe = new Probe();
        var from = Color.Parse("#2C2C2C");
        var color = ((ISolidColorBrush)probe.At(0.5, new ImmutableSolidColorBrush(from), Brushes.Transparent)!).Color;
        (color.R, color.G, color.B, color.A).ShouldBe((from.R, from.G, from.B, (byte)128));
    }

    [Fact]
    public void Opaque_Colors_Mix_Linearly_And_Brush_Opacity_Counts()
    {
        ArcBrushTransition.MixPremultiplied(Colors.Black, Colors.White, 0.5).ShouldBe(Color.FromArgb(255, 128, 128, 128));
        var probe = new Probe();
        var half = new ImmutableSolidColorBrush(Colors.White, 0.5);
        var color = ((ISolidColorBrush)probe.At(0, half, Brushes.Black)!).Color;
        color.A.ShouldBe((byte)128);
    }

    [Fact]
    public void Ends_On_The_Target_And_Switches_Other_Brushes_Halfway()
    {
        var probe = new Probe();
        var target = new ImmutableSolidColorBrush(Colors.Red);
        probe.At(1, Brushes.Transparent, target).ShouldBeSameAs(target);
        var gradient = new LinearGradientBrush();
        probe.At(0.4, gradient, target).ShouldBeSameAs(gradient);
        probe.At(0.6, gradient, target).ShouldBeSameAs(target);
    }
}
