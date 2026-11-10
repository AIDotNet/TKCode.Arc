using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>The action morph: opening and closing, focus, the ways it folds back, events and automation.</summary>
public class ActionMorphTests
{
    private static (ActionMorph Morph, TextBox Field, Avalonia.Controls.Button Save) Morph()
    {
        var field = new TextBox();
        var save = new Avalonia.Controls.Button { Content = "Save" };
        ActionMorph.SetCloses(save, true);
        var morph = new ActionMorph
        {
            Label = "New list",
            Content = new StackPanel { Children = { field, save } }
        };
        return (morph, field, save);
    }

    private static void Expand(Window window, ActionMorph morph)
    {
        morph.Face!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void It_starts_as_a_named_button()
    {
        var (morph, _, _) = Morph();
        var window = TestHelpers.Show(morph);
        try
        {
            morph.IsExpanded.ShouldBeFalse();
            AutomationProperties.GetName(morph.Face!).ShouldBe("New list");
            TestHelpers.Descendant<PresenceHost>(morph, ActionMorph.PanelHostPart).IsPresent.ShouldBeFalse();
            new ActionMorph().Label.ShouldBe("Add");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_on_the_button_opens_the_panel_and_focuses_its_first_field()
    {
        var (morph, field, _) = Morph();
        var expanded = 0;
        morph.Expanded += (_, _) => expanded++;
        var window = TestHelpers.Show(morph);
        try
        {
            Expand(window, morph);
            morph.IsExpanded.ShouldBeTrue();
            expanded.ShouldBe(1);
            TestHelpers.Descendant<PresenceHost>(morph, ActionMorph.PanelHostPart).IsPresent.ShouldBeTrue();
            morph.Face!.IsTabStop.ShouldBeFalse();
            AutomationProperties.GetItemStatus(morph.Face).ShouldBe("Expanded");
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(field);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_folds_it_back_and_returns_focus_to_the_button()
    {
        var (morph, _, _) = Morph();
        var collapsed = 0;
        morph.Collapsed += (_, _) => collapsed++;
        var window = TestHelpers.Show(morph);
        try
        {
            Expand(window, morph);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            morph.IsExpanded.ShouldBeFalse();
            collapsed.ShouldBe(1);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(morph.Face);
            morph.Face!.IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_button_marked_closes_folds_it_back_after_its_click()
    {
        var (morph, _, save) = Morph();
        var window = TestHelpers.Show(morph);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            save.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_close_button_folds_it_back()
    {
        var (morph, _, _) = Morph();
        var window = TestHelpers.Show(morph);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            var close = TestHelpers.Descendant<Avalonia.Controls.Button>(morph, ActionMorph.ClosePart);
            AutomationProperties.GetName(close).ShouldBe("Close");
            close.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            morph.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_outside_folds_it_back_unless_told_not_to()
    {
        var (morph, _, _) = Morph();
        var outside = new Border { Height = 80, Background = Avalonia.Media.Brushes.Transparent };
        var root = new DockPanel();
        DockPanel.SetDock(outside, Dock.Bottom);
        root.Children.Add(outside);
        root.Children.Add(morph);
        var window = TestHelpers.Show(root);
        try
        {
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var point = new Point(200, window.Bounds.Height - 20);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            morph.IsExpanded.ShouldBeFalse();

            morph.CollapsesOnOutsidePress = false;
            morph.Expand();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            morph.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_primary_variant_is_a_pseudo_class()
    {
        var (morph, _, _) = Morph();
        morph.Variant = ActionMorphVariant.Primary;
        var window = TestHelpers.Show(morph);
        try
        {
            morph.Classes.Contains(ActionMorph.PrimaryPseudoClass).ShouldBeTrue();
            morph.Expand();
            morph.Classes.Contains(ActionMorph.ExpandedPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
