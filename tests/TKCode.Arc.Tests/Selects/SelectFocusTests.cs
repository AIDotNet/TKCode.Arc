using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Selects;

/// <summary>
/// Where focus lands after a pick or a close: on the field, with the keyboard ring only when a key made the pick.
/// </summary>
public class SelectFocusTests
{
    private static SelectOption[] Options() =>
    [
        new SelectOption("pt", "Pacific Time"), new SelectOption("et", "Eastern Time"), new SelectOption("lon", "London"),
        new SelectOption("tyo", "Tokyo")
    ];

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } });
        window.Width = 600;
        window.Height = 700;
        Pump();
        return window;
    }

    private static Point OptionPoint(SelectOptionView options, int position, Window window) =>
        options.TranslatePoint(options.OptionRect(position).Center, window)!.Value;

    [AvaloniaFact]
    public void Select_Pointer_Pick_Leaves_The_Field_Focused_Without_The_Ring()
    {
        var select = new Select { Label = "Zone", Width = 300, ItemsSource = Options() };
        var window = Show(select);
        try
        {
            Click(window, select.GetVisualDescendants().OfType<Border>().First(b => b.Name == Select.TriggerPart));
            select.IsOpen.ShouldBeTrue();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            Click(window, options, OptionPoint(options, 1, window));
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("et");
            select.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Select_Keyboard_Pick_Keeps_The_Ring()
    {
        var select = new Select { Label = "Zone", Width = 300, ItemsSource = Options() };
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeFalse();
            select.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MorphSelect_Pointer_Pick_Returns_Focus_Without_The_Ring()
    {
        var select = new MorphSelect { Label = "Zone", ItemsSource = Options() };
        var window = Show(select);
        try
        {
            Click(window, select.GetVisualDescendants().OfType<Border>().First(b => b.Name == MorphSelect.TriggerPart));
            select.IsOpen.ShouldBeTrue();
            Click(window, select.OptionView!, OptionPoint(select.OptionView!, 2, window));
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("lon");
            select.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MorphSelect_Keyboard_Pick_Returns_Focus_With_The_Ring()
    {
        var select = new MorphSelect { Label = "Zone", ItemsSource = Options() };
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeFalse();
            select.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MultiSelect_Closed_By_A_Click_Takes_Focus_Back_From_Its_Search_Without_The_Ring()
    {
        var select = new MultiSelect { Label = "Labels", Width = 320, IsSearchable = true, ItemsSource = Options() };
        var window = Show(select);
        try
        {
            var trigger = select.GetVisualDescendants().OfType<Control>().First(c => c.Name == MultiSelect.TriggerPart);
            Click(window, trigger);
            select.IsOpen.ShouldBeTrue();
            TestHelpers.PumpUntil(() => TopLevel.GetTopLevel(select)!.FocusManager!.GetFocusedElement() is TextBox);
            Click(window, trigger);
            select.IsOpen.ShouldBeFalse();
            select.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MultiSelect_Closed_With_Escape_Takes_Focus_Back_With_The_Ring()
    {
        var select = new MultiSelect { Label = "Labels", Width = 320, IsSearchable = true, ItemsSource = Options() };
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            TestHelpers.PumpUntil(() => TopLevel.GetTopLevel(select)!.FocusManager!.GetFocusedElement() is TextBox);
            Press(window, PhysicalKey.Escape);
            select.IsOpen.ShouldBeFalse();
            select.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Combobox_Pointer_Pick_Shows_No_Ring()
    {
        var combo = new Combobox { Label = "Zone", Width = 300, ItemsSource = Options() };
        var window = Show(combo);
        try
        {
            Click(window, combo.InputBox!);
            combo.IsOpen.ShouldBeTrue();
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            Click(window, options, OptionPoint(options, 3, window));
            combo.IsOpen.ShouldBeFalse();
            combo.SelectedValue.ShouldBe("tyo");
            combo.InputBox!.ShouldBeFocusedWithoutRing();
            combo.GetVisualDescendants().OfType<StyledElement>().Any(HasRing).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Combobox_Keyboard_Pick_Keeps_Focus_In_The_Field()
    {
        var combo = new Combobox { Label = "Zone", Width = 300, ItemsSource = Options() };
        var window = Show(combo);
        try
        {
            combo.FocusInput();
            Pump();
            combo.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            combo.IsOpen.ShouldBeFalse();
            combo.InputBox!.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
