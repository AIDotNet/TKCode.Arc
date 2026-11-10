using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>The orbit menu: places on the arc, opening and folding, the keyboard ring, invoking and automation.</summary>
public class OrbitMenuTests
{
    private static OrbitMenu Menu(int count = 4)
    {
        var menu = new OrbitMenu { Label = "Create", Margin = new Thickness(120) };
        for (var i = 0; i < count; i++)
        {
            menu.Items.Add(new OrbitMenuAction { Glyph = ArcIcons.Plus, Label = $"Action {i}" });
        }
        return menu;
    }

    private static OrbitMenuAction Action(OrbitMenu menu, int index) => (OrbitMenuAction)menu.ContainerFromIndex(index)!;

    private static void OpenWithKeyboard(Window window, OrbitMenu menu)
    {
        menu.Trigger!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Actions_spread_evenly_over_the_arc()
    {
        var first = OrbitMenu.PlaceOnArc(0, 3, 100, -180, 180);
        var middle = OrbitMenu.PlaceOnArc(1, 3, 100, -180, 180);
        var last = OrbitMenu.PlaceOnArc(2, 3, 100, -180, 180);
        first.X.ShouldBe(-100, 1e-9);
        first.Y.ShouldBe(0, 1e-9);
        middle.X.ShouldBe(0, 1e-9);
        middle.Y.ShouldBe(-100, 1e-9);
        last.X.ShouldBe(100, 1e-9);
        last.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void A_full_ring_does_not_put_the_last_action_on_the_first()
    {
        var places = Enumerable.Range(0, 4).Select(i => OrbitMenu.PlaceOnArc(i, 4, 50, -90, 360)).ToArray();
        places[0].Y.ShouldBe(-50, 1e-9);
        places[1].X.ShouldBe(50, 1e-9);
        places[2].Y.ShouldBe(50, 1e-9);
        places[3].X.ShouldBe(-50, 1e-9);
        var single = OrbitMenu.PlaceOnArc(0, 1, 50, -180, 180);
        single.Y.ShouldBe(-50, 1e-9);
    }

    [AvaloniaFact]
    public void Folded_actions_are_hidden_and_open_ones_rest_on_the_arc()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            Action(menu, 0).IsVisible.ShouldBeFalse();
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < 3; i++)
            {
                var action = Action(menu, i);
                action.IsVisible.ShouldBeTrue();
                action.Opacity.ShouldBe(1);
                action.Offset.ShouldBe(OrbitMenu.PlaceOnArc(i, 3, 88, -180, 180));
                // Each action sits on the trigger's centre; its transform carries it out to its place.
                var center = action.TranslatePoint(new Point(action.Bounds.Width / 2, action.Bounds.Height / 2), menu)!.Value;
                var trigger = new Point(menu.Bounds.Width / 2, menu.Bounds.Height / 2);
                (center - trigger).X.ShouldBe(action.Offset.X, 0.5);
                (center - trigger).Y.ShouldBe(action.Offset.Y, 0.5);
            }
            menu.Classes.Contains(OrbitMenu.OpenPseudoClass).ShouldBeTrue();
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            Action(menu, 1).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_arrow_keys_walk_round_the_ring_and_escape_returns_to_the_trigger()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            OpenWithKeyboard(window, menu);
            menu.IsOpen.ShouldBeTrue();
            var focus = window.FocusManager!;
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 1));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(menu.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_on_an_action_invokes_it_and_folds_the_ring()
    {
        var menu = Menu(3);
        var executed = 0;
        Action(menu, 0);
        var window = TestHelpers.Show(menu);
        try
        {
            var target = Action(menu, 1);
            target.Command = new RelayCommandProbe(() => executed++);
            OrbitMenuAction? invoked = null;
            menu.ActionInvoked += (_, e) => invoked = e.Action;
            OpenWithKeyboard(window, menu);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBeSameAs(target);
            executed.ShouldBe(1);
            menu.IsOpen.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(menu.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_disabled_action_is_skipped()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            Action(menu, 1).IsEnabled = false;
            OpenWithKeyboard(window, menu);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_trigger_and_actions_are_named_for_assistive_technology()
    {
        var menu = Menu(2);
        var window = TestHelpers.Show(menu);
        try
        {
            AutomationProperties.GetName(menu.Trigger!).ShouldBe("Create");
            AutomationProperties.GetItemStatus(menu.Trigger!).ShouldBe("Collapsed");
            var peer = ControlAutomationPeer.CreatePeerForElement(Action(menu, 1));
            peer.GetName().ShouldBe("Action 1");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
            OrbitMenuAction? invoked = null;
            menu.ActionInvoked += (_, e) => invoked = e.Action;
            menu.Open();
            ((IInvokeProvider)peer).Invoke();
            invoked.ShouldBeSameAs(Action(menu, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_actions_leave_one_after_another()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu(4);
        var window = TestHelpers.Show(menu, ArcMotionLevel.Full);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            var first = Action(menu, 0).Travel!.Value;
            var last = Action(menu, 3).Travel!.Value;
            first.ShouldBeGreaterThan(last);
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            Action(menu, 3).Travel!.Value.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommandProbe : System.Windows.Input.ICommand
    {
        private readonly System.Action _run;

        public RelayCommandProbe(System.Action run) => _run = run;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run();
    }
}
