using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Selects;

public class TreeSelectTests
{
    private sealed record Entry(string Path, string Name, IReadOnlyList<Entry>? Children = null, bool Disabled = false);

    // src/ app/ (App.axaml, App.axaml.cs), components/ (Button.cs, Card.cs disabled), Program.cs; docs/ (readme.md, appendix.md)
    private static IReadOnlyList<Entry> Data() =>
    [
        new("src", "src",
        [
            new("src/app", "app", [new("src/app/App.axaml", "App.axaml"), new("src/app/App.axaml.cs", "App.axaml.cs")]),
            new("src/components", "components", [new("src/components/Button.cs", "Button.cs"), new("src/components/Card.cs", "Card.cs", Disabled: true)]),
            new("src/Program.cs", "Program.cs"),
        ]),
        new("docs", "docs", [new("docs/readme.md", "readme.md"), new("docs/appendix.md", "appendix.md")]),
    ];

    private static TreeSelect Create(IEnumerable<Entry>? data = null, TreeSelectMode mode = TreeSelectMode.Single)
    {
        var select = new TreeSelect
        {
            Label = "Files",
            SelectionMode = mode,
            ChildrenSelector = node => ((Entry)node).Children,
            TextSelector = node => ((Entry)node).Name,
            ValueSelector = node => ((Entry)node).Path,
            DisabledSelector = node => ((Entry)node).Disabled,
        };
        if (data is not null)
        {
            select.ItemsSource = data;
        }
        return select;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, level);
        window.Width = 700;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static IEnumerable<string> Rows(TreeSelect select) => select.VisibleRows.Cast<TreeSelectNode>().Select(n => n.Label);

    private static TreeSelectNode Node(TreeSelect select, IReadOnlyList<Entry> data, string path) =>
        select.NodeFor(Find(data, path)!)!;

    private static Entry? Find(IEnumerable<Entry> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (node.Path == path)
            {
                return node;
            }
            if (node.Children is { } children && Find(children, path) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Spec()
    {
        var select = new TreeSelect();
        select.SelectionMode.ShouldBe(TreeSelectMode.Single);
        select.CheckStrategy.ShouldBe(TreeSelectCheckStrategy.Leaves);
        select.IsSearchable.ShouldBeTrue();
        select.LeafOnly.ShouldBeFalse();
        select.ExpandDepth.ShouldBe(0);
        select.ShowFullPath.ShouldBeFalse();
        select.PathSeparator.ShouldBe(" / ");
        select.MaxVisibleChips.ShouldBe(3);
        select.IsClearable.ShouldBeTrue();
        select.MaxDropDownHeight.ShouldBe(320);
        select.SelectedItems.ShouldBeEmpty();
        select.SelectedValues.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Search_Keeps_Ancestors_Opens_Them_And_Clearing_Restores_The_Open_State()
    {
        var data = Data();
        var select = Create(data);
        var window = Show(select);
        try
        {
            select.Expand(data[1]);
            select.Open();
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldBe(["src", "docs", "readme.md", "appendix.md"]);

            select.SearchText = "app";
            Dispatcher.UIThread.RunJobs();
            // "app" matches the folder (its files follow it) and the files under it; appendix.md keeps docs.
            Rows(select).ShouldBe(["src", "app", "App.axaml", "App.axaml.cs", "docs", "appendix.md"]);
            select.IsItemExpanded(data[0]).ShouldBeTrue();
            Node(select, data, "src/app/App.axaml").MatchStart.ShouldBe(0);
            Node(select, data, "docs/appendix.md").MatchLength.ShouldBe(3);
            select.ActiveItem.ShouldBe(Find(data, "src/app"));

            select.SearchText = null;
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldBe(["src", "docs", "readme.md", "appendix.md"]);
            select.IsItemExpanded(data[0]).ShouldBeFalse();
            Node(select, data, "src/app/App.axaml").MatchStart.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_In_The_Open_Field_Searches_And_No_Match_Shows_The_Empty_Line()
    {
        var select = Create(Data());
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            select.IsOpen.ShouldBeTrue();
            select.SearchBox.ShouldNotBeNull();
            select.SearchBox!.IsFocused.ShouldBeTrue();

            window.KeyTextInput("read");
            Dispatcher.UIThread.RunJobs();
            select.SearchText.ShouldBe("read");
            Rows(select).ShouldBe(["docs", "readme.md"]);

            window.KeyTextInput("zz");
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldBeEmpty();
            select.EmptyHost!.IsPresent.ShouldBeTrue();

            // Escape clears the search first, then closes.
            Press(window, PhysicalKey.Escape);
            select.SearchText.ShouldBeNullOrEmpty();
            select.IsOpen.ShouldBeTrue();
            select.EmptyHost.IsPresent.ShouldBeFalse();
            Press(window, PhysicalKey.Escape);
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FilterPredicate_Replaces_The_Default_Match()
    {
        var data = Data();
        var select = Create(data);
        select.FilterPredicate = (node, text) => ((Entry)node).Path.EndsWith(text, StringComparison.Ordinal);
        var window = Show(select);
        try
        {
            select.Open();
            select.SearchText = ".md";
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldBe(["docs", "readme.md", "appendix.md"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checking_A_Parent_Checks_Enabled_Descendants_And_Parents_Show_Three_States()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            var src = Node(select, data, "src");
            var components = Node(select, data, "src/components");
            var card = Node(select, data, "src/components/Card.cs");

            select.ToggleNode(components);
            Node(select, data, "src/components/Button.cs").State.ShouldBe(CheckboxState.Checked);
            card.State.ShouldBe(CheckboxState.Unchecked); // disabled: not carried along
            components.State.ShouldBe(CheckboxState.Checked); // the disabled child does not hold it back
            src.State.ShouldBe(CheckboxState.Indeterminate);

            select.ToggleNode(src);
            src.State.ShouldBe(CheckboxState.Checked);
            card.State.ShouldBe(CheckboxState.Unchecked);
            select.SelectedValues.ShouldBe(["src/app/App.axaml", "src/app/App.axaml.cs", "src/components/Button.cs", "src/Program.cs"]);

            // A disabled node cannot be toggled by the user.
            select.ToggleNode(card);
            card.State.ShouldBe(CheckboxState.Unchecked);

            select.ToggleNode(src);
            src.State.ShouldBe(CheckboxState.Unchecked);
            select.SelectedItems.ShouldBeEmpty();
            select.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CheckStrategy_Decides_What_Is_Reported()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            select.ToggleNode(Node(select, data, "src"));
            select.ToggleNode(Node(select, data, "docs/readme.md"));

            select.SelectedValues.ShouldBe(["src/app/App.axaml", "src/app/App.axaml.cs", "src/components/Button.cs", "src/Program.cs", "docs/readme.md"]);

            select.CheckStrategy = TreeSelectCheckStrategy.Parents;
            select.SelectedValues.ShouldBe(["src", "docs/readme.md"]);

            select.CheckStrategy = TreeSelectCheckStrategy.All;
            select.SelectedValues.ShouldBe(["src", "src/app", "src/app/App.axaml", "src/app/App.axaml.cs", "src/components",
                "src/components/Button.cs", "src/Program.cs", "docs/readme.md"]);
            select.SelectedItems.Count.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_SelectedValues_Checks_The_Nodes_Even_Before_The_Data_Arrives()
    {
        var data = Data();
        var select = Create(mode: TreeSelectMode.Multiple);
        select.SelectedValues = ["src/app", "docs/readme.md"];
        select.SelectedValues.ShouldBe(["src/app", "docs/readme.md"]);
        var window = Show(select);
        try
        {
            select.ItemsSource = data;
            Dispatcher.UIThread.RunJobs();
            Node(select, data, "src/app").State.ShouldBe(CheckboxState.Checked);
            Node(select, data, "docs").State.ShouldBe(CheckboxState.Indeterminate);
            // Reported by the strategy: a parent's value stands for its leaves.
            select.SelectedValues.ShouldBe(["src/app/App.axaml", "src/app/App.axaml.cs", "docs/readme.md"]);

            select.SelectedValues = ["src/Program.cs"];
            Node(select, data, "src/app").State.ShouldBe(CheckboxState.Unchecked);
            Node(select, data, "src/Program.cs").State.ShouldBe(CheckboxState.Checked);
            select.SelectedItems.ShouldBe([Find(data, "src/Program.cs")!]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectionChanged_Carries_The_Added_And_Removed_Items()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            var events = new List<TreeSelectSelectionChangedEventArgs>();
            select.SelectionChanged += (_, e) => events.Add(e);
            select.ToggleNode(Node(select, data, "src/app"));
            events.Count.ShouldBe(1);
            events[0].AddedItems.ShouldBe([Find(data, "src/app/App.axaml")!, Find(data, "src/app/App.axaml.cs")!]);
            events[0].RemovedItems.ShouldBeEmpty();
            events[0].RoutedEvent.ShouldBe(TreeSelect.SelectionChangedEvent);

            select.ToggleNode(Node(select, data, "src/app/App.axaml"));
            events.Count.ShouldBe(2);
            events[1].AddedItems.ShouldBeEmpty();
            events[1].RemovedItems.ShouldBe([Find(data, "src/app/App.axaml")!]);

            select.Clear();
            events.Count.ShouldBe(3);
            events[2].RemovedItems.ShouldBe([Find(data, "src/app/App.axaml.cs")!]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Pick_Selects_And_Closes_And_LeafOnly_Only_Opens_Parents()
    {
        var data = Data();
        var select = Create(data);
        select.ShowFullPath = true;
        var window = Show(select);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            select.PickNode(Node(select, data, "src"));
            select.SelectedValue.ShouldBe("src");
            select.IsOpen.ShouldBeFalse();

            select.LeafOnly = true;
            select.Open();
            select.PickNode(Node(select, data, "docs"));
            select.SelectedValue.ShouldBe("src");
            select.IsOpen.ShouldBeTrue();
            select.IsItemExpanded(data[1]).ShouldBeTrue();

            select.PickNode(Node(select, data, "docs/readme.md"));
            select.SelectedValue.ShouldBe("docs/readme.md");
            select.SelectedItem.ShouldBe(Find(data, "docs/readme.md"));
            select.SelectedText.ShouldBe("docs / readme.md");
            select.SelectedValues.ShouldBe(["docs/readme.md"]);

            // A disabled node cannot be picked.
            select.Open();
            select.PickNode(Node(select, data, "src/components/Card.cs"));
            select.SelectedValue.ShouldBe("docs/readme.md");

            // Opening again shows the selection: its ancestors open.
            select.Close();
            select.Collapse(data[1]);
            select.Open();
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldContain("readme.md");
            select.ActiveItem.ShouldBe(Find(data, "docs/readme.md"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedValue_Written_First_Is_Kept_Until_The_Data_Arrives()
    {
        var data = Data();
        var select = Create();
        select.SelectedValue = "docs/appendix.md";
        var window = Show(select);
        try
        {
            select.SelectedItem.ShouldBeNull();
            select.ItemsSource = data;
            Dispatcher.UIThread.RunJobs();
            select.SelectedItem.ShouldBe(Find(data, "docs/appendix.md"));
            select.SelectedText.ShouldBe("appendix.md");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Walks_The_Tree_And_Picks()
    {
        var data = Data();
        var select = Create(data);
        select.IsSearchable = false;
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeTrue();
            select.ActiveItem.ShouldBe(data[0]);

            Press(window, PhysicalKey.ArrowRight); // opens src
            select.IsItemExpanded(data[0]).ShouldBeTrue();
            Press(window, PhysicalKey.ArrowRight); // into app
            select.ActiveItem.ShouldBe(Find(data, "src/app"));
            Press(window, PhysicalKey.ArrowDown);
            select.ActiveItem.ShouldBe(Find(data, "src/components"));
            Press(window, PhysicalKey.ArrowLeft); // to the parent
            select.ActiveItem.ShouldBe(data[0]);
            Press(window, PhysicalKey.ArrowLeft); // closes src
            select.IsItemExpanded(data[0]).ShouldBeFalse();
            Press(window, PhysicalKey.End);
            select.ActiveItem.ShouldBe(data[1]);
            Press(window, PhysicalKey.Home);
            select.ActiveItem.ShouldBe(data[0]);

            Press(window, PhysicalKey.End);
            Press(window, PhysicalKey.ArrowRight);
            Press(window, PhysicalKey.ArrowDown);
            Press(window, PhysicalKey.Enter);
            select.IsOpen.ShouldBeFalse();
            select.SelectedValue.ShouldBe("docs/readme.md");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Space_And_Enter_Toggle_Checks_And_Backspace_Removes_The_Last_Chip()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            select.IsOpen.ShouldBeTrue();
            Press(window, PhysicalKey.End); // docs
            Press(window, PhysicalKey.Space);
            select.SelectedValues.ShouldBe(["docs/readme.md", "docs/appendix.md"]);
            select.IsOpen.ShouldBeTrue();
            select.ChipRow!.ChipLabels.ShouldBe(["readme.md", "appendix.md"]);

            Press(window, PhysicalKey.Backspace);
            select.SelectedValues.ShouldBe(["docs/readme.md"]);

            Press(window, PhysicalKey.Enter);
            select.SelectedValues.ShouldBe(["docs/readme.md", "docs/appendix.md"]);
            Press(window, PhysicalKey.Enter);
            select.SelectedValues.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Fold_Past_MaxVisibleChips()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        select.MaxVisibleChips = 2;
        var window = Show(select);
        try
        {
            select.ToggleNode(Node(select, data, "src"));
            select.ChipRow!.ChipLabels.ShouldBe(["App.axaml", "App.axaml.cs"]);
            select.ChipRow.MoreCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_Visible_Rows_Are_Built_For_Ten_Thousand_Nodes()
    {
        var roots = Enumerable.Range(0, 100)
            .Select(i => new Entry($"g{i}", $"Group {i}", Enumerable.Range(0, 99).Select(j => new Entry($"g{i}/{j}", $"Item {i}.{j}")).ToList()))
            .ToList();
        var select = Create(roots, TreeSelectMode.Multiple);
        select.ExpandDepth = -1;
        var window = Show(select);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            select.VisibleRows.Count.ShouldBe(10_000);
            var realized = select.RowsPanel!.RealizedRows.Count();
            realized.ShouldBeGreaterThan(0);
            realized.ShouldBeLessThan(60);

            select.ToggleNode(select.NodeFor(roots[0])!);
            select.SelectedValues.Count.ShouldBe(99);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ExpandDepth_Opens_Levels_When_The_Data_Arrives()
    {
        var data = Data();
        var select = Create();
        select.ExpandDepth = 1;
        select.ItemsSource = data;
        var window = Show(select);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            Rows(select).ShouldBe(["src", "app", "components", "Program.cs", "docs", "readme.md", "appendix.md"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_Are_Tree_Items_With_Toggle_And_The_Field_Is_A_Combo_Box()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            var fieldPeer = ControlAutomationPeer.CreatePeerForElement(select);
            fieldPeer.GetAutomationControlType().ShouldBe(AutomationControlType.ComboBox);
            select.Open();
            Dispatcher.UIThread.RunJobs();
            var row = select.RowsPanel!.RowFor(data[1])!;
            var peer = ControlAutomationPeer.CreatePeerForElement(row);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.TreeItem);
            peer.GetName().ShouldBe("docs");
            var expand = peer.GetProvider<IExpandCollapseProvider>()!;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            select.IsItemExpanded(data[1]).ShouldBeTrue();
            var toggle = peer.GetProvider<IToggleProvider>()!;
            toggle.Toggle();
            toggle.ToggleState.ShouldBe(ToggleState.On);
            select.SelectedValues.Count.ShouldBe(2);
            ((IValueProvider)fieldPeer).Value.ShouldBe("2 selected");
            Node(select, data, "src").State.ShouldBe(CheckboxState.Unchecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Built_In_Text_Follows_The_Language()
    {
        var languages = ArcLanguageManager.Current!;
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        var window = Show(select);
        try
        {
            select.Open();
            select.SearchText = "zzz";
            Dispatcher.UIThread.RunJobs();
            var empty = (TextBlock)select.EmptyHost!.Child!;
            empty.Text.ShouldBe("No matches");
            var clear = TestHelpers.Descendant<AvaloniaButton>(select, TreeSelect.ClearPart);
            AutomationProperties.GetName(clear).ShouldBe("Clear selection");

            languages.Set(LanguageTags.ZhCN);
            Dispatcher.UIThread.RunJobs();
            empty.Text.ShouldBe("无匹配项");
            AutomationProperties.GetName(clear).ShouldBe("清除选择");
            TreeSelectText.Count(2).ShouldBe("已选 2 项");

            languages.Set(LanguageTags.ZhTW);
            Dispatcher.UIThread.RunJobs();
            empty.Text.ShouldBe("沒有符合的項目");
        }
        finally
        {
            languages.Set(LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_TreeViewItems_Need_No_Selectors()
    {
        var docs = new TreeViewItem { Header = "docs", Items = { new TreeViewItem { Header = "readme.md" }, new TreeViewItem { Header = "draft.md", IsDisabled = true } } };
        var select = new TreeSelect { SelectionMode = TreeSelectMode.Multiple, ItemsSource = new object[] { docs } };
        var window = Show(select);
        try
        {
            select.ToggleNode(select.NodeFor(docs)!);
            select.SelectedItems.ShouldBe([docs.Items[0]]);
            select.SelectedText.ShouldBe("readme.md");
            select.NodeFor(docs.Items[1])!.IsDisabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ReadOnly_Opens_But_Does_Not_Change()
    {
        var data = Data();
        var select = Create(data, TreeSelectMode.Multiple);
        select.IsReadOnly = true;
        var window = Show(select);
        try
        {
            select.Open();
            select.ToggleNode(Node(select, data, "docs"));
            select.SelectedValues.ShouldBeEmpty();
            select.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
