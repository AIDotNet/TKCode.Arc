using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Overlays;

public class HoverCardTests
{
    private static HoverCardProfile Profile() => new()
    {
        DisplayName = "Maya Chen",
        Role = "Product designer, Payments",
        Bio = "Designs checkout flows.",
        Stats = { new HoverCardStat("Projects", "12") }
    };

    [AvaloniaFact]
    public void Keyboard_Focus_Opens_At_Once_And_Escape_Closes()
    {
        HoverCard.ResetCoordinator();
        var trigger = new Button { Content = "@maya", Variant = ButtonVariant.Ghost };
        var card = new HoverCard { Trigger = trigger, Content = Profile() };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { card } });
        try
        {
            trigger.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            card.IsOpen.ShouldBeTrue();
            card.Surface!.Bounds.Width.ShouldBe(296, 0.5);
            card.Surface.Focusable.ShouldBeFalse();
            trigger.IsFocused.ShouldBeTrue();
            AutomationProperties.GetHelpText(trigger).ShouldBe("Maya Chen, Product designer, Payments");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.IsOpen.ShouldBeFalse();
            AutomationProperties.GetHelpText(trigger).ShouldBeNull();
        }
        finally
        {
            window.Close();
            HoverCard.ResetCoordinator();
        }
    }

    private static void Raise(Control target, Avalonia.Interactivity.RoutedEvent<PointerEventArgs> routedEvent, Window window)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        var position = target.TranslatePoint(new Point(2, 2), window)!.Value;
        target.RaiseEvent(new PointerEventArgs(routedEvent, target, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None));
    }

    [AvaloniaFact]
    public void Hover_Waits_For_The_Open_Delay_Stays_Open_Over_The_Card_And_Only_One_Is_Open()
    {
        HoverCard.ResetCoordinator();
        var first = new Button { Content = "@maya" };
        var second = new Button { Content = "@ari" };
        var a = new HoverCard { Trigger = first, Content = "Maya" };
        var b = new HoverCard { Trigger = second, Content = "Ari" };
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Spacing = 60, Children = { a, b } });
        try
        {
            Raise(first, InputElement.PointerEnteredEvent, window);
            a.IsOpen.ShouldBeFalse();
            a.IsOpenScheduled.ShouldBeTrue();
            a.FlushTimers();
            a.IsOpen.ShouldBeTrue();
            // Leaving the trigger starts the grace; entering the card cancels it.
            Raise(first, InputElement.PointerExitedEvent, window);
            a.IsCloseScheduled.ShouldBeTrue();
            Raise(a.Surface!, InputElement.PointerEnteredEvent, window);
            a.IsCloseScheduled.ShouldBeFalse();
            a.IsOpen.ShouldBeTrue();
            Raise(a.Surface!, InputElement.PointerExitedEvent, window);
            // Moving to another trigger while a card is open: the page is warm, the next opens after the short intent delay.
            Raise(second, InputElement.PointerEnteredEvent, window);
            HoverCard.IsWarm.ShouldBeTrue();
            b.IsOpenScheduled.ShouldBeTrue();
            b.FlushTimers();
            b.IsOpen.ShouldBeTrue();
            a.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
            HoverCard.ResetCoordinator();
        }
    }

    [AvaloniaFact]
    public void Attached_Card_And_Profile_Parts()
    {
        HoverCard.ResetCoordinator();
        var target = new TextBlock { Text = "@maya" };
        var profile = Profile();
        var card = new HoverCard { Content = profile };
        HoverCard.SetAttach(target, card);
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(40), Children = { target } });
        try
        {
            card.Show();
            Dispatcher.UIThread.RunJobs();
            card.Parent.ShouldBe(target);
            profile.Classes.Contains(HoverCardProfile.HasStatsPseudoClass).ShouldBeTrue();
            profile.Classes.Contains(HoverCardProfile.HasBioPseudoClass).ShouldBeTrue();
            profile.Classes.Contains(HoverCardProfile.HasMetaPseudoClass).ShouldBeFalse();
            card.Hide();
            Dispatcher.UIThread.RunJobs();
            card.Parent.ShouldBeNull();
        }
        finally
        {
            window.Close();
            HoverCard.ResetCoordinator();
        }
    }
}
