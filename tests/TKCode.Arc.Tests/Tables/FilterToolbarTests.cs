using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Tables;

public class FilterToolbarTests
{
    private static FilterField[] Fields() =>
    [
        new("status", "Status", "Open", "In progress", "Closed"),
        new("owner", "Owner", "Mina", "Theo")
    ];

    private static (FilterToolbar Toolbar, Window Window) Show(FilterToolbar toolbar)
    {
        var window = TestHelpers.Show(toolbar);
        window.Width = 900;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (toolbar, window);
    }

    private static TextBlock Announcer(FilterToolbar toolbar) =>
        toolbar.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == FilterToolbar.AnnouncerPart);

    [AvaloniaFact]
    public void Empty_Toolbar_Shows_The_Note_And_Hides_Actions()
    {
        var (toolbar, window) = Show(new FilterToolbar { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top });
        try
        {
            toolbar.HasFilters.ShouldBeFalse();
            toolbar.Classes.Contains(":empty").ShouldBeTrue();
            toolbar.Menu!.IsVisible.ShouldBeFalse();
            var clear = toolbar.GetVisualDescendants().OfType<FilterToolbarChipSlot>().Single(s => s.Name == FilterToolbar.ClearSlotPart);
            clear.IsLeaving.ShouldBeTrue();
            toolbar.Bounds.Height.ShouldBe(62, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Follow_The_Collection_And_Remove_Moves_Focus()
    {
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open"), new("owner", "Owner", "Mina") };
        var events = new List<FilterToolbarFiltersChangedEventArgs>();
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        toolbar.FiltersChanged += (_, e) => events.Add(e);
        var (_, window) = Show(toolbar);
        try
        {
            toolbar.Slots.Count.ShouldBe(2);
            var remove = FilterToolbar.FindRemove(toolbar.Slots["status"])!;
            Avalonia.Automation.AutomationProperties.GetName(remove).ShouldBe("Remove Status: Open");
            remove.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            filters.Select(f => f.Id).ShouldBe(["owner"]);
            events.Single().Kind.ShouldBe(FilterChangeKind.Removed);
            events.Single().Filter!.Id.ShouldBe("status");
            Announcer(toolbar).Text.ShouldBe("Removed Status: Open");
            FilterToolbar.FindRemove(toolbar.Slots["owner"])!.IsFocused.ShouldBeTrue();
            filters.Add(new FilterChip("priority", "Priority", "High"));
            Dispatcher.UIThread.RunJobs();
            toolbar.Slots.ContainsKey("priority").ShouldBeTrue();
            Announcer(toolbar).Text.ShouldBe("Added Priority: High");
            filters[0] = new FilterChip("owner", "Owner", "Theo");
            Dispatcher.UIThread.RunJobs();
            Announcer(toolbar).Text.ShouldBe("Owner changed to Theo");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_All_Empties_The_List()
    {
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open"), new("owner", "Owner", "Mina") };
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        var kinds = new List<FilterChangeKind>();
        toolbar.FiltersChanged += (_, e) => kinds.Add(e.Kind);
        var (_, window) = Show(toolbar);
        try
        {
            toolbar.ClearFilters();
            Dispatcher.UIThread.RunJobs();
            filters.Count.ShouldBe(0);
            kinds.ShouldBe([FilterChangeKind.Cleared]);
            Announcer(toolbar).Text.ShouldBe("All filters cleared");
            toolbar.HasFilters.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Filter_Menu_Picks_A_Field_Then_A_Value()
    {
        var filters = new ObservableCollection<FilterChip> { new("owner", "Owner", "Mina") };
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        foreach (var field in Fields())
        {
            toolbar.Fields.Add(field);
        }
        var events = new List<FilterToolbarFiltersChangedEventArgs>();
        toolbar.FiltersChanged += (_, e) => events.Add(e);
        var (_, window) = Show(toolbar);
        try
        {
            var menu = toolbar.Menu!;
            menu.IsVisible.ShouldBeTrue();
            menu.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            var fields = menu.CurrentList!;
            fields.IsFocused.ShouldBeTrue();
            fields.Rows.Select(r => r.Meta).ShouldBe([null, "Mina"]);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.CurrentField!.Id.ShouldBe("status");
            var values = menu.CurrentList!;
            values.Rows.Select(r => r.Label).ShouldBe(["Open", "In progress", "Closed"]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.IsFocused.ShouldBeTrue();
            filters.Select(f => f.Text).ShouldBe(["Owner: Mina", "Status: In progress"]);
            events.Single().Kind.ShouldBe(FilterChangeKind.Added);
            events.Single().Field!.Id.ShouldBe("status");

            // A second pick for a field replaces its chip; ← returns to the fields.
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.CurrentList!.Current.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.CurrentList!.Rows.Single(r => r.Checked).Label.ShouldBe("Mina");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.CurrentField.ShouldBeNull();
            menu.CurrentList!.Current.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            filters.Select(f => f.Text).ShouldBe(["Owner: Theo", "Status: In progress"]);
            events[^1].Kind.ShouldBe(FilterChangeKind.Changed);
            events[^1].Previous!.Value.ShouldBe("Mina");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Menu_And_Typeahead_Jumps()
    {
        var menu = new FilterMenu { Fields = Fields() };
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.CurrentList!.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "o" });
            menu.CurrentList.Current.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Lists_Are_Replaced_Two_Way()
    {
        var toolbar = new FilterToolbar { ActiveFilters = new[] { new FilterChip("status", "Status", "Open") } };
        var (_, window) = Show(toolbar);
        try
        {
            toolbar.AddFilter(new FilterChip("owner", "Owner", "Ana"));
            Dispatcher.UIThread.RunJobs();
            toolbar.ActiveFilters!.Select(f => f.Id).ShouldBe(["status", "owner"]);
            toolbar.Slots.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Search_Text_Is_Two_Way_And_Escape_Clears()
    {
        var toolbar = new FilterToolbar { IsSearchVisible = true, SearchText = "beta" };
        var (_, window) = Show(toolbar);
        try
        {
            var box = toolbar.GetVisualDescendants().OfType<TextBox>().Single();
            box.Text.ShouldBe("beta");
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            toolbar.SearchText.ShouldBe(string.Empty);
            box.Text = "gamma";
            toolbar.SearchText.ShouldBe("gamma");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Describe_Matches_Arc_Status_Text()
    {
        var a = new FilterChip("status", "Status", "Open");
        var b = new FilterChip("owner", "Owner", "Mina");
        FilterToolbar.Describe([a, b], []).ShouldBe("All filters cleared");
        FilterToolbar.Describe([a], [a, b]).ShouldBe("Added Owner: Mina");
        FilterToolbar.Describe([a], [new FilterChip("status", "Status", "Closed")]).ShouldBe("Status changed to Closed");
        FilterToolbar.Describe([a, b], [b]).ShouldBe("Removed Status: Open");
    }

    [AvaloniaFact]
    public void A_Toolbar_Is_Collected_While_Its_Filters_Live_On()
    {
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open") };
        var weak = TestHelpers.ShowAndClose(() => new FilterToolbar { ActiveFilters = filters });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        filters.Add(new FilterChip("owner", "Owner", "Mina"));
        filters.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_Toolbar_Taken_Out_And_Put_Back_Shows_What_Changed_Meanwhile()
    {
        var filters = new ObservableCollection<FilterChip> { new("status", "Status", "Open") };
        var toolbar = new FilterToolbar { ActiveFilters = filters };
        var host = new Border { Child = toolbar };
        var window = TestHelpers.Show(host);
        window.Width = 900;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        try
        {
            toolbar.Slots.Keys.ShouldBe(["status"]);

            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            filters.Add(new FilterChip("owner", "Owner", "Mina"));
            filters.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            // Away from the window the chips do not follow the list.
            toolbar.Slots.Keys.ShouldBe(["status"]);

            host.Child = toolbar;
            Dispatcher.UIThread.RunJobs();
            toolbar.Slots.Keys.ShouldBe(["owner"]);
            toolbar.HasFilters.ShouldBeTrue();

            filters.Clear();
            Dispatcher.UIThread.RunJobs();
            toolbar.Slots.ShouldBeEmpty();
            toolbar.HasFilters.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
