using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>Core <see cref="ViewportVisibility"/> and <see cref="ArcViewport"/>: watching controls that are already laid out.</summary>
public class ViewportVisibilityTests
{
    private static (Window Window, ScrollViewer Scroller, Border Top, Border Below) Page()
    {
        var top = new Border { Height = 100 };
        var below = new Border { Height = 100, Margin = new Thickness(0, 900, 0, 0) };
        var scroller = new ScrollViewer { Height = 300, Content = new StackPanel { Children = { top, below } } };
        var window = TestHelpers.Show(scroller);
        Dispatcher.UIThread.RunJobs();
        return (window, scroller, top, below);
    }

    [AvaloniaFact]
    public void Calculate_Matches_The_Viewport_Avalonia_Reports()
    {
        var (window, scroller, _, below) = Page();
        try
        {
            Rect? reported = null;
            below.EffectiveViewportChanged += (_, e) => reported = e.EffectiveViewport;
            scroller.Offset = new Vector(0, 850);
            Dispatcher.UIThread.RunJobs();
            reported.ShouldNotBeNull();
            var calculated = ArcViewport.Calculate(below);
            calculated.X.ShouldBe(reported!.Value.X, 0.5);
            calculated.Y.ShouldBe(reported.Value.Y, 0.5);
            calculated.Width.ShouldBe(reported.Value.Width, 0.5);
            calculated.Height.ShouldBe(reported.Value.Height, 0.5);
            ArcViewport.Calculate(new Border()).ShouldBe(default);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Watching_A_Control_That_Is_Already_Laid_Out_Reports_Its_First_State()
    {
        var (window, _, top, below) = Page();
        try
        {
            // Layout has settled: Avalonia would report no viewport change for listeners added now.
            using var onScreen = new ViewportVisibility(top, 0.5, once: false);
            using var offScreen = new ViewportVisibility(below, 0.5, once: false);
            var events = new List<bool>();
            onScreen.VisibilityChanged += (_, visible) => events.Add(visible);
            Dispatcher.UIThread.RunJobs();
            onScreen.IsVisible.ShouldBeTrue();
            events.ShouldBe([true]);
            offScreen.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
