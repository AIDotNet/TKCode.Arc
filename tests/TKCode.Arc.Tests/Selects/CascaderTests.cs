using System.Collections.ObjectModel;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Selects;

public class CascaderTests
{
    private sealed record Region(string Code, string Name, IReadOnlyList<Region>? Children = null, bool Disabled = false);

    private static IReadOnlyList<Region> Data() =>
    [
        new("44", "Guangdong",
        [
            new("4403", "Shenzhen", [new("440305", "Nanshan"), new("440304", "Futian")]),
            new("4401", "Guangzhou", [new("440106", "Tianhe")]),
        ]),
        new("33", "Zhejiang",
        [
            new("3301", "Hangzhou", [new("330106", "Xihu"), new("330102", "Nanshan")]),
        ]),
        new("11", "Beijing"),
        new("99", "Closed", [new("9901", "Hidden")], Disabled: true),
    ];

    private static Cascader Create(IEnumerable<Region>? data = null)
    {
        var cascader = new Cascader
        {
            Label = "Region",
            ChildrenSelector = node => ((Region)node).Children,
            TextSelector = node => ((Region)node).Name,
            ValueSelector = node => ((Region)node).Code,
            DisabledSelector = node => ((Region)node).Disabled,
        };
        if (data is not null)
        {
            cascader.ItemsSource = data;
        }
        return cascader;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, level);
        window.Width = 700;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Invoke(Cascader cascader, int column, int position) =>
        ((ICascaderColumnOwner)cascader).OnColumnInvoked(cascader.ColumnsPanel!.Columns[column], position);

    private static IEnumerable<string> Names(CascaderColumn column) => column.Nodes.Cast<Region>().Select(r => r.Name);

    [AvaloniaFact]
    public void Clicking_A_Parent_Opens_Its_Children_And_A_Leaf_Selects_And_Closes()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = cascader.ColumnsPanel!;
            panel.Columns.Count.ShouldBe(1);

            Invoke(cascader, 0, 0);
            panel.Columns.Count.ShouldBe(2);
            Names(panel.Columns[1]).ShouldBe(["Shenzhen", "Guangzhou"]);
            cascader.IsOpen.ShouldBeTrue();
            cascader.SelectedItem.ShouldBeNull();

            Invoke(cascader, 1, 0);
            panel.Columns.Count.ShouldBe(3);
            Names(panel.Columns[2]).ShouldBe(["Nanshan", "Futian"]);

            Invoke(cascader, 2, 0);
            cascader.IsOpen.ShouldBeFalse();
            cascader.SelectedValue.ShouldBe("440305");
            cascader.SelectedPath.Cast<Region>().Select(r => r.Name).ShouldBe(["Guangdong", "Shenzhen", "Nanshan"]);
            cascader.SelectedText.ShouldBe("Guangdong / Shenzhen / Nanshan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_The_Parent_Collapses_The_Deeper_Columns()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Open();
            Invoke(cascader, 0, 0);
            Invoke(cascader, 1, 0);
            cascader.ColumnsPanel!.Columns.Count.ShouldBe(3);
            Invoke(cascader, 0, 1);
            cascader.ColumnsPanel.Columns.Count.ShouldBe(2);
            Names(cascader.ColumnsPanel.Columns[1]).ShouldBe(["Hangzhou"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Parents_Are_Not_Selectable_Unless_LeafOnly_Is_Off()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Open();
            Invoke(cascader, 0, 0);
            cascader.SelectedItem.ShouldBeNull();

            cascader.LeafOnly = false;
            Invoke(cascader, 0, 1);
            cascader.SelectedValue.ShouldBe("33");
            cascader.IsOpen.ShouldBeTrue();
            cascader.SelectedText.ShouldBe("Zhejiang");
            cascader.ColumnsPanel!.Columns.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Nodes_Can_Neither_Be_Picked_Nor_Opened()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Open();
            Invoke(cascader, 0, 3);
            cascader.ColumnsPanel!.Columns.Count.ShouldBe(1);
            cascader.LeafOnly = false;
            Invoke(cascader, 0, 3);
            cascader.SelectedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selected_Item_Value_And_Path_Stay_In_Step()
    {
        var data = Data();
        var cascader = Create(data);
        var window = Show(cascader);
        try
        {
            var events = new List<CascaderSelectionChangedEventArgs>();
            cascader.SelectionChanged += (_, e) => events.Add(e);

            cascader.SelectedValue = "330102";
            var xihu = data[1].Children![0].Children![1];
            cascader.SelectedItem.ShouldBeSameAs(xihu);
            cascader.SelectedPath.Count.ShouldBe(3);
            events.Count.ShouldBe(1);
            events[0].OldPath.ShouldBeEmpty();
            events[0].NewPath.Count.ShouldBe(3);

            cascader.SelectedItem = data[0].Children![1].Children![0];
            cascader.SelectedValue.ShouldBe("440106");
            cascader.SelectedText.ShouldBe("Guangdong / Guangzhou / Tianhe");
            events.Count.ShouldBe(2);

            cascader.SelectedValue = null;
            cascader.SelectedItem.ShouldBeNull();
            cascader.SelectedPath.ShouldBeEmpty();
            cascader.SelectedText.ShouldBeNull();
            events.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nodes_With_The_Same_Name_Are_Told_Apart_By_Value()
    {
        var data = Data();
        var cascader = Create(data);
        var window = Show(cascader);
        try
        {
            cascader.SelectedValue = "330102"; // Nanshan in Hangzhou, not the one in Shenzhen
            cascader.SelectedPath.Cast<Region>().Select(r => r.Code).ShouldBe(["33", "3301", "330102"]);
            cascader.SelectedValue = "440305";
            cascader.SelectedPath.Cast<Region>().Select(r => r.Code).ShouldBe(["44", "4403", "440305"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Value_Written_Before_The_Data_Is_Kept_And_Resolved_When_It_Arrives()
    {
        var cascader = Create();
        var window = Show(cascader);
        try
        {
            cascader.SelectedValue = "440304";
            cascader.SelectedValue.ShouldBe("440304");
            cascader.SelectedItem.ShouldBeNull();
            cascader.SelectedPath.ShouldBeEmpty();
            cascader.SelectedText.ShouldBeNull();

            cascader.ItemsSource = Data();
            cascader.SelectedValue.ShouldBe("440304");
            cascader.SelectedPath.Cast<Region>().Select(r => r.Name).ShouldBe(["Guangdong", "Shenzhen", "Futian"]);
            cascader.SelectedText.ShouldBe("Guangdong / Shenzhen / Futian");

            // Data swapped for a set that lacks the node: the value stays, the item goes.
            cascader.ItemsSource = new[] { new Region("11", "Beijing") };
            cascader.SelectedValue.ShouldBe("440304");
            cascader.SelectedItem.ShouldBeNull();
            cascader.SelectedPath.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Items_Added_To_An_Observable_Source_Resolve_A_Waiting_Value()
    {
        var source = new ObservableCollection<Region>();
        var cascader = Create(source);
        var window = Show(cascader);
        try
        {
            cascader.SelectedValue = "11";
            cascader.SelectedItem.ShouldBeNull();
            source.Add(new Region("11", "Beijing"));
            cascader.SelectedText.ShouldBe("Beijing");
            cascader.SelectedItem.ShouldBeSameAs(source[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShowFullPath_And_PathSeparator_Shape_The_Text()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.SelectedValue = "440305";
            cascader.PathSeparator = " > ";
            cascader.SelectedText.ShouldBe("Guangdong > Shenzhen > Nanshan");
            cascader.ShowFullPath = false;
            cascader.SelectedText.ShouldBe("Nanshan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clear_Empties_The_Selection_Without_Opening_The_Menu()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.SelectedValue = "11";
            var button = TestHelpers.Descendant<AvaloniaButton>(cascader, Cascader.ClearPart);
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(AvaloniaButton.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            cascader.SelectedValue.ShouldBeNull();
            cascader.SelectedItem.ShouldBeNull();
            cascader.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReadOnly_Opens_But_Cannot_Change_Or_Clear()
    {
        var cascader = Create(Data());
        cascader.SelectedValue = "11";
        cascader.IsReadOnly = true;
        var window = Show(cascader);
        try
        {
            cascader.Open();
            cascader.IsOpen.ShouldBeTrue();
            Invoke(cascader, 0, 0);
            Invoke(cascader, 1, 0);
            Invoke(cascader, 2, 0);
            cascader.SelectedValue.ShouldBe("11");
            cascader.Clear();
            cascader.SelectedValue.ShouldBe("11");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Restores_The_Columns_Of_The_Selected_Path()
    {
        var cascader = Create(Data());
        cascader.SelectedValue = "330106";
        var window = Show(cascader);
        try
        {
            cascader.Open();
            Dispatcher.UIThread.RunJobs();
            var columns = cascader.ColumnsPanel!.Columns;
            columns.Count.ShouldBe(3);
            Names(columns[2]).ShouldBe(["Xihu", "Nanshan"]);
            columns[2].Options.ActiveOption.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Right_Left_Enter_And_Escape_Walk_The_Columns()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            cascader.IsOpen.ShouldBeTrue();
            var panel = cascader.ColumnsPanel!;
            panel.Columns[0].Options.ActiveOption.ShouldBe(0);

            Press(window, PhysicalKey.ArrowDown); // Zhejiang
            Press(window, PhysicalKey.ArrowRight);
            cascader.ActiveLevel.ShouldBe(1);
            panel.Columns.Count.ShouldBe(2);
            panel.Columns[1].Options.ActiveOption.ShouldBe(0);

            Press(window, PhysicalKey.ArrowLeft);
            cascader.ActiveLevel.ShouldBe(0);
            panel.Columns[0].Options.ActiveOption.ShouldBe(1);

            Press(window, PhysicalKey.Escape);
            cascader.IsOpen.ShouldBeFalse();
            cascader.SelectedItem.ShouldBeNull();

            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.ArrowDown); // Beijing, a leaf
            Press(window, PhysicalKey.Enter);
            cascader.IsOpen.ShouldBeFalse();
            cascader.SelectedValue.ShouldBe("11");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Enter_On_A_Parent_Opens_It_When_LeafOnly()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.Enter);
            cascader.IsOpen.ShouldBeTrue();
            cascader.SelectedItem.ShouldBeNull();
            cascader.ColumnsPanel!.Columns.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Skip_Disabled_Nodes()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            cascader.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            Press(window, PhysicalKey.End);
            cascader.ColumnsPanel!.Columns[0].Options.ActiveOption.ShouldBe(2); // "Closed" is disabled
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Expands_Parents_Without_A_Click()
    {
        var cascader = Create(Data());
        cascader.ExpandTrigger = CascaderExpandTrigger.Hover;
        var window = Show(cascader);
        try
        {
            cascader.Open();
            ((ICascaderColumnOwner)cascader).OnColumnHovered(cascader.ColumnsPanel!.Columns[0], 1);
            cascader.ColumnsPanel.Columns.Count.ShouldBe(2);
            cascader.SelectedItem.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_Clears_A_Closed_Field()
    {
        var cascader = Create(Data());
        cascader.SelectedValue = "11";
        var window = Show(cascader);
        try
        {
            cascader.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Delete);
            cascader.SelectedValue.ShouldBeNull();
            cascader.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Template_Parts_And_Token_Are_Wired()
    {
        var cascader = Create(Data());
        var window = Show(cascader);
        try
        {
            TestHelpers.Descendant<Border>(cascader, Cascader.TriggerPart).ShouldNotBeNull();
            cascader.ColumnsPanel.ShouldNotBeNull();
            typeof(CascaderToken).GetProperties().Length.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Width_And_Redirects_When_The_Parent_Changes()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var cascader = Create(Data());
        var window = Show(cascader, ArcMotionLevel.Full);
        try
        {
            cascader.Open();
            Run(clock, 64);
            var panel = cascader.ColumnsPanel!;
            var one = panel.TargetWidth;
            panel.IsWidthAnimating.ShouldBeFalse();

            Invoke(cascader, 0, 0);
            Dispatcher.UIThread.RunJobs();
            panel.TargetWidth.ShouldBeGreaterThan(one);
            panel.CurrentWidth.ShouldBeLessThan(panel.TargetWidth);
            Run(clock, 48);
            var midway = panel.CurrentWidth;
            midway.ShouldBeGreaterThan(one);

            // A third column opened mid-flight redirects the spring to the larger total instead of waiting.
            var two = panel.TargetWidth;
            Invoke(cascader, 1, 0);
            Dispatcher.UIThread.RunJobs();
            panel.TargetWidth.ShouldBeGreaterThan(two);
            Run(clock, 1500);
            panel.CurrentWidth.ShouldBe(panel.TargetWidth, 0.5);
            panel.IsWidthAnimating.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    private static void Run(ManualFrameClock clock, int milliseconds)
    {
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 16)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }
}
