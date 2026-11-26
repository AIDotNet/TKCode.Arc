using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.TextInput;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

/// <summary>
/// Tests for <see cref="TextArea"/>: multiline defaults, min/max line constraints, character counter and limit state,
/// clear button visibility and built-in <see cref="TextEditMenu"/>.
/// </summary>
public class TextAreaTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Defaults_Enable_Multiline_Wrapping_And_TextEditMenu()
    {
        var area = new TextArea { PlaceholderText = "Write a note" };
        var window = new Window { Content = area, Width = 360, Height = 300 };
        window.Show();
        try
        {
            Settle(window);
            area.AcceptsReturn.ShouldBeTrue();
            area.TextWrapping.ShouldBe(TextWrapping.Wrap);
            area.MinLines.ShouldBe(3);
            TextInputOptions.GetMultiline(area).ShouldBeTrue();
            area.ContextFlyout.ShouldBeOfType<TextEditMenu>();

            var scroller = area.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PART_ScrollViewer");
            var threeLines = scroller.MinHeight;
            threeLines.ShouldBeGreaterThan(44);

            area.MinLines = 5;
            area.MaxLines = 6;
            Settle(window);
            scroller.MinHeight.ShouldBeGreaterThan(threeLines);
            scroller.MaxHeight.ShouldBeGreaterThan(scroller.MinHeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Counter_Tracks_Length_And_Flags_AtLimit()
    {
        var area = new TextArea { ShowCounter = true, MaxLength = 10, Text = "Hello" };
        var window = new Window { Content = area, Width = 360, Height = 300 };
        window.Show();
        try
        {
            Settle(window);
            area.IsCounterVisible.ShouldBeTrue();
            area.CounterText.ShouldBe("5 / 10");
            area.Classes.Contains(":at-limit").ShouldBeFalse();

            area.Text = "0123456789";
            Settle(window);
            area.CounterText.ShouldBe("10 / 10");
            area.Classes.Contains(":at-limit").ShouldBeTrue();

            area.ShowCounter = false;
            Settle(window);
            area.IsCounterVisible.ShouldBeFalse();
            area.Classes.Contains(":at-limit").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Button_Shows_Only_When_Enabled_Focused_And_NonEmpty()
    {
        var area = new TextArea { ShowClearButton = true, Text = "Draft" };
        var window = new Window { Content = area, Width = 360, Height = 300 };
        window.Show();
        try
        {
            Settle(window);
            area.IsClearVisible.ShouldBeFalse();

            area.Focus();
            Settle(window);
            area.IsClearVisible.ShouldBeTrue();

            var clear = area.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == "PART_Clear");
            clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Settle(window);
            area.Text.ShouldBeEmpty();
            area.IsClearVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
