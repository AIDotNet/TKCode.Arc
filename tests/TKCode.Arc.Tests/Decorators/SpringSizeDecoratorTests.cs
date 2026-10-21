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

    [AvaloniaFact]
    public void A_Left_Pinned_Child_Grows_From_Its_Left_Edge_And_A_Centered_One_Overflows_Evenly()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var pinned = new TextBlock { Text = "+1%", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var centered = new TextBlock { Text = "+1%" };
        var left = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Child = pinned, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var middle = new SpringSizeDecorator { Axis = SpringSizeAxis.Width, Child = centered, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = TestHelpers.Show(new StackPanel { Children = { left, middle } }, ArcMotionLevel.Full);
        try
        {
            pinned.Text = "+1,234,567%";
            centered.Text = "+1,234,567%";
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            window.UpdateLayout();
            left.IsMoving.ShouldBeTrue();
            left.Bounds.Width.ShouldBeLessThan(pinned.Bounds.Width);
            pinned.Bounds.X.ShouldBe(0, 0.01);
            // Layout rounding snaps arranged positions to whole pixels.
            centered.Bounds.X.ShouldBe((middle.Bounds.Width - centered.Bounds.Width) / 2, 1);
            clock.Advance(TimeSpan.FromSeconds(2));
            window.UpdateLayout();
            left.IsMoving.ShouldBeFalse();
            left.Bounds.Width.ShouldBe(pinned.Bounds.Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Height_Clip_Keeps_The_Cross_Overflow_Only_While_Moving()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var text = new TextBlock { Text = "Short.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var plain = new TextBlock { Text = "Short.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var frame = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, CrossClipOverflow = 12, Child = text, Width = 200 };
        var standard = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = plain, Width = 200 };
        var window = TestHelpers.Show(new StackPanel { Children = { frame, standard } }, ArcMotionLevel.Full);
        try
        {
            frame.Clip.ShouldBeNull();
            const string longer = "A much longer description that wraps onto a second line, and then onto a third one.";
            text.Text = longer;
            plain.Text = longer;
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            window.UpdateLayout();
            frame.IsMoving.ShouldBeTrue();
            var clip = frame.Clip.ShouldBeOfType<Avalonia.Media.RectangleGeometry>().Rect;
            clip.X.ShouldBe(-12);
            clip.Width.ShouldBe(200 + 24, 0.01);
            clip.Height.ShouldBe(frame.Bounds.Height, 1);
            // The default keeps the height clip flush with the sides.
            standard.Clip.ShouldBeOfType<Avalonia.Media.RectangleGeometry>().Rect.X.ShouldBe(0);
            clock.Advance(TimeSpan.FromSeconds(2));
            window.UpdateLayout();
            frame.IsMoving.ShouldBeFalse();
            frame.Clip.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Height_Clip_Spans_The_Arranged_Width_Of_A_Stretched_Child()
    {
        // A stretched child is arranged wider than it asked for; clipping to its desired width would cut its right edge
        // off while the height springs.
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var content = new StackPanel { Children = { new TextBlock { Text = "Short" } } };
        var frame = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, Spring = ArcSpring.Smooth, Child = content, Width = 300 };
        var window = TestHelpers.Show(new StackPanel { Children = { frame } }, ArcMotionLevel.Full);
        try
        {
            content.Children.Add(new TextBlock { Text = "Added" });
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            window.UpdateLayout();
            frame.IsMoving.ShouldBeTrue();
            content.DesiredSize.Width.ShouldBeLessThan(300);
            frame.Clip.ShouldBeOfType<Avalonia.Media.RectangleGeometry>().Rect.Width.ShouldBe(300, 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
