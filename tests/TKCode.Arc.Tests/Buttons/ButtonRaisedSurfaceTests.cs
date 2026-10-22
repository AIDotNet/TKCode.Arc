using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Buttons;

/// <summary>Ghost buttons on raised surfaces (dialogs, drawers, sheets, popovers, menus) use a hover fill that stays visible.</summary>
public class ButtonRaisedSurfaceTests
{
    [AvaloniaFact]
    public void Buttons_Inside_A_Dialog_Surface_Are_Raised_And_Others_Are_Not()
    {
        var inside = new Button { Content = "Add folder", Variant = ButtonVariant.Ghost };
        var outside = new Button { Content = "Elsewhere", Variant = ButtonVariant.Ghost };
        var surface = new DialogSurface { Title = "Create project", Content = inside };
        var panel = new StackPanel { Children = { surface, outside } };
        var window = TestHelpers.Show(panel);
        try
        {
            ArcSurface.GetElevation(surface).ShouldBe(ArcSurfaceElevation.Raised);
            inside.Classes.Contains(Button.RaisedPseudoClass).ShouldBeTrue();
            outside.Classes.Contains(Button.RaisedPseudoClass).ShouldBeFalse();

            // Moving the button out of the surface drops the pseudo-class
            surface.Content = null;
            panel.Children.Add(inside);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            inside.Classes.Contains(Button.RaisedPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ghost_Hover_On_A_Raised_Surface_Is_Clearly_Off_The_Surface_Fill()
    {
        var raised = new Button { Content = "Add folder", Variant = ButtonVariant.Ghost };
        var resting = new Button { Content = "Plain", Variant = ButtonVariant.Ghost };
        ArcSurface.SetElevation(raised, ArcSurfaceElevation.Raised);
        var window = TestHelpers.Show(new StackPanel { Children = { raised, resting } });
        try
        {
            // :pointerover cannot be set from outside; the pressed state shares the hover fill
            ((IPseudoClasses)raised.Classes).Set(":pressed", true);
            ((IPseudoClasses)resting.Classes).Set(":pressed", true);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var raisedFill = ((ISolidColorBrush)raised.Background!).Color;
            var restingFill = ((ISolidColorBrush)resting.Background!).Color;
            raisedFill.ShouldNotBe(restingFill);
            var surface = window.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? Color.Parse("#1F1F1F") : Colors.White;
            Math.Abs(raisedFill.R - surface.R).ShouldBeGreaterThan(Math.Abs(restingFill.R - surface.R));
        }
        finally
        {
            window.Close();
        }
    }
}
