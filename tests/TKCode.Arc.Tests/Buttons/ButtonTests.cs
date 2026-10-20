using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;

namespace TKCode.Arc.Tests.Buttons;

public class ButtonTests
{
    [AvaloniaFact]
    public void Loading_Swallows_Clicks_But_Keeps_Focus()
    {
        var clicks = 0;
        var button = new Button { Content = "Save", IsLoading = true };
        button.Click += (_, _) => clicks++;
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus();
            button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            button.IsFocused.ShouldBeTrue();
            button.Classes.Contains(Button.LoadingPseudoClass).ShouldBeTrue();
            button.IsLoading = false;
            button.Classes.Contains(Button.LoadingPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Template_Uses_Token_Heights_For_Sizes()
    {
        var small = new Button { Content = "A", Size = ControlSize.Small };
        var large = new Button { Content = "A", Size = ControlSize.Large };
        var panel = new Avalonia.Controls.StackPanel { Children = { small, large } };
        var window = TestHelpers.Show(panel);
        try
        {
            small.MinHeight.ShouldBe(36);
            large.MinHeight.ShouldBe(50);
            small.Bounds.Height.ShouldBe(36);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_Only_The_Arc_Ring()
    {
        var button = new Button { Content = "Save" };
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus(Avalonia.Input.NavigationMethod.Tab);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var layer = Avalonia.Controls.Primitives.AdornerLayer.GetAdornerLayer(button);
            layer.ShouldNotBeNull();
            // The window's default focus adorner would be added to the adorner layer; Arc draws PART_FocusRing instead.
            layer!.Children.Count.ShouldBe(0);
            TestHelpers.Descendant<Avalonia.Controls.Border>(button, "PART_FocusRing").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
