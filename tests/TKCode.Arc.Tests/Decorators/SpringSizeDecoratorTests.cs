using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Decorators;

public class SpringSizeDecoratorTests
{
    [AvaloniaFact]
    public void Width_Change_Animates_When_Measured_With_Unbounded_Height()
    {
        // A vertical StackPanel measures its children with infinite height; the decorator must still treat a
        // content change as active (spring) and not as a passive container resize (jump).
        var label = new TextBlock { Text = "Save" };
        var decorator = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Child = label };
        var window = TestHelpers.Show(new StackPanel { Children = { decorator } }, ArcMotionLevel.Full);
        try
        {
            using var clock = new ManualFrameClock().Install();
            var before = decorator.Bounds.Width;
            label.Text = "Saved to your library";
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            decorator.Bounds.Width.ShouldBe(before, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
