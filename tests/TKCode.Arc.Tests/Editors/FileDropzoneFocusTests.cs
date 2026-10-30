using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Remove hands focus to the next row's Remove: with the keyboard ring after a key only.</summary>
public class FileDropzoneFocusTests
{
    private static (FileDropzone Zone, Window Window) Show()
    {
        var zone = new FileDropzone { Width = 520 };
        zone.Items.Add(new FileDropzoneItem("a.csv", 4096));
        zone.Items.Add(new FileDropzoneItem("b.csv", 4096));
        zone.Items.Add(new FileDropzoneItem("c.csv", 4096));
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = zone });
        window.Width = 560;
        window.Height = 700;
        Pump();
        return (zone, window);
    }

    [AvaloniaFact]
    public void A_Clicked_Remove_Moves_Focus_On_Without_The_Ring()
    {
        var (zone, window) = Show();
        try
        {
            Click(window, zone.RowOf(zone.Items[0])!.RemoveButton!);
            zone.Items.Count.ShouldBe(2);
            zone.RowOf(zone.Items[0])!.RemoveButton!.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Remove_Pressed_With_Enter_Moves_Focus_On_With_The_Ring()
    {
        var (zone, window) = Show();
        try
        {
            zone.RowOf(zone.Items[0])!.RemoveButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            zone.Items.Count.ShouldBe(2);
            zone.RowOf(zone.Items[0])!.RemoveButton!.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
