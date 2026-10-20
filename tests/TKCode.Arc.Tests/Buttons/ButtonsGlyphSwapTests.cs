using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Buttons;

public class ButtonsGlyphSwapTests
{
    [AvaloniaFact]
    public void Swapped_Glyph_Comes_To_Rest_At_Full_Scale()
    {
        // Regression: the rest pose was `new ButtonsGlyphPose()`, which for a struct is all zeros (scale 0), so the
        // spinner of a pending ActionButton shrank to a dot.
        var swap = new ButtonsGlyphSwap { Width = 17, Height = 17 };
        var window = TestHelpers.Show(swap, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        try
        {
            swap.Show(new Border { Width = 16, Height = 16 }, null, null);
            swap.Show(new Border { Width = 16, Height = 16 }, ButtonsGlyphPose.Pop, ButtonsGlyphPose.Pop);
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            var host = (Decorator)Avalonia.VisualTree.VisualExtensions.GetVisualChildren(swap).Last();
            var scale = ((Avalonia.Media.TransformGroup)host.RenderTransform!).Children.OfType<Avalonia.Media.ScaleTransform>().Single();
            scale.ScaleX.ShouldBe(1, 0.01);
            host.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
