using TKCode.Arc.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="DragTracker"/>: a press whose release went elsewhere does not block the next one.</summary>
public class DragTrackerTests
{
    [AvaloniaFact]
    public void A_Press_Released_Elsewhere_Does_Not_Block_The_Next_Press()
    {
        var target = new Border { Width = 120, Height = 60, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left };
        var other = new Border { Width = 120, Height = 60, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Left };
        var window = TestHelpers.Show(new StackPanel { Children = { target, other } });
        using var tracker = new DragTracker(target);
        var presses = 0;
        Point? start = null;
        tracker.Pressed += (_, _) => presses++;
        tracker.Started += (_, e) => start = e.Start;
        // A scrolling parent takes the first press over, so its release never reaches the target.
        target.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (presses == 1)
            {
                e.Pointer.Capture(other);
            }
        }, handledEventsToo: true);
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var first = target.TranslatePoint(new Point(20, 30), window)!.Value;
            var outside = other.TranslatePoint(new Point(20, 30), window)!.Value;

            window.MouseDown(first, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);

            var second = target.TranslatePoint(new Point(60, 30), window)!.Value;
            window.MouseDown(second, MouseButton.Left);
            window.MouseMove(second + new Point(20, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(second + new Point(20, 0), MouseButton.Left);

            presses.ShouldBe(2);
            start.ShouldNotBeNull();
            start!.Value.X.ShouldBe(second.X, 0.5);
            tracker.IsDragging.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
