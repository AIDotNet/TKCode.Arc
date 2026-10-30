using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests;

/// <summary>
/// Keyboard focus rings are drawn a few pixels outside their control (a negative margin). Templated controls clip to
/// their own box by default, which leaves only the ring's four corners; a focused control must show the whole ring.
/// </summary>
public class FocusRingClipTests
{
    [AvaloniaFact]
    public void CopyButton_Focused_By_Tab_Shows_Its_Whole_Ring()
    {
        var button = new CopyButton
        {
            Value = "npm i tkcode-arc",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24)
        };
        var window = TestHelpers.Show(button);
        try
        {
            ShouldShowWholeRing(button);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TabItem_Focused_By_Tab_Shows_Its_Whole_Ring()
    {
        var tabs = new Tabs { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(24) };
        foreach (var header in new[] { "Overview", "Activity", "Settings" })
        {
            tabs.Items.Add(new TabItem { Header = header, Content = new TextBlock { Text = header + " panel" } });
        }
        var window = TestHelpers.Show(tabs);
        try
        {
            ShouldShowWholeRing((TabItem)tabs.ContainerFromIndex(0)!);
        }
        finally
        {
            window.Close();
        }
    }

    private static void ShouldShowWholeRing(Control control)
    {
        control.Focus(NavigationMethod.Tab).ShouldBeTrue();
        Pump();
        control.Classes.Contains(":focus-visible").ShouldBeTrue();

        var ring = control.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_FocusRing");
        ring.IsVisible.ShouldBeTrue();
        ring.Margin.Left.ShouldBeLessThan(0);
        // No ancestor up to the window clips the ring's box (a clip at the control's own box leaves only the corners).
        for (Visual? v = ring.GetVisualParent(); v is not null; v = v.GetVisualParent())
        {
            if (!v.ClipToBounds)
            {
                continue;
            }
            var topLeft = ring.TranslatePoint(default, v)!.Value;
            var drawn = new Rect(topLeft, ring.Bounds.Size);
            new Rect(v.Bounds.Size).Inflate(0.5).Contains(drawn).ShouldBeTrue($"{v.GetType().Name} clips the focus ring");
        }
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }
}
