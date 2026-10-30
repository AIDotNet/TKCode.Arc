using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// TeamSwitcher focus after a pick: the trigger gets focus back with its keyboard ring only when the pick came from the
/// keyboard, and the ring (4px outside the trigger) is never clipped by the switcher.
/// </summary>
public class TeamSwitcherFocusTests
{
    private static (TeamSwitcher Switcher, Window Window) Open()
    {
        var switcher = new TeamSwitcher
        {
            SimulatedDelay = TimeSpan.Zero,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(24),
        };
        switcher.Workspaces.Add(new TeamSwitcherWorkspace("first", "First workspace", "Free", 4, "Owner"));
        switcher.Workspaces.Add(new TeamSwitcherWorkspace("second", "Second workspace", "Pro", 12, "Member"));
        var window = TestHelpers.Show(switcher);
        window.Width = 420;
        window.Height = 600;
        Pump();
        return (switcher, window);
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

    private static Point Center(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Point point)
    {
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    [AvaloniaFact]
    public void A_Pointer_Pick_Returns_Focus_Without_The_Keyboard_Ring()
    {
        var (switcher, window) = Open();
        var trigger = switcher.Trigger!;
        Click(window, Center(trigger, window));
        switcher.IsOpen.ShouldBeTrue();

        var row = switcher.Menu!.GetVisualDescendants().OfType<TeamSwitcherRow>().First(r => r.Workspace!.Id == "second");
        Click(window, Center(row, window));

        switcher.IsOpen.ShouldBeFalse();
        trigger.IsFocused.ShouldBeTrue();
        trigger.Classes.Contains(":focus-visible").ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Keyboard_Pick_Returns_Focus_With_The_Whole_Ring()
    {
        var (switcher, window) = Open();
        var trigger = switcher.Trigger!;
        trigger.Focus(NavigationMethod.Tab);
        Pump();
        Press(window, PhysicalKey.Enter);
        switcher.IsOpen.ShouldBeTrue();
        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.Enter);

        switcher.IsOpen.ShouldBeFalse();
        trigger.IsFocused.ShouldBeTrue();
        trigger.Classes.Contains(":focus-visible").ShouldBeTrue();

        var ring = trigger.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_FocusRing");
        ring.IsVisible.ShouldBeTrue();
        // Nothing between the ring and the window clips it to the trigger's box (only its corners would show).
        for (Visual? v = ring.GetVisualParent(); v is not null && v is not Window; v = v.GetVisualParent())
        {
            v.ClipToBounds.ShouldBeFalse($"{v.GetType().Name} clips the focus ring");
        }
    }
}
