using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Tables;

/// <summary>
/// Removing filters hands focus on (to a neighbour's remove, or to Add filter once none is left): with the keyboard ring
/// after a key only.
/// </summary>
public class FilterToolbarFocusTests
{
    private static (FilterToolbar Toolbar, ObservableCollection<FilterChip> Filters, Window Window) Show()
    {
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open"), new("owner", "Owner", "Mina") };
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        toolbar.Fields.Add(new FilterField("status", "Status", "Open", "In progress", "Closed"));
        toolbar.Fields.Add(new FilterField("owner", "Owner", "Mina", "Theo"));
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = toolbar });
        window.Width = 900;
        window.Height = 400;
        Pump();
        return (toolbar, filters, window);
    }

    private static Avalonia.Controls.Button ClearAll(FilterToolbar toolbar) =>
        toolbar.GetVisualDescendants().OfType<Avalonia.Controls.Button>().First(b => b.Name == FilterToolbar.ClearAllPart);

    [AvaloniaFact]
    public void Clear_All_Clicked_Hands_Focus_To_Add_Filter_Without_The_Ring()
    {
        var (toolbar, filters, window) = Show();
        try
        {
            Click(window, ClearAll(toolbar));
            filters.ShouldBeEmpty();
            toolbar.Menu!.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_All_Pressed_With_Enter_Hands_Focus_To_Add_Filter_With_The_Ring()
    {
        var (toolbar, filters, window) = Show();
        try
        {
            ClearAll(toolbar).Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            filters.ShouldBeEmpty();
            toolbar.Menu!.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Remove_Pressed_With_Enter_Moves_Focus_On_With_The_Ring()
    {
        var (toolbar, filters, window) = Show();
        try
        {
            FilterToolbar.FindRemove(toolbar.Slots["status"])!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            filters.Select(f => f.Id).ShouldBe(["owner"]);
            var next = FilterToolbar.FindRemove(toolbar.Slots["owner"])!;
            next.IsFocused.ShouldBeTrue();
            HasRing(next).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
