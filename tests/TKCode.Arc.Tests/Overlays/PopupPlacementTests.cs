using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

/// <summary>
/// Arc popups are hosted in the window's overlay layer: the floating shadow is paint only, so a popup a few pixels
/// from its trigger never takes the trigger's pointer input (a native popup window would, and hover popups flicker).
/// </summary>
public class PopupPlacementTests
{
    [AvaloniaFact]
    public void Open_Tooltip_Sits_Eight_Pixels_Above_And_Leaves_The_Trigger_Hit_Testable()
    {
        var target = new Button { Content = "Share", Margin = new Thickness(80, 140, 0, 0) };
        Tooltip.SetTip(target, "Copy link");
        var window = TestHelpers.Show(new StackPanel { Children = { target } });
        try
        {
            Tooltip.SetIsOpen(target, true);
            Dispatcher.UIThread.RunJobs();
            var popup = Tooltip.GetPopup(target)!;
            popup.ShouldUseOverlayLayer.ShouldBeTrue();
            var bubble = (Control)popup.Child!;
            var bubbleTop = bubble.TranslatePoint(default, window)!.Value.Y;
            var targetTop = target.TranslatePoint(default, window)!.Value.Y;
            (targetTop - (bubbleTop + bubble.Bounds.Height)).ShouldBe(8, 1);

            // Hit testing reads the composition tree, which only catches up with layout on a render tick.
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var centre = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            var hit = window.InputHitTest(centre) as Visual;
            hit.ShouldNotBeNull();
            (ReferenceEquals(hit, target) || hit!.GetVisualAncestors().Contains(target)).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
