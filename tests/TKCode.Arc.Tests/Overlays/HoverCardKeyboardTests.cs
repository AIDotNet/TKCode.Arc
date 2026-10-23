using AtomUI.MotionScene;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Overlays;

/// <summary>Keyboard opening (<see cref="HoverCard.OpensOnFocus"/>), external control through IsOpen, Escape and accessibility.</summary>
public class HoverCardKeyboardTests
{
    private sealed class Page
    {
        public required Window Window { get; init; }
        public required StackPanel Root { get; init; }
        public required Button Before { get; init; }
        public required Button Trigger { get; init; }
        public required Button After { get; init; }
        public required HoverCard Card { get; init; }
        public int EscapesAtHost { get; set; }
    }

    /// <summary>A citation-like trigger between two ordinary buttons; the host counts Escape keys that reach it unhandled.</summary>
    private static Page Setup(Action<HoverCard>? configure = null, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        HoverCard.ResetCoordinator();
        var before = new Button { Content = "Before" };
        var trigger = new Button { Content = "1", Variant = ButtonVariant.Ghost };
        var after = new Button { Content = "After" };
        var card = new HoverCard
        {
            Trigger = trigger,
            Placement = PopoverPlacement.Top,
            Alignment = PopoverAlignment.Center,
            CardWidth = 288,
            Content = "Postgres docs: ALTER TABLE"
        };
        configure?.Invoke(card);
        var root = new StackPanel { Margin = new Thickness(40, 200, 40, 40), Spacing = 12, Children = { before, card, after } };
        var window = TestHelpers.Show(root, level);
        var page = new Page { Window = window, Root = root, Before = before, Trigger = trigger, After = after, Card = card };
        root.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                page.EscapesAtHost++;
            }
        }, RoutingStrategies.Bubble);
        return page;
    }

    private static void Close(Page page)
    {
        page.Window.Close();
        HoverCard.ResetCoordinator();
    }

    private static void FocusOn(Control control, NavigationMethod method)
    {
        control.Focus(method);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Page page, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        page.Window.KeyPressQwerty(key, modifiers);
        page.Window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Raise(Control target, RoutedEvent<PointerEventArgs> routedEvent, Window window)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var position = target.TranslatePoint(new Point(2, 2), window)!.Value;
        target.RaiseEvent(new PointerEventArgs(routedEvent, target, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
    }

    private static void Delay350(HoverCard card)
    {
        card.OpensOnFocus = true;
        card.OpenDelay = TimeSpan.FromMilliseconds(350);
    }

    // ---- keyboard focus -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Keyboard_Focus_Opens_After_The_Open_Delay()
    {
        var page = Setup(Delay350);
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsOpenScheduled.ShouldBeTrue();
            page.Card.FlushTimers();
            Dispatcher.UIThread.RunJobs();
            page.Card.IsOpen.ShouldBeTrue();
            page.Trigger.IsFocused.ShouldBeTrue();

            // Arrow-key focus counts as keyboard focus too.
            FocusOn(page.After, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeFalse();
            HoverCard.ResetCoordinator();
            FocusOn(page.Trigger, NavigationMethod.Directional);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeTrue();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Zero_Open_Delay_Opens_On_Keyboard_Focus_At_Once()
    {
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
        });
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Directional);
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.IsOpenScheduled.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Pointer_And_Code_Focus_Do_Not_Open()
    {
        var page = Setup(Delay350);
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Pointer);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsOpenScheduled.ShouldBeFalse();
            FocusOn(page.After, NavigationMethod.Pointer);
            FocusOn(page.Trigger, NavigationMethod.Unspecified);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsOpenScheduled.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Focus_Leaving_Closes_At_Once_And_Cancels_A_Pending_Open()
    {
        var page = Setup(Delay350);
        try
        {
            // Leaving before the delay ran out: the card never opens.
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            FocusOn(page.After, NavigationMethod.Tab);
            page.Card.IsOpenScheduled.ShouldBeFalse();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeFalse();

            // Leaving an open card closes it without the pointer grace.
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeTrue();
            FocusOn(page.Before, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsCloseScheduled.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Focus_Moving_Inside_The_Trigger_Keeps_The_Card()
    {
        HoverCard.ResetCoordinator();
        var first = new Button { Content = "Ada" };
        var second = new Button { Content = "Lin" };
        var outside = new Button { Content = "Outside" };
        var card = new HoverCard
        {
            Trigger = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { first, second } },
            OpensOnFocus = true,
            OpenDelay = TimeSpan.Zero,
            Content = "Two reviewers"
        };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { card, outside } });
        try
        {
            FocusOn(first, NavigationMethod.Tab);
            card.IsOpen.ShouldBeTrue();
            FocusOn(second, NavigationMethod.Tab);
            card.IsOpen.ShouldBeTrue();
            card.IsCloseScheduled.ShouldBeFalse();
            FocusOn(outside, NavigationMethod.Tab);
            card.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
            HoverCard.ResetCoordinator();
        }
    }

    [AvaloniaFact]
    public void The_Pointer_Over_The_Card_Keeps_It_When_Focus_Leaves()
    {
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
        });
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            Raise(page.Card.Surface!, InputElement.PointerEnteredEvent, page.Window);
            FocusOn(page.After, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            // The pointer leaving then closes it after the usual grace.
            Raise(page.Card.Surface!, InputElement.PointerExitedEvent, page.Window);
            page.Card.IsCloseScheduled.ShouldBeTrue();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Tab_Moves_On_Past_The_Card_And_Closes_It()
    {
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
            // Even a focusable control inside the card is passed over (content should be read-only anyway).
            c.Content = new StackPanel { Children = { new TextBlock { Text = "Read the docs" }, new Button { Content = "Inside" } } };
        });
        try
        {
            FocusOn(page.Before, NavigationMethod.Tab);
            Press(page, PhysicalKey.Tab);
            page.Trigger.IsFocused.ShouldBeTrue();
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.Surface!.IsKeyboardFocusWithin.ShouldBeFalse();

            Press(page, PhysicalKey.Tab);
            page.After.IsFocused.ShouldBeTrue();
            page.Card.IsOpen.ShouldBeFalse();

            // Shift+Tab comes back to the trigger and reopens it; the card itself never takes focus.
            Press(page, PhysicalKey.Tab, RawInputModifiers.Shift);
            page.Trigger.IsFocused.ShouldBeTrue();
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.Surface.Focusable.ShouldBeFalse();
            KeyboardNavigation.GetTabNavigation(page.Card.Surface).ShouldBe(KeyboardNavigationMode.None);
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void OpensOnFocus_False_Keeps_The_Arc_Hover_Card_Model()
    {
        var page = Setup(c => c.OpenDelay = TimeSpan.FromMilliseconds(350));
        try
        {
            page.Card.OpensOnFocus.ShouldBeFalse();
            // Arrow-key focus does not open it; Tab opens it at once (no delay).
            FocusOn(page.Trigger, NavigationMethod.Directional);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsOpenScheduled.ShouldBeFalse();
            FocusOn(page.Before, NavigationMethod.Tab);
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            // Losing focus starts the close grace instead of closing at once.
            FocusOn(page.After, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.IsCloseScheduled.ShouldBeTrue();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    // ---- IsOpen ----------------------------------------------------------------------------------------------------

    private sealed class Model : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _open;

        public bool Open
        {
            get => _open;
            set
            {
                // Raises even for the same value, like a view model that does not compare.
                _open = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Open)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [AvaloniaFact]
    public void IsOpen_Is_Two_Way()
    {
        var page = Setup(Delay350);
        var model = new Model();
        page.Card.Bind(HoverCard.IsOpenProperty, new Avalonia.Data.Binding(nameof(Model.Open)) { Source = model, Mode = Avalonia.Data.BindingMode.TwoWay });
        var opened = 0;
        var reasons = new List<DismissReason>();
        page.Card.Opened += (_, _) => opened++;
        page.Card.Closed += (_, e) => reasons.Add(e.Reason);
        try
        {
            // The card opening by itself (keyboard focus, then the delay) reaches the source.
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.FlushTimers();
            model.Open.ShouldBeTrue();
            opened.ShouldBe(1);
            // Escape closes it; the source follows.
            Press(page, PhysicalKey.Escape);
            model.Open.ShouldBeFalse();
            reasons.ShouldBe(new[] { DismissReason.Escape });

            // The source opens it at once, without the delay, and closes it again.
            model.Open = true;
            Dispatcher.UIThread.RunJobs();
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.Popup!.IsOpen.ShouldBeTrue();
            opened.ShouldBe(2);
            model.Open = false;
            Dispatcher.UIThread.RunJobs();
            page.Card.IsOpen.ShouldBeFalse();
            reasons.ShouldBe(new[] { DismissReason.Escape, DismissReason.Programmatic });

            // Hover opening and focus closing also reach the source.
            FocusOn(page.Before, NavigationMethod.Pointer);
            Raise(page.Trigger, InputElement.PointerEnteredEvent, page.Window);
            page.Card.FlushTimers();
            model.Open.ShouldBeTrue();
            Raise(page.Trigger, InputElement.PointerExitedEvent, page.Window);
            page.Card.FlushTimers();
            model.Open.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Setting_IsOpen_Cancels_A_Pending_Delay()
    {
        var page = Setup(Delay350);
        try
        {
            // A pending keyboard open, then code writes false (the value it already has): the open is cancelled.
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            page.Card.IsOpen = false;
            page.Card.IsOpenScheduled.ShouldBeFalse();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeFalse();

            // A pending hover open, then code writes true: open at once, nothing left pending.
            FocusOn(page.After, NavigationMethod.Pointer);
            Raise(page.Trigger, InputElement.PointerEnteredEvent, page.Window);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            page.Card.IsOpen = true;
            page.Card.IsOpen.ShouldBeTrue();
            page.Card.IsOpenScheduled.ShouldBeFalse();

            // A pending close grace, then code writes true (the value it already has): the card stays open.
            Raise(page.Trigger, InputElement.PointerExitedEvent, page.Window);
            page.Card.IsCloseScheduled.ShouldBeTrue();
            page.Card.IsOpen = true;
            page.Card.IsCloseScheduled.ShouldBeFalse();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeTrue();

            // SetCurrentValue counts as a write too (Show / Hide use it).
            page.Card.Hide();
            Raise(page.Trigger, InputElement.PointerEnteredEvent, page.Window);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            page.Card.SetCurrentValue(HoverCard.IsOpenProperty, false);
            page.Card.IsOpenScheduled.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }

    // ---- Escape ----------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Escape_Closes_Without_Moving_Focus_And_Is_Handled_Only_When_It_Closed_Something()
    {
        var page = Setup(Delay350);
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeTrue();
            Press(page, PhysicalKey.Escape);
            page.Card.IsOpen.ShouldBeFalse();
            page.Trigger.IsFocused.ShouldBeTrue();
            page.EscapesAtHost.ShouldBe(0);

            // Nothing open: the second Escape reaches the host, and the card stays closed while focus stays.
            Press(page, PhysicalKey.Escape);
            page.EscapesAtHost.ShouldBe(1);
            page.Card.IsOpen.ShouldBeFalse();
            page.Card.IsOpenScheduled.ShouldBeFalse();
            page.Trigger.IsFocused.ShouldBeTrue();

            // A card opened from code while focus is on the trigger closes the same way.
            page.Card.IsOpen = true;
            Press(page, PhysicalKey.Escape);
            page.Card.IsOpen.ShouldBeFalse();
            page.EscapesAtHost.ShouldBe(1);

            // Escape during the open delay cancels the wait but is not handled (nothing was closed).
            FocusOn(page.After, NavigationMethod.Tab);
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpenScheduled.ShouldBeTrue();
            Press(page, PhysicalKey.Escape);
            page.Card.IsOpenScheduled.ShouldBeFalse();
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeFalse();
            page.EscapesAtHost.ShouldBe(2);

            // Enter still opens it at once.
            Press(page, PhysicalKey.Enter);
            page.Card.IsOpen.ShouldBeTrue();
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void Escape_Elsewhere_Closes_A_Hovered_Card_After_The_Focused_Control_Had_It()
    {
        var page = Setup(Delay350);
        try
        {
            // Focus is on another control: its own Escape handling goes first, then the window-level Escape closes the card.
            FocusOn(page.After, NavigationMethod.Tab);
            Raise(page.Trigger, InputElement.PointerEnteredEvent, page.Window);
            page.Card.FlushTimers();
            page.Card.IsOpen.ShouldBeTrue();
            Press(page, PhysicalKey.Escape);
            page.Card.IsOpen.ShouldBeFalse();
            page.After.IsFocused.ShouldBeTrue();
            page.EscapesAtHost.ShouldBe(1);

            // A focused control that handles Escape keeps the card open.
            page.Card.Show();
            page.After.AddHandler(InputElement.KeyDownEvent, (_, e) => e.Handled = e.Key == Key.Escape, RoutingStrategies.Bubble);
            Press(page, PhysicalKey.Escape);
            page.Card.IsOpen.ShouldBeTrue();
        }
        finally
        {
            Close(page);
        }
    }

    // ---- accessibility ---------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Card_Is_A_Tooltip_That_Describes_Its_Trigger()
    {
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
            c.Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "postgresql.org" },
                    new TextBlock { Text = "ALTER TABLE" },
                    new TextBlock { Text = "Hidden", IsVisible = false },
                    new TextBlock { Text = "Validating a constraint takes a light lock." }
                }
            };
        });
        AutomationProperties.SetHelpText(page.Trigger, "Source 1");
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            var peer = ControlAutomationPeer.CreatePeerForElement(page.Card.Surface!);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ToolTip);
            AutomationProperties.GetHelpText(page.Trigger).ShouldBe("postgresql.org, ALTER TABLE, Validating a constraint takes a light lock.");
            FocusOn(page.After, NavigationMethod.Tab);
            AutomationProperties.GetHelpText(page.Trigger).ShouldBe("Source 1");

            // Content that describes itself wins over its text; the card's own name wins over both.
            page.Card.Content = new TextBlock { Text = "ALTER TABLE", [AutomationProperties.HelpTextProperty] = "Postgres docs, ALTER TABLE" };
            FocusOn(page.Trigger, NavigationMethod.Tab);
            AutomationProperties.GetHelpText(page.Trigger).ShouldBe("Postgres docs, ALTER TABLE");
            FocusOn(page.After, NavigationMethod.Tab);
            AutomationProperties.SetName(page.Card, "Citation preview");
            FocusOn(page.Trigger, NavigationMethod.Tab);
            AutomationProperties.GetHelpText(page.Trigger).ShouldBe("Citation preview");
        }
        finally
        {
            Close(page);
        }
    }

    [AvaloniaFact]
    public void A_Templated_Card_Describes_Its_Trigger_Once_Laid_Out()
    {
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
            c.Content = new SourceNote("Release notes", "Version 17 adds virtual generated columns.");
            c.ContentTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SourceNote>((note, _) => new StackPanel
            {
                Children = { new TextBlock { Text = note?.Title }, new TextBlock { Text = note?.Snippet } }
            });
        });
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeTrue();
            page.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetHelpText(page.Trigger).ShouldBe("Release notes, Version 17 adds virtual generated columns.");
            FocusOn(page.After, NavigationMethod.Tab);
            AutomationProperties.GetHelpText(page.Trigger).ShouldBeNull();
        }
        finally
        {
            Close(page);
        }
    }

    private sealed record SourceNote(string Title, string Snippet);

    [AvaloniaFact]
    public void Card_Padding_Reaches_The_Surface()
    {
        var page = Setup();
        try
        {
            page.Card.Show();
            Dispatcher.UIThread.RunJobs();
            page.Card.Surface!.Padding.ShouldBe(new Thickness(16));
            page.Card.CardPadding = new Thickness(16, 14);
            page.Card.Surface.Padding.ShouldBe(new Thickness(16, 14));
            page.Window.UpdateLayout();
            page.Card.Surface.Bounds.Width.ShouldBe(288, 0.5);
        }
        finally
        {
            Close(page);
        }
    }

    // ---- motion ----------------------------------------------------------------------------------------------------

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Full)]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Keyboard_Opening_Follows_The_Motion_Level(ArcMotionLevel level)
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var page = Setup(c =>
        {
            c.OpensOnFocus = true;
            c.OpenDelay = TimeSpan.Zero;
        }, level);
        try
        {
            FocusOn(page.Trigger, NavigationMethod.Tab);
            page.Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var actor = page.Card.Surface!.FindAncestorOfType<BaseMotionActor>();
            actor.ShouldNotBeNull();
            clock.Advance(TimeSpan.FromMilliseconds(34));
            var matrix = (actor.MotionTransform as Avalonia.Media.MatrixTransform)?.Matrix ?? Matrix.Identity;
            switch (level)
            {
                case ArcMotionLevel.Full:
                    // Grows from the trigger edge: still scaled down and short of its place, fading in.
                    matrix.M11.ShouldBeLessThan(1);
                    actor.Opacity.ShouldBeLessThan(1);
                    break;
                case ArcMotionLevel.Reduced:
                    // No spatial motion, only a short fade.
                    matrix.M11.ShouldBe(1, 1e-6);
                    matrix.M32.ShouldBe(0, 1e-6);
                    actor.Opacity.ShouldBeGreaterThan(0);
                    actor.Opacity.ShouldBeLessThan(1);
                    break;
                default:
                    matrix.M11.ShouldBe(1, 1e-6);
                    actor.Opacity.ShouldBe(1, 1e-6);
                    break;
            }
            clock.Advance(TimeSpan.FromSeconds(1.5));
            actor.Opacity.ShouldBe(1, 1e-3);
            page.Trigger.IsFocused.ShouldBeTrue();

            // Leaving: Off removes it at once, the others after a short fade.
            FocusOn(page.After, NavigationMethod.Tab);
            page.Card.IsOpen.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(400));
            Dispatcher.UIThread.RunJobs();
            page.Card.Popup!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            Close(page);
        }
    }
}
