using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// The Special package's focus moves follow the input (SpecialFocusModality): closing the column editor hands focus back
/// to Edit with the keyboard ring after a key only.
/// </summary>
public class EnrichColumnFocusTests
{
    private static (EnrichColumn Column, Window Window) Show()
    {
        var column = new EnrichColumn { Simulation = EnrichColumnTestData.Simulation(), Clock = new EnrichColumnTestClock() };
        var window = TestHelpers.Show(column);
        window.Width = 720;
        window.Height = 600;
        Pump();
        return (column, window);
    }

    private static Avalonia.Controls.Button Part(EnrichColumn column, string name) =>
        column.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == name);

    [AvaloniaFact]
    public void Cancel_Clicked_Returns_Focus_To_Edit_Without_The_Ring()
    {
        var (column, window) = Show();
        try
        {
            Click(window, column.EditButton!);
            column.IsEditing.ShouldBeTrue();
            Click(window, Part(column, EnrichColumn.CancelPart));
            column.IsEditing.ShouldBeFalse();
            column.EditButton!.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Pressed_With_Enter_Returns_Focus_To_Edit_With_The_Ring()
    {
        var (column, window) = Show();
        try
        {
            column.EditButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            column.IsEditing.ShouldBeTrue();
            Part(column, EnrichColumn.CancelPart).Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            column.IsEditing.ShouldBeFalse();
            column.EditButton.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
