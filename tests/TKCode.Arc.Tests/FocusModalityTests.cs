using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests;

/// <summary>The input tracker behind focus moved by code: Pointer after a press, the keyboard method after a key.</summary>
public class FocusModalityTests
{
    [AvaloniaFact]
    public void Follows_The_Last_Press_Or_Key_Of_Each_Window()
    {
        FocusModality.Watch();
        // Filled so it hit-tests (a plain Avalonia Button has no template here and draws nothing).
        var button = new Border { Background = Brushes.Gray, Width = 80, Height = 32 };
        var window = TestHelpers.Show(new StackPanel { Children = { button } });
        var other = TestHelpers.Show(new Border());
        try
        {
            FocusModality.LastWasPointer(button).ShouldBeNull();
            FocusModality.For(button).ShouldBe(NavigationMethod.Tab);

            Click(window, button);
            FocusModality.LastWasPointer(button).ShouldBe(true);
            FocusModality.For(button).ShouldBe(NavigationMethod.Pointer);
            FocusModality.For(button, NavigationMethod.Directional).ShouldBe(NavigationMethod.Pointer);
            // Another window keeps its own state.
            FocusModality.LastWasPointer(other.Content as Border).ShouldBeNull();

            Press(window, PhysicalKey.ShiftLeft);
            FocusModality.LastWasPointer(button).ShouldBe(false);
            FocusModality.For(button).ShouldBe(NavigationMethod.Tab);
            FocusModality.For(button, NavigationMethod.Directional).ShouldBe(NavigationMethod.Directional);
        }
        finally
        {
            window.Close();
            other.Close();
        }
    }
}
